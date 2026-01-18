namespace Core
{
    public class ILQR_Controller
    {
        // === MAIN SOLVER ===
        public static double[] Solve(double[] xInit, double[,] Q, double[,] R, List<Obstacle> obstacles, int horizon, int maxIterations, double dt, List<double[]>? referencePath = null, double trackWidth = 0)
        {
            int nInput = 2;

            // === TUNING OVERRIDE FOR RACING ===
            // If we are racing (referencePath exists), we ignore the passed Q/R and force
            // "High Accuracy" weights to keep the car glued to the center line.
            if (referencePath != null && referencePath.Count > 0)
            {
                Q = new double[,] {
                    { 20, 0, 0, 0 },
                    { 0, 20, 0, 0 },
                    { 0, 0, 10, 0 },
                    { 0, 0, 0, 200 }
                };

                // R Matrix: [Accel, Steer]
                // Increased Steering penalty (2000) to force smooth, non-jerky turns.
                R = new double[,] {
                    { 1, 0 },
                    { 0, 1000 }
                };
            }
            // ==================================

            // 1. INITIALIZATION
            List<double[]> u_trajectory = new List<double[]>();
            for (int t = 0; t < horizon; t++) u_trajectory.Add(new double[nInput]);

            // 2. THE ITERATION LOOP
            for (int iter = 0; iter < maxIterations; iter++)
            {
                // --- A. ROLLOUT ---
                List<double[]> x_trajectory = new List<double[]>();
                double[] xCurrent = (double[])xInit.Clone();
                x_trajectory.Add(xCurrent);

                for (int t = 0; t < horizon; t++)
                {
                    xCurrent = PhysicsEngine.Step(xCurrent, u_trajectory[t], dt);
                    x_trajectory.Add(xCurrent);
                }

                double totalCost = CalculateCost(x_trajectory, u_trajectory, Q, R, obstacles, referencePath, trackWidth);

                if (totalCost < 0.1) break;

                // --- B. LINEARIZE & QUADRATICIZE ---
                List<double[,]> A_list = new List<double[,]>();
                List<double[,]> B_list = new List<double[,]>();
                List<double[,]> Q_list = new List<double[,]>();
                List<double[,]> R_list = new List<double[,]>();
                List<double[]> q_list = new List<double[]>();
                List<double[]> r_list = new List<double[]>();

                for (int t = 0; t <= horizon; t++)
                {
                    if (t < horizon)
                    {
                        var (At, Bt) = PhysicsEngine.Linearize(x_trajectory[t], u_trajectory[t], dt);
                        A_list.Add(At);
                        B_list.Add(Bt);
                    }

                    // --- TARGET SELECTION ---
                    double[] xTarget;
                    double[] xError = new double[4];

                    if (referencePath != null && referencePath.Count > 0)
                    {
                        // === RACING MODE ===
                        int currentIdx = GetClosestPathIndex(x_trajectory[t], referencePath);

                        // REDUCED Lookahead:
                        // Looking too far ahead (e.g. 2.0 * t) makes the car "cut" the corner.
                        // Keeping it close (1.0 * t) forces it to stick to the track shape.
                        int lookAhead = (int)(t * 1.0);

                        int targetIdx = (currentIdx + lookAhead) % referencePath.Count;
                        xTarget = referencePath[targetIdx];
                    }
                    else
                    {
                        // === PARKING MODE ===
                        double distToGoal = Math.Sqrt(Math.Pow(x_trajectory[t][0], 2) + Math.Pow(x_trajectory[t][1], 2));
                        double targetVel = (distToGoal < 5.0) ? 0.0 : 10.0;
                        xTarget = new double[] { 0, 0, targetVel, 0 };
                    }

                    // Calculate Error State
                    for (int i = 0; i < 4; i++) xError[i] = x_trajectory[t][i] - xTarget[i];

                    // Normalize Angle Error
                    while (xError[3] > Math.PI) xError[3] -= 2 * Math.PI;
                    while (xError[3] < -Math.PI) xError[3] += 2 * Math.PI;

                    // Standard Cost Derivatives (Q, q)
                    double[,] Q_total = (double[,])Q.Clone();
                    double[,] xVec = Helpers.ToColumnVector(xError);
                    double[,] q_std = Helpers.MultiplyMatrices(Q, xVec);
                    double[] q_total = { 2 * q_std[0, 0], 2 * q_std[1, 0], 2 * q_std[2, 0], 2 * q_std[3, 0] };

                    // Add Obstacle Derivatives
                    if (obstacles != null)
                    {
                        foreach (var obs in obstacles)
                        {
                            var (obsCost, obs_q, obs_Q) = GetSingleObstacleDerivatives(x_trajectory[t], obs);
                            Q_total = Helpers.AddMatrices(Q_total, obs_Q);
                            for (int i = 0; i < 4; i++) q_total[i] += obs_q[i];
                        }
                    }

                    Q_list.Add(Q_total);
                    q_list.Add(q_total);

                    // Input Cost Derivatives (R, r)
                    if (t < horizon)
                    {
                        R_list.Add(R);
                        double[,] uVec = Helpers.ToColumnVector(u_trajectory[t]);
                        double[,] r_val = Helpers.MultiplyMatrices(R, uVec);
                        r_list.Add(new double[] { 2 * r_val[0, 0], 2 * r_val[1, 0] });
                    }
                }

                // --- C. SOLVE LQR ---
                var gains = DynamicLQR.BackwardPass(A_list, B_list, Q_list, R_list, q_list, r_list, horizon);

                // --- D. UPDATE CONTROLS ---
                double bestCost = totalCost;
                bool improved = false;
                List<double[]> best_u_trajectory = u_trajectory;
                double[] alphas = { 1.0, 0.5, 0.25, 0.125, 0.0625 };

                foreach (double alpha in alphas)
                {
                    List<double[]> candidate_u_traj = new List<double[]>();
                    List<double[]> candidate_x_traj = new List<double[]>();
                    double[] xSim = (double[])xInit.Clone();
                    candidate_x_traj.Add(xSim);

                    for (int t = 0; t < horizon; t++)
                    {
                        double[] k = gains[t].k;
                        double[,] K = gains[t].K;
                        double[] dx = new double[4];
                        for (int j = 0; j < 4; j++) dx[j] = xSim[j] - x_trajectory[t][j];

                        double[,] dx_col = Helpers.ToColumnVector(dx);
                        double[,] Kdx = Helpers.MultiplyMatrices(K, dx_col);

                        double[] u_update = new double[nInput];
                        for (int i = 0; i < nInput; i++)
                        {
                            u_update[i] = u_trajectory[t][i] + alpha * k[i] + Kdx[i, 0];
                        }

                        candidate_u_traj.Add(u_update);
                        xSim = PhysicsEngine.Step(xSim, u_update, dt);
                        candidate_x_traj.Add(xSim);
                    }

                    double candidateCost = CalculateCost(candidate_x_traj, candidate_u_traj, Q, R, obstacles, referencePath, trackWidth);

                    if (candidateCost < bestCost)
                    {
                        bestCost = candidateCost;
                        u_trajectory = candidate_u_traj;
                        improved = true;
                        break;
                    }
                }

                if (!improved) break;
            }

            return u_trajectory[0];
        }

        // === HELPERS ===

        private static double CalculateCost(List<double[]> X, List<double[]> U, double[,] Q, double[,] R, List<Obstacle>? obstacles, List<double[]>? path, double trackWidth)
        {
            double cost = 0;
            for (int t = 0; t < X.Count; t++)
            {
                double[] xTarget;
                if (path != null && path.Count > 0)
                {
                    int idx = GetClosestPathIndex(X[t], path);
                    xTarget = path[idx];
                }
                else
                {
                    double dist = Math.Sqrt(X[t][0] * X[t][0] + X[t][1] * X[t][1]);
                    xTarget = new double[] { 0, 0, (dist < 5 ? 0 : 10), 0 };
                }

                double[] err = { X[t][0] - xTarget[0], X[t][1] - xTarget[1], X[t][2] - xTarget[2], X[t][3] - xTarget[3] };
                while (err[3] > Math.PI) err[3] -= 2 * Math.PI;
                while (err[3] < -Math.PI) err[3] += 2 * Math.PI;

                cost += Helpers.VectorQuadForm(Helpers.ToColumnVector(err), Q);

                if (obstacles != null)
                {
                    foreach (var obs in obstacles)
                    {
                        cost += GetSingleObstacleCost(X[t], obs);
                    }
                }
            }
            for (int t = 0; t < U.Count; t++)
            {
                double[,] uVec = Helpers.ToColumnVector(U[t]);
                cost += Helpers.VectorQuadForm(uVec, R);
            }
            return cost;
        }

        private static int GetClosestPathIndex(double[] x, List<double[]> path)
        {
            double min = double.MaxValue;
            int idx = 0;
            for (int i = 0; i < path.Count; i++)
            {
                double d = Math.Pow(x[0] - path[i][0], 2) + Math.Pow(x[1] - path[i][1], 2);
                if (d < min) { min = d; idx = i; }
            }
            return idx;
        }

        private static double GetSingleObstacleCost(double[] x, Obstacle obs)
        {
            double distSq = Math.Pow(x[0] - obs.X, 2) + Math.Pow(x[1] - obs.Y, 2);
            return obs.Weight * Math.Exp(-distSq / (obs.Radius * obs.Radius));
        }

        public static (double cost, double[] q, double[,] Q) GetSingleObstacleDerivatives(double[] x, Obstacle obs)
        {
            double px = x[0];
            double py = x[1];

            double dx = px - obs.X;
            double dy = py - obs.Y;
            double distSq = dx * dx + dy * dy;
            double rSq = obs.Radius * obs.Radius;

            double exponent = -distSq / rSq;
            double cost = obs.Weight * Math.Exp(exponent);

            double factor = cost * (-2.0 / rSq);
            double[] q = new double[4];
            q[0] = factor * dx;
            q[1] = factor * dy;

            double[,] Q_mat = new double[4, 4];
            double hessianFactor = -factor;
            if (hessianFactor < 0) hessianFactor = 0;

            Q_mat[0, 0] = hessianFactor;
            Q_mat[1, 1] = hessianFactor;

            return (cost, q, Q_mat);
        }
    }
}
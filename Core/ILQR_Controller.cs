namespace Core
{
    public class ILQR_Controller
    {
        // === MAIN SOLVER ===
        // Now accepts a list of obstacles
        public static double[] Solve(double[] xInit, double[,] Q, double[,] R, List<Obstacle> obstacles, int horizon, int maxIterations)
        {
            int nInput = 2;
            double dt = 0.1;

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

                double totalCost = CalculateTrajectoryCost(x_trajectory, u_trajectory, Q, R, obstacles);
                if (totalCost < 1.0) break;

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

                    // Derivatives of Cost (Q, q) including ALL Obstacles
                    double[,] Q_total = (double[,])Q.Clone();
                    double[,] xVec = Helpers.ToColumnVector(x_trajectory[t]);
                    double[,] q_std = Helpers.MultiplyMatrices(Q, xVec);
                    double[] q_total = { 2 * q_std[0, 0], 2 * q_std[1, 0], 2 * q_std[2, 0], 2 * q_std[3, 0] };

                    // Sum up derivatives for every obstacle
                    foreach (var obs in obstacles)
                    {
                        var (obsCost, obs_q, obs_Q) = GetSingleObstacleDerivatives(x_trajectory[t], obs);

                        Q_total = Helpers.AddMatrices(Q_total, obs_Q); // Add curvature
                        for (int i = 0; i < 4; i++) q_total[i] += obs_q[i]; // Add gradient push
                    }

                    Q_list.Add(Q_total);
                    q_list.Add(q_total);

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

                    double candidateCost = CalculateTrajectoryCost(candidate_x_traj, candidate_u_traj, Q, R, obstacles);

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

        private static double CalculateTrajectoryCost(List<double[]> X, List<double[]> U, double[,] Q, double[,] R, List<Obstacle> obstacles)
        {
            double cost = 0;
            for (int t = 0; t < X.Count; t++)
            {
                double[,] xVec = Helpers.ToColumnVector(X[t]);
                cost += Helpers.VectorQuadForm(xVec, Q);

                // Sum costs of all obstacles
                foreach (var obs in obstacles)
                {
                    cost += GetSingleObstacleCost(X[t], obs);
                }
            }
            for (int t = 0; t < U.Count; t++)
            {
                double[,] uVec = Helpers.ToColumnVector(U[t]);
                cost += Helpers.VectorQuadForm(uVec, R);
            }
            return cost;
        }

        private static double GetSingleObstacleCost(double[] x, Obstacle obs)
        {
            double px = x[0];
            double py = x[1];
            double distSq = Math.Pow(px - obs.X, 2) + Math.Pow(py - obs.Y, 2);
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

            // 1. Cost Value
            double exponent = -distSq / rSq;
            double cost = obs.Weight * Math.Exp(exponent);

            // 2. Gradient (q) 
            double factor = cost * (-2.0 / rSq);
            double[] q = new double[4];
            q[0] = factor * dx;
            q[1] = factor * dy;
            q[2] = 0;
            q[3] = 0;

            // 3. Hessian (Q)
            double[,] Q = new double[4, 4];
            double hessianFactor = -factor;
            if (hessianFactor < 0) hessianFactor = 0;

            Q[0, 0] = hessianFactor;
            Q[1, 1] = hessianFactor;

            return (cost, q, Q);
        }
    }
}
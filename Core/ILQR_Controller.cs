namespace Core
{
    public class ILQR_Controller
    {
        public static double[] Solve(double[] xInit, double[,] Q, double[,] R, List<Obstacle> obstacles, int horizon, int maxIterations, double dt, PhysicsModel physicsModel, List<double[]>? referencePath = null, double trackWidth = 0)
        {
            int nx = xInit.Length;
            int nu = physicsModel.Nu;

            // Initialize control trajectory with zeros
            List<double[]> u_trajectory = new List<double[]>();
            for (int t = 0; t < horizon; ++t) u_trajectory.Add(new double[nu]);

            for (int iter = 0; iter < maxIterations; ++iter)
            {
                // A. Rollout
                List<double[]> x_trajectory = new List<double[]>();
                double[] xCurrent = (double[])xInit.Clone();
                x_trajectory.Add(xCurrent);

                for (int t = 0; t < horizon; ++t)
                {
                    xCurrent = physicsModel.Step(xCurrent, u_trajectory[t], dt);
                    x_trajectory.Add(xCurrent);
                }

                double totalCost = CalculateCost(x_trajectory, u_trajectory, Q, R, obstacles, referencePath, trackWidth);
                if (totalCost < 1e-6) break;

                // B. Linearize & Quadraticize
                var A_list = new List<double[,]>();
                var B_list = new List<double[,]>();
                var Q_list = new List<double[,]>();
                var R_list = new List<double[,]>();
                var q_list = new List<double[]>();
                var r_list = new List<double[]>();

                for (int t = 0; t <= horizon; ++t)
                {
                    if (t < horizon)
                    {
                        var (At, Bt) = physicsModel.Linearize(x_trajectory[t], u_trajectory[t], dt);
                        A_list.Add(At);
                        B_list.Add(Bt);
                    }

                    // Target selection - keep same logic but dimension-aware
                    double[] xTarget = new double[nx];
                    double[] xError = new double[nx];

                    if (referencePath != null && referencePath.Count > 0)
                    {
                        int currentIdx = GetClosestPathIndex(x_trajectory[t], referencePath);
                        int lookAhead = Math.Max(0, (int)(t * 1.0));
                        int targetIdx = (currentIdx + lookAhead) % referencePath.Count;
                        xTarget = referencePath[targetIdx];
                    }
                    else
                    {
                        // Default: goal at origin and sensible velocity target if state has velocity
                        double distToGoal = Math.Sqrt(Math.Pow(x_trajectory[t][0], 2) + Math.Pow(x_trajectory[t][1], 2));
                        double targetVel = (distToGoal < 5.0 && nx > 2) ? 0.0 : (nx > 2 ? 10.0 : 0.0);
                        // Build xTarget with zeros and targetVel if available
                        for (int i = 0; i < nx; ++i) xTarget[i] = 0.0;
                        if (nx > 2) xTarget[2] = targetVel;
                    }

                    for (int i = 0; i < nx; i++) xError[i] = x_trajectory[t][i] - xTarget[i];

                    // If angle exists (assumed last element), normalize
                    if (nx > 3)
                    {
                        while (xError[3] > Math.PI) xError[3] -= 2 * Math.PI;
                        while (xError[3] < -Math.PI) xError[3] += 2 * Math.PI;
                    }

                    // Cost derivatives
                    double[,] Q_total = (double[,])Q.Clone();
                    var xVec = Helpers.ToColumnVector(xError);
                    var q_std = Helpers.MultiplyMatrices(Q, xVec);
                    double[] q_total = new double[nx];
                    for (int i = 0; i < nx; ++i) q_total[i] = 2 * q_std[i, 0];

                    if (obstacles != null)
                    {
                        foreach (var obs in obstacles)
                        {
                            var (obsCost, obs_q, obs_Q) = GetSingleObstacleDerivatives(x_trajectory[t], obs, nx);
                            Q_total = Helpers.AddMatrices(Q_total, obs_Q);
                            for (int i = 0; i < nx; ++i) q_total[i] += obs_q[i];
                        }
                    }

                    Q_list.Add(Q_total);
                    q_list.Add(q_total);

                    if (t < horizon)
                    {
                        R_list.Add(R);
                        var uVec = Helpers.ToColumnVector(u_trajectory[t]);
                        var r_val = Helpers.MultiplyMatrices(R, uVec);
                        double[] r_arr = new double[nu];
                        for (int i = 0; i < nu; ++i) r_arr[i] = 2 * r_val[i, 0];
                        r_list.Add(r_arr);
                    }
                }

                // C. Solve LQR
                var gains = DynamicLQR.BackwardPass(A_list, B_list, Q_list, R_list, q_list, r_list, horizon);

                // D. Line search and control update
                double bestCost = totalCost;
                bool improved = false;
                double[] alphas = { 1.0, 0.5, 0.25, 0.125, 0.0625 };

                foreach (double alpha in alphas)
                {
                    var candidate_u = new List<double[]>();
                    var candidate_x = new List<double[]>();
                    double[] xSim = (double[])xInit.Clone();
                    candidate_x.Add(xSim);

                    for (int t = 0; t < horizon; ++t)
                    {
                        double[] k = gains[t].k;
                        double[,] K = gains[t].K;

                        double[] dx = new double[nx];
                        for (int j = 0; j < nx; ++j) dx[j] = xSim[j] - x_trajectory[t][j];

                        var Kdx = Helpers.MultiplyMatrices(K, Helpers.ToColumnVector(dx));

                        double[] u_update = new double[nu];
                        for (int i = 0; i < nu; ++i)
                        {
                            u_update[i] = u_trajectory[t][i] + alpha * k[i] + Kdx[i, 0];
                        }

                        candidate_u.Add(u_update);
                        xSim = physicsModel.Step(xSim, u_update, dt);
                        candidate_x.Add(xSim);
                    }

                    double candidateCost = CalculateCost(candidate_x, candidate_u, Q, R, obstacles, referencePath, trackWidth);

                    if (candidateCost < bestCost)
                    {
                        bestCost = candidateCost;
                        u_trajectory = candidate_u;
                        improved = true;
                        break;
                    }
                }

                if (!improved) break;
            }

            return u_trajectory.Count > 0 ? u_trajectory[0] : new double[physicsModel.Nu];
        }

        // Helpers (dimension-aware)
        private static double CalculateCost(List<double[]> X, List<double[]> U, double[,] Q, double[,] R, List<Obstacle>? obstacles, List<double[]>? path, double trackWidth)
        {
            double cost = 0.0;
            int nx = Q.GetLength(0);
            int nu = R.GetLength(0);

            for (int t = 0; t < X.Count; ++t)
            {
                double[] xTarget = new double[nx];
                if (path != null && path.Count > 0)
                {
                    int idx = GetClosestPathIndex(X[t], path);
                    xTarget = path[idx];
                }
                else
                {
                    double dist = Math.Sqrt(X[t][0] * X[t][0] + X[t][1] * X[t][1]);
                    double targetVel = (dist < 5 ? 0 : 10);
                    for (int i = 0; i < nx; i++) xTarget[i] = 0;
                    if (nx > 2) xTarget[2] = targetVel;
                }

                double[] err = new double[nx];
                for (int i = 0; i < nx; ++i) err[i] = X[t][i] - xTarget[i];
                if (nx > 3)
                {
                    while (err[3] > Math.PI) err[3] -= 2 * Math.PI;
                    while (err[3] < -Math.PI) err[3] += 2 * Math.PI;
                }

                cost += Helpers.VectorQuadForm(Helpers.ToColumnVector(err), Q);

                if (obstacles != null)
                {
                    foreach (var obs in obstacles)
                        cost += GetSingleObstacleCost(X[t], obs);
                }
            }

            for (int t = 0; t < U.Count; ++t)
            {
                var uVec = Helpers.ToColumnVector(U[t]);
                cost += Helpers.VectorQuadForm(uVec, R);
            }

            return cost;
        }

        private static int GetClosestPathIndex(double[] x, List<double[]> path)
        {
            double min = double.MaxValue; int idx = 0;
            for (int i = 0; i < path.Count; ++i)
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

        public static (double cost, double[] q, double[,] Q) GetSingleObstacleDerivatives(double[] x, Obstacle obs, int nx)
        {
            double px = x[0];
            double py = x[1];
            double dx = px - obs.X;
            double dy = py - obs.Y;
            double distSq = dx * dx + dy * dy;
            double rSq = obs.Radius * obs.Radius;

            double cost = obs.Weight * Math.Exp(-distSq / rSq);
            double factor = cost * (-2.0 / rSq);

            double[] q = new double[nx];
            q[0] = factor * dx;
            q[1] = factor * dy;

            double[,] Q_mat = new double[nx, nx];
            double hessianFactor = -factor;
            if (hessianFactor < 0) hessianFactor = 0; // keep Hessian PSD

            if (nx > 0) Q_mat[0, 0] = hessianFactor;
            if (nx > 1) Q_mat[1, 1] = hessianFactor;

            return (cost, q, Q_mat);
        }
    }
}
namespace Core
{
    public class ILQR_Controller_2
    {
        // === MAIN SOLVER ===
        // Implements the iLQR Loop (Slide 54) with Obstacles and Line Search (Slide 63)
        public static double[] Solve(double[,] Q, double[,] R, double[] xInit, int horizon, int maxIterations)
        {
            // 1. INITIALIZATION (Slide 64)
            int nInput = 2;
            double dt = 0.1;

            // Initialize generic guess (Zero Inputs / Coasting) if starting fresh
            List<double[]> u_trajectory = new List<double[]>();
            for (int t = 0; t < horizon; t++) u_trajectory.Add(new double[nInput]);

            // 2. THE ITERATION LOOP
            for (int iter = 0; iter < maxIterations; iter++)
            {
                // --- A. ROLLOUT (Forward Simulation) ---
                //
                List<double[]> x_trajectory = new List<double[]>();
                double[] xCurrent = (double[])xInit.Clone();
                x_trajectory.Add(xCurrent);

                for (int t = 0; t < horizon; t++)
                {
                    xCurrent = PhysicsEngine.Step(xCurrent, u_trajectory[t], dt);
                    x_trajectory.Add(xCurrent);
                }

                // Check Cost
                double totalCost = CalculateTrajectoryCost(x_trajectory, u_trajectory, Q, R);

                // Convergence check (optional, can skip for MPC speed)
                if (totalCost < 1.0) break;

                // --- B. LINEARIZE DYNAMICS & QUADRATICIZE COSTS ---
                //
                List<double[,]> A_list = new List<double[,]>();
                List<double[,]> B_list = new List<double[,]>();
                List<double[,]> Q_list = new List<double[,]>();
                List<double[,]> R_list = new List<double[,]>();
                List<double[]> q_list = new List<double[]>();
                List<double[]> r_list = new List<double[]>();

                for (int t = 0; t <= horizon; t++)
                {
                    // 1. Derivatives of Physics (A, B)
                    // Only exist up to T-1
                    if (t < horizon)
                    {
                        var (At, Bt) = PhysicsEngine.Linearize(x_trajectory[t], u_trajectory[t], dt);
                        A_list.Add(At);
                        B_list.Add(Bt);
                    }

                    // 2. Derivatives of Cost (Q, q) including Obstacles
                    // Standard State Cost: 0.5 * x'Qx  -> Gradient = Qx, Hessian = Q
                    // Note: If your Q matrix already includes the 0.5 factor, adjust accordingly.
                    // Assuming standard quadratic form J = x'Qx:
                    double[,] Q_total = (double[,])Q.Clone();
                    double[,] xVec = Helpers.ToColumnVector(x_trajectory[t]);
                    double[,] q_std = Helpers.MultiplyMatrices(Q, xVec);
                    double[] q_total = { 2 * q_std[0, 0], 2 * q_std[1, 0], 2 * q_std[2, 0], 2 * q_std[3, 0] };

                    // Obstacle Cost Derivatives
                    var obsDeriv = GetObstacleDerivatives(x_trajectory[t]);
                    Q_total = Helpers.AddMatrices(Q_total, obsDeriv.Q); // Add curvature
                    for (int i = 0; i < 4; i++) q_total[i] += obsDeriv.q[i]; // Add gradient push

                    Q_list.Add(Q_total);
                    q_list.Add(q_total);

                    // 3. Derivatives of Input Cost (R, r)
                    if (t < horizon)
                    {
                        R_list.Add(R);
                        // Gradient r = 2 * R * u
                        double[,] uVec = Helpers.ToColumnVector(u_trajectory[t]);
                        double[,] r_val = Helpers.MultiplyMatrices(R, uVec);
                        r_list.Add(new double[] { 2 * r_val[0, 0], 2 * r_val[1, 0] });
                    }
                }

                // --- C. SOLVE LQR (Backward Pass) ---
                //
                var gains = DynamicLQR.BackwardPass(A_list, B_list, Q_list, R_list, q_list, r_list, horizon);

                // --- D. UPDATE CONTROLS (Forward Pass with Line Search) ---
                //
                double bestCost = totalCost;
                bool improved = false;
                List<double[]> best_u_trajectory = u_trajectory;

                // Alpha (Learning Rate): 1.0, 0.5, 0.25...
                double[] alphas = { 1.0, 0.5, 0.25, 0.125, 0.0625 };

                foreach (double alpha in alphas)
                {
                    List<double[]> candidate_u_traj = new List<double[]>();
                    List<double[]> candidate_x_traj = new List<double[]>();
                    double[] xSim = (double[])xInit.Clone();
                    candidate_x_traj.Add(xSim);

                    for (int t = 0; t < horizon; t++)
                    {
                        // Extract Gains
                        double[] k = gains[t].k;      // Feedforward (Avoid obstacles)
                        double[,] K = gains[t].K;     // Feedback (Stay on track)

                        // Calculate deviation from the path we linearized around
                        double[] dx = new double[4];
                        for (int j = 0; j < 4; j++) dx[j] = xSim[j] - x_trajectory[t][j];

                        // Formula: u_new = u_old + alpha*k + K*dx
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

                    double candidateCost = CalculateTrajectoryCost(candidate_x_traj, candidate_u_traj, Q, R);

                    if (candidateCost < bestCost)
                    {
                        bestCost = candidateCost;
                        u_trajectory = candidate_u_traj;
                        improved = true;
                        break;
                    }
                }

                if (!improved) break; // Optimization stuck, exit loop
            }

            // Return the first optimal move for MPC
            return u_trajectory[0];
        }

        // === HELPERS ===

        private static double CalculateTrajectoryCost(List<double[]> X, List<double[]> U, double[,] Q, double[,] R)
        {
            double cost = 0;
            // Cost for States
            for (int t = 0; t < X.Count; t++)
            {
                double[,] xVec = Helpers.ToColumnVector(X[t]);
                cost += Helpers.VectorQuadForm(xVec, Q);
                cost += GetObstacleCost(X[t]); // Add the soft constraint penalty
            }
            // Cost for Inputs
            for (int t = 0; t < U.Count; t++)
            {
                double[,] uVec = Helpers.ToColumnVector(U[t]);
                cost += Helpers.VectorQuadForm(uVec, R);
            }
            return cost;
        }

        // Returns the scalar cost of being near the obstacle
        private static double GetObstacleCost(double[] x)
        {
            double px = x[0];
            double py = x[1];

            // CONFIG: Must match GetObstacleDerivatives
            double obsX = -13.0;
            double obsY = -4.0;
            double radius = 4.0;
            double weight = 7500.0;

            double distSq = Math.Pow(px - obsX, 2) + Math.Pow(py - obsY, 2);
            return weight * Math.Exp(-distSq / (radius * radius));
        }

        // Returns Cost, Gradient (q), and Hessian (Q) for the obstacle
        public static (double cost, double[] q, double[,] Q) GetObstacleDerivatives(double[] x)
        {
            double px = x[0];
            double py = x[1];

            // CONFIG: Must match GetObstacleCost
            double obsX = -13.0;
            double obsY = -4.0;
            double radius = 4.0;
            double weight = 7500.0;

            double dx = px - obsX;
            double dy = py - obsY;
            double distSq = dx * dx + dy * dy;
            double rSq = radius * radius;

            // 1. Cost Value
            double exponent = -distSq / rSq;
            double cost = weight * Math.Exp(exponent);

            // 2. Gradient (q) 
            double factor = cost * (-2.0 / rSq);
            double[] q = new double[4];
            q[0] = factor * dx;
            q[1] = factor * dy;
            q[2] = 0;
            q[3] = 0;

            // 3. Hessian (Q) - Positive Definite Approximation
            double[,] Q = new double[4, 4];

            // We ensure curvature is positive so the solver pushes AWAY from the obstacle
            // instead of getting confused inside it.
            double hessianFactor = -factor;
            if (hessianFactor < 0) hessianFactor = 0;

            Q[0, 0] = hessianFactor;
            Q[1, 1] = hessianFactor;

            return (cost, q, Q);
        }
    }
}
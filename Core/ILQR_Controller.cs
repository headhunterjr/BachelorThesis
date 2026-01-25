using System;
using System.Collections.Generic;

namespace Core
{
    public class ILQR_Controller
    {
        public static double[] Solve(double[] xInit, int horizon, int maxIterations, double dt, PhysicsModel physicsModel, ICostModel costModel)
        {
            int nx = xInit.Length;
            int nu = physicsModel.Nu;

            List<double[]> u_trajectory = new List<double[]>();
            for (int t = 0; t < horizon; ++t)
            {
                u_trajectory.Add(new double[nu]);
            }

            for (int iter = 0; iter < maxIterations; ++iter)
            {
                // --- A. ROLLOUT ---
                List<double[]> x_trajectory = new List<double[]>();
                double[] xCurrent = (double[])xInit.Clone();
                x_trajectory.Add(xCurrent);

                double totalCost = 0;

                for (int t = 0; t < horizon; ++t)
                {
                    // Pass 't' here
                    totalCost += costModel.Evaluate(xCurrent, u_trajectory[t], dt, t);
                    xCurrent = physicsModel.Step(xCurrent, u_trajectory[t], dt);
                    x_trajectory.Add(xCurrent);
                }
                // Terminal cost (t = horizon)
                totalCost += costModel.Evaluate(xCurrent, new double[nu], dt, horizon);

                if (totalCost < 1e-6)
                {
                    break;
                }

                // --- B. LINEARIZE & QUADRATICIZE ---
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

                    double[,] Qt = new double[nx, nx];
                    double[,] Rt = new double[nu, nu];
                    double[] qt = new double[nx];
                    double[] rt = new double[nu];

                    double[] u_t = (t < horizon) ? u_trajectory[t] : new double[nu];

                    // Pass 't' here as well
                    costModel.GetDerivatives(x_trajectory[t], u_t, dt, t, ref Qt, ref Rt, ref qt, ref rt);

                    Q_list.Add(Qt);
                    q_list.Add(qt);

                    if (t < horizon)
                    {
                        R_list.Add(Rt);
                        r_list.Add(rt);
                    }
                }

                // --- C. SOLVE LQR ---
                var gains = DynamicLQR.BackwardPass(A_list, B_list, Q_list, R_list, q_list, r_list, horizon);

                // --- D. UPDATE CONTROLS ---
                double bestCost = totalCost;
                bool improved = false;
                double[] alphas = { 1.0, 0.5, 0.25, 0.125, 0.0625 };

                foreach (double alpha in alphas)
                {
                    var cand_u = new List<double[]>();
                    var cand_x = new List<double[]>();
                    double[] xSim = (double[])xInit.Clone();
                    cand_x.Add(xSim);
                    double currentAlphaCost = 0;

                    for (int t = 0; t < horizon; ++t)
                    {
                        double[] k = gains[t].k;
                        double[,] K = gains[t].K;
                        double[] dx = new double[nx];
                        for (int j = 0; j < nx; ++j)
                        {
                            dx[j] = xSim[j] - x_trajectory[t][j];
                        }

                        double[] u_update = new double[nu];
                        for (int i = 0; i < nu; ++i)
                        {
                            double feedback = 0;
                            for (int j = 0; j < nx; ++j)
                            {
                                feedback += K[i, j] * dx[j];
                            }
                            u_update[i] = u_trajectory[t][i] + alpha * k[i] + feedback;
                        }

                        cand_u.Add(u_update);
                        // Pass 't'
                        currentAlphaCost += costModel.Evaluate(xSim, u_update, dt, t);
                        xSim = physicsModel.Step(xSim, u_update, dt);
                        cand_x.Add(xSim);
                    }
                    // Pass 'horizon' for terminal cost
                    currentAlphaCost += costModel.Evaluate(xSim, new double[nu], dt, horizon);

                    if (currentAlphaCost < bestCost)
                    {
                        bestCost = currentAlphaCost;
                        u_trajectory = cand_u;
                        improved = true;
                        break;
                    }
                }
                if (!improved)
                {
                    break;
                }
            }

            return u_trajectory.Count > 0 ? u_trajectory[0] : new double[nu];
        }
    }
}
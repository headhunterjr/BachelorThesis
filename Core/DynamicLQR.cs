namespace Core
{
    public class DynamicLQR
    {
        public struct Gain
        {
            public double[,] K; // Feedback Gain (Matrix)
            public double[] k;  // Feedforward Gain (Vector)
        }

        // Backward pass expects Q_list and q_list of length horizon+1 (terminal included)
        // R_list and r_list of length horizon
        public static List<Gain> BackwardPass(List<double[,]> A_list, List<double[,]> B_list, List<double[,]> Q_list, List<double[,]> R_list, List<double[]> q_list, List<double[]> r_list, int horizon)
        {
            List<Gain> gains = new List<Gain>(horizon);

            int nx = Q_list[horizon].GetLength(0);
            int nu = R_list[0].GetLength(0);

            // Terminal cost
            double[,] V_xx = (double[,])Q_list[horizon].Clone();
            double[] V_x = (double[])q_list[horizon].Clone();

            for (int t = horizon - 1; t >= 0; --t)
            {
                var A = A_list[t];
                var B = B_list[t];
                var Q = Q_list[t];
                var R = R_list[t];
                var q = q_list[t];
                var r = r_list[t];

                // Q_u = r + B^T * V_x
                var B_T = Helpers.Transpose(B);
                var Vx_col = Helpers.ToColumnVector(V_x);
                var term1 = Helpers.MultiplyMatrices(B_T, Vx_col);
                double[] Q_u = new double[nu];
                for (int i = 0; i < nu; ++i)
                {
                    Q_u[i] = r[i] + term1[i, 0];
                }

                // Q_uu = R + B^T * V_xx * B
                var term2 = Helpers.MultiplyMatrices(Helpers.MultiplyMatrices(B_T, V_xx), B);
                var Q_uu = Helpers.AddMatrices(R, term2);

                // Regularize for numerical stability
                for (int i = 0; i < Q_uu.GetLength(0); ++i) Q_uu[i, i] += 1e-6;

                // Q_ux = B^T * V_xx * A
                var Q_ux = Helpers.MultiplyMatrices(Helpers.MultiplyMatrices(B_T, V_xx), A);

                // Q_x = q + A^T * V_x
                var A_T = Helpers.Transpose(A);
                var term3 = Helpers.MultiplyMatrices(A_T, Vx_col);
                double[] Q_x = new double[nx];
                for (int i = 0; i < nx; ++i) Q_x[i] = q[i] + term3[i, 0];

                // Q_xx = Q + A^T * V_xx * A
                var term4 = Helpers.MultiplyMatrices(Helpers.MultiplyMatrices(A_T, V_xx), A);
                var Q_xx = Helpers.AddMatrices(Q, term4);

                // Gains
                var Q_uu_inv = Helpers.Inverse(Q_uu);

                var Qu_col = Helpers.ToColumnVector(Q_u);
                var k_mat = Helpers.MultiplyMatrices(Q_uu_inv, Qu_col);
                double[] k = new double[nu];
                for (int i = 0; i < nu; ++i) k[i] = -k_mat[i, 0];

                var K_raw = Helpers.MultiplyMatrices(Q_uu_inv, Q_ux);
                var K = new double[K_raw.GetLength(0), K_raw.GetLength(1)];
                for (int i = 0; i < K.GetLength(0); ++i)
                    for (int j = 0; j < K.GetLength(1); ++j)
                        K[i, j] = -K_raw[i, j];

                gains.Insert(0, new Gain { K = K, k = k });

                // Update V_x and V_xx
                var K_T = Helpers.Transpose(K);
                var k_col = Helpers.ToColumnVector(k);

                var tempA = Helpers.MultiplyMatrices(K_T, Q_uu);
                var tempA2 = Helpers.MultiplyMatrices(tempA, k_col);
                var tempB = Helpers.MultiplyMatrices(K_T, Qu_col);
                var Q_ux_T = Helpers.Transpose(Q_ux);
                var tempC = Helpers.MultiplyMatrices(Q_ux_T, k_col);

                for (int i = 0; i < nx; ++i)
                {
                    V_x[i] = Q_x[i] + tempA2[i, 0] + tempB[i, 0] + tempC[i, 0];
                }

                var termK1 = Helpers.MultiplyMatrices(Q_ux_T, K);
                var termK2 = Helpers.MultiplyMatrices(K_T, Q_ux);
                var termK3 = Helpers.MultiplyMatrices(Helpers.MultiplyMatrices(K_T, Q_uu), K);

                V_xx = Helpers.AddMatrices(Q_xx, termK1);
                V_xx = Helpers.AddMatrices(V_xx, termK2);
                V_xx = Helpers.AddMatrices(V_xx, termK3);
            }

            return gains;
        }
    }
}
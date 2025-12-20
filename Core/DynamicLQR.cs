namespace Core
{
    public class DynamicLQR
    {
        public struct Gain
        {
            public double[,] K; // Feedback Gain (Matrix)
            public double[] k;  // Feedforward Gain (Vector) - The "Nudge"
        }

        public static List<Gain> BackwardPass(
            List<double[,]> A_list,
            List<double[,]> B_list,
            List<double[,]> Q_list, // Now a List (changes every step)
            List<double[,]> R_list,
            List<double[]> q_list,  // NEW: Gradient of Cost wrt State
            List<double[]> r_list,  // NEW: Gradient of Cost wrt Input
            int horizon)
        {
            List<Gain> gains = new List<Gain>();

            // Initialize Cost-to-Go (V) at the final step T
            // V_x = q_T, V_xx = Q_T
            double[,] V_xx = (double[,])Q_list[horizon].Clone();
            double[] V_x = (double[])q_list[horizon].Clone();

            for (int t = horizon - 1; t >= 0; t--)
            {
                var A = A_list[t];
                var B = B_list[t];
                var Q = Q_list[t];
                var R = R_list[t];
                var q = q_list[t];
                var r = r_list[t];

                // --- 1. Calculate Q-Function Terms (Slide 52) ---
                //

                // Q_u = r + B^T * V_x
                double[,] B_T = Helpers.Transpose(B);
                double[,] Vx_col = Helpers.ToColumnVector(V_x);
                double[,] term1 = Helpers.MultiplyMatrices(B_T, Vx_col);
                double[] Q_u = new double[r.Length];
                for (int i = 0; i < r.Length; i++) Q_u[i] = r[i] + term1[i, 0];

                // Q_uu = R + B^T * V_xx * B
                double[,] term2 = Helpers.MultiplyMatrices(Helpers.MultiplyMatrices(B_T, V_xx), B);
                double[,] Q_uu = Helpers.AddMatrices(R, term2);

                // Regularization (Fixes instability)
                for (int i = 0; i < Q_uu.GetLength(0); i++) Q_uu[i, i] += 1.0;

                // Q_ux = B^T * V_xx * A
                double[,] Q_ux = Helpers.MultiplyMatrices(Helpers.MultiplyMatrices(B_T, V_xx), A);

                // Q_x = q + A^T * V_x
                double[,] A_T = Helpers.Transpose(A);
                double[,] term3 = Helpers.MultiplyMatrices(A_T, Vx_col);
                double[] Q_x = new double[q.Length];
                for (int i = 0; i < q.Length; i++) Q_x[i] = q[i] + term3[i, 0];

                // Q_xx = Q + A^T * V_xx * A
                double[,] term4 = Helpers.MultiplyMatrices(Helpers.MultiplyMatrices(A_T, V_xx), A);
                double[,] Q_xx = Helpers.AddMatrices(Q, term4);

                // --- 2. Calculate Gains (Slide 52) ---
                double[,] Q_uu_inv = Helpers.Inverse(Q_uu);

                // k (Feedforward) = -Q_uu^-1 * Q_u
                double[,] Qu_col = Helpers.ToColumnVector(Q_u);
                double[,] k_mat = Helpers.MultiplyMatrices(Q_uu_inv, Qu_col);
                double[] k = new double[k_mat.GetLength(0)];
                for (int i = 0; i < k.Length; i++) k[i] = -k_mat[i, 0];

                // K (Feedback) = -Q_uu^-1 * Q_ux
                double[,] K_raw = Helpers.MultiplyMatrices(Q_uu_inv, Q_ux);
                double[,] K = new double[K_raw.GetLength(0), K_raw.GetLength(1)];
                for (int i = 0; i < K.GetLength(0); i++)
                    for (int j = 0; j < K.GetLength(1); j++)
                        K[i, j] = -K_raw[i, j];

                gains.Insert(0, new Gain { K = K, k = k });

                // --- 3. Update Cost-to-Go V (for next step) ---
                // V_x = Q_x + K^T * Q_uu * k + K^T * Q_u + Q_ux^T * k (Simplified: V_x = Q_x + K^T * Q_u ...)
                // Actually, let's use the robust update:
                // V_x = Q_x + K^T Q_uu k + K^T Q_u + Q_ux^T k  <-- This is heavy.
                // Standard iLQR update: V_x = Q_x + K^T * Q_uu * k + K^T * Q_u + Q_ux^T * k

                // Let's implement the simpler V_x update usually used:
                // V_x = Q_x + K^T * (Q_uu * k + Q_u) + Q_ux^T * k

                // For now, let's assume V_x approx Q_x + A^T V_next ... 
                // The exact update is critical for obstacles.
                // V_x_new = Q_x + K^T * Q_uu * k + K^T * Q_u + Q_ux^T * k;

                double[,] K_T = Helpers.Transpose(K);
                double[,] k_col = Helpers.ToColumnVector(k);

                // Term A: K^T * Q_uu * k
                var tempA = Helpers.MultiplyMatrices(K_T, Q_uu);
                var tempA2 = Helpers.MultiplyMatrices(tempA, k_col);

                // Term B: K^T * Q_u
                var tempB = Helpers.MultiplyMatrices(K_T, Qu_col);

                // Term C: Q_ux^T * k
                var Q_ux_T = Helpers.Transpose(Q_ux);
                var tempC = Helpers.MultiplyMatrices(Q_ux_T, k_col);

                // Sum for V_x
                for (int i = 0; i < V_x.Length; i++)
                {
                    V_x[i] = Q_x[i] + tempA2[i, 0] + tempB[i, 0] + tempC[i, 0];
                }

                // V_xx = Q_xx + K^T * Q_uu * K + K^T * Q_ux + Q_ux^T * K
                // Standard simplification: V_xx = Q_xx + K^T * Q_uu * K + K^T * Q_ux + ...
                // Let's use: V_xx = Q_xx + Q_ux^T K + K^T Q_ux + K^T Q_uu K
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
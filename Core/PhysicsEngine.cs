namespace Core
{
    public static class PhysicsEngine
    {
        // 1. NON-LINEAR DYNAMICS (The "Real" World)
        // x = [x, y, v, theta]
        // u = [accel, steer]
        public static double[] Step(double[] x, double[] u, double dt)
        {
            double px = x[0];
            double py = x[1];
            double v = x[2];
            double theta = x[3];

            double accel = u[0];
            double steer = u[1];
            double L = 2.5; // Wheelbase length

            // --- CONSTRAINT FIX ---
            // Limit Steering to +/- 45 degrees (0.8 rad)
            if (steer > 0.8) steer = 0.8;
            if (steer < -0.8) steer = -0.8;

            // Limit Acceleration (e.g., +/- 10 m/s^2)
            if (accel > 10.0) accel = 10.0;
            if (accel < -10.0) accel = -10.0;

            // Kinematic Bicycle Model Equations
            double px_new = px + v * Math.Cos(theta) * dt;
            double py_new = py + v * Math.Sin(theta) * dt;
            double v_new = v + accel * dt;
            double theta_new = theta + (v / L) * Math.Tan(steer) * dt;

            return [px_new, py_new, v_new, theta_new];
        }

        // 2. LINEARIZATION (Calculating A and B matrices automatically)
        //
        public static (double[,] A, double[,] B) Linearize(double[] x, double[] u, double dt)
        {
            int nx = 4;
            int nu = 2;
            double eps = 1e-5; // Finite difference epsilon

            double[,] A = new double[nx, nx];
            double[,] B = new double[nx, nu];

            // Baseline
            double[] x_next_base = Step(x, u, dt);

            // Compute A (Jacobian wrt X)
            for (int i = 0; i < nx; i++)
            {
                double[] x_p = (double[])x.Clone();
                x_p[i] += eps;
                double[] x_next_p = Step(x_p, u, dt);

                for (int j = 0; j < nx; j++)
                {
                    A[j, i] = (x_next_p[j] - x_next_base[j]) / eps;
                }
            }

            // Compute B (Jacobian wrt U)
            for (int i = 0; i < nu; i++)
            {
                double[] u_p = (double[])u.Clone();
                u_p[i] += eps;
                double[] x_next_p = Step(x, u_p, dt);

                for (int j = 0; j < nx; j++)
                {
                    B[j, i] = (x_next_p[j] - x_next_base[j]) / eps;
                }
            }

            return (A, B);
        }
    }
}

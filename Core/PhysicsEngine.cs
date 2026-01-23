namespace Core
{
    public static class PhysicsEngine
    {
        // Default kinematic bicycle model (configurable limits are public constants here)
        public static double SteeringLimit = 0.6; // radians (~45deg)
        public static double AccelLimit = 10.0;   // m/s^2
        public static double WheelBase = 3.5;     // meters

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

            // Apply simple actuator limits
            if (steer > SteeringLimit) steer = SteeringLimit;
            if (steer < -SteeringLimit) steer = -SteeringLimit;

            if (accel > AccelLimit) accel = AccelLimit;
            if (accel < -AccelLimit) accel = -AccelLimit;

            double px_new = px + v * Math.Cos(theta) * dt;
            double py_new = py + v * Math.Sin(theta) * dt;
            double v_new = v + accel * dt;
            double theta_new = theta + (v / WheelBase) * Math.Tan(steer) * dt;

            return new double[] { px_new, py_new, v_new, theta_new };
        }

        // 2. LINEARIZATION (Calculating A and B matrices using finite differences)
        // Returns (A, B)
        public static (double[,], double[,]) Linearize(double[] x, double[] u, double dt)
        {
            int nx = x.Length;
            int nu = u.Length;
            double eps = 1e-6; // smaller eps for accurate numeric derivative

            double[,] A = new double[nx, nx];
            double[,] B = new double[nx, nu];

            double[] x_next_base = Step(x, u, dt);

            // A
            for (int i = 0; i < nx; ++i)
            {
                var x_p = (double[])x.Clone();
                x_p[i] += eps;
                var x_next_p = Step(x_p, u, dt);
                for (int j = 0; j < nx; ++j)
                {
                    A[j, i] = (x_next_p[j] - x_next_base[j]) / eps;
                }
            }

            // B
            for (int i = 0; i < nu; ++i)
            {
                var u_p = (double[])u.Clone();
                u_p[i] += eps;
                var x_next_p = Step(x, u_p, dt);
                for (int j = 0; j < nx; ++j)
                {
                    B[j, i] = (x_next_p[j] - x_next_base[j]) / eps;
                }
            }

            return (A, B);
        }
    }
}

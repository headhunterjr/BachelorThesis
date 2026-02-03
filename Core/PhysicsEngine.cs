namespace Core
{
    public class PhysicsEngine
    {
        // Configurable properties per instance
        public double SteeringLimit { get; private set; }
        public double AccelLimit { get; private set; }
        public double WheelBase { get; private set; }

        // Constructor allows each scenario to define its own "Car"
        public PhysicsEngine(double wheelBase, double steeringLimit, double accelLimit)
        {
            WheelBase = wheelBase;
            SteeringLimit = steeringLimit;
            AccelLimit = accelLimit;
        }

        // 1. NON-LINEAR DYNAMICS (Uses instance properties now)
        public double[] Step(double[] x, double[] u, double dt, int t)
        {
            double px = x[0];
            double py = x[1];
            double v = x[2];
            double theta = x[3];

            double accel = u[0];
            double steer = u[1];

            // Apply limits defined in THIS instance
            if (steer > SteeringLimit)
            {
                steer = SteeringLimit;
            }
            if (steer < -SteeringLimit)
            {
                steer = -SteeringLimit;
            }

            if (accel > AccelLimit)
            {
                accel = AccelLimit;
            }
            if (accel < -AccelLimit)
            {
                accel = -AccelLimit;
            }

            double px_new = px + v * Math.Cos(theta) * dt;
            double py_new = py + v * Math.Sin(theta) * dt;
            double v_new = v + accel * dt;

            // Uses this.WheelBase
            double theta_new = theta + (v / WheelBase) * Math.Tan(steer) * dt;

            return new double[] { px_new, py_new, v_new, theta_new };
        }

        // 2. LINEARIZATION
        public (double[,], double[,]) Linearize(double[] x, double[] u, double dt, int t)
        {
            int nx = x.Length;
            int nu = u.Length;
            double eps = 1e-6;

            double[,] A = new double[nx, nx];
            double[,] B = new double[nx, nu];

            // Calls the instance method Step()
            double[] x_next_base = Step(x, u, dt, t);

            // A Matrix
            for (int i = 0; i < nx; ++i)
            {
                var x_p = (double[])x.Clone();
                x_p[i] += eps;
                var x_next_p = Step(x_p, u, dt, t);
                for (int j = 0; j < nx; ++j)
                {
                    A[j, i] = (x_next_p[j] - x_next_base[j]) / eps;
                }
            }

            // B Matrix
            for (int i = 0; i < nu; ++i)
            {
                var u_p = (double[])u.Clone();
                u_p[i] += eps;
                var x_next_p = Step(x, u_p, dt, t);
                for (int j = 0; j < nx; ++j)
                {
                    B[j, i] = (x_next_p[j] - x_next_base[j]) / eps;
                }
            }

            return (A, B);
        }
    }
}
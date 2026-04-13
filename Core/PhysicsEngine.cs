namespace Core
{
    public class PhysicsEngine
    {
        public double SteeringLimit { get; private set; }
        public double AccelLimit { get; private set; }
        public double WheelBase { get; private set; }

        public PhysicsEngine(double wheelBase, double steeringLimit, double accelLimit)
        {
            WheelBase = wheelBase;
            SteeringLimit = steeringLimit;
            AccelLimit = accelLimit;
        }

        public double[] Step(double[] x, double[] u, double dt, int t)
        {
            double px = x[0];
            double py = x[1];
            double v = x[2];
            double theta = x[3];

            double accel = u[0];
            double steer = u[1];

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

            double theta_new = theta + (v / WheelBase) * Math.Tan(steer) * dt;

            return new double[] { px_new, py_new, v_new, theta_new };
        }

        public (double[,], double[,]) Linearize(double[] x, double[] u, double dt, int t)
        {
            int nx = x.Length;
            int nu = u.Length;
            double eps = 1e-6;

            double[,] A = new double[nx, nx];
            double[,] B = new double[nx, nu];

            double[] x_next_base = Step(x, u, dt, t);

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
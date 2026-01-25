using Core;
using Web.Data;

namespace Web.Services.Scenarios
{
    public class ParkingScenario : ISimulationScenario, ICostModel
    {
        private List<Obstacle> _obstacles = new();

        public int Horizon { get; set; } = 40;
        public double ObstacleRadius { get; set; } = 4.0;
        public double ObstacleWeight { get; set; } = 7500.0;

        private double[,] _Q = {
            { 10, 0, 0, 0 },
            { 0, 10, 0, 0 },
            { 0, 0, 100, 0 },
            { 0, 0, 0, 1 }
        };
        private double[,] _R = {
            { 1, 0 },
            { 0, 100 }
        };

        // Cache the physics engine so we don't recreate it every step
        private PhysicsEngine _physicsEngine;

        public ParkingScenario()
        {
            // "Limo" Physics: Smooth, stable, easier to control precisely
            // WheelBase: 3.5 (Longer)
            // SteeringLimit: 0.6 (Restricted)
            _physicsEngine = new PhysicsEngine(3.5, 0.6, 10.0);
        }

        public void Reset()
        {
            _obstacles.Clear();
        }

        public BaseStateDTO RunStep(double dt, double[] carState)
        {
            var state = carState != null && carState.Length >= 4 ? carState : new double[4];
            var u = ILQR_Controller.Solve(state, Horizon, 5, dt, GetPhysicsModel(), this);
            return new CarStateDTO { Control = new double[] { u.ElementAtOrDefault(0), u.ElementAtOrDefault(1) } };
        }

        public double Evaluate(double[] x, double[] u, double dt, int t)
        {
            double cost = 0;
            double dist = Math.Sqrt(x[0] * x[0] + x[1] * x[1]);
            double targetVel = (dist < 5.0) ? 0.0 : 10.0;
            double[] target = { 0, 0, targetVel, 0 };

            double[] err = new double[4];
            for (int i = 0; i < 4; ++i)
            {
                err[i] = x[i] - target[i];
            }

            while (err[3] > Math.PI)
            {
                err[3] -= 2 * Math.PI;
            }
            while (err[3] < -Math.PI) 
            { 
                err[3] += 2 * Math.PI; 
            }

            cost += Helpers.VectorQuadForm(Helpers.ToColumnVector(err), _Q);
            cost += Helpers.VectorQuadForm(Helpers.ToColumnVector(u), _R);

            foreach (var obs in _obstacles)
            {
                double dSq = Math.Pow(x[0] - obs.X, 2) + Math.Pow(x[1] - obs.Y, 2);
                cost += obs.Weight * Math.Exp(-dSq / (obs.Radius * obs.Radius));
            }
            return cost;
        }

        public void GetDerivatives(double[] x, double[] u, double dt, int t, ref double[,] Q, ref double[,] R, ref double[] q, ref double[] r)
        {
            double dist = Math.Sqrt(x[0] * x[0] + x[1] * x[1]);
            double targetVel = (dist < 5.0) ? 0.0 : 10.0;
            double[] target = { 0, 0, targetVel, 0 };

            double[] err = new double[4];
            for (int i = 0; i < 4; ++i)
            {
                err[i] = x[i] - target[i];
            }

            while (err[3] > Math.PI)
            {
                err[3] -= 2 * Math.PI;
            }
            while (err[3] < -Math.PI)
            {
                err[3] += 2 * Math.PI;
            }

            var xVec = Helpers.ToColumnVector(err);
            var q_std = Helpers.MultiplyMatrices(_Q, xVec);
            for (int i = 0; i < 4; ++i)
            {
                q[i] = 2 * q_std[i, 0];
                for (int j = 0; j < 4; ++j)
                {
                    Q[i, j] = 2 * _Q[i, j];
                }
            }

            var uVec = Helpers.ToColumnVector(u);
            var r_val = Helpers.MultiplyMatrices(_R, uVec);
            for (int i = 0; i < 2; ++i)
            {
                r[i] = 2 * r_val[i, 0];
                for (int j = 0; j < 2; ++j)
                {
                    R[i, j] = 2 * _R[i, j];
                }
            }

            foreach (var obs in _obstacles)
            {
                double dx = x[0] - obs.X;
                double dy = x[1] - obs.Y;
                double distSq = dx * dx + dy * dy;
                double rSq = obs.Radius * obs.Radius;

                double costVal = obs.Weight * Math.Exp(-distSq / rSq);
                double factor = costVal * (-2.0 / rSq);

                q[0] += factor * dx;
                q[1] += factor * dy;

                double hessFactor = -factor;
                if (hessFactor < 0)
                {
                    hessFactor = 0;
                }

                Q[0, 0] += hessFactor;
                Q[1, 1] += hessFactor;
            }
        }

        public void HandleInteraction(double x, double y, string mode)
        {
            if (mode == "AddObstacle")
            {
                _obstacles.Add(new Obstacle { X = x, Y = y, Radius = ObstacleRadius, Weight = ObstacleWeight });
            }
            else if (mode == "RemoveObstacle")
            {
                for (int i = 0; i < _obstacles.Count; i++)
                {
                    double d = Math.Sqrt(Math.Pow(x - _obstacles[i].X, 2) + Math.Pow(y - _obstacles[i].Y, 2));
                    if (d < _obstacles[i].Radius)
                    {
                        _obstacles.RemoveAt(i); break;
                    }
                }
            }
        }

        public void UpdateObstacle(int index, double x, double y)
        {
            if (index >= 0 && index < _obstacles.Count)
            {
                _obstacles[index].X = x; _obstacles[index].Y = y;
            }
        }

        public object GetVisualizationData()
        {
            return _obstacles;
        }

        // Wrap our configured instance in the PhysicsModel DTO
        public PhysicsModel GetPhysicsModel()
        {
            return new PhysicsModel(_physicsEngine.Step, _physicsEngine.Linearize, 4, 2);
        }
    }
}
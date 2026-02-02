using Core;
using Web.Data;
using static Web.Data.Enums;
using System.Text;

namespace Web.Services.Scenarios
{
    public class RacingScenario : ISimulationScenario, ICostModel
    {
        private List<double[]> _trackPoints = new();
        private List<double[]> _processedPath = new();

        private Queue<RacingRecord> _carTrail = new();

        private bool _isDrawing = false;
        private double _trackWidth = 10.0;
        private double _currentTime = 0.0;

        public int Horizon { get; set; } = 30; // Default lower than parking for speed

        // 2. Car Type
        private Enums.CarType _carType = Enums.CarType.Sedan; // Default to GoKart for racing
        public Enums.CarType CurrentCarType
        {
            get => _carType;
            set
            {
                _carType = value;
                UpdatePhysicsEngine();
            }
        }

        // Cost to deviate from the track line (Cross-Track Error)
        public double PrecisionPosition
        {
            get => _Q[0, 0];
            set { _Q[0, 0] = value; _Q[1, 1] = value; }
        }

        // Cost to deviate from target velocity
        public double PrecisionVelocity
        {
            get => _Q[2, 2];
            set => _Q[2, 2] = value;
        }

        // Cost to deviate from track angle (Heading Error)
        public double PrecisionAngle
        {
            get => _Q[3, 3];
            set => _Q[3, 3] = value;
        }

        // 4. Matrix Weights (R - Smoothness)

        public double SmoothnessAccel
        {
            get => _R[0, 0];
            set => _R[0, 0] = value;
        }

        public double SmoothnessSteering
        {
            get => _R[1, 1];
            set => _R[1, 1] = value;
        }

        private double[,] _Q = {
            { 20, 0, 0, 0 },
            { 0, 20, 0, 0 },
            { 0, 0, 10, 0 },
            { 0, 0, 0, 200 }
        };
        private double[,] _R = {
            { 1, 0 },
            { 0, 2000 }
        };

        private PhysicsEngine _physicsEngine;

        public RacingScenario()
        {
            UpdatePhysicsEngine();
        }

        private void UpdatePhysicsEngine()
        {
            switch (_carType)
            {
                case CarType.GoKart:
                    // Twitchy, agile, small turning radius
                    _physicsEngine = new PhysicsEngine(1.5, 0.8, 15.0);
                    break;
                case CarType.Limo:
                    // Heavy, slow turning, stable
                    _physicsEngine = new PhysicsEngine(3.5, 0.5, 8.0);
                    break;
                case CarType.Sedan:
                default:
                    // Balanced
                    _physicsEngine = new PhysicsEngine(2.5, 0.7, 10.0);
                    break;
            }
        }

        public void Reset()
        {
            _trackPoints.Clear();
            _processedPath.Clear();
            _carTrail.Clear();
            _isDrawing = false;
            _currentTime = 0.0;
        }

        public BaseStateDTO RunStep(double dt, double[] carState)
        {
            if (_processedPath.Count == 0)
            {
                return new CarStateDTO { Control = new double[] { 0.0, 0.0 } };
            }

            var u = ILQR_Controller.Solve(carState, 30, 5, dt, GetPhysicsModel(), this);

            double cost = Evaluate(carState, u, dt, 0);

            _carTrail.Enqueue(new RacingRecord(
                _currentTime,
                carState[0], carState[1], carState[2], carState[3],
                u.ElementAtOrDefault(0), // Accel
                u.ElementAtOrDefault(1), // Steering
                cost
            ));
            _currentTime += dt;

            // Sliding Window: Keep approx 1 minute of data
            int minuteOfTrailData = (int)(1.0 / dt * 60.0);
            if (_carTrail.Count > minuteOfTrailData)
            {
                _carTrail.Dequeue();
            }

            return new CarStateDTO { Control = new double[] { u.ElementAtOrDefault(0), u.ElementAtOrDefault(1) } };
        }

        public string GetCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Time,X,Y,Velocity,Theta,Acceleration,Steering,StepCost");
            foreach (var r in _carTrail)
            {
                sb.AppendLine($"{r.Time:F2},{r.X:F4},{r.Y:F4},{r.Velocity:F4},{r.Theta:F4},{r.Acceleration:F4},{r.Steering:F4},{r.Cost:F4}");
            }
            return sb.ToString();
        }

        public double Evaluate(double[] x, double[] u, double dt, int t)
        {
            double[] target = GetTargetState(x, t);
            double cost = 0;

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

            var uVec = Helpers.ToColumnVector(u);
            cost += Helpers.VectorQuadForm(uVec, _R);

            return cost;
        }

        public void GetDerivatives(double[] x, double[] u, double dt, int t, ref double[,] Q, ref double[,] R, ref double[] q, ref double[] r)
        {
            double[] target = GetTargetState(x, t);

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
        }

        private double[] GetTargetState(double[] x, int t)
        {
            if (_processedPath.Count == 0)
            {
                return new double[4];
            }
            int closestIdx = 0;
            double minDSq = double.MaxValue;
            for (int i = 0; i < _processedPath.Count; ++i)
            {
                double d = Math.Pow(x[0] - _processedPath[i][0], 2) + Math.Pow(x[1] - _processedPath[i][1], 2);
                if (d < minDSq)
                {
                    minDSq = d; closestIdx = i;
                }
            }
            int lookAhead = (int)(t * 1.0);
            int targetIdx = (closestIdx + lookAhead) % _processedPath.Count;
            return _processedPath[targetIdx];
        }

        public void HandleInteraction(double x, double y, string mode)
        {
            if (mode == "StartDraw")
            {
                _trackPoints.Clear(); _processedPath.Clear(); _isDrawing = true; _trackPoints.Add(new double[] { x, y });
            }
            else if (mode == "Drawing" && _isDrawing)
            {
                var last = _trackPoints.Last();
                if (Math.Sqrt(Math.Pow(x - last[0], 2) + Math.Pow(y - last[1], 2)) > 2.0)
                {
                    _trackPoints.Add(new double[] { x, y });
                }
            }
            else if (mode == "EndDraw")
            {
                _isDrawing = false; ProcessTrack();
            }
            else if (mode == "ClearTrail")
            {
                _carTrail.Clear();
            }
        }

        private void ProcessTrack()
        {
            if (_trackPoints.Count < 3)
            {
                return;
            }
            _trackPoints.Add(_trackPoints[0]);
            _processedPath.Clear();
            for (int i = 0; i < _trackPoints.Count - 1; ++i)
            {
                var p1 = _trackPoints[i]; var p2 = _trackPoints[i + 1];
                double dist = Math.Sqrt(Math.Pow(p2[0] - p1[0], 2) + Math.Pow(p2[1] - p1[1], 2));
                int steps = Math.Max(1, (int)(dist / 0.5));
                for (int s = 0; s < steps; ++s)
                {
                    double t = (double)s / steps;
                    double ang = Math.Atan2(p2[1] - p1[1], p2[0] - p1[0]);
                    _processedPath.Add(new double[] { p1[0] + (p2[0] - p1[0]) * t, p1[1] + (p2[1] - p1[1]) * t, 15.0, ang });
                }
            }
        }

        public object GetVisualizationData()
        {
            var trailArrays = _carTrail.Select(r => new double[] { r.X, r.Y }).ToList();

            return new RacingVisuals
            {
                Track = _processedPath,
                Trail = trailArrays,
                RawPoints = _isDrawing ? _trackPoints : new List<double[]>()
            };
        }
        public PhysicsModel GetPhysicsModel()
        {
            return new PhysicsModel(_physicsEngine.Step, _physicsEngine.Linearize, 4, 2);
        }
    }
}
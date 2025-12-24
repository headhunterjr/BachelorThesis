using Core;
using Web.Data;

namespace Web.Services
{
    public class SimulationService
    {
        // The "Real" state of the car in the world: [X, Y, Velocity, Angle]
        private double[] _realCarState = new double[4];

        // The Obstacles in the world
        private List<Obstacle> _obstacles = new();

        private double[,] _Q = {
            { 10, 0, 0, 0 },
            { 0, 10, 0, 0 },
            { 0, 0, 100, 0 },
            { 0, 0, 0, 10 }
        };
        private double[,] _R = {
            { 1, 0 },
            { 0, 1000 }
        };

        // Your default starting configuration
        public double[] StartPoint { get; set; } = { 0.0, 0.0, 0.0, 0.0 };
        public double[] TargetPoint { get; set; } = { 0.0, 0.0 };

        // These properties now act as "Live" sliders for the next time you move the car
        public double InitialVelocity { get; set; } = 10.0;
        public double InitialAngle { get; set; } = 0.5;

        public int Horizon { get; set; } = 40;
        public double Dt { get; set; } = 0.1;
        public double ObstacleRadius { get; set; } = 4.0;
        public double ObstacleWeight { get; set; } = 10000.0;

        public void Reset()
        {
            // Reset to the default StartPoint array
            _realCarState = new double[] { StartPoint[0], StartPoint[1], StartPoint[2], StartPoint[3] };
        }

        public CarStateDTO RunStep()
        {
            double[] u = ILQR_Controller.Solve(_realCarState, _Q, _R, _obstacles, Horizon, maxIterations: 5, Dt);
            _realCarState = PhysicsEngine.Step(_realCarState, u, Dt);

            return new CarStateDTO
            {
                X = _realCarState[0],
                Y = _realCarState[1],
                Velocity = _realCarState[2],
                Theta = _realCarState[3],
                Accel = u[0],
                Steer = u[1]
            };
        }

        public List<Obstacle> GetObstacles() => _obstacles;

        public void AddObstacle(double x, double y)
        {
            _obstacles.Add(new Obstacle { X = x, Y = y, Radius = ObstacleRadius, Weight = ObstacleWeight });
        }

        public void UpdateObstaclePosition(int index, double x, double y)
        {
            if (index >= 0 && index < _obstacles.Count)
            {
                _obstacles[index].X = x;
                _obstacles[index].Y = y;
            }
        }

        public CarStateDTO GetCurrentState()
        {
            return new CarStateDTO
            {
                X = _realCarState[0],
                Y = _realCarState[1],
                Velocity = _realCarState[2],
                Theta = _realCarState[3]
            };
        }

        public void SetCarPosition(double x, double y)
        {
            // This is where we sync the sliders and the click
            // X and Y come from the mouse click
            _realCarState[0] = x;
            _realCarState[1] = y;
            // Velocity and Angle come from your sidebar sliders
            _realCarState[2] = InitialVelocity;
            _realCarState[3] = InitialAngle;
        }

        public void ClearAllObstacles()
        {
            _obstacles.Clear();
        }

        public void RemoveObstacle(int index)
        {
            if (index >= 0 && index < _obstacles.Count)
            {
                _obstacles.RemoveAt(index);
            }
        }
    }
}
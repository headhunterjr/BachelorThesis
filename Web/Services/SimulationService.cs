using Core;
using Web.Data;

namespace Web.Services
{
    public class SimulationService
    {
        // The "Real" state of the car in the world
        private double[] _realCarState = { -50.0, -15.0, 10.0, 0.5 };

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

        public int Horizon { get; set; } = 40;
        public double Dt { get; set; } = 0.1;
        public double ObstacleRadius { get; set; } = 4.0;
        public double ObstacleWeight { get; set; } = 10000.0;

        public void Reset()
        {
            _realCarState = new double[] { -50.0, -15.0, 10.0, 0.5 };
        }

        // This method runs one step of the MPC loop
        public CarStateDTO RunStep()
        {
            // 1. Solve for the next move
            double[] u = ILQR_Controller.Solve(_realCarState, _Q, _R, _obstacles, Horizon, maxIterations: 5, Dt);

            // 2. Apply the physics (move the car)
            _realCarState = PhysicsEngine.Step(_realCarState, u, Dt);

            // 3. Package data for the Frontend
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
    }
}

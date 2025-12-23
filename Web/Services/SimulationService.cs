using Core;
using Web.Data;

namespace Web.Services
{
    public class SimulationService
    {
        // The "Real" state of the car in the world
        private double[] _realCarState = { -50.0, -15.0, 10.0, 0.5 };

        // The Obstacles in the world
        private List<Obstacle> _obstacles = new()
        {
            new Obstacle { X = -35.0, Y = -10.0},
            new Obstacle { X = -15.0, Y = -5.0 }
        };

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

        public void Reset()
        {
            _realCarState = new double[] { -50.0, -15.0, 10.0, 0.5 };
        }

        // This method runs one step of the MPC loop
        public CarStateDTO RunStep()
        {
            // 1. Solve for the next move
            double[] u = ILQR_Controller.Solve(_realCarState, _Q, _R, _obstacles, horizon: 40, maxIterations: 5);

            // 2. Apply the physics (move the car)
            _realCarState = PhysicsEngine.Step(_realCarState, u, dt: 0.1);

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
            _obstacles.Add(new Obstacle { X = x, Y = y, Radius = 4.0, Weight = 10000 });
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

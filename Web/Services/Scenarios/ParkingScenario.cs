using System.Collections.Generic;
using Core;
using Web.Data;

namespace Web.Services.Scenarios
{
    public class ParkingScenario : ISimulationScenario
    {
        private List<Obstacle> _obstacles = new();

        // Public Properties for UI Binding
        public int Horizon { get; set; } = 40;
        public double ObstacleRadius { get; set; } = 4.0;
        public double ObstacleWeight { get; set; } = 7500.0;

        private double[,] _Q = {
            { 10, 0, 0, 0 },
            { 0, 10, 0, 0 },
            { 0, 0, 100, 0 },
            { 0, 0, 0, 10 }
        };
        private double[,] _R = {
            { 1, 0 },
            { 0, 1 }
        };

        public void Reset()
        {
            _obstacles.Clear();
        }

        public CarStateDTO RunStep(double dt, double[] carState)
        {
            // Use the public Horizon property here
            var u = ILQR_Controller.Solve(carState, _Q, _R, _obstacles, Horizon, 5, dt, null, 0);
            return new CarStateDTO { Accel = u[0], Steer = u[1] };
        }

        public void HandleInteraction(double x, double y, string mode)
        {
            if (mode == "AddObstacle")
            {
                _obstacles.Add(new Obstacle { X = x, Y = y, Radius = ObstacleRadius, Weight = ObstacleWeight });
            }
            else if (mode == "RemoveObstacle")
            {
                // Simple radius check to remove
                for (int i = 0; i < _obstacles.Count; i++)
                {
                    double dist = System.Math.Sqrt(System.Math.Pow(x - _obstacles[i].X, 2) + System.Math.Pow(y - _obstacles[i].Y, 2));
                    if (dist < _obstacles[i].Radius)
                    {
                        _obstacles.RemoveAt(i);
                        break;
                    }
                }
            }
            else if (mode == "UpdateDragged")
            {
                // This logic is usually handled by index in the UI, 
                // but for simplicity we can handle drag updates in the UI or here.
                // Keeping it simple for now, drag logic stays in Razor for index finding.
            }
        }

        // Helper to update specific obstacle (called from UI dragging)
        public void UpdateObstacle(int index, double x, double y)
        {
            if (index >= 0 && index < _obstacles.Count)
            {
                _obstacles[index].X = x;
                _obstacles[index].Y = y;
            }
        }

        public object GetVisualizationData() => _obstacles;
    }
}
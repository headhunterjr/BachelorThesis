using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Web.Data;

namespace Web.Services.Scenarios
{
    // DTO for passing data to JavaScript
    public class RacingVisuals
    {
        public List<double[]> Track { get; set; } = new();
        public List<double[]> Trail { get; set; } = new();
    }

    public class RacingScenario : ISimulationScenario
    {
        private List<double[]> _trackPoints = new(); // Raw mouse points
        private List<double[]> _processedPath = new(); // The Reference Line
        private List<double[]> _carTrail = new(); // The actual path taken

        private bool _isDrawing = false;
        private double _trackWidth = 10.0;

        // Tuned Weights (High Accuracy)
        private double[,] _Q = 
            { 
                { 20, 0, 0, 0 }, 
                { 0, 20, 0, 0 }, 
                { 0, 0, 10, 0 }, 
                { 0, 0, 0, 200 } 
            };
        private double[,] _R = 
            { 
                { 1, 0 }, 
                { 0, 2000 } 
            };

        public void Reset()
        {
            _trackPoints.Clear();
            _processedPath.Clear();
            _carTrail.Clear(); // Clear the trail on reset
            _isDrawing = false;
        }

        public CarStateDTO RunStep(double dt, double[] carState)
        {
            if (_processedPath.Count == 0) return new CarStateDTO();
            _carTrail.Add(new double[] { carState[0], carState[1] });
            var physics = GetPhysicsModel();
            var u = ILQR_Controller.Solve(carState, _Q, _R, new List<Obstacle>(), 30, 5, dt, physics, _processedPath, _trackWidth);
            return new CarStateDTO { Accel = u.ElementAtOrDefault(0), Steer = u.ElementAtOrDefault(1) };
        }

        public void HandleInteraction(double x, double y, string mode)
        {
            if (mode == "StartDraw")
            {
                _trackPoints.Clear();
                _isDrawing = true;
                _trackPoints.Add(new double[] { x, y });
            }
            else if (mode == "Drawing" && _isDrawing)
            {
                var last = _trackPoints.Last();
                double dist = Math.Sqrt(Math.Pow(x - last[0], 2) + Math.Pow(y - last[1], 2));
                if (dist > 2.0) _trackPoints.Add(new double[] { x, y });
            }
            else if (mode == "EndDraw")
            {
                _isDrawing = false;
                ProcessTrack();
            }
        }

        private void ProcessTrack()
        {
            if (_trackPoints.Count < 3) return;
            _trackPoints.Add(_trackPoints[0]); // Close loop

            _processedPath.Clear();
            for (int i = 0; i < _trackPoints.Count - 1; i++)
            {
                var p1 = _trackPoints[i];
                var p2 = _trackPoints[i + 1];
                double segmentDist = Math.Sqrt(Math.Pow(p2[0] - p1[0], 2) + Math.Pow(p2[1] - p1[1], 2));

                int steps = (int)(segmentDist / 0.5);
                if (steps < 1) steps = 1;

                for (int s = 0; s < steps; s++)
                {
                    double t = (double)s / steps;
                    double px = p1[0] + (p2[0] - p1[0]) * t;
                    double py = p1[1] + (p2[1] - p1[1]) * t;
                    double ang = Math.Atan2(p2[1] - p1[1], p2[0] - p1[0]);
                    _processedPath.Add(new double[] { px, py, 15.0, ang });
                }
            }
        }

        // Return the DTO containing both Track and Trail
        public object GetVisualizationData()
        {
            return new RacingVisuals
            {
                Track = _processedPath,
                Trail = _carTrail
            };
        }

        public PhysicsModel GetPhysicsModel()
        {
            // The default bicycle model: nx=4, nu=2
            return new PhysicsModel(PhysicsEngine.Step, PhysicsEngine.Linearize, 4, 2);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Web.Data;

namespace Web.Services.Scenarios
{
    public class RacingScenario : ISimulationScenario
    {
        private List<double[]> _trackPoints = new(); // Raw points from mouse
        private List<double[]> _processedPath = new(); // Resampled points for MPC
        private bool _isDrawing = false;
        private double _trackWidth = 10.0;

        // Tuning for Racing: Low Position penalty (allow swerving), High Angle penalty (maintain flow)
        private double[,] _Q = {
            { 2, 0, 0, 0 },
            { 0, 2, 0, 0 },
            { 0, 0, 10, 0 },
            { 0, 0, 0, 50 }
        };
        private double[,] _R = {
            { 1, 0 },
            { 0, 500 }
        };

        public void Reset()
        {
            _trackPoints.Clear();
            _processedPath.Clear();
            _isDrawing = false;
        }

        public CarStateDTO RunStep(double dt, double[] carState)
        {
            if (_processedPath.Count == 0) return new CarStateDTO(); // Do nothing if no track

            // Solve WITH path -> Triggers Racing Mode logic
            // We pass an empty obstacle list for now, but you can add obstacles to the track later
            var u = ILQR_Controller.Solve(carState, _Q, _R, new List<Obstacle>(), 30, 5, dt, _processedPath, _trackWidth);

            return new CarStateDTO { Accel = u[0], Steer = u[1] };
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
                // Filter noise: Only add point if it's > 2 meters from the last one
                var last = _trackPoints.Last();
                double dist = Math.Sqrt(Math.Pow(x - last[0], 2) + Math.Pow(y - last[1], 2));
                if (dist > 2.0)
                {
                    _trackPoints.Add(new double[] { x, y });
                }
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

            // 1. Close the loop (Connect end to start)
            _trackPoints.Add(_trackPoints[0]);

            // 2. Resample the path to have consistent density (e.g., every 0.5 meters)
            _processedPath.Clear();
            for (int i = 0; i < _trackPoints.Count - 1; i++)
            {
                var p1 = _trackPoints[i];
                var p2 = _trackPoints[i + 1];
                double segmentDist = Math.Sqrt(Math.Pow(p2[0] - p1[0], 2) + Math.Pow(p2[1] - p1[1], 2));

                int steps = (int)(segmentDist / 0.5); // 0.5m resolution
                if (steps < 1) steps = 1;

                for (int s = 0; s < steps; s++)
                {
                    double t = (double)s / steps;
                    double px = p1[0] + (p2[0] - p1[0]) * t;
                    double py = p1[1] + (p2[1] - p1[1]) * t;

                    // Calculate tangent angle for this segment
                    double ang = Math.Atan2(p2[1] - p1[1], p2[0] - p1[0]);

                    // Target State: [x, y, CruiseSpeed=15.0, Angle]
                    _processedPath.Add(new double[] { px, py, 15.0, ang });
                }
            }
        }

        public object GetVisualizationData()
        {
            return _processedPath;
        }
    }
}
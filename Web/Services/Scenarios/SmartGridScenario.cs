using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Web.Data;

namespace Web.Services.Scenarios
{
    // Data Transfer Object for the Frontend Chart
    public class GridVisuals
    {
        public double[] DemandProfile { get; set; }
        public double[] SolarProfile { get; set; }
        public double[] PriceProfile { get; set; }
        // The "Future Plan" the solver calculated
        public double[] PlannedBattery { get; set; }
        public double[] PlannedGrid { get; set; }
        public double[] PlannedGen { get; set; }
        public double TotalCost { get; set; }
        public double CurrentCost { get; set; }
        public int CurrentStep { get; set; } // Added to track progress line
    }

    public class SmartGridScenario : ISimulationScenario, ICostModel
    {
        // --- CONFIGURATION ---
        public string Season { get; set; } = "Summer"; // "Summer", "Winter"
        public bool HasGenerator { get; set; } = true;
        public bool HasSolar { get; set; } = true;
        public bool IsBlackout { get; set; } = false;

        private double _batteryCapacity = 100.0;
        private double _maxGenPower = 50.0;
        private double _maxGridPower = 100.0;

        // The "Track" (Forecasts)
        private double[] _demand;
        private double[] _solar;
        private double[] _price;

        // Store the last plan for visualization
        private List<double> _batteryHistory = new(); // Changed to List<double> for easier plotting
        private double _accumulatedCost = 0;
        private int _currentStepIndex = 0;

        public SmartGridScenario()
        {
            RegenerateProfiles();
            _batteryHistory.Add(50.0); // Start at 50%
        }

        public void Reset()
        {
            RegenerateProfiles();
            _batteryHistory.Clear();
            _batteryHistory.Add(50.0);
            _accumulatedCost = 0;
            _currentStepIndex = 0;
        }

        // --- PHYSICS MODEL (Time-Varying) ---
        // State x[0]: Battery Energy (kWh)
        // Control u[0]: Grid Power (kW)
        // Control u[1]: Generator Power (kW)
        public PhysicsModel GetPhysicsModel()
        {
            return new PhysicsModel(GridStep, GridLinearize, 1, 2);
        }

        // Exact Physics: E_next = E_now + (P_in - P_out) * dt
        private double[] GridStep(double[] x, double[] u, double dt, int t)
        {
            // Clamp t to horizon to avoid crash if solver looks past profile
            int safeT = Math.Min(t, _demand.Length - 1);

            double currentE = x[0];
            double grid = u[0];
            double gen = u[1];
            double solar = HasSolar ? _solar[safeT] : 0.0;
            double demand = _demand[safeT];

            // Power Balance:
            // Net Flow into Battery = Sources - Loads
            // If Net Flow is positive, battery charges. Negative, it drains.
            double netPower = grid + gen + solar - demand;

            double nextE = currentE + netPower * dt;

            return new double[] { nextE };
        }

        private (double[,], double[,]) GridLinearize(double[] x, double[] u, double dt, int t)
        {
            // System is Linear: x_next = 1*x + dt*u_grid + dt*u_gen + dt*(Solar-Demand)
            // A = [1]
            // B = [dt, dt]
            // The disturbance (Solar-Demand) is constant w.r.t x and u, so it disappears in linearization.

            double[,] A = { { 1.0 } };
            double[,] B = { { dt, dt } };
            return (A, B);
        }

        // --- COST MODEL ---
        public double Evaluate(double[] x, double[] u, double dt, int t)
        {
            int safeT = Math.Min(t, _demand.Length - 1);

            double gridP = u[0];
            double genP = u[1];
            double price = IsBlackout ? 1000.0 : _price[safeT]; // High cost if blackout

            double cost = 0;

            // 1. Economic Cost
            cost += gridP * price * dt; // Money spent on grid
            cost += genP * 0.40 * dt;   // Diesel cost

            // 2. Constraints (Barriers)
            // Battery Limits: 0 < E < 100
            double E = x[0];
            if (E < 0) cost += 1000.0 * Math.Exp(-E); // Soft barrier below 0
            if (E > _batteryCapacity) cost += 1000.0 * Math.Exp(E - _batteryCapacity);

            // Generator Limits: 0 < Gen < Max
            if (genP < 0) cost += 10000.0 * genP * genP; // No reverse generator
            if (genP > _maxGenPower) cost += 100.0 * Math.Pow(genP - _maxGenPower, 2);

            // Generator existence check
            if (!HasGenerator && genP > 0.1) cost += 10000.0;

            // Grid Limits (Capacity)
            if (IsBlackout && Math.Abs(gridP) > 0.1) cost += 10000.0;
            if (Math.Abs(gridP) > _maxGridPower) cost += 100.0 * Math.Pow(Math.Abs(gridP) - _maxGridPower, 2);

            return cost;
        }

        public void GetDerivatives(double[] x, double[] u, double dt, int t, ref double[,] Q, ref double[,] R, ref double[] q, ref double[] r)
        {
            int safeT = Math.Min(t, _demand.Length - 1);
            double price = IsBlackout ? 1000.0 : _price[safeT];

            // Gradients
            // r[0] (Grid) = Price * dt
            r[0] = price * dt;
            // r[1] (Gen) = Fuel * dt
            r[1] = 0.40 * dt;

            // Hessians (Regularization)
            R[0, 0] = 0.01;
            R[1, 1] = 0.01;

            // Battery Center Attraction (keeps it away from 0/100 bounds gently)
            // This approximates the barrier gradient
            double target = _batteryCapacity / 2.0;
            double err = x[0] - target;
            q[0] = 0.1 * err;
            Q[0, 0] = 0.1;
        }

        // --- EXECUTION ---
        public BaseStateDTO RunStep(double dt, double[] currentState)
        {
            var state = currentState != null && currentState.Length == 1 ? currentState : new double[] { 50.0 };

            // We solve for a 24-hour horizon (assuming dt=1 hour for simplicity in this demo logic, 
            // or scaled appropriately). Let's say Horizon=48 steps (2 days)
            var u = ILQR_Controller.Solve(state, 48, 10, dt, GetPhysicsModel(), this);

            // Cost Accumulation for display
            double stepCost = Evaluate(state, u, dt, _currentStepIndex);
            _accumulatedCost += stepCost;
            _currentStepIndex++;

            // NOTE: We don't have access to the full trajectory from Solve() directly in the DTO return 
            // without modifying ILQR return type. 
            // BUT, for the "RunStep" simulation (advancing time), we only need the first input.

            // Simulate one step forward to record history for the graph
            var nextState = GridStep(state, u, dt, _currentStepIndex - 1);
            if (_batteryHistory.Count <= _currentStepIndex)
            {
                _batteryHistory.Add(nextState[0]);
            }

            // However, to Visualize the plan, we will re-simulate locally:
            return new SmartGridStateDTO
            {
                State = state,
                Control = u
            };
        }

        public object GetVisualizationData()
        {
            // For the graph, we want to show what the solver IS PLANNING.
            // Since Solve() only returns u[0], we can't show the future plan exactly.
            // To fix this properly, we'd change Solve() to return the whole path.
            // For now, we will return the "World State" (Profiles).

            double[] historyArray = new double[_demand.Length];
            for (int i = 0; i < historyArray.Length; i++)
            {
                if (i < _batteryHistory.Count) historyArray[i] = _batteryHistory[i];
                else historyArray[i] = 0; // Future unknown in this simplified view
            }

            return new GridVisuals
            {
                DemandProfile = _demand,
                SolarProfile = _solar,
                PriceProfile = _price,
                // Placeholder for battery plan until ILQR returns trajectory
                PlannedBattery = historyArray,
                CurrentCost = _accumulatedCost,
                CurrentStep = _currentStepIndex
            };
        }

        public void HandleInteraction(double x, double y, string mode)
        {
            if (mode == "ToggleGen") HasGenerator = !HasGenerator;
            if (mode == "ToggleSolar") HasSolar = !HasSolar;
            if (mode == "ToggleBlackout") IsBlackout = !IsBlackout;
            if (mode == "NextSeason") Season = (Season == "Summer") ? "Winter" : "Summer";

            RegenerateProfiles();
        }

        private void RegenerateProfiles()
        {
            int steps = 50;
            _demand = new double[steps];
            _solar = new double[steps];
            _price = new double[steps];
            Random rnd = new Random();

            for (int t = 0; t < steps; t++)
            {
                double hour = (t / (double)steps) * 24.0;

                // Demand: Peak at 19:00
                _demand[t] = 20.0 + 15.0 * Math.Exp(-Math.Pow(hour - 19, 2) / 10.0);
                _demand[t] += (rnd.NextDouble() - 0.5) * 5.0; // Noise

                // Solar: Peak at 12:00
                if (HasSolar)
                {
                    double peak = (Season == "Summer") ? 60.0 : 10.0;
                    double width = (Season == "Summer") ? 10.0 : 5.0;
                    _solar[t] = peak * Math.Exp(-Math.Pow(hour - 12, 2) / width);
                }

                // Price
                if (IsBlackout)
                {
                    _price[t] = 1000.0;
                }
                else
                {
                    _price[t] = 0.10; // Base
                    if (hour > 17 && hour < 21) _price[t] = 0.50; // Peak
                }
            }
        }
    }
}
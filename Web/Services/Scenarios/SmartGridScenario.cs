using Core;
using Web.Data;

namespace Web.Services.Scenarios
{
    // Data Transfer Object for the Frontend Chart
    public class GridVisuals
    {
        public double[]? DemandProfile { get; set; }
        public double[]? SolarProfile { get; set; }
        public double[]? PriceProfile { get; set; }
        // The "Future Plan" the solver calculated
        public double[]? PlannedBattery { get; set; }
        public double[]? PlannedGrid { get; set; }
        public double[]? PlannedGen { get; set; }
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

        // --- CUSTOMIZABLE PARAMETERS ---

        // 1. System Specifications (Physical Limits)
        // Converted from private fields to public properties for the UI
        public double BatteryCapacity { get; set; } = 100.0; // kWh
        public double MaxGenPower { get; set; } = 50.0;      // kW
        public double MaxGridPower { get; set; } = 100.0;    // kW

        // 2. Economics (Costs)
        // Cost of Diesel per kWh produced
        public double FuelCost { get; set; } = 0.40;

        // 3. Solver Tuning
        // How hard we penalize going below 0% or above 100% battery
        public double ConstraintWeight { get; set; } = 1000.0;

        // --- DATA STORAGE ---

        // 1. Base Data (The "Random" part - preserves shape when toggling switches)
        private double[] _baseDemand;
        private double[] _baseSolar;  // Normalized 0.0 to 1.0 solar curve

        // 2. Active Profiles (The "Effective" part - sent to Solver/Visuals)
        private double[] _demand;
        private double[] _solar;
        private double[] _price;

        // 3. Simulation History
        // Store the last plan for visualization
        private List<double> _batteryHistory = new();
        private List<double> _gridHistory = new();
        private List<double> _genHistory = new();

        private double _accumulatedCost = 0;
        private int _currentStepIndex = 0;

        public SmartGridScenario()
        {
            GenerateRandomDay(); // Create the random noise once
            ApplySystemState();  // Apply the initial settings (Summer, No Blackout, etc)

            // Initial history state
            _batteryHistory.Add(50.0); // Start at 50%
            _gridHistory.Add(0.0);
            _genHistory.Add(0.0);
        }

        public void Reset()
        {
            GenerateRandomDay(); // New random seed on reset
            ApplySystemState();

            _batteryHistory.Clear();
            _batteryHistory.Add(50.0);

            _gridHistory.Clear();
            _gridHistory.Add(0.0);

            _genHistory.Clear();
            _genHistory.Add(0.0);

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

            // Use the ACTIVE profiles
            double solar = _solar[safeT]; // Was HasSolar ? ... now handled in ApplySystemState
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

            // Use ACTIVE price (which reflects Blackout state)
            double price = _price[safeT];

            double cost = 0;

            // 1. Economic Cost
            cost += gridP * price * dt;     // Money spent on grid
            cost += genP * FuelCost * dt;   // Diesel cost (Customizable)

            // 2. Constraints (Barriers)
            // Battery Limits: 0 < E < Capacity
            double E = x[0];
            if (E < 0)
            {
                cost += ConstraintWeight * Math.Exp(-E); // Soft barrier below 0
            }

            if (E > BatteryCapacity)
            {
                cost += ConstraintWeight * Math.Exp(E - BatteryCapacity);
            }

            // Generator Limits: 0 < Gen < Max
            if (genP < 0)
            {
                cost += (ConstraintWeight * 10) * genP * genP; // No reverse generator
            }
            if (genP > MaxGenPower)
            {
                cost += (ConstraintWeight / 10.0) * Math.Pow(genP - MaxGenPower, 2);
            }

            // Generator existence check
            // Use quadratic penalty (u^2) instead of constant step so solver feels the slope
            if (!HasGenerator)
            {
                cost += ConstraintWeight * 100.0 * genP * genP;
            }

            // Grid Limits (Capacity)
            if (IsBlackout)
            {
                // In blackout, Grid Power > 0 is heavily penalized
                // But we allow 0 (floating), so penalize usage magnitude
                cost += ConstraintWeight * 100.0 * gridP * gridP;
            }
            if (Math.Abs(gridP) > MaxGridPower)
            {
                cost += (ConstraintWeight / 10.0) * Math.Pow(Math.Abs(gridP) - MaxGridPower, 2);
            }

            return cost;
        }

        public void GetDerivatives(double[] x, double[] u, double dt, int t, ref double[,] Q, ref double[,] R, ref double[] q, ref double[] r)
        {
            int safeT = Math.Min(t, _demand.Length - 1);
            double price = _price[safeT];
            double genP = u[1];
            double gridP = u[0];

            // Gradients
            // r[0] (Grid) = Price * dt
            r[0] = price * dt;
            // r[1] (Gen) = Fuel * dt
            r[1] = FuelCost * dt;

            // Hessians (Regularization)
            R[0, 0] = 0.01;
            R[1, 1] = 0.01;

            // Add derivatives for disabled generator
            if (!HasGenerator)
            {
                double K = ConstraintWeight * 100.0;
                r[1] += 2 * K * genP;
                R[1, 1] += 2 * K;
            }

            // Add derivatives for Blackout
            if (IsBlackout)
            {
                double K = ConstraintWeight * 100.0;
                r[0] += 2 * K * gridP;
                R[0, 0] += 2 * K;
            }

            // Battery Center Attraction (keeps it away from 0/100 bounds gently)
            // This approximates the barrier gradient
            double target = BatteryCapacity / 2.0;
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

            // Append histories if we haven't already for this step
            if (_batteryHistory.Count <= _currentStepIndex)
            {
                _batteryHistory.Add(nextState[0]);
                _gridHistory.Add(u[0]);
                _genHistory.Add(u[1]);
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
            double[] historyGrid = new double[_demand.Length];
            double[] historyGen = new double[_demand.Length];

            for (int i = 0; i < historyArray.Length; ++i)
            {
                if (i < _batteryHistory.Count)
                {
                    historyArray[i] = _batteryHistory[i];
                    // Also populate grid/gen history, handling potential index mismatches gracefully
                    if (i < _gridHistory.Count) historyGrid[i] = _gridHistory[i];
                    if (i < _genHistory.Count) historyGen[i] = _genHistory[i];
                }
                else
                {
                    historyArray[i] = 0; // Future unknown in this simplified view
                }
            }

            // Returns the ACTIVE profiles (_demand, _solar, _price) 
            return new GridVisuals
            {
                DemandProfile = _demand,
                SolarProfile = _solar,
                PriceProfile = _price,
                // Placeholder for battery plan until ILQR returns trajectory
                PlannedBattery = historyArray,
                PlannedGrid = historyGrid, // Passed to visualization
                PlannedGen = historyGen,   // Passed to visualization
                CurrentCost = _accumulatedCost,
                CurrentStep = _currentStepIndex
            };
        }

        public void HandleInteraction(double x, double y, string mode)
        {
            if (mode == "ToggleGen")
            {
                HasGenerator = !HasGenerator;
            }
            if (mode == "ToggleSolar")
            {
                HasSolar = !HasSolar;
            }
            if (mode == "ToggleBlackout")
            {
                IsBlackout = !IsBlackout;
            }

            if (mode == "NextSeason")
            {
                Season = (Season == "Summer") ? "Winter" : "Summer";
                // Only re-generate random noise on Season change or Reset
                GenerateRandomDay();
            }

            // Always re-calculate the effective curves (Price/Solar) based on the new switches
            ApplySystemState();
        }

        // 1. Generate the "Random" part (Base Demand / Sun Shape)
        private void GenerateRandomDay()
        {
            int steps = 50;
            _baseDemand = new double[steps];
            _baseSolar = new double[steps];
            _price = new double[steps]; // Will be overwritten by ApplySystemState, but size it here

            Random rnd = new Random();

            for (int t = 0; t < steps; ++t)
            {
                double hour = (t / (double)steps) * 24.0;

                // Base Demand: Peak at 19:00
                _baseDemand[t] = 20.0 + 15.0 * Math.Exp(-Math.Pow(hour - 19, 2) / 10.0);
                _baseDemand[t] += (rnd.NextDouble() - 0.5) * 5.0; // Noise

                // Base Solar Shape: Normalized Bell curve (0.0 to 1.0) peaking at 12:00
                _baseSolar[t] = Math.Exp(-Math.Pow(hour - 12, 2) / 10.0);
                if (_baseSolar[t] < 0.01) _baseSolar[t] = 0;
            }
        }

        // 2. Apply the "Switches" (Scale Solar, Set Prices)
        private void ApplySystemState()
        {
            int steps = _baseDemand.Length;
            _demand = new double[steps];
            _solar = new double[steps];
            _price = new double[steps];

            double solarPeak = (Season == "Summer") ? 60.0 : 15.0;

            for (int t = 0; t < steps; ++t)
            {
                double hour = (t / (double)steps) * 24.0;

                // Demand is constant unless we add modifiers later
                _demand[t] = _baseDemand[t];

                // Solar is Base Shape * Peak * Toggle
                if (HasSolar)
                {
                    _solar[t] = _baseSolar[t] * solarPeak;
                }
                else
                {
                    _solar[t] = 0.0;
                }

                // Price Logic
                if (IsBlackout)
                {
                    _price[t] = 1000.0; // Black/Red bar
                }
                else
                {
                    _price[t] = 0.10; // Base rate
                    // Peak pricing 5pm - 9pm
                    if (hour > 17 && hour < 21)
                    {
                        _price[t] = 0.50; // Peak
                    }
                }
            }
        }
    }
}
using Core;
using System.Text;
using Web.Data;

namespace Web.Services.Scenarios
{
    public class GridScenario : ISimulationScenario, ICostModel
    {
        // --- CONFIGURATION ---
        public string Season { get; set; } = "Summer"; // Summer, Spring, Autumn, Winter
        public bool HasGenerator { get; set; } = true;
        public bool HasSolar { get; set; } = true;
        public bool IsBlackout { get; set; } = false;
        public bool UsePeakPricing { get; set; } = true;
        public bool IsCloudy { get; set; } = false;

        // --- CUSTOMIZABLE PARAMETERS ---
        public double BatteryCapacity { get; set; } = 100.0;
        public double MaxGenPower { get; set; } = 50.0;
        public double MaxGridPower { get; set; } = 100.0;
        public double BatteryEfficiency { get; set; } = 1.0; // kept linear & consistent with linearization

        public double FuelCost { get; set; } = 0.40;
        public double BasePrice { get; set; } = 0.10;
        public double PeakPrice { get; set; } = 0.50;
        public int PeakStartHour { get; set; } = 17;
        public int PeakEndHour { get; set; } = 21;
        public double SellPriceRatio { get; set; } = 0.5; // not used for execution (we disallow negative grid execution)

        public double ConstraintWeight { get; set; } = 1000.0;
        public int Horizon { get; set; } = 12;
        public double InitialBatteryPercent { get; set; } = 50.0;
        public double ForecastError { get; set; } = 0.20;
        public double GridSmoothing { get; set; } = 0.05;

        // --- INTERNAL STATE ---
        private List<GridRecord> _history = new();

        private List<double> _batteryHistory = new();
        private List<double> _gridHistory = new();
        private List<double> _genHistory = new();

        // Base arrays (shape + noise)
        private double[] _baseDemand;
        private double[] _baseSolarShape;
        private double[] _baseSolarNoise;

        // Active profiles (effective)
        private double[] _demand;
        private double[] _actualSolar;
        private double[] _forecastSolar;
        private double[] _price;

        private bool _useForecast = false;
        private double _accumulatedFinancialCost = 0;
        private int _currentStepIndex = 0;

        // profile resolution used for generating arrays (set when dt changes)
        private double _profileDt = 1.0;

        public GridScenario()
        {
            GenerateEnvironment();
            ResetHistory();
        }

        public void Reset()
        {
            GenerateEnvironment();
            ResetHistory();
        }

        private void ResetHistory()
        {
            _history.Clear();
            _batteryHistory.Clear();
            _gridHistory.Clear();
            _genHistory.Clear();

            double startEnergy = BatteryCapacity * (InitialBatteryPercent / 100.0);
            _batteryHistory.Add(startEnergy);
            _gridHistory.Add(0.0);
            _genHistory.Add(0.0);

            _accumulatedFinancialCost = 0;
            _currentStepIndex = 0;
        }

        // --- PHYSICS MODEL ---
        public PhysicsModel GetPhysicsModel()
        {
            // Use consistent linear model so ILQR's linearization is stable.
            return new PhysicsModel(GridStep, GridLinearize, 1, 2);
        }

        private double[] GridStep(double[] x, double[] u, double dt, int t)
        {
            if (_demand == null || _actualSolar == null) return x;

            int safeT = Math.Min(t, _demand.Length - 1);

            double solar = _useForecast ? _forecastSolar[safeT] : _actualSolar[safeT];
            double demand = _demand[safeT];

            double currentE = x[0];
            double gridP = u.ElementAtOrDefault(0);
            double genP = u.ElementAtOrDefault(1);

            // Net flow into battery: sources - demand
            double netPower = gridP + genP + solar - demand;

            // Apply a single linear efficiency factor (keeps linearization simple & predictable).
            double eff = Math.Clamp(BatteryEfficiency, 0.0, 1.0);

            double nextE = currentE + netPower * dt * eff;

            // Only clamp in execution (when _useForecast == false)
            if (!_useForecast)
            {
                if (nextE < 0) nextE = 0;
                if (nextE > BatteryCapacity) nextE = BatteryCapacity;
            }

            return new double[] { nextE };
        }

        private (double[,], double[,]) GridLinearize(double[] x, double[] u, double dt, int t)
        {
            // Linearized model consistent with GridStep:
            // x_next = 1*x + dt*eff * [1, 1] * u + dt*eff*(solar - demand) (disturbance)
            double eff = Math.Clamp(BatteryEfficiency, 0.0, 1.0);

            double[,] A = { { 1.0 } };
            double[,] B = { { dt * eff, dt * eff } };
            return (A, B);
        }

        // --- COST MODEL ---
        public double Evaluate(double[] x, double[] u, double dt, int t)
        {
            if (_price == null) return 0;
            int safeT = Math.Min(t, _demand.Length - 1);
            double price = _price[safeT];

            double gridP = u.ElementAtOrDefault(0);
            double genP = u.ElementAtOrDefault(1);
            double E = x[0];

            double cost = 0;

            // Linear economic cost (simple and consistent)
            // We treat gridP as positive = import. For the solver, negative grid will be penalized heavily below.
            cost += gridP * price * dt;
            cost += genP * FuelCost * dt;

            // Quadratic smoothing on grid usage (to penalize spikes)
            cost += GridSmoothing * gridP * gridP * dt;

            // Soft-box constraints for battery
            if (E < 0) cost += ConstraintWeight * Math.Exp(-E);
            if (E > BatteryCapacity) cost += ConstraintWeight * Math.Exp(E - BatteryCapacity);

            // Generator constraints
            if (genP < 0) cost += (ConstraintWeight * 10) * genP * genP;
            if (genP > MaxGenPower) cost += (ConstraintWeight / 10.0) * Math.Pow(genP - MaxGenPower, 2);
            if (!HasGenerator) cost += ConstraintWeight * 100.0 * genP * genP;

            // Grid constraints / blackout
            if (IsBlackout)
            {
                // In blackout, using grid is heavily penalized (we also clamp execution to 0)
                cost += ConstraintWeight * 100.0 * gridP * gridP;
            }
            else if (Math.Abs(gridP) > MaxGridPower)
            {
                cost += (ConstraintWeight / 10.0) * Math.Pow(Math.Abs(gridP) - MaxGridPower, 2);
            }

            // Discourage negative grid (selling) in planning unless you explicitly want it;
            // this is an extra soft penalty so solver won't plan to sell which we don't execute.
            if (gridP < 0)
            {
                cost += ConstraintWeight * 100.0 * gridP * gridP;
            }

            return cost;
        }

        public void GetDerivatives(double[] x, double[] u, double dt, int t, ref double[,] Q, ref double[,] R, ref double[] q, ref double[] r)
        {
            if (_price == null) return;
            int safeT = Math.Min(t, _demand.Length - 1);
            double price = _price[safeT];
            double gridP = u.ElementAtOrDefault(0);
            double genP = u.ElementAtOrDefault(1);

            // Linear gradients matching Evaluate
            r[0] = price * dt; // grid marginal cost
            r[1] = FuelCost * dt; // generator marginal cost

            // Hessians (regularization)
            double smoothingHessian = GridSmoothing * 2.0 * dt;
            Q[0, 0] = 0.1; // small battery center attraction
            R[0, 0] = 0.001 + smoothingHessian; // grid control curvature
            R[1, 1] = 0.001; // gen control curvature

            if (!HasGenerator) R[1, 1] += 2 * ConstraintWeight * 100.0;
            if (IsBlackout) R[0, 0] += 2 * ConstraintWeight * 100.0;

            // battery attraction toward middle
            double target = BatteryCapacity / 2.0;
            q[0] = 0.1 * (x[0] - target);
        }

        // --- SIMULATION STEP ---
        public BaseStateDTO RunStep(double dt, double[] currentState)
        {
            // If dt changed externally, regenerate environment arrays to the new resolution
            if (Math.Abs(dt - _profileDt) > 1e-9)
            {
                _profileDt = dt > 0 ? dt : 1.0;
                GenerateEnvironment();
            }

            double actualDt = dt;
            double startEnergy = BatteryCapacity * (InitialBatteryPercent / 100.0);
            var state = currentState != null && currentState.Length == 1 ? currentState : new double[] { startEnergy };

            // 1) Plan using forecast
            _useForecast = true;
            var plannedU = ILQR_Controller.Solve(state, Horizon, 10, actualDt, GetPhysicsModel(), this);

            // 2) Convert planner output -> executed controls (apply physical limits, clamp negative grid)
            double plannedGrid = plannedU.ElementAtOrDefault(0);
            double plannedGen = plannedU.ElementAtOrDefault(1);

            // Execution clamping
            double execGrid = plannedGrid;
            // We disallow negative imports (no selling) in execution: clamp to >= 0
            execGrid = Math.Max(0.0, execGrid);
            if (IsBlackout) execGrid = 0.0; // no import allowed in blackout (penalized heavily)
            execGrid = Math.Min(execGrid, MaxGridPower);

            double execGen = plannedGen;
            if (!HasGenerator) execGen = 0.0;
            execGen = Math.Max(0.0, execGen);
            execGen = Math.Min(execGen, MaxGenPower);

            var executedU = new double[] { execGrid, execGen };

            // 3) Execute using actual solar & physics
            _useForecast = false;
            var nextState = GridStep(state, executedU, actualDt, _currentStepIndex);

            // 4) Bookkeeping: compute real costs based on executed controls
            int safeT = Math.Min(_currentStepIndex, _demand.Length - 1);
            double realGridCost = execGrid * _price[safeT] * actualDt;
            double realGenCost = execGen * FuelCost * actualDt;
            double stepFinancialCost = realGridCost + realGenCost;
            _accumulatedFinancialCost += stepFinancialCost;

            // 5) Save history (record executed values)
            _history.Add(new GridRecord(
                _currentStepIndex * actualDt,
                _demand[safeT],
                _actualSolar[safeT],
                _price[safeT],
                state[0],
                execGrid,
                execGen,
                stepFinancialCost
            ));

            _currentStepIndex++;

            if (_batteryHistory.Count <= _currentStepIndex)
            {
                _batteryHistory.Add(nextState[0]);
                _gridHistory.Add(execGrid);
                _genHistory.Add(execGen);
            }

            // Return the planned state/control to the caller (for display or further processing).
            // Note: visuals / csv / histories reflect the executed values.
            return new GridStateDTO { State = nextState, Control = plannedU };
        }

        // --- CSV EXPORT ---
        public string GetCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Hour,Demand(kW),Solar(kW),Price($/kWh),Battery(kWh),Grid(kW),Gen(kW),StepCost($)");
            foreach (var r in _history)
            {
                sb.AppendLine($"{r.Hour:F2},{r.Demand:F2},{r.Solar:F2},{r.Price:F2},{r.BatteryLevel:F2},{r.GridUsage:F2},{r.GenUsage:F2},{r.CurrentCost:F2}");
            }
            return sb.ToString();
        }

        public object GetVisualizationData()
        {
            int len = _demand.Length;
            double[] histBat = new double[len];
            double[] histGrid = new double[len];
            double[] histGen = new double[len];

            for (int i = 0; i < len; ++i)
            {
                if (i < _batteryHistory.Count)
                {
                    histBat[i] = _batteryHistory[i];
                    if (i < _gridHistory.Count) histGrid[i] = _gridHistory[i];
                    if (i < _genHistory.Count) histGen[i] = _genHistory[i];
                }
                else
                {
                    histBat[i] = 0;
                    histGrid[i] = 0;
                    histGen[i] = 0;
                }
            }

            // CurrentStep is allowed to equal len (sim finished). JS will hide the dotted line when currentStep >= steps.
            int reportedStep = _currentStepIndex;

            return new GridVisuals
            {
                DemandProfile = _demand,
                SolarProfile = _actualSolar,
                PriceProfile = _price,
                PlannedBattery = histBat,
                PlannedGrid = histGrid,
                PlannedGen = histGen,
                CurrentCost = _accumulatedFinancialCost,
                CurrentStep = reportedStep
            };
        }

        public void HandleInteraction(double x, double y, string mode)
        {
            if (string.IsNullOrEmpty(mode))
            {
                // refresh / noop
            }
            else if (mode == "ToggleGen")
            {
                HasGenerator = !HasGenerator;
            }
            else if (mode == "ToggleSolar")
            {
                HasSolar = !HasSolar;
            }
            else if (mode == "ToggleBlackout")
            {
                IsBlackout = !IsBlackout;
            }
            else if (mode == "ToggleCloudy")
            {
                IsCloudy = !IsCloudy;
            }
            else if (mode.StartsWith("SetSeason:"))
            {
                var val = mode.Substring("SetSeason:".Length);
                if (!string.IsNullOrEmpty(val))
                {
                    Season = val;
                    GenerateEnvironment();
                    return;
                }
            }
            else if (mode.StartsWith("SetCloudy:"))
            {
                var val = mode.Substring("SetCloudy:".Length);
                if (bool.TryParse(val, out var flag))
                {
                    IsCloudy = flag;
                    ApplySystemState();
                    return;
                }
            }
            else if (mode.StartsWith("SetDt:") || mode.StartsWith("SetStepSize:"))
            {
                var prefix = mode.StartsWith("SetDt:") ? "SetDt:" : "SetStepSize:";
                var val = mode.Substring(prefix.Length);
                if (double.TryParse(val, out var d) && d > 0)
                {
                    _profileDt = d;
                    GenerateEnvironment();
                    return;
                }
            }

            // If profile resolution changed in other ways, ensure we re-generate or reapply
            int expectedSteps = Math.Max(2, (int)(24.0 / _profileDt) + 1);
            if (_baseDemand == null || _baseDemand.Length != expectedSteps)
            {
                GenerateEnvironment();
            }
            else
            {
                ApplySystemState();
            }
        }

        private void GenerateEnvironment()
        {
            int steps = Math.Max(2, (int)(24.0 / _profileDt) + 1);

            _baseDemand = new double[steps];
            _baseSolarShape = new double[steps];
            _baseSolarNoise = new double[steps];
            _demand = new double[steps];
            _forecastSolar = new double[steps];
            _actualSolar = new double[steps];
            _price = new double[steps];

            Random rnd = new Random();

            for (int t = 0; t < steps; ++t)
            {
                double hour = t * _profileDt;

                double demandCurve = 20.0 + 15.0 * Math.Exp(-Math.Pow(hour - 19, 2) / 10.0);
                double noise = Math.Sin(hour * 0.8) * 2.0 + (rnd.NextDouble() - 0.5) * 3.0;
                _baseDemand[t] = Math.Max(0, demandCurve + noise);

                double solarCurve = Math.Exp(-Math.Pow(hour - 12, 2) / 8.0);
                if (solarCurve < 0.01) solarCurve = 0;
                _baseSolarShape[t] = solarCurve;

                _baseSolarNoise[t] = (rnd.NextDouble() - 0.5) * 2.0;
            }

            ApplySystemState();
        }

        private void ApplySystemState()
        {
            if (_baseDemand == null) return;
            int steps = _baseDemand.Length;

            double solarPeak = Season switch
            {
                "Summer" => 60.0,
                "Spring" => 45.0,
                "Autumn" => 30.0,
                "Winter" => 15.0,
                _ => 30.0
            };

            if (IsCloudy) solarPeak *= 0.4;

            for (int t = 0; t < steps; ++t)
            {
                double hour = t * _profileDt;

                _demand[t] = _baseDemand[t];

                if (!HasSolar)
                {
                    _forecastSolar[t] = 0;
                    _actualSolar[t] = 0;
                }
                else
                {
                    double baseVal = _baseSolarShape[t] * solarPeak;
                    _forecastSolar[t] = baseVal;

                    double noise = _baseSolarNoise[t];
                    double errorMag = solarPeak * ForecastError;
                    _actualSolar[t] = Math.Max(0, baseVal + (noise * errorMag));
                }

                if (IsBlackout)
                {
                    _price[t] = 1000.0;
                }
                else
                {
                    _price[t] = BasePrice;
                    if (UsePeakPricing && hour >= PeakStartHour && hour < PeakEndHour)
                    {
                        _price[t] = PeakPrice;
                    }
                }
            }
        }
    }
}

using Core;
using System.Text;
using Web.Data;

namespace Web.Services.Scenarios
{
    public class GridScenario : ISimulationScenario, ICostModel
    {
        public string Season { get; set; } = "Summer";
        public bool HasGenerator { get; set; } = true;
        public bool HasSolar { get; set; } = true;
        public bool IsBlackout { get; set; } = false;
        public bool UsePeakPricing { get; set; } = true;
        public bool IsCloudy { get; set; } = false;

        public double BatteryCapacity { get; set; } = 100.0;
        public double TargetBatteryPercent { get; set; } = 20.0;
        public double MaxGenPower { get; set; } = 50.0;
        public double MaxGridPower { get; set; } = 100.0;
        public double BatteryEfficiency { get; set; } = 1.0;

        public double BatteryAttraction
        {
            get => _Q[0, 0];
            set => _Q[0, 0] = value;
        }

        public double GridControlPenalty
        {
            get => _R[0, 0];
            set => _R[0, 0] = value;
        }

        public double GenControlPenalty
        {
            get => _R[1, 1];
            set => _R[1, 1] = value;
        }

        public double FuelCost { get; set; } = 0.40;
        public double BasePrice { get; set; } = 0.10;
        public double PeakPrice { get; set; } = 0.50;
        public int PeakStartHour { get; set; } = 17;
        public int PeakEndHour { get; set; } = 21;

        public double ConstraintWeight { get; set; } = 1000.0;
        public int Horizon { get; set; } = 12;
        public double InitialBatteryPercent { get; set; } = 50.0;
        public double ForecastError { get; set; } = 0.0;

        private List<GridRecord> _history = new();

        private List<double> _batteryHistory = new();
        private List<double> _gridHistory = new();
        private List<double> _genHistory = new();

        private double[] _baseDemand;
        private double[] _baseSolarShape;
        private double[] _baseSolarNoise;

        private double[] _demand;
        private double[] _actualSolar;
        private double[] _forecastSolar;
        private double[] _price;

        private bool _useForecast = false;
        private double _accumulatedFinancialCost = 0;
        private int _currentStepIndex = 0;

        private double _profileDt = 1.0;

        private double[,] _Q =
            {
                { 0.001 }
            };

        private double[,] _R =
            {
                { 0.008, 0 },
                { 0, 0.008 }
            };

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

        public PhysicsModel GetPhysicsModel()
        {
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

            double netPower = gridP + genP + solar - demand;

            double eff = Math.Clamp(BatteryEfficiency, 0.0, 1.0);

            double nextE = currentE + netPower * dt * eff;

            if (!_useForecast)
            {
                if (nextE < 0) nextE = 0;
                if (nextE > BatteryCapacity) nextE = BatteryCapacity;
            }

            return new double[] { nextE };
        }

        private (double[,], double[,]) GridLinearize(double[] x, double[] u, double dt, int t)
        {
            double eff = Math.Clamp(BatteryEfficiency, 0.0, 1.0);

            double[,] A = { { 1.0 } };
            double[,] B = { { dt * eff, dt * eff } };
            return (A, B);
        }

        public double Evaluate(double[] x, double[] u, double dt, int t)
        {
            if (_price == null) return 0;
            int safeT = Math.Min(t, _demand.Length - 1);
            double price = _price[safeT];

            double gridP = u.ElementAtOrDefault(0);
            double genP = u.ElementAtOrDefault(1);
            double E = x[0];

            double cost = 0;

            cost += gridP * price * dt;
            cost += genP * FuelCost * dt;

            cost += _R[0, 0] * gridP * gridP * dt;
            cost += _R[1, 1] * genP * genP * dt;

            double targetCharge = BatteryCapacity * (TargetBatteryPercent / 100.0);
            double dev = E - targetCharge;

            if (dev < 0)
            {
                cost += _Q[0, 0] * 3.0 * dev * dev;
            }
            else
            {
                cost += _Q[0, 0] * 0.5 * dev * dev;
            }

            if (E < 0) cost += ConstraintWeight * Math.Exp(-E) * dt;
            if (E > BatteryCapacity) cost += ConstraintWeight * Math.Exp(E - BatteryCapacity) * dt;

            if (genP < 0) cost += (ConstraintWeight * 100) * genP * genP * dt;
            if (genP > MaxGenPower) cost += (ConstraintWeight / 10.0) * Math.Pow(genP - MaxGenPower, 2) * dt;
            if (!HasGenerator) cost += ConstraintWeight * 100 * genP * genP * dt;

            if (IsBlackout)
            {
                cost += ConstraintWeight * 100.0 * gridP * gridP * dt;
            }
            else if (Math.Abs(gridP) > MaxGridPower)
            {
                cost += (ConstraintWeight / 10.0) * Math.Pow(Math.Abs(gridP) - MaxGridPower, 2) * dt;
            }

            if (gridP < 0)
            {
                cost += ConstraintWeight * 100.0 * gridP * gridP * dt;
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
            double E = x[0];

            r[0] = price * dt;
            r[1] = FuelCost * dt;

            R[0, 0] = 2.0 * _R[0, 0] * dt;
            R[1, 1] = 2.0 * _R[1, 1] * dt;

            double targetCharge = BatteryCapacity * (TargetBatteryPercent / 100.0);
            double dev = E - targetCharge;

            if (dev < 0)
            {
                q[0] = 2.0 * _Q[0, 0] * 3.0 * dev;
                Q[0, 0] = 2.0 * _Q[0, 0] * 3.0;
            }
            else
            {
                q[0] = 2.0 * _Q[0, 0] * 0.5 * dev;
                Q[0, 0] = 2.0 * _Q[0, 0] * 0.5;
            }

            if (!HasGenerator) R[1, 1] += 2.0 * ConstraintWeight * 100.0 * dt;
            if (IsBlackout) R[0, 0] += 2.0 * ConstraintWeight * 100.0 * dt;

            if (gridP < 0)
            {
                r[0] += 2.0 * ConstraintWeight * 100.0 * gridP * dt;
                R[0, 0] += 2.0 * ConstraintWeight * 100.0 * dt;
            }
            if (genP < 0)
            {
                r[1] += 2.0 * ConstraintWeight * 100.0 * genP * dt;
                R[1, 1] += 2.0 * ConstraintWeight * 100.0 * dt;
            }
        }

        public BaseStateDTO RunStep(double dt, double[] currentState)
        {
            if (Math.Abs(dt - _profileDt) > 1e-9)
            {
                _profileDt = dt > 0 ? dt : 1.0;
                GenerateEnvironment();
            }

            double actualDt = dt;
            double startEnergy = BatteryCapacity * (InitialBatteryPercent / 100.0);
            var state = currentState != null && currentState.Length == 1 ? currentState : new double[] { startEnergy };

            _useForecast = true;
            var plannedU = ILQR_Controller.Solve(state, Horizon, 10, actualDt, GetPhysicsModel(), this);

            if (plannedU != null)
            {
                if (plannedU.Length >= 1)
                {
                    if (plannedU[0] < 0.0) plannedU[0] = 0.0;
                    if (plannedU[0] > MaxGridPower) plannedU[0] = MaxGridPower;
                }
                if (plannedU.Length >= 2)
                {
                    if (plannedU[1] < 0.0) plannedU[1] = 0.0;
                    if (plannedU[1] > MaxGenPower) plannedU[1] = MaxGenPower;
                }
            }

            double plannedGrid = plannedU.ElementAtOrDefault(0);
            double plannedGen = plannedU.ElementAtOrDefault(1);

            double execGrid = plannedGrid;
            execGrid = Math.Max(0.0, execGrid);
            if (IsBlackout) execGrid = 0.0;
            execGrid = Math.Min(execGrid, MaxGridPower);

            double execGen = plannedGen;
            if (!HasGenerator) execGen = 0.0;
            execGen = Math.Max(0.0, execGen);
            execGen = Math.Min(execGen, MaxGenPower);

            var executedU = new double[] { execGrid, execGen };

            _useForecast = false;
            var nextState = GridStep(state, executedU, actualDt, _currentStepIndex);

            int safeT = Math.Min(_currentStepIndex, _demand.Length - 1);
            double realGridCost = execGrid * _price[safeT] * actualDt;
            double realGenCost = execGen * FuelCost * actualDt;
            double stepFinancialCost = realGridCost + realGenCost;
            _accumulatedFinancialCost += stepFinancialCost;

            _history.Add(new GridRecord(
                _currentStepIndex * actualDt,
                _demand[safeT],
                _actualSolar[safeT],
                _price[safeT],
                state[0],
                plannedGrid,
                plannedGen,
                stepFinancialCost
            ));

            _currentStepIndex++;

            if (_batteryHistory.Count <= _currentStepIndex)
            {
                _batteryHistory.Add(nextState[0]);
                _gridHistory.Add(execGrid);
                _genHistory.Add(execGen);
            }

            return new GridStateDTO { State = nextState, Control = plannedU };
        }

        public string GetCsv()
        {
            var sb = new StringBuilder();
            sb.Append("\uFEFF");
            sb.AppendLine("Час,Споживання(kW),Сонце(kW),Тариф($/kWh),Акумулятор(kWh),Мережа(kW),Генератор(kW),Вартість($)");
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

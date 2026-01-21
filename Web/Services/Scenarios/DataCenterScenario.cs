using Core;
using Web.Data;

namespace Web.Services.Scenarios
{
    public class DataCenterScenario : ISimulationScenario
    {
        private double alpha = 0.1;     // thermal gain factor (°C per heat unit per hour)
        private double beta = 1.0;      // cooling efficiency: heat units removed per unit u
        public double Tmax { get; set; } = 35.0;   // safety limit (°C)
        public double Tref { get; set; } = 22.0;   // desired temperature

        // MPC params
        public int HorizonSteps { get; set; } = 12; // lookahead steps
        public int MaxIters { get; set; } = 4;      // optimization iterations
        public double Dt { get; set; } = 1.0;       // time-step in hours (1.0 => 1 hour per step)

        // Cost weights
        public double WTemp { get; set; } = 200.0;      // state tracking weight
        public double WEnergyBase { get; set; } = 1.0; // multiply by price[t]
        public double WSmooth { get; set; } = 50.0;    // smoothing on delta-u
        public double OverheatPenalty { get; set; } = 5e4;

        // Forecasts (external UI should set these)
        // Use List<double> — we will RemoveAt(0) when a step is applied
        public List<double> ServerHeatForecast { get; set; } = new List<double>();
        public List<double> PriceForecast { get; set; } = new List<double>();

        // Internal: optional history for visualization
        private List<double> _historyTemps = new List<double>();
        private List<double> _historyU = new List<double>();

        // Constructor: optional default forecasts
        public DataCenterScenario()
        {
            // default constant forecasts (fill a bit more than horizon)
            for (int i = 0; i < HorizonSteps + 10; ++i)
            {
                ServerHeatForecast.Add(1.0); // nominal heat units
                PriceForecast.Add(1.0);      // normalized price
            }
        }

        public void Reset()
        {
            _historyTemps.Clear();
            _historyU.Clear();
            // keep forecasts as-is; UI can reset them
        }

        // RunStep: compute control (u ∈ [0,1]) for current state (x[0] == T)
        // Note: this method does NOT advance forecasts. The physics Step returned by GetPhysicsModel()
        // WILL consume the forecasts when SimulationService calls it to apply the control.
        public BaseStateDTO RunStep(double dt, double[] currentState)
        {
            double Tnow = currentState != null && currentState.Length > 0 ? currentState[0] : Tref;

            int H = HorizonSteps;
            var heatForecast = new double[H];
            var priceForecast = new double[H];
            for (int i = 0; i < H; ++i)
            {
                heatForecast[i] = (i < ServerHeatForecast.Count) ? ServerHeatForecast[i] : ServerHeatForecast.LastOrDefault();
                priceForecast[i] = (i < PriceForecast.Count) ? PriceForecast[i] : PriceForecast.LastOrDefault();
            }

            // initial guess u trajectory
            var u_traj = new List<double[]>();
            for (int i = 0; i < H; ++i)
            {
                u_traj.Add(new double[] { 0.0 });
            }

            // iteration (simple)
            for (int iter = 0; iter < MaxIters; ++iter)
            {
                // rollout
                var x_traj = new List<double[]>();
                double Tsim = Tnow;
                x_traj.Add(new double[] { Tsim });
                for (int t = 0; t < H; ++t)
                {
                    double net = heatForecast[t] - beta * u_traj[t][0];
                    Tsim = Tsim + alpha * net * Dt;
                    x_traj.Add(new double[] { Tsim });
                }

                // build lists for DynamicLQR
                var A_list = new List<double[,]>();
                var B_list = new List<double[,]>();
                var Q_list = new List<double[,]>();
                var q_list = new List<double[]>();
                var R_list = new List<double[,]>();
                var r_list = new List<double[]>();

                var A_const = new double[1, 1] { { 1.0 } };
                var B_const = new double[1, 1] { { -alpha * beta * Dt } };

                for (int t = 0; t < H; ++t)
                {
                    A_list.Add((double[,])A_const.Clone());
                    B_list.Add((double[,])B_const.Clone());
                }

                for (int t = 0; t <= H; ++t)
                {
                    var Qm = new double[1, 1] { { WTemp } };
                    Q_list.Add(Qm);

                    double Tt = x_traj[Math.Min(t, x_traj.Count - 1)][0];
                    double qv = 2.0 * WTemp * (Tt - Tref);
                    if (Tt > Tmax) qv += 2.0 * OverheatPenalty * (Tt - Tmax);
                    q_list.Add(new double[] { qv });
                }

                for (int t = 0; t < H; ++t)
                {
                    double price = priceForecast[t];
                    var Rm = new double[1, 1] { { WEnergyBase * (price + 1e-6) } };
                    R_list.Add(Rm);

                    double r0 = price * WEnergyBase;
                    if (t > 0)
                    {
                        double du = u_traj[t][0] - u_traj[t - 1][0];
                        r0 += 2.0 * WSmooth * du;
                    }
                    r_list.Add(new double[] { r0 });
                }

                var gains = DynamicLQR.BackwardPass(A_list, B_list, Q_list, R_list, q_list, r_list, H);

                var new_u_traj = new List<double[]>();
                double[] xsim = new double[] { Tnow };
                for (int t = 0; t < H; ++t)
                {
                    var k = gains[t].k;
                    var K = gains[t].K;
                    double dx = xsim[0] - x_traj[t][0];
                    double Kdx = 0.0;
                    if (K != null && K.Length > 0) Kdx = K[0, 0] * dx;
                    double u_new = u_traj[t][0] + k[0] + Kdx;
                    if (u_new < 0.0) u_new = 0.0;
                    if (u_new > 1.0) u_new = 1.0;
                    new_u_traj.Add(new double[] { u_new });
                    double net = heatForecast[t] - beta * u_new;
                    double Tnext = xsim[0] + alpha * net * Dt;
                    xsim = new double[] { Tnext };
                }

                u_traj = new_u_traj;
            }

            double appliedU = u_traj.Count > 0 ? u_traj[0][0] : 0.0;
            _historyU.Add(appliedU);

            // Return a DataCenterStateDTO with Control only; SimulationService will apply physics and populate State
            var dto = new DataCenterStateDTO
            {
                Control = new double[] { appliedU }
            };

            return dto;
        }

        // Provide visualization data for UI (minimal)
        public object GetVisualizationData()
        {
            return new
            {
                HistoryTemps = _historyTemps.ToArray(),
                HistoryU = _historyU.ToArray(),
                ServerForecast = ServerHeatForecast.ToArray(),
                PriceForecast = PriceForecast.ToArray()
            };
        }

        public void HandleInteraction(double x, double y, string mode)
        {
            // Minimal: expose some modes for preset selection from UI.
            if (mode == "Preset_BusyDay")
            {
                ServerHeatForecast.Clear();
                PriceForecast.Clear();
                // Cheap night, expensive midday pattern
                for (int i = 0; i < HorizonSteps + 10; i++)
                {
                    double hour = i % 24;
                    // server load high midday
                    ServerHeatForecast.Add((hour >= 8 && hour <= 18) ? 2.0 : 0.8);
                    // price low at night, high midday
                    PriceForecast.Add((hour >= 8 && hour <= 18) ? 2.0 : 0.5);
                }
            }
            else if (mode == "Preset_QuietNight")
            {
                ServerHeatForecast.Clear();
                PriceForecast.Clear();
                for (int i = 0; i < HorizonSteps + 10; ++i)
                {
                    ServerHeatForecast.Add(0.5);
                    PriceForecast.Add(0.4);
                }
            }
        }

        // Provide the PhysicsModel. The Step delegate *consumes* the next forecast entry (index 0)
        // when called, so SimulationService applying the Step will advance forecasts automatically.
        public PhysicsModel GetPhysicsModel()
        {
            // Step delegate captures 'this' so it can read & consume forecasts
            Func<double[], double[], double, double[]> step = (x, u, dt) =>
            {
                double T = (x != null && x.Length > 0) ? x[0] : Tref;
                double uu = (u != null && u.Length > 0) ? u[0] : 0.0;
                if (uu < 0.0) uu = 0.0;
                if (uu > 1.0) uu = 1.0;

                // Get current heat (if available), otherwise assume nominal 1.0
                double heatNow = ServerHeatForecast.Count > 0 ? ServerHeatForecast[0] : 1.0;

                // Compute Tnext
                double Tnext = T + alpha * (heatNow - beta * uu) * Dt;

                // Shift forecasts (consume the first entry)
                if (ServerHeatForecast.Count > 0) ServerHeatForecast.RemoveAt(0);
                if (PriceForecast.Count > 0) PriceForecast.RemoveAt(0);

                // Keep minimal history for visualization
                _historyTemps.Add(Tnext);

                return new double[] { Tnext };
            };

            // Linearize (A,B) do not depend on heat here (A = 1, B = -alpha*beta*Dt)
            Func<double[], double[], double, (double[,], double[,])> linearize = (x, u, dt) =>
            {
                var A = new double[1, 1] { { 1.0 } };
                var B = new double[1, 1] { { -alpha * beta * Dt } };
                return (A, B);
            };

            return new PhysicsModel(step, linearize, 1, 1);
        }
    }
}

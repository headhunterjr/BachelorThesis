using Web.Data;
using Web.Services.Scenarios;

namespace Web.Services
{
    public class SimulationService
    {
        public ISimulationScenario ActiveScenario { get; private set; }
        private double[] _realState = Array.Empty<double>();
        private Func<double[], double[], double, int, double[]> _physicsStep;
        private int _currentTimeStep = 0;
        public double Dt { get; set; } = 0.1;
        public SimulationService()
        {
            SetScenario(new ParkingScenario());
        }
        public void SetScenario(ISimulationScenario scenario)
        {
            ActiveScenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            var pm = ActiveScenario.GetPhysicsModel() ?? throw new InvalidOperationException("Scenario must provide a PhysicsModel.");
            _realState = new double[pm.Nx];
            _physicsStep = pm.Step ?? ((x, u, dt, t) => x);
            ActiveScenario.Reset();
        }

        public void LoadScenario(string name)
        {
            if (name == "parking") SetScenario(new ParkingScenario());
            else if (name == "racing") SetScenario(new RacingScenario());
            else if (name == "grid") SetScenario(new SmartGridScenario());
            else throw new ArgumentException($"Unknown scenario '{name}'");
        }

        public BaseStateDTO RunStep()
        {
            if (ActiveScenario == null) throw new InvalidOperationException("No active scenario set.");
            var stateCopy = (double[])_realState.Clone();
            var result = ActiveScenario.RunStep(Dt, stateCopy) ?? new BaseStateDTO();
            var control = result.Control ?? Array.Empty<double>();
            try
            {
                var newState = _physicsStep != null ? _physicsStep(_realState, control, Dt, _currentTimeStep) : _realState;
                if (newState != null) _realState = newState;
            }
            catch
            {
            }

            ++_currentTimeStep;
            result.State = (double[])_realState.Clone();
            result.Control = control.Length > 0 ? control : Array.Empty<double>();

            return result;
        }

        public BaseStateDTO GetCurrentState()
        {
            return new BaseStateDTO
            {
                State = (double[])_realState.Clone(),
                Control = Array.Empty<double>()
            };
        }

        public void HandleInteraction(double x, double y, string mode)
        {
            ActiveScenario?.HandleInteraction(x, y, mode);
        }

        public object GetCurrentVisuals()
        {
            return ActiveScenario?.GetVisualizationData();
        }

        public void Reset()
        {
            _currentTimeStep = 0;
            for (int i = 0; i < _realState.Length; i++) _realState[i] = 0.0;
            ActiveScenario?.Reset();
        }

        public void SetState(double[] newState)
        {
            if (newState == null) return;
            if (newState.Length != _realState.Length) throw new ArgumentException("New state length mismatch.");
            _realState = (double[])newState.Clone();
        }
    }
}

using Web.Data;
using Web.Services.Scenarios;

namespace Web.Services
{
    public class SimulationService
    {
        // Active scenario
        public ISimulationScenario ActiveScenario { get; private set; }

        // Global state vector (length = current scenario Nx)
        private double[] _realState = Array.Empty<double>();

        // Physics step delegate (from PhysicsModel.Step)
        private Func<double[], double[], double, double[]> _physicsStep;

        // Simulation settings
        public double Dt { get; set; } = 0.1;

        // default ctor sets a benign scenario
        public SimulationService()
        {
            // default to parking to avoid null errors on startup
            SetScenario(new ParkingScenario());
        }

        // SetScenario: use scenario's PhysicsModel to size _realState and _physicsStep
        public void SetScenario(ISimulationScenario scenario)
        {
            ActiveScenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            var pm = ActiveScenario.GetPhysicsModel() ?? throw new InvalidOperationException("Scenario must provide a PhysicsModel.");
            // size global state to Nx
            _realState = new double[pm.Nx];
            // set physics step
            _physicsStep = pm.Step ?? ((x, u, dt) => x);
            ActiveScenario.Reset();
        }

        // String-based convenience
        public void LoadScenario(string name)
        {
            if (name == "parking") SetScenario(new ParkingScenario());
            else if (name == "racing") SetScenario(new RacingScenario());
            else if (name == "datacenter") SetScenario(new DataCenterScenario());
            else throw new ArgumentException($"Unknown scenario '{name}'");
        }

        // === CORE: RunStep returns BaseStateDTO (no legacy wrappers) ===
        public BaseStateDTO RunStep()
        {
            if (ActiveScenario == null) throw new InvalidOperationException("No active scenario set.");

            // defensive clone
            var stateCopy = (double[])_realState.Clone();

            // ask scenario what to do; scenario may return a specialized DTO (e.g. DataCenterStateDTO or CarStateDTO)
            var result = ActiveScenario.RunStep(Dt, stateCopy) ?? new BaseStateDTO();

            // the scenario is expected to populate result.Control (or Leave it empty).
            var control = result.Control ?? Array.Empty<double>();

            // apply physics using the configured step - physics expects (state, control, dt) -> new state
            try
            {
                var newState = _physicsStep != null ? _physicsStep(_realState, control, Dt) : _realState;
                if (newState != null) _realState = newState;
            }
            catch
            {
                // swallow physics exceptions to avoid crashing UI; optionally log
            }

            // populate and return the result DTO with the updated state and applied control
            result.State = (double[])_realState.Clone();
            result.Control = control.Length > 0 ? control : Array.Empty<double>();

            return result;
        }

        // Current state getter (generic)
        public BaseStateDTO GetCurrentState()
        {
            return new BaseStateDTO
            {
                State = (double[])_realState.Clone(),
                Control = Array.Empty<double>()
            };
        }

        // Bridge UI interaction to active scenario
        public void HandleInteraction(double x, double y, string mode) => ActiveScenario?.HandleInteraction(x, y, mode);

        public object GetCurrentVisuals() => ActiveScenario?.GetVisualizationData();

        public void Reset()
        {
            // size remains same, reset elements to zero
            for (int i = 0; i < _realState.Length; i++) _realState[i] = 0.0;
            ActiveScenario?.Reset();
        }

        // Helper: manually move state (keeps Nx)
        public void SetState(double[] newState)
        {
            if (newState == null) return;
            if (newState.Length != _realState.Length) throw new ArgumentException("New state length mismatch.");
            _realState = (double[])newState.Clone();
        }
    }
}

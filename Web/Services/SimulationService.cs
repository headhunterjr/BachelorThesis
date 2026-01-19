using Core;
using Web.Data;
using Web.Services.Scenarios;

namespace Web.Services
{
    public class SimulationService
    {
        // Holds the current logic (Parking vs Racing)
        public ISimulationScenario ActiveScenario { get; private set; }

        // The global "Physical" state of the car: [x, y, v, theta]
        private double[] _realState = new double[4];

        private Func<double[], double[], double, double[]> _physicsStep;

        // Global Simulation Settings
        public double Dt { get; set; } = 0.1;
        public double InitialVelocity { get; set; } = 0.0;
        public double InitialAngle { get; set; } = 0.0;

        public SimulationService()
        {
            // Default to parking to avoid null errors on startup
            _physicsStep = PhysicsEngine.Step;
            LoadScenario("parking");
        }

        // === THIS WAS MISSING ===
        // Allows Razor pages to switch scenarios type-safely
        public void SetScenario(ISimulationScenario scenario)
        {
            ActiveScenario = scenario;
            _realState = new double[] { 0, 0, 0, 0 }; // Reset car position on switch
            _physicsStep = scenario.GetPhysicsModel();
            ActiveScenario.Reset();
        }

        // String-based loader for default constructor
        public void LoadScenario(string name)
        {
            if (name == "parking") SetScenario(new ParkingScenario());
            else if (name == "racing") SetScenario(new RacingScenario());
        }

        public CarStateDTO RunStep()
        {
            // 1. Ask the active scenario to calculate the next move
            var result = ActiveScenario.RunStep(Dt, _realState);

            // 2. Apply Physics (Global)
            double[] u = { result.Accel, result.Steer };
            _realState = _physicsStep(_realState, u, Dt);

            // 3. Return full state to UI
            return new CarStateDTO
            {
                X = _realState[0],
                Y = _realState[1],
                Velocity = _realState[2],
                Theta = _realState[3],
                Accel = u[0],
                Steer = u[1]
            };
        }

        // Bridge UI interaction to active scenario
        public void HandleInteraction(double x, double y, string mode) => ActiveScenario.HandleInteraction(x, y, mode);

        public object GetCurrentVisuals() => ActiveScenario.GetVisualizationData();

        public void Reset()
        {
            _realState = new double[] { 0, 0, 0, 0 };
            ActiveScenario.Reset();
        }

        // Helper to get raw state without advancing physics (Critical for DrawFrame)
        public CarStateDTO GetCurrentState()
        {
            return new CarStateDTO
            {
                X = _realState[0],
                Y = _realState[1],
                Velocity = _realState[2],
                Theta = _realState[3]
            };
        }

        // Helper to manually move car (used in Parking UI)
        public void SetCarPosition(double x, double y)
        {
            _realState[0] = x;
            _realState[1] = y;
            _realState[2] = InitialVelocity;
            _realState[3] = InitialAngle;
        }

        // Expose raw state if needed for debugging or advanced scenarios
        public double[] GetRealCarState() => _realState;
    }
}
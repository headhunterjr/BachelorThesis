using Core;
using Web.Data;

namespace Web.Services
{
    public interface ISimulationScenario
    {
        void Reset();
        BaseStateDTO RunStep(double dt, double[] currentCarState);
        void HandleInteraction(double x, double y, string mode);
        object GetVisualizationData();
        PhysicsModel GetPhysicsModel();
    }
}

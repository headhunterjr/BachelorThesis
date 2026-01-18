using Web.Data;

namespace Web.Services
{
    public interface ISimulationScenario
    {
        // Resets the scenario (clears obstacles or track)
        void Reset();

        // Calculates the next move. 
        // We pass currentCarState so the scenario knows where the car is.
        CarStateDTO RunStep(double dt, double[] currentCarState);

        // Handles clicks (Adding obstacles or Drawing track)
        void HandleInteraction(double x, double y, string mode);

        // Returns whatever data needs to be drawn (List<Obstacle> or List<double[]>)
        object GetVisualizationData();
    }
}

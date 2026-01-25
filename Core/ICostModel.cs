namespace Core
{
    public interface ICostModel
    {
        // Added 'timeStep' so scenarios can change behavior over the horizon
        double Evaluate(double[] x, double[] u, double dt, int timeStep);

        void GetDerivatives(double[] x, double[] u, double dt, int timeStep, ref double[,] Q, ref double[,] R, ref double[] q, ref double[] r);
    }
}
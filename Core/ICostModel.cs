namespace Core
{
    public interface ICostModel
    {
        double Evaluate(double[] x, double[] u, double dt, int timeStep);

        void GetDerivatives(double[] x, double[] u, double dt, int timeStep, ref double[,] Q, ref double[,] R, ref double[] q, ref double[] r);
    }
}
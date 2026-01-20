namespace Core
{
    public class PhysicsModel
    {
        public Func<double[], double[], double, double[]> Step { get; }
        public Func<double[], double[], double, (double[,], double[,])> Linearize { get; }
        public int Nx { get; }
        public int Nu { get; }

        public PhysicsModel(Func<double[], double[], double, double[]> step,
                            Func<double[], double[], double, (double[,], double[,])> linearize,
                            int nx, int nu)
        {
            Step = step;
            Linearize = linearize;
            Nx = nx;
            Nu = nu;
        }
    }
}

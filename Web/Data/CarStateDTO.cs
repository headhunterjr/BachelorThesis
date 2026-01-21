namespace Web.Data
{
    public class CarStateDTO : BaseStateDTO
    {
        public double X
        {
            get => State.Length > 0 ? State[0] : 0.0;
            set
            {
                if (State.Length == 0) State = new double[4];
                State[0] = value;
            }
        }
        public double Y
        {
            get => State.Length > 1 ? State[1] : 0.0;
            set
            {
                if (State.Length == 0) State = new double[4];
                State[1] = value;
            }
        }
        public double Velocity
        {
            get => State.Length > 2 ? State[2] : 0.0;
            set
            {
                if (State.Length == 0) State = new double[4];
                State[2] = value;
            }
        }
        public double Theta
        {
            get => State.Length > 3 ? State[3] : 0.0;
            set
            {
                if (State.Length == 0) State = new double[4];
                State[3] = value;
            }
        }

        // Control conveniences mapped to Control indices
        public double Accel
        {
            get => Control.Length > 0 ? Control[0] : 0.0;
            set
            {
                if (Control.Length == 0) Control = new double[2];
                Control[0] = value;
            }
        }
        public double Steer
        {
            get => Control.Length > 1 ? Control[1] : 0.0;
            set
            {
                if (Control.Length == 0) Control = new double[2];
                Control[1] = value;
            }
        }

        public CarStateDTO() { }
        public CarStateDTO(double[] state, double[] control)
        {
            State = state ?? Array.Empty<double>();
            Control = control ?? Array.Empty<double>();
        }
    }
}

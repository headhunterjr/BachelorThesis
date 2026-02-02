namespace Web.Data
{
    public class GridStateDTO : BaseStateDTO
    {
        // State[0] is Battery Energy
        public double BatteryLevel
        {
            get => State.Length > 0 ? State[0] : 0.0;
            set
            {
                if (State.Length == 0) State = new double[1];
                State[0] = value;
            }
        }

        // Control[0] is Grid Import/Export
        public double GridPower
        {
            get => Control.Length > 0 ? Control[0] : 0.0;
            set
            {
                if (Control.Length == 0) Control = new double[2];
                Control[0] = value;
            }
        }

        // Control[1] is Generator Output
        public double GenPower
        {
            get => Control.Length > 1 ? Control[1] : 0.0;
            set
            {
                if (Control.Length == 0) Control = new double[2];
                Control[1] = value;
            }
        }

        public GridStateDTO() { }

        public GridStateDTO(double[] state, double[] control)
        {
            State = state ?? Array.Empty<double>();
            Control = control ?? Array.Empty<double>();
        }
    }
}
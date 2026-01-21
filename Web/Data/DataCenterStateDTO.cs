namespace Web.Data
{
    public class DataCenterStateDTO : BaseStateDTO
    {
        // Temperature mapped to State[0]
        public double Temperature
        {
            get => State.Length > 0 ? State[0] : 0.0;
            set
            {
                if (State.Length == 0) State = new double[1];
                State[0] = value;
            }
        }

        // Cooling power control mapped to Control[0]
        public double CoolingPower
        {
            get => Control.Length > 0 ? Control[0] : 0.0;
            set
            {
                if (Control.Length == 0) Control = new double[1];
                Control[0] = value;
            }
        }

        public DataCenterStateDTO() { }
        public DataCenterStateDTO(double temp, double cooling)
        {
            State = new double[] { temp };
            Control = new double[] { cooling };
        }
    }
}

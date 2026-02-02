namespace Web.Data
{
    public class GridVisuals
    {
        public double[]? DemandProfile { get; set; }
        public double[]? SolarProfile { get; set; }
        public double[]? PriceProfile { get; set; }
        public double[]? PlannedBattery { get; set; }
        public double[]? PlannedGrid { get; set; }
        public double[]? PlannedGen { get; set; }
        public double TotalCost { get; set; }
        public double CurrentCost { get; set; }
        public int CurrentStep { get; set; }
    }
}

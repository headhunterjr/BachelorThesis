namespace Web.Data
{
    public record ParkingRecord(double Time, double X, double Y, double Velocity, double Theta, double Acceleration, double Steering, double Cost);
    public record RacingRecord(double Time, double X, double Y, double Velocity, double Theta, double Acceleration, double Steering, double Cost);
    public record GridRecord(double Hour, double Demand, double Solar, double Price, double BatteryLevel, double GridUsage, double GenUsage, double CurrentCost);
}
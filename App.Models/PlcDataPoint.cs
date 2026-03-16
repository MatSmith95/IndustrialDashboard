namespace App.Models;

public class PlcDataPoint
{
    public DateTime Timestamp { get; set; }
    public double Temperature { get; set; }
    public double Pressure { get; set; }
    public double MotorSpeed { get; set; }
}

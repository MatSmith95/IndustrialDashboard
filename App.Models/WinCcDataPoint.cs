namespace App.Models;

public class WinCcDataPoint
{
    public DateTime Timestamp { get; set; }
    public long TagId { get; set; }
    public string TagName { get; set; } = string.Empty;
    public double Value { get; set; }
}

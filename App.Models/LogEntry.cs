using System.ComponentModel.DataAnnotations;

namespace App.Models;

public class LogEntry
{
    [Key]
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string TagName { get; set; } = string.Empty;
    public double Value { get; set; }
}

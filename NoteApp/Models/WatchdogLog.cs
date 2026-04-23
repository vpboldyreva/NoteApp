namespace NoteApp.Models;

public class WatchdogLog
{
    public int Id { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public double? CpuUsage { get; set; }       // percent, null = metric disabled
    public double? RamUsageMb { get; set; }     // MB, null = metric disabled
    public double? DiskUsagePercent { get; set; } // percent, null = metric disabled
}

public class WatchdogConfig
{
    public int Id { get; set; }
    public string Metric { get; set; } = string.Empty; // cpu | ram | hdd
    public bool Enabled { get; set; } = true;
}

public class ActionLog
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public string Command { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

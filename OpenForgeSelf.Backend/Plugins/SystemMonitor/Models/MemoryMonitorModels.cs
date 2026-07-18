namespace OpenForgeSelf.Backend.Plugins.SystemMonitor.Models;

public class MemoryInfo
{
    public long TotalMemoryBytes { get; set; }
    public long AvailableMemoryBytes { get; set; }
    public long UsedMemoryBytes { get; set; }
    public double UsagePercent { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public class MemoryHistoryRequest
{
    public string Duration { get; set; } = "1m";
}

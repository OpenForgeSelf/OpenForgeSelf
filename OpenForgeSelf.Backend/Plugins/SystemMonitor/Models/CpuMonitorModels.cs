namespace OpenForgeSelf.Backend.Plugins.SystemMonitor.Models;

public class CpuUsage
{
    public double TotalUsagePercent { get; set; }
    public List<CpuCoreUsage> PerCoreUsage { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public class CpuCoreUsage
{
    public int CoreIndex { get; set; }
    public double UsagePercent { get; set; }
}

public class CpuHistoryRequest
{
    public string Duration { get; set; } = "1m";
}

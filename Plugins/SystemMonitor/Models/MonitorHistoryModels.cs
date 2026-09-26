namespace ForgeSelf.Api.Plugins.SystemMonitor.Models;

public class MonitorHistoryPoint
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
}

public class CpuHistoryData
{
    public List<MonitorHistoryPoint> TotalUsage { get; set; } = new();
    public Dictionary<int, List<MonitorHistoryPoint>> PerCoreUsage { get; set; } = new();
}

public class MemoryHistoryData
{
    public List<MonitorHistoryPoint> UsagePercent { get; set; } = new();
    public List<MonitorHistoryPoint> UsedBytes { get; set; } = new();
}

public class DiskHistoryData
{
    public string DriveName { get; set; } = string.Empty;
    public List<MonitorHistoryPoint> UsagePercent { get; set; } = new();
    public List<MonitorHistoryPoint> ReadSpeed { get; set; } = new();
    public List<MonitorHistoryPoint> WriteSpeed { get; set; } = new();
}

public class NetworkHistoryData
{
    public List<MonitorHistoryPoint> UploadSpeed { get; set; } = new();
    public List<MonitorHistoryPoint> DownloadSpeed { get; set; } = new();
    public long TotalUploadedBytes { get; set; }
    public long TotalDownloadedBytes { get; set; }
}

public class SystemOverview
{
    public CpuUsage Cpu { get; set; } = new();
    public MemoryInfo Memory { get; set; } = new();
    public List<DiskDriveInfo> Disks { get; set; } = new();
    public NetworkSpeedInfo Network { get; set; } = new();
    public int ProcessCount { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

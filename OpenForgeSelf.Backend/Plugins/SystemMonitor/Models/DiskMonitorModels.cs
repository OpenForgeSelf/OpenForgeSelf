namespace OpenForgeSelf.Backend.Plugins.SystemMonitor.Models;

public class DiskDriveInfo
{
    public string DriveName { get; set; } = string.Empty;
    public string VolumeLabel { get; set; } = string.Empty;
    public string DriveFormat { get; set; } = string.Empty;
    public long TotalSizeBytes { get; set; }
    public long AvailableFreeSpaceBytes { get; set; }
    public long UsedSpaceBytes { get; set; }
    public double UsagePercent { get; set; }
    public bool IsLowSpaceWarning { get; set; }
}

public class DiskIOInfo
{
    public string DriveName { get; set; } = string.Empty;
    public double ReadSpeedBytesPerSecond { get; set; }
    public double WriteSpeedBytesPerSecond { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public class DiskHistoryRequest
{
    public string DriveName { get; set; } = string.Empty;
    public string Duration { get; set; } = "1m";
}

namespace ForgeSelf.Api.Plugins.SystemMonitor.Models;

public class NetworkSpeedInfo
{
    public double UploadSpeedBytesPerSecond { get; set; }
    public double DownloadSpeedBytesPerSecond { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public class NetworkConnectionInfo
{
    public string LocalEndPoint { get; set; } = string.Empty;
    public string RemoteEndPoint { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
    public int ProcessId { get; set; }
}

public class NetworkHistoryRequest
{
    public string Duration { get; set; } = "1m";
}

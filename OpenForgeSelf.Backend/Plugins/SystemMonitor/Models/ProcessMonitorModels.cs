namespace OpenForgeSelf.Backend.Plugins.SystemMonitor.Models;

public class ProcessInfo
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public double CpuUsagePercent { get; set; }
    public long MemoryUsageBytes { get; set; }
    public long WorkingSetBytes { get; set; }
    public string? FilePath { get; set; }
    public DateTime StartTime { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
}

public class ProcessListRequest
{
    public string? SortBy { get; set; }
    public bool Ascending { get; set; }
    public string? SearchFilter { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class ProcessListResult
{
    public List<ProcessInfo> Processes { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class KillProcessRequest
{
    public int ProcessId { get; set; }
    public bool Force { get; set; } = false;
}

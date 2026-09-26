using ForgeSelf.Api.Plugins.SystemMonitor.Models;

namespace ForgeSelf.Api.Plugins.SystemMonitor.Services;

public interface IProcessMonitorService
{
    Task<ProcessListResult> GetProcessesAsync(ProcessListRequest request);
    Task<ProcessInfo?> GetProcessByIdAsync(int pid);
    Task<bool> KillProcessAsync(int pid, bool force = false);
}

using OpenForgeSelf.Backend.Plugins.SystemMonitor.Models;

namespace OpenForgeSelf.Backend.Plugins.SystemMonitor.Services;

public interface IProcessMonitorService
{
    Task<ProcessListResult> GetProcessesAsync(ProcessListRequest request);
    Task<ProcessInfo?> GetProcessByIdAsync(int pid);
    Task<bool> KillProcessAsync(int pid, bool force = false);
}

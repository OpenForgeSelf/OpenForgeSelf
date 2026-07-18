using OpenForgeSelf.Backend.Plugins.SystemMonitor.Models;

namespace OpenForgeSelf.Backend.Plugins.SystemMonitor.Services;

public interface IMemoryMonitorService
{
    Task<MemoryInfo> GetMemoryUsageAsync();
    Task<MemoryInfo> GetMemoryInfoAsync();
    Task<MemoryHistoryData> GetMemoryHistoryAsync(string duration = "1m");
    void StartSampling();
    void StopSampling();
}

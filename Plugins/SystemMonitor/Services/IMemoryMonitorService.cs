using ForgeSelf.Api.Plugins.SystemMonitor.Models;

namespace ForgeSelf.Api.Plugins.SystemMonitor.Services;

public interface IMemoryMonitorService
{
    Task<MemoryInfo> GetMemoryUsageAsync();
    Task<MemoryInfo> GetMemoryInfoAsync();
    Task<MemoryHistoryData> GetMemoryHistoryAsync(string duration = "1m");
    void StartSampling();
    void StopSampling();
}

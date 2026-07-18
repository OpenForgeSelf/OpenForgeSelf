using OpenForgeSelf.Backend.Plugins.SystemMonitor.Models;

namespace OpenForgeSelf.Backend.Plugins.SystemMonitor.Services;

public interface ICpuMonitorService
{
    Task<CpuUsage> GetCpuUsageAsync();
    Task<List<CpuCoreUsage>> GetPerCoreUsageAsync();
    Task<CpuHistoryData> GetCpuHistoryAsync(string duration = "1m");
    void StartSampling();
    void StopSampling();
}

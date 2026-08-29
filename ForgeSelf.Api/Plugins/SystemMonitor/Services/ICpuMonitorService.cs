using ForgeSelf.Api.Plugins.SystemMonitor.Models;

namespace ForgeSelf.Api.Plugins.SystemMonitor.Services;

public interface ICpuMonitorService
{
    Task<CpuUsage> GetCpuUsageAsync();
    Task<List<CpuCoreUsage>> GetPerCoreUsageAsync();
    Task<CpuHistoryData> GetCpuHistoryAsync(string duration = "1m");
    void StartSampling();
    void StopSampling();
}

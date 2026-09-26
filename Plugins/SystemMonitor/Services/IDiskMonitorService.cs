using ForgeSelf.Api.Plugins.SystemMonitor.Models;

namespace ForgeSelf.Api.Plugins.SystemMonitor.Services;

public interface IDiskMonitorService
{
    Task<List<DiskDriveInfo>> GetDiskDrivesAsync();
    Task<DiskIOInfo> GetDiskIOAsync(string driveName);
    Task<DiskHistoryData> GetDiskHistoryAsync(string driveName, string duration = "1m");
    void StartSampling();
    void StopSampling();
}

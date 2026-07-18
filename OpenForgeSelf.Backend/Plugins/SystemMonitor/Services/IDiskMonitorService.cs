using OpenForgeSelf.Backend.Plugins.SystemMonitor.Models;

namespace OpenForgeSelf.Backend.Plugins.SystemMonitor.Services;

public interface IDiskMonitorService
{
    Task<List<DiskDriveInfo>> GetDiskDrivesAsync();
    Task<DiskIOInfo> GetDiskIOAsync(string driveName);
    Task<DiskHistoryData> GetDiskHistoryAsync(string driveName, string duration = "1m");
    void StartSampling();
    void StopSampling();
}

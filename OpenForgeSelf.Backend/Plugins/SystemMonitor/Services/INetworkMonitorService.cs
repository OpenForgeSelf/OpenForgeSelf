using OpenForgeSelf.Backend.Plugins.SystemMonitor.Models;

namespace OpenForgeSelf.Backend.Plugins.SystemMonitor.Services;

public interface INetworkMonitorService
{
    Task<NetworkSpeedInfo> GetNetworkSpeedAsync();
    Task<List<NetworkConnectionInfo>> GetNetworkConnectionsAsync();
    Task<NetworkHistoryData> GetNetworkHistoryAsync(string duration = "1m");
    void StartSampling();
    void StopSampling();
}

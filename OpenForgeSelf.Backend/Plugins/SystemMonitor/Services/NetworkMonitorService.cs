using System.Diagnostics;
using System.Net.NetworkInformation;
using OpenForgeSelf.Backend.Plugins.SystemMonitor.Models;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.SystemMonitor.Services;

public class NetworkMonitorService : INetworkMonitorService, IDisposable
{
    private readonly int _sampleIntervalMs = 1000;
    private readonly int _maxHistoryMinutes = 15;
    private readonly int _historyCapacity;

    private readonly CircularBuffer<MonitorHistoryPoint> _uploadSpeedHistory;
    private readonly CircularBuffer<MonitorHistoryPoint> _downloadSpeedHistory;

    private NetworkSpeedInfo _lastSpeedInfo = new();
    private long _totalUploadedBytes;
    private long _totalDownloadedBytes;
    private long _lastSentBytes;
    private long _lastReceivedBytes;
    private DateTime _lastSampleTime;

    private readonly Dictionary<string, PerformanceCounter> _sentCounters = new();
    private readonly Dictionary<string, PerformanceCounter> _receivedCounters = new();

    private CancellationTokenSource? _cts;
    private Task? _samplingTask;
    private bool _disposed;
    private readonly object _lock = new();

    public NetworkMonitorService()
    {
        _historyCapacity = _maxHistoryMinutes * 60;
        _uploadSpeedHistory = new CircularBuffer<MonitorHistoryPoint>(_historyCapacity);
        _downloadSpeedHistory = new CircularBuffer<MonitorHistoryPoint>(_historyCapacity);

        try
        {
            InitializeCounters();
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[NetworkMonitorService] 初始化网络计数器失败: {0}", ex.Message);
        }
    }

    private void InitializeCounters()
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up &&
                              nic.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            foreach (var nic in interfaces)
            {
                try
                {
                    var sentCounter = new PerformanceCounter("Network Interface", "Bytes Sent/sec", nic.Description);
                    var receivedCounter = new PerformanceCounter("Network Interface", "Bytes Received/sec", nic.Description);
                    sentCounter.NextValue();
                    receivedCounter.NextValue();

                    _sentCounters[nic.Name] = sentCounter;
                    _receivedCounters[nic.Name] = receivedCounter;
                }
                catch (Exception ex)
                {
                    XTrace.Log.Debug("[NetworkMonitorService] 初始化网络接口 {0} 计数器失败: {1}", nic.Name, ex.Message);
                }
            }

            var stats = GetTotalNetworkBytes();
            _lastSentBytes = stats.sentBytes;
            _lastReceivedBytes = stats.receivedBytes;
            _lastSampleTime = DateTime.Now;
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[NetworkMonitorService] 初始化网络计数器异常: {0}", ex.Message);
        }
    }

    private (long sentBytes, long receivedBytes) GetTotalNetworkBytes()
    {
        long sent = 0;
        long received = 0;

        try
        {
            if (OperatingSystem.IsWindows() && _sentCounters.Count > 0)
            {
                foreach (var counter in _sentCounters.Values)
                {
                    try { sent += (long)counter.NextValue(); } catch { }
                }
                foreach (var counter in _receivedCounters.Values)
                {
                    try { received += (long)counter.NextValue(); } catch { }
                }
            }
            else
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic => nic.OperationalStatus == OperationalStatus.Up &&
                                  nic.NetworkInterfaceType != NetworkInterfaceType.Loopback);

                foreach (var nic in interfaces)
                {
                    try
                    {
                        var stats = nic.GetIPv4Statistics();
                        sent += stats.BytesSent;
                        received += stats.BytesReceived;
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Debug("[NetworkMonitorService] 获取网络字节统计失败: {0}", ex.Message);
        }

        return (sent, received);
    }

    public void StartSampling()
    {
        if (_samplingTask != null && !_samplingTask.IsCompleted)
            return;

        _cts = new CancellationTokenSource();
        _samplingTask = Task.Run(() => SamplingLoop(_cts.Token));
        XTrace.Log.Info("[NetworkMonitorService] 网络监控采样已启动");
    }

    public void StopSampling()
    {
        _cts?.Cancel();
        try
        {
            _samplingTask?.Wait(2000);
        }
        catch { }
        XTrace.Log.Info("[NetworkMonitorService] 网络监控采样已停止");
    }

    private async Task SamplingLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await SampleNetworkAsync();
            }
            catch (Exception ex)
            {
                XTrace.Log.Warn("[NetworkMonitorService] 网络采样失败: {0}", ex.Message);
            }

            try
            {
                await Task.Delay(_sampleIntervalMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private Task SampleNetworkAsync()
    {
        var timestamp = DateTime.Now;

        try
        {
            double uploadSpeed = 0;
            double downloadSpeed = 0;

            if (OperatingSystem.IsWindows() && _sentCounters.Count > 0)
            {
                foreach (var counter in _sentCounters.Values)
                {
                    try { uploadSpeed += counter.NextValue(); } catch { }
                }
                foreach (var counter in _receivedCounters.Values)
                {
                    try { downloadSpeed += counter.NextValue(); } catch { }
                }
            }
            else
            {
                var stats = GetTotalNetworkBytesCrossPlatform();
                var timeSpan = (timestamp - _lastSampleTime).TotalSeconds;

                if (timeSpan > 0)
                {
                    uploadSpeed = Math.Max(0, (stats.sentBytes - _lastSentBytes) / timeSpan);
                    downloadSpeed = Math.Max(0, (stats.receivedBytes - _lastReceivedBytes) / timeSpan);
                }

                _lastSentBytes = stats.sentBytes;
                _lastReceivedBytes = stats.receivedBytes;
                _totalUploadedBytes = stats.sentBytes;
                _totalDownloadedBytes = stats.receivedBytes;
            }

            _lastSampleTime = timestamp;

            lock (_lock)
            {
                _lastSpeedInfo = new NetworkSpeedInfo
                {
                    UploadSpeedBytesPerSecond = uploadSpeed,
                    DownloadSpeedBytesPerSecond = downloadSpeed,
                    Timestamp = timestamp
                };

                _uploadSpeedHistory.Add(new MonitorHistoryPoint { Timestamp = timestamp, Value = uploadSpeed });
                _downloadSpeedHistory.Add(new MonitorHistoryPoint { Timestamp = timestamp, Value = downloadSpeed });
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[NetworkMonitorService] 网络采样异常: {0}", ex.Message);
        }

        return Task.CompletedTask;
    }

    private (long sentBytes, long receivedBytes) GetTotalNetworkBytesCrossPlatform()
    {
        long sent = 0;
        long received = 0;

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up &&
                              nic.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            foreach (var nic in interfaces)
            {
                try
                {
                    var stats = nic.GetIPv4Statistics();
                    sent += stats.BytesSent;
                    received += stats.BytesReceived;
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Debug("[NetworkMonitorService] 获取跨平台网络统计失败: {0}", ex.Message);
        }

        return (sent, received);
    }

    public Task<NetworkSpeedInfo> GetNetworkSpeedAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_lastSpeedInfo);
        }
    }

    public Task<List<NetworkConnectionInfo>> GetNetworkConnectionsAsync()
    {
        var result = new List<NetworkConnectionInfo>();

        try
        {
            var ipProperties = IPGlobalProperties.GetIPGlobalProperties();

            var tcpConnections = ipProperties.GetActiveTcpConnections();
            foreach (var conn in tcpConnections)
            {
                result.Add(new NetworkConnectionInfo
                {
                    LocalEndPoint = conn.LocalEndPoint?.ToString() ?? string.Empty,
                    RemoteEndPoint = conn.RemoteEndPoint?.ToString() ?? string.Empty,
                    State = conn.State.ToString(),
                    Protocol = "TCP",
                    ProcessId = 0
                });
            }

            var tcpListeners = ipProperties.GetActiveTcpListeners();
            foreach (var listener in tcpListeners)
            {
                result.Add(new NetworkConnectionInfo
                {
                    LocalEndPoint = listener.ToString(),
                    RemoteEndPoint = string.Empty,
                    State = "LISTENING",
                    Protocol = "TCP",
                    ProcessId = 0
                });
            }

            var udpListeners = ipProperties.GetActiveUdpListeners();
            foreach (var listener in udpListeners)
            {
                result.Add(new NetworkConnectionInfo
                {
                    LocalEndPoint = listener.ToString(),
                    RemoteEndPoint = string.Empty,
                    State = "LISTENING",
                    Protocol = "UDP",
                    ProcessId = 0
                });
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[NetworkMonitorService] 获取网络连接失败: {0}", ex.Message);
        }

        return Task.FromResult(result);
    }

    public Task<NetworkHistoryData> GetNetworkHistoryAsync(string duration = "1m")
    {
        var result = new NetworkHistoryData();
        var count = CalculateHistoryCount(duration);

        lock (_lock)
        {
            result.UploadSpeed = _uploadSpeedHistory.GetLast(count);
            result.DownloadSpeed = _downloadSpeedHistory.GetLast(count);
            result.TotalUploadedBytes = _totalUploadedBytes;
            result.TotalDownloadedBytes = _totalDownloadedBytes;
        }

        return Task.FromResult(result);
    }

    private int CalculateHistoryCount(string duration)
    {
        var minutes = duration switch
        {
            "1m" => 1,
            "5m" => 5,
            "15m" => 15,
            _ => 1
        };

        return Math.Min(minutes * 60, _historyCapacity);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        StopSampling();

        foreach (var counter in _sentCounters.Values)
        {
            counter.Dispose();
        }
        foreach (var counter in _receivedCounters.Values)
        {
            counter.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}

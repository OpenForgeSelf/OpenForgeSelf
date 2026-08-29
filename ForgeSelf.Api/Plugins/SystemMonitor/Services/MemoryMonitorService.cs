using System.Diagnostics;
using ForgeSelf.Api.Plugins.SystemMonitor.Models;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.SystemMonitor.Services;

public class MemoryMonitorService : IMemoryMonitorService, IDisposable
{
    private readonly int _sampleIntervalMs = 1000;
    private readonly int _maxHistoryMinutes = 15;
    private readonly int _historyCapacity;

    private readonly CircularBuffer<MonitorHistoryPoint> _usagePercentHistory;
    private readonly CircularBuffer<MonitorHistoryPoint> _usedBytesHistory;

    private PerformanceCounter? _availableMemoryCounter;
    private CancellationTokenSource? _cts;
    private Task? _samplingTask;
    private bool _disposed;

    private MemoryInfo _lastMemoryInfo = new();
    private readonly object _lock = new();

    public MemoryMonitorService()
    {
        _historyCapacity = _maxHistoryMinutes * 60;
        _usagePercentHistory = new CircularBuffer<MonitorHistoryPoint>(_historyCapacity);
        _usedBytesHistory = new CircularBuffer<MonitorHistoryPoint>(_historyCapacity);

        try
        {
            InitializeCounters();
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[MemoryMonitorService] 初始化性能计数器失败: {0}", ex.Message);
        }
    }

    private void InitializeCounters()
    {
        if (OperatingSystem.IsWindows())
        {
            _availableMemoryCounter = new PerformanceCounter("Memory", "Available Bytes");
            _availableMemoryCounter.NextValue();
        }
    }

    public void StartSampling()
    {
        if (_samplingTask != null && !_samplingTask.IsCompleted)
            return;

        _cts = new CancellationTokenSource();
        _samplingTask = Task.Run(() => SamplingLoop(_cts.Token));
        XTrace.Log.Info("[MemoryMonitorService] 内存监控采样已启动");
    }

    public void StopSampling()
    {
        _cts?.Cancel();
        try
        {
            _samplingTask?.Wait(2000);
        }
        catch { }
        XTrace.Log.Info("[MemoryMonitorService] 内存监控采样已停止");
    }

    private async Task SamplingLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await SampleMemoryAsync();
            }
            catch (Exception ex)
            {
                XTrace.Log.Warn("[MemoryMonitorService] 内存采样失败: {0}", ex.Message);
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

    private Task SampleMemoryAsync()
    {
        var timestamp = DateTime.Now;

        try
        {
            var memoryInfo = GetMemoryInfoInternal();

            lock (_lock)
            {
                _lastMemoryInfo = memoryInfo;
                _usagePercentHistory.Add(new MonitorHistoryPoint
                {
                    Timestamp = timestamp,
                    Value = memoryInfo.UsagePercent
                });
                _usedBytesHistory.Add(new MonitorHistoryPoint
                {
                    Timestamp = timestamp,
                    Value = memoryInfo.UsedMemoryBytes
                });
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[MemoryMonitorService] 内存采样异常: {0}", ex.Message);
        }

        return Task.CompletedTask;
    }

    private MemoryInfo GetMemoryInfoInternal()
    {
        var result = new MemoryInfo
        {
            Timestamp = DateTime.Now
        };

        try
        {
            if (OperatingSystem.IsWindows())
            {
                var totalMemory = GetTotalPhysicalMemory();
                var availableMemory = _availableMemoryCounter?.NextValue() ?? 0;

                result.TotalMemoryBytes = (long)totalMemory;
                result.AvailableMemoryBytes = (long)availableMemory;
                result.UsedMemoryBytes = result.TotalMemoryBytes - result.AvailableMemoryBytes;
                result.UsagePercent = result.TotalMemoryBytes > 0
                    ? (double)result.UsedMemoryBytes / result.TotalMemoryBytes * 100
                    : 0;
            }
            else
            {
                var currentProcess = Process.GetCurrentProcess();
                result.TotalMemoryBytes = (long)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes);
                result.UsedMemoryBytes = GC.GetTotalMemory(false);
                result.AvailableMemoryBytes = Math.Max(0, result.TotalMemoryBytes - result.UsedMemoryBytes);
                result.UsagePercent = result.TotalMemoryBytes > 0
                    ? (double)result.UsedMemoryBytes / result.TotalMemoryBytes * 100
                    : 0;
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[MemoryMonitorService] 获取内存信息失败: {0}", ex.Message);
        }

        return result;
    }

    private static long GetTotalPhysicalMemory()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var pc = new PerformanceCounter("Memory", "Committed Bytes");
                pc.NextValue();
            }

            var totalMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            return (long)totalMemory;
        }
        catch
        {
            return (long)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes);
        }
    }

    public Task<MemoryInfo> GetMemoryUsageAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_lastMemoryInfo);
        }
    }

    public Task<MemoryInfo> GetMemoryInfoAsync()
    {
        return GetMemoryUsageAsync();
    }

    public Task<MemoryHistoryData> GetMemoryHistoryAsync(string duration = "1m")
    {
        var result = new MemoryHistoryData();
        var count = CalculateHistoryCount(duration);

        lock (_lock)
        {
            result.UsagePercent = _usagePercentHistory.GetLast(count);
            result.UsedBytes = _usedBytesHistory.GetLast(count);
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

        _availableMemoryCounter?.Dispose();

        GC.SuppressFinalize(this);
    }
}

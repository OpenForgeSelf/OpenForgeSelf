using System.Diagnostics;
using ForgeSelf.Api.Plugins.SystemMonitor.Models;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.SystemMonitor.Services;

public class CpuMonitorService : ICpuMonitorService, IDisposable
{
    private readonly int _sampleIntervalMs = 1000;
    private readonly int _maxHistoryMinutes = 15;
    private readonly int _historyCapacity;

    private readonly CircularBuffer<MonitorHistoryPoint> _totalUsageHistory;
    private readonly Dictionary<int, CircularBuffer<MonitorHistoryPoint>> _perCoreHistory = new();

    private PerformanceCounter? _cpuCounter;
    private List<PerformanceCounter>? _coreCounters;

    private CancellationTokenSource? _cts;
    private Task? _samplingTask;
    private bool _disposed;

    private double _lastTotalUsage;
    private readonly object _lock = new();

    public CpuMonitorService()
    {
        _historyCapacity = _maxHistoryMinutes * 60;
        _totalUsageHistory = new CircularBuffer<MonitorHistoryPoint>(_historyCapacity);

        try
        {
            InitializeCounters();
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[CpuMonitorService] 初始化性能计数器失败: {0}", ex.Message);
        }
    }

    private void InitializeCounters()
    {
        if (OperatingSystem.IsWindows())
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuCounter.NextValue();

            var cpuCount = Environment.ProcessorCount;
            _coreCounters = new List<PerformanceCounter>();
            for (int i = 0; i < cpuCount; i++)
            {
                var coreCounter = new PerformanceCounter("Processor", "% Processor Time", i.ToString());
                coreCounter.NextValue();
                _coreCounters.Add(coreCounter);
                _perCoreHistory[i] = new CircularBuffer<MonitorHistoryPoint>(_historyCapacity);
            }
        }
    }

    public void StartSampling()
    {
        if (_samplingTask != null && !_samplingTask.IsCompleted)
            return;

        _cts = new CancellationTokenSource();
        _samplingTask = Task.Run(() => SamplingLoop(_cts.Token));
        XTrace.Log.Info("[CpuMonitorService] CPU监控采样已启动");
    }

    public void StopSampling()
    {
        _cts?.Cancel();
        try
        {
            _samplingTask?.Wait(2000);
        }
        catch { }
        XTrace.Log.Info("[CpuMonitorService] CPU监控采样已停止");
    }

    private async Task SamplingLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await SampleCpuAsync();
            }
            catch (Exception ex)
            {
                XTrace.Log.Warn("[CpuMonitorService] CPU采样失败: {0}", ex.Message);
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

    private Task SampleCpuAsync()
    {
        var timestamp = DateTime.Now;

        try
        {
            double totalUsage = 0;

            if (OperatingSystem.IsWindows() && _cpuCounter != null)
            {
                totalUsage = _cpuCounter.NextValue();
            }
            else
            {
                totalUsage = GetCpuUsageCrossPlatform();
            }

            lock (_lock)
            {
                _lastTotalUsage = totalUsage;
                _totalUsageHistory.Add(new MonitorHistoryPoint
                {
                    Timestamp = timestamp,
                    Value = totalUsage
                });
            }

            if (OperatingSystem.IsWindows() && _coreCounters != null)
            {
                for (int i = 0; i < _coreCounters.Count; i++)
                {
                    var coreUsage = _coreCounters[i].NextValue();
                    if (_perCoreHistory.TryGetValue(i, out var buffer))
                    {
                        buffer.Add(new MonitorHistoryPoint
                        {
                            Timestamp = timestamp,
                            Value = coreUsage
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[CpuMonitorService] CPU采样异常: {0}", ex.Message);
        }

        return Task.CompletedTask;
    }

    private double GetCpuUsageCrossPlatform()
    {
        try
        {
            var startTime = DateTime.UtcNow;
            var startCpuUsage = Process.GetProcesses().Sum(p =>
            {
                try { return p.TotalProcessorTime.TotalMilliseconds; }
                catch { return 0; }
            });

            Thread.Sleep(100);

            var endTime = DateTime.UtcNow;
            var endCpuUsage = Process.GetProcesses().Sum(p =>
            {
                try { return p.TotalProcessorTime.TotalMilliseconds; }
                catch { return 0; }
            });

            var cpuUsedMs = endCpuUsage - startCpuUsage;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed) * 100;

            return Math.Min(100, Math.Max(0, cpuUsageTotal));
        }
        catch
        {
            return 0;
        }
    }

    public Task<CpuUsage> GetCpuUsageAsync()
    {
        var result = new CpuUsage
        {
            TotalUsagePercent = _lastTotalUsage,
            Timestamp = DateTime.Now
        };

        try
        {
            if (_coreCounters != null)
            {
                for (int i = 0; i < _coreCounters.Count; i++)
                {
                    result.PerCoreUsage.Add(new CpuCoreUsage
                    {
                        CoreIndex = i,
                        UsagePercent = _perCoreHistory.TryGetValue(i, out var buffer)
                            ? buffer.GetLast(1).FirstOrDefault()?.Value ?? 0
                            : 0
                    });
                }
            }
            else
            {
                for (int i = 0; i < Environment.ProcessorCount; i++)
                {
                    result.PerCoreUsage.Add(new CpuCoreUsage
                    {
                        CoreIndex = i,
                        UsagePercent = _lastTotalUsage / Environment.ProcessorCount
                    });
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[CpuMonitorService] 获取CPU使用率失败: {0}", ex.Message);
        }

        return Task.FromResult(result);
    }

    public Task<List<CpuCoreUsage>> GetPerCoreUsageAsync()
    {
        var result = new List<CpuCoreUsage>();

        try
        {
            if (_coreCounters != null)
            {
                for (int i = 0; i < _coreCounters.Count; i++)
                {
                    result.Add(new CpuCoreUsage
                    {
                        CoreIndex = i,
                        UsagePercent = _perCoreHistory.TryGetValue(i, out var buffer)
                            ? buffer.GetLast(1).FirstOrDefault()?.Value ?? 0
                            : 0
                    });
                }
            }
            else
            {
                for (int i = 0; i < Environment.ProcessorCount; i++)
                {
                    result.Add(new CpuCoreUsage
                    {
                        CoreIndex = i,
                        UsagePercent = _lastTotalUsage / Environment.ProcessorCount
                    });
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[CpuMonitorService] 获取各核心使用率失败: {0}", ex.Message);
        }

        return Task.FromResult(result);
    }

    public Task<CpuHistoryData> GetCpuHistoryAsync(string duration = "1m")
    {
        var result = new CpuHistoryData();
        var count = CalculateHistoryCount(duration);

        lock (_lock)
        {
            result.TotalUsage = _totalUsageHistory.GetLast(count);

            foreach (var kvp in _perCoreHistory)
            {
                result.PerCoreUsage[kvp.Key] = kvp.Value.GetLast(count);
            }
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

        _cpuCounter?.Dispose();
        if (_coreCounters != null)
        {
            foreach (var counter in _coreCounters)
            {
                counter.Dispose();
            }
        }

        GC.SuppressFinalize(this);
    }
}

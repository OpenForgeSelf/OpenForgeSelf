using System.Diagnostics;
using System.IO;
using OpenForgeSelf.Backend.Plugins.SystemMonitor.Models;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.SystemMonitor.Services;

public class DiskMonitorService : IDiskMonitorService, IDisposable
{
    private readonly int _sampleIntervalMs = 1000;
    private readonly int _maxHistoryMinutes = 15;
    private readonly int _historyCapacity;
    private const double LowSpaceThresholdPercent = 90.0;

    private readonly Dictionary<string, CircularBuffer<MonitorHistoryPoint>> _usagePercentHistory = new();
    private readonly Dictionary<string, CircularBuffer<MonitorHistoryPoint>> _readSpeedHistory = new();
    private readonly Dictionary<string, CircularBuffer<MonitorHistoryPoint>> _writeSpeedHistory = new();
    private readonly Dictionary<string, DiskIOInfo> _lastDiskIO = new();
    private readonly Dictionary<string, PerformanceCounter> _readCounters = new();
    private readonly Dictionary<string, PerformanceCounter> _writeCounters = new();

    private CancellationTokenSource? _cts;
    private Task? _samplingTask;
    private bool _disposed;
    private readonly object _lock = new();

    public DiskMonitorService()
    {
        _historyCapacity = _maxHistoryMinutes * 60;
        try
        {
            InitializeDiskCounters();
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[DiskMonitorService] 初始化磁盘计数器失败: {0}", ex.Message);
        }
    }

    private void InitializeDiskCounters()
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            var drives = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed);
            foreach (var drive in drives)
            {
                var driveName = drive.Name.TrimEnd('\\');
                try
                {
                    var readCounter = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", driveName);
                    var writeCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", driveName);
                    readCounter.NextValue();
                    writeCounter.NextValue();

                    _readCounters[drive.Name] = readCounter;
                    _writeCounters[drive.Name] = writeCounter;

                    _usagePercentHistory[drive.Name] = new CircularBuffer<MonitorHistoryPoint>(_historyCapacity);
                    _readSpeedHistory[drive.Name] = new CircularBuffer<MonitorHistoryPoint>(_historyCapacity);
                    _writeSpeedHistory[drive.Name] = new CircularBuffer<MonitorHistoryPoint>(_historyCapacity);
                    _lastDiskIO[drive.Name] = new DiskIOInfo { DriveName = drive.Name };
                }
                catch (Exception ex)
                {
                    XTrace.Log.Debug("[DiskMonitorService] 初始化磁盘 {0} 计数器失败: {1}", drive.Name, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[DiskMonitorService] 初始化磁盘计数器异常: {0}", ex.Message);
        }
    }

    public void StartSampling()
    {
        if (_samplingTask != null && !_samplingTask.IsCompleted)
            return;

        _cts = new CancellationTokenSource();
        _samplingTask = Task.Run(() => SamplingLoop(_cts.Token));
        XTrace.Log.Info("[DiskMonitorService] 磁盘监控采样已启动");
    }

    public void StopSampling()
    {
        _cts?.Cancel();
        try
        {
            _samplingTask?.Wait(2000);
        }
        catch { }
        XTrace.Log.Info("[DiskMonitorService] 磁盘监控采样已停止");
    }

    private async Task SamplingLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await SampleDiskAsync();
            }
            catch (Exception ex)
            {
                XTrace.Log.Warn("[DiskMonitorService] 磁盘采样失败: {0}", ex.Message);
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

    private Task SampleDiskAsync()
    {
        var timestamp = DateTime.Now;

        try
        {
            var drives = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed);

            foreach (var drive in drives)
            {
                try
                {
                    var usagePercent = drive.TotalSize > 0
                        ? (double)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize * 100
                        : 0;

                    double readSpeed = 0;
                    double writeSpeed = 0;

                    if (_readCounters.TryGetValue(drive.Name, out var readCounter))
                    {
                        readSpeed = readCounter.NextValue();
                    }
                    if (_writeCounters.TryGetValue(drive.Name, out var writeCounter))
                    {
                        writeSpeed = writeCounter.NextValue();
                    }

                    lock (_lock)
                    {
                        if (_usagePercentHistory.TryGetValue(drive.Name, out var usageBuffer))
                        {
                            usageBuffer.Add(new MonitorHistoryPoint { Timestamp = timestamp, Value = usagePercent });
                        }
                        if (_readSpeedHistory.TryGetValue(drive.Name, out var readBuffer))
                        {
                            readBuffer.Add(new MonitorHistoryPoint { Timestamp = timestamp, Value = readSpeed });
                        }
                        if (_writeSpeedHistory.TryGetValue(drive.Name, out var writeBuffer))
                        {
                            writeBuffer.Add(new MonitorHistoryPoint { Timestamp = timestamp, Value = writeSpeed });
                        }

                        _lastDiskIO[drive.Name] = new DiskIOInfo
                        {
                            DriveName = drive.Name,
                            ReadSpeedBytesPerSecond = readSpeed,
                            WriteSpeedBytesPerSecond = writeSpeed,
                            Timestamp = timestamp
                        };
                    }
                }
                catch (Exception ex)
                {
                    XTrace.Log.Debug("[DiskMonitorService] 采样磁盘 {0} 失败: {1}", drive.Name, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[DiskMonitorService] 磁盘采样异常: {0}", ex.Message);
        }

        return Task.CompletedTask;
    }

    public Task<List<DiskDriveInfo>> GetDiskDrivesAsync()
    {
        var result = new List<DiskDriveInfo>();

        try
        {
            var drives = DriveInfo.GetDrives();
            foreach (var drive in drives)
            {
                try
                {
                    if (!drive.IsReady) continue;
                    if (drive.DriveType != DriveType.Fixed && drive.DriveType != DriveType.Removable) continue;

                    var usagePercent = drive.TotalSize > 0
                        ? (double)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize * 100
                        : 0;

                    result.Add(new DiskDriveInfo
                    {
                        DriveName = drive.Name,
                        VolumeLabel = drive.VolumeLabel,
                        DriveFormat = drive.DriveFormat,
                        TotalSizeBytes = drive.TotalSize,
                        AvailableFreeSpaceBytes = drive.AvailableFreeSpace,
                        UsedSpaceBytes = drive.TotalSize - drive.AvailableFreeSpace,
                        UsagePercent = usagePercent,
                        IsLowSpaceWarning = usagePercent > LowSpaceThresholdPercent
                    });
                }
                catch (Exception ex)
                {
                    XTrace.Log.Debug("[DiskMonitorService] 获取磁盘 {0} 信息失败: {1}", drive.Name, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DiskMonitorService] 获取磁盘列表失败: {0}", ex.Message);
        }

        return Task.FromResult(result);
    }

    public Task<DiskIOInfo> GetDiskIOAsync(string driveName)
    {
        lock (_lock)
        {
            if (_lastDiskIO.TryGetValue(driveName, out var ioInfo))
            {
                return Task.FromResult(ioInfo);
            }
        }

        return Task.FromResult(new DiskIOInfo { DriveName = driveName });
    }

    public Task<DiskHistoryData> GetDiskHistoryAsync(string driveName, string duration = "1m")
    {
        var result = new DiskHistoryData { DriveName = driveName };
        var count = CalculateHistoryCount(duration);

        lock (_lock)
        {
            if (_usagePercentHistory.TryGetValue(driveName, out var usageBuffer))
            {
                result.UsagePercent = usageBuffer.GetLast(count);
            }
            if (_readSpeedHistory.TryGetValue(driveName, out var readBuffer))
            {
                result.ReadSpeed = readBuffer.GetLast(count);
            }
            if (_writeSpeedHistory.TryGetValue(driveName, out var writeBuffer))
            {
                result.WriteSpeed = writeBuffer.GetLast(count);
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

        foreach (var counter in _readCounters.Values)
        {
            counter.Dispose();
        }
        foreach (var counter in _writeCounters.Values)
        {
            counter.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}

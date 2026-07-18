using System.Diagnostics;
using OpenForgeSelf.Backend.Plugins.SystemMonitor.Models;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.SystemMonitor.Services;

public class ProcessMonitorService : IProcessMonitorService
{
    private readonly Dictionary<int, double> _processCpuTimes = new();
    private readonly Dictionary<int, double> _processCpuUsages = new();
    private DateTime _lastCpuUpdateTime = DateTime.MinValue;
    private readonly object _lock = new();

    public ProcessMonitorService()
    {
        _ = UpdateCpuUsageAsync();
    }

    private async Task UpdateCpuUsageAsync()
    {
        while (true)
        {
            try
            {
                CalculateCpuUsages();
                await Task.Delay(2000);
            }
            catch
            {
                await Task.Delay(5000);
            }
        }
    }

    private void CalculateCpuUsages()
    {
        try
        {
            var now = DateTime.Now;
            var processes = Process.GetProcesses();
            var newCpuTimes = new Dictionary<int, TimeSpan>();

            foreach (var proc in processes)
            {
                try
                {
                    newCpuTimes[proc.Id] = proc.TotalProcessorTime;
                }
                catch { }
            }

            if (_lastCpuUpdateTime != DateTime.MinValue)
            {
                var timeSpan = (now - _lastCpuUpdateTime).TotalMilliseconds;
                if (timeSpan > 0)
                {
                    var newUsages = new Dictionary<int, double>();
                    foreach (var kvp in newCpuTimes)
                    {
                        if (_processCpuTimes.TryGetValue(kvp.Key, out var prevTime))
                        {
                            var cpuUsed = (kvp.Value.TotalMilliseconds - prevTime);
                            var usagePercent = cpuUsed / (Environment.ProcessorCount * timeSpan) * 100;
                            newUsages[kvp.Key] = Math.Min(100, Math.Max(0, usagePercent));
                        }
                    }

                    lock (_lock)
                    {
                        _processCpuUsages.Clear();
                        foreach (var kvp in newUsages)
                        {
                            _processCpuUsages[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }

            lock (_lock)
            {
                _processCpuTimes.Clear();
                foreach (var kvp in newCpuTimes)
                {
                    _processCpuTimes[kvp.Key] = kvp.Value.TotalMilliseconds;
                }
            }

            _lastCpuUpdateTime = now;
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[ProcessMonitorService] 计算CPU使用率失败: {0}", ex.Message);
        }
    }

    public Task<ProcessListResult> GetProcessesAsync(ProcessListRequest request)
    {
        var result = new ProcessListResult();

        try
        {
            var processes = Process.GetProcesses();
            var processList = new List<ProcessInfo>();

            foreach (var proc in processes)
            {
                try
                {
                    var info = GetProcessInfo(proc);
                    processList.Add(info);
                }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(request.SearchFilter))
            {
                var filter = request.SearchFilter.Trim().ToLowerInvariant();
                processList = processList.Where(p =>
                    p.ProcessName.ToLowerInvariant().Contains(filter) ||
                    p.ProcessId.ToString().Contains(filter)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(request.SortBy))
            {
                processList = request.SortBy.ToLowerInvariant() switch
                {
                    "name" => request.Ascending
                        ? processList.OrderBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase).ToList()
                        : processList.OrderByDescending(p => p.ProcessName, StringComparer.OrdinalIgnoreCase).ToList(),
                    "cpu" => request.Ascending
                        ? processList.OrderBy(p => p.CpuUsagePercent).ToList()
                        : processList.OrderByDescending(p => p.CpuUsagePercent).ToList(),
                    "memory" => request.Ascending
                        ? processList.OrderBy(p => p.MemoryUsageBytes).ToList()
                        : processList.OrderByDescending(p => p.MemoryUsageBytes).ToList(),
                    "pid" => request.Ascending
                        ? processList.OrderBy(p => p.ProcessId).ToList()
                        : processList.OrderByDescending(p => p.ProcessId).ToList(),
                    _ => processList
                };
            }

            result.TotalCount = processList.Count;

            if (request.Page > 0 && request.PageSize > 0)
            {
                result.Page = request.Page;
                result.PageSize = request.PageSize;
                result.Processes = processList
                    .Skip((request.Page - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .ToList();
            }
            else
            {
                result.Processes = processList;
                result.Page = 1;
                result.PageSize = processList.Count;
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ProcessMonitorService] 获取进程列表失败: {0}", ex.Message);
        }

        return Task.FromResult(result);
    }

    public Task<ProcessInfo?> GetProcessByIdAsync(int pid)
    {
        try
        {
            var proc = Process.GetProcessById(pid);
            var info = GetProcessInfo(proc);
            return Task.FromResult<ProcessInfo?>(info);
        }
        catch (ArgumentException)
        {
            XTrace.Log.Warn("[ProcessMonitorService] 进程不存在: {0}", pid);
            return Task.FromResult<ProcessInfo?>(null);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ProcessMonitorService] 获取进程详情失败: {0}", ex.Message);
            return Task.FromResult<ProcessInfo?>(null);
        }
    }

    public Task<bool> KillProcessAsync(int pid, bool force = false)
    {
        try
        {
            var proc = Process.GetProcessById(pid);
            if (force)
            {
                proc.Kill(true);
            }
            else
            {
                proc.Kill();
            }
            XTrace.Log.Info("[ProcessMonitorService] 已结束进程: PID={0}, Name={1}, Force={2}", pid, proc.ProcessName, force);
            return Task.FromResult(true);
        }
        catch (ArgumentException)
        {
            XTrace.Log.Warn("[ProcessMonitorService] 进程不存在，无法结束: {0}", pid);
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ProcessMonitorService] 结束进程失败: PID={0}, Error={1}", pid, ex.Message);
            return Task.FromResult(false);
        }
    }

    private ProcessInfo GetProcessInfo(Process proc)
    {
        double cpuUsage = 0;
        lock (_lock)
        {
            _processCpuUsages.TryGetValue(proc.Id, out cpuUsage);
        }

        var info = new ProcessInfo
        {
            ProcessId = proc.Id,
            ProcessName = proc.ProcessName,
            CpuUsagePercent = cpuUsage,
            MemoryUsageBytes = proc.WorkingSet64,
            WorkingSetBytes = proc.WorkingSet64,
            ThreadCount = proc.Threads.Count,
            HandleCount = proc.HandleCount
        };

        try
        {
            info.StartTime = proc.StartTime;
        }
        catch { }

        try
        {
            info.FilePath = proc.MainModule?.FileName;
        }
        catch { }

        try
        {
            info.MemoryUsageBytes = proc.PrivateMemorySize64;
        }
        catch { }

        return info;
    }
}

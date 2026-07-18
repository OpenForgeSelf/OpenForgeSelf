using System.Diagnostics;
using System.Text.Json;
using OpenForgeSelf.Backend.Plugins.Abstractions;
using OpenForgeSelf.Backend.Plugins.SystemMonitor.Models;
using OpenForgeSelf.Backend.Plugins.SystemMonitor.Services;
using OpenForgeSelf.Backend.Plugins.SystemMonitor.Hubs;
using OpenForgeSelf.Backend.Services.UsageStats;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.SystemMonitor;

public class SystemMonitorPlugin : IPlugin
{
    public string Id => "systemmonitor.plugin";
    public string Name => "系统监控插件";
    public string Version => "1.0.0";
    public string Author => "OpenForgeSelf Team";
    public string Description => "提供CPU、内存、磁盘、网络、进程等系统资源的实时监控和历史数据分析功能。";
    public string IconUrl => "https://example.com/system-monitor-icon.png";

    public List<IMenuExtension> MenuExtensions { get; } = new();
    public List<IToolFunctionExtension> ToolExtensions { get; } = new();

    private IServiceProvider? _serviceProvider;
    private ICpuMonitorService? _cpuMonitorService;
    private IMemoryMonitorService? _memoryMonitorService;
    private IDiskMonitorService? _diskMonitorService;
    private INetworkMonitorService? _networkMonitorService;
    private IProcessMonitorService? _processMonitorService;

    public void Initialize(IServiceProvider services)
    {
        _serviceProvider = services;
        XTrace.Log.Info("[SystemMonitorPlugin] 初始化系统监控插件");

        InitializeServices(services);
        RegisterMenuExtensions();
        RegisterToolFunctionExtensions();

        MonitorHub.SetServiceProvider(services);

        XTrace.Log.Info("[SystemMonitorPlugin] 系统监控插件初始化完成");
    }

    private void InitializeServices(IServiceProvider services)
    {
        try
        {
            _cpuMonitorService = services.GetService<ICpuMonitorService>();
            _memoryMonitorService = services.GetService<IMemoryMonitorService>();
            _diskMonitorService = services.GetService<IDiskMonitorService>();
            _networkMonitorService = services.GetService<INetworkMonitorService>();
            _processMonitorService = services.GetService<IProcessMonitorService>();

            XTrace.Log.Debug("[SystemMonitorPlugin] 监控服务初始化完成");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[SystemMonitorPlugin] 监控服务初始化失败: {0}", ex.Message);
        }
    }

    public void Start()
    {
        XTrace.Log.Info("[SystemMonitorPlugin] 启动系统监控插件");

        try
        {
            _cpuMonitorService?.StartSampling();
            _memoryMonitorService?.StartSampling();
            _diskMonitorService?.StartSampling();
            _networkMonitorService?.StartSampling();

            XTrace.Log.Info("[SystemMonitorPlugin] 系统监控采样已启动");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[SystemMonitorPlugin] 启动监控采样失败: {0}", ex.Message);
        }

        XTrace.Log.Info("[SystemMonitorPlugin] 系统监控插件启动完成");
    }

    public void Stop()
    {
        XTrace.Log.Info("[SystemMonitorPlugin] 停止系统监控插件");

        try
        {
            _cpuMonitorService?.StopSampling();
            _memoryMonitorService?.StopSampling();
            _diskMonitorService?.StopSampling();
            _networkMonitorService?.StopSampling();

            XTrace.Log.Info("[SystemMonitorPlugin] 系统监控采样已停止");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[SystemMonitorPlugin] 停止监控采样失败: {0}", ex.Message);
        }

        XTrace.Log.Info("[SystemMonitorPlugin] 系统监控插件已停止");
    }

    public void Destroy()
    {
        XTrace.Log.Info("[SystemMonitorPlugin] 销毁系统监控插件");

        try
        {
            if (_cpuMonitorService is IDisposable cpuDisposable)
                cpuDisposable.Dispose();
            if (_memoryMonitorService is IDisposable memoryDisposable)
                memoryDisposable.Dispose();
            if (_diskMonitorService is IDisposable diskDisposable)
                diskDisposable.Dispose();
            if (_networkMonitorService is IDisposable networkDisposable)
                networkDisposable.Dispose();
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[SystemMonitorPlugin] 销毁服务时发生异常: {0}", ex.Message);
        }

        MenuExtensions.Clear();
        ToolExtensions.Clear();

        XTrace.Log.Info("[SystemMonitorPlugin] 系统监控插件已销毁");
    }

    private void RegisterMenuExtensions()
    {
        XTrace.Log.Debug("[SystemMonitorPlugin] 注册菜单扩展点");

        var systemMonitorMenu = new SystemMonitorMenuExtension
        {
            Id = "systemmonitor.menu.main",
            Name = "系统监控",
            PluginId = Id,
            Icon = "fa-solid fa-gauge-high",
            Path = "/system-monitor",
            Order = 400,
            ParentId = null,
            Children = new List<IMenuExtension>
            {
                new SystemMonitorMenuExtension
                {
                    Id = "systemmonitor.menu.overview",
                    Name = "概览",
                    PluginId = Id,
                    Icon = "fa-solid fa-chart-line",
                    Path = "/system-monitor/overview",
                    Order = 1,
                    ParentId = "systemmonitor.menu.main"
                },
                new SystemMonitorMenuExtension
                {
                    Id = "systemmonitor.menu.processes",
                    Name = "进程管理",
                    PluginId = Id,
                    Icon = "fa-solid fa-microchip",
                    Path = "/system-monitor/processes",
                    Order = 2,
                    ParentId = "systemmonitor.menu.main"
                },
                new SystemMonitorMenuExtension
                {
                    Id = "systemmonitor.menu.disks",
                    Name = "磁盘信息",
                    PluginId = Id,
                    Icon = "fa-solid fa-hard-drive",
                    Path = "/system-monitor/disks",
                    Order = 3,
                    ParentId = "systemmonitor.menu.main"
                },
                new SystemMonitorMenuExtension
                {
                    Id = "systemmonitor.menu.network",
                    Name = "网络状态",
                    PluginId = Id,
                    Icon = "fa-solid fa-network-wired",
                    Path = "/system-monitor/network",
                    Order = 4,
                    ParentId = "systemmonitor.menu.main"
                }
            }
        };

        MenuExtensions.Add(systemMonitorMenu);
        XTrace.Log.Debug("[SystemMonitorPlugin] 菜单扩展点注册完成，共 {0} 个菜单项", MenuExtensions.Count);
    }

    private void RegisterToolFunctionExtensions()
    {
        XTrace.Log.Debug("[SystemMonitorPlugin] 注册AI工具函数扩展点");

        ToolExtensions.Add(new GetCpuUsageToolFunction(Id, _serviceProvider));
        ToolExtensions.Add(new GetMemoryUsageToolFunction(Id, _serviceProvider));
        ToolExtensions.Add(new GetDiskInfoToolFunction(Id, _serviceProvider));
        ToolExtensions.Add(new GetProcessListToolFunction(Id, _serviceProvider));
        ToolExtensions.Add(new KillProcessToolFunction(Id, _serviceProvider));

        XTrace.Log.Debug("[SystemMonitorPlugin] AI工具函数扩展点注册完成，共 {0} 个工具函数", ToolExtensions.Count);
    }
}

public class SystemMonitorMenuExtension : IMenuExtension
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PluginId { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int Order { get; set; }
    public string? ParentId { get; set; }
    public IReadOnlyList<IMenuExtension>? Children { get; set; }
}

public class GetCpuUsageToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id => "systemmonitor.get_cpu_usage";
    public string Name => "get_cpu_usage";
    public string PluginId { get; }
    public string Description => "获取当前系统CPU使用率，包括总使用率和各核心使用率。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {},
    ""required"": []
}";

    public GetCpuUsageToolFunction(string pluginId, IServiceProvider? serviceProvider)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[SystemMonitorPlugin] 执行 get_cpu_usage 工具函数");

            var service = GetService();
            if (service == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "CPU监控服务不可用" });
            }

            var cpuUsage = await service.GetCpuUsageAsync();
            var response = new
            {
                success = true,
                totalUsagePercent = cpuUsage.TotalUsagePercent,
                perCoreUsage = cpuUsage.PerCoreUsage.Select(c => new { coreIndex = c.CoreIndex, usagePercent = c.UsagePercent }).ToList(),
                timestamp = cpuUsage.Timestamp
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("get_cpu_usage", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["totalUsagePercent"] = cpuUsage.TotalUsagePercent
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[SystemMonitorPlugin] get_cpu_usage 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("get_cpu_usage", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });

            var errorResponse = new
            {
                success = false,
                error = ex.Message
            };
            return JsonSerializer.Serialize(errorResponse);
        }
    }

    private ICpuMonitorService? GetService()
    {
        if (_serviceProvider == null) return null;
        using var scope = _serviceProvider.CreateScope();
        return scope.ServiceProvider.GetService<ICpuMonitorService>();
    }

    private async Task RecordUsageAsync(string actionType, long durationMs, Dictionary<string, object>? metadata = null)
    {
        try
        {
            if (_serviceProvider == null) return;

            using var scope = _serviceProvider.CreateScope();
            var usageStatsService = scope.ServiceProvider.GetService<IUsageStatsService>();
            if (usageStatsService != null)
            {
                await usageStatsService.RecordUsageAsync(
                    PluginId,
                    Id,
                    actionType,
                    durationMs,
                    metadata);
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[SystemMonitorPlugin] 记录使用统计失败: {0}", ex.Message);
        }
    }
}

public class GetMemoryUsageToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id => "systemmonitor.get_memory_usage";
    public string Name => "get_memory_usage";
    public string PluginId { get; }
    public string Description => "获取当前系统内存使用情况，包括总内存、可用内存、已用内存和使用率。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {},
    ""required"": []
}";

    public GetMemoryUsageToolFunction(string pluginId, IServiceProvider? serviceProvider)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[SystemMonitorPlugin] 执行 get_memory_usage 工具函数");

            var service = GetService();
            if (service == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "内存监控服务不可用" });
            }

            var memoryInfo = await service.GetMemoryInfoAsync();
            var response = new
            {
                success = true,
                totalMemoryBytes = memoryInfo.TotalMemoryBytes,
                availableMemoryBytes = memoryInfo.AvailableMemoryBytes,
                usedMemoryBytes = memoryInfo.UsedMemoryBytes,
                usagePercent = memoryInfo.UsagePercent,
                timestamp = memoryInfo.Timestamp
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("get_memory_usage", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["usagePercent"] = memoryInfo.UsagePercent
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[SystemMonitorPlugin] get_memory_usage 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("get_memory_usage", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });

            var errorResponse = new
            {
                success = false,
                error = ex.Message
            };
            return JsonSerializer.Serialize(errorResponse);
        }
    }

    private IMemoryMonitorService? GetService()
    {
        if (_serviceProvider == null) return null;
        using var scope = _serviceProvider.CreateScope();
        return scope.ServiceProvider.GetService<IMemoryMonitorService>();
    }

    private async Task RecordUsageAsync(string actionType, long durationMs, Dictionary<string, object>? metadata = null)
    {
        try
        {
            if (_serviceProvider == null) return;

            using var scope = _serviceProvider.CreateScope();
            var usageStatsService = scope.ServiceProvider.GetService<IUsageStatsService>();
            if (usageStatsService != null)
            {
                await usageStatsService.RecordUsageAsync(
                    PluginId,
                    Id,
                    actionType,
                    durationMs,
                    metadata);
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[SystemMonitorPlugin] 记录使用统计失败: {0}", ex.Message);
        }
    }
}

public class GetDiskInfoToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id => "systemmonitor.get_disk_info";
    public string Name => "get_disk_info";
    public string PluginId { get; }
    public string Description => "获取系统所有磁盘分区信息，包括盘符、总大小、可用空间、使用率和空间预警状态。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {},
    ""required"": []
}";

    public GetDiskInfoToolFunction(string pluginId, IServiceProvider? serviceProvider)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[SystemMonitorPlugin] 执行 get_disk_info 工具函数");

            var service = GetService();
            if (service == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "磁盘监控服务不可用" });
            }

            var disks = await service.GetDiskDrivesAsync();
            var response = new
            {
                success = true,
                diskCount = disks.Count,
                disks = disks.Select(d => new
                {
                    driveName = d.DriveName,
                    volumeLabel = d.VolumeLabel,
                    driveFormat = d.DriveFormat,
                    totalSizeBytes = d.TotalSizeBytes,
                    availableFreeSpaceBytes = d.AvailableFreeSpaceBytes,
                    usedSpaceBytes = d.UsedSpaceBytes,
                    usagePercent = d.UsagePercent,
                    isLowSpaceWarning = d.IsLowSpaceWarning
                }).ToList()
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("get_disk_info", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["diskCount"] = disks.Count,
                ["lowSpaceCount"] = disks.Count(d => d.IsLowSpaceWarning)
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[SystemMonitorPlugin] get_disk_info 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("get_disk_info", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });

            var errorResponse = new
            {
                success = false,
                error = ex.Message
            };
            return JsonSerializer.Serialize(errorResponse);
        }
    }

    private IDiskMonitorService? GetService()
    {
        if (_serviceProvider == null) return null;
        using var scope = _serviceProvider.CreateScope();
        return scope.ServiceProvider.GetService<IDiskMonitorService>();
    }

    private async Task RecordUsageAsync(string actionType, long durationMs, Dictionary<string, object>? metadata = null)
    {
        try
        {
            if (_serviceProvider == null) return;

            using var scope = _serviceProvider.CreateScope();
            var usageStatsService = scope.ServiceProvider.GetService<IUsageStatsService>();
            if (usageStatsService != null)
            {
                await usageStatsService.RecordUsageAsync(
                    PluginId,
                    Id,
                    actionType,
                    durationMs,
                    metadata);
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[SystemMonitorPlugin] 记录使用统计失败: {0}", ex.Message);
        }
    }
}

public class GetProcessListToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id => "systemmonitor.get_process_list";
    public string Name => "get_process_list";
    public string PluginId { get; }
    public string Description => "获取系统进程列表，支持按名称/CPU/内存排序，支持搜索过滤。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""sortBy"": {
            ""type"": ""string"",
            ""enum"": [""name"", ""cpu"", ""memory"", ""pid""],
            ""description"": ""排序方式，默认按CPU降序"",
            ""default"": ""cpu""
        },
        ""ascending"": {
            ""type"": ""boolean"",
            ""description"": ""是否升序排列，默认false（降序）"",
            ""default"": false
        },
        ""searchFilter"": {
            ""type"": ""string"",
            ""description"": ""按进程名称或PID搜索过滤""
        },
        ""limit"": {
            ""type"": ""integer"",
            ""description"": ""返回的进程数量限制，默认20"",
            ""default"": 20
        }
    },
    ""required"": []
}";

    public GetProcessListToolFunction(string pluginId, IServiceProvider? serviceProvider)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[SystemMonitorPlugin] 执行 get_process_list 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var sortBy = "cpu";
            if (paramsDoc.RootElement.TryGetProperty("sortBy", out var sortByProp))
            {
                sortBy = sortByProp.GetString() ?? "cpu";
            }

            var ascending = false;
            if (paramsDoc.RootElement.TryGetProperty("ascending", out var ascendingProp))
            {
                ascending = ascendingProp.GetBoolean();
            }

            string? searchFilter = null;
            if (paramsDoc.RootElement.TryGetProperty("searchFilter", out var searchProp))
            {
                searchFilter = searchProp.GetString();
            }

            var limit = 20;
            if (paramsDoc.RootElement.TryGetProperty("limit", out var limitProp))
            {
                limit = limitProp.GetInt32();
            }

            var service = GetService();
            if (service == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "进程监控服务不可用" });
            }

            var request = new ProcessListRequest
            {
                SortBy = sortBy,
                Ascending = ascending,
                SearchFilter = searchFilter,
                Page = 1,
                PageSize = limit
            };

            var result = await service.GetProcessesAsync(request);
            var response = new
            {
                success = true,
                totalCount = result.TotalCount,
                processCount = result.Processes.Count,
                processes = result.Processes.Select(p => new
                {
                    processId = p.ProcessId,
                    processName = p.ProcessName,
                    cpuUsagePercent = p.CpuUsagePercent,
                    memoryUsageBytes = p.MemoryUsageBytes,
                    workingSetBytes = p.WorkingSetBytes,
                    threadCount = p.ThreadCount,
                    startTime = p.StartTime
                }).ToList()
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("get_process_list", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["totalCount"] = result.TotalCount,
                ["returnedCount"] = result.Processes.Count,
                ["sortBy"] = sortBy,
                ["searchFilter"] = searchFilter ?? ""
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[SystemMonitorPlugin] get_process_list 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("get_process_list", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });

            var errorResponse = new
            {
                success = false,
                error = ex.Message
            };
            return JsonSerializer.Serialize(errorResponse);
        }
    }

    private IProcessMonitorService? GetService()
    {
        if (_serviceProvider == null) return null;
        using var scope = _serviceProvider.CreateScope();
        return scope.ServiceProvider.GetService<IProcessMonitorService>();
    }

    private async Task RecordUsageAsync(string actionType, long durationMs, Dictionary<string, object>? metadata = null)
    {
        try
        {
            if (_serviceProvider == null) return;

            using var scope = _serviceProvider.CreateScope();
            var usageStatsService = scope.ServiceProvider.GetService<IUsageStatsService>();
            if (usageStatsService != null)
            {
                await usageStatsService.RecordUsageAsync(
                    PluginId,
                    Id,
                    actionType,
                    durationMs,
                    metadata);
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[SystemMonitorPlugin] 记录使用统计失败: {0}", ex.Message);
        }
    }
}

public class KillProcessToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id => "systemmonitor.kill_process";
    public string Name => "kill_process";
    public string PluginId { get; }
    public string Description => "结束指定的系统进程，支持强制结束。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""processId"": {
            ""type"": ""integer"",
            ""description"": ""要结束的进程ID（PID）""
        },
        ""force"": {
            ""type"": ""boolean"",
            ""description"": ""是否强制结束进程"",
            ""default"": false
        }
    },
    ""required"": [""processId""]
}";

    public KillProcessToolFunction(string pluginId, IServiceProvider? serviceProvider)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[SystemMonitorPlugin] 执行 kill_process 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var processId = paramsDoc.RootElement.GetProperty("processId").GetInt32();
            var force = paramsDoc.RootElement.TryGetProperty("force", out var forceProp) && forceProp.GetBoolean();

            var service = GetService();
            if (service == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "进程监控服务不可用" });
            }

            var result = await service.KillProcessAsync(processId, force);
            var response = new
            {
                success = result,
                processId = processId,
                force = force,
                message = result ? "进程已成功结束" : "进程结束失败"
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("kill_process", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["processId"] = processId,
                ["force"] = force,
                ["result"] = result
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[SystemMonitorPlugin] kill_process 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("kill_process", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });

            var errorResponse = new
            {
                success = false,
                error = ex.Message
            };
            return JsonSerializer.Serialize(errorResponse);
        }
    }

    private IProcessMonitorService? GetService()
    {
        if (_serviceProvider == null) return null;
        using var scope = _serviceProvider.CreateScope();
        return scope.ServiceProvider.GetService<IProcessMonitorService>();
    }

    private async Task RecordUsageAsync(string actionType, long durationMs, Dictionary<string, object>? metadata = null)
    {
        try
        {
            if (_serviceProvider == null) return;

            using var scope = _serviceProvider.CreateScope();
            var usageStatsService = scope.ServiceProvider.GetService<IUsageStatsService>();
            if (usageStatsService != null)
            {
                await usageStatsService.RecordUsageAsync(
                    PluginId,
                    Id,
                    actionType,
                    durationMs,
                    metadata);
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[SystemMonitorPlugin] 记录使用统计失败: {0}", ex.Message);
        }
    }
}

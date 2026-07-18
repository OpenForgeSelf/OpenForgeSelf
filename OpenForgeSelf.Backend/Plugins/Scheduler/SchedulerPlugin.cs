using System.Diagnostics;
using System.Text.Json;
using OpenForgeSelf.Backend.Plugins.Abstractions;
using OpenForgeSelf.Backend.Plugins.Scheduler.Models;
using OpenForgeSelf.Backend.Plugins.Scheduler.Services;
using OpenForgeSelf.Backend.Services.UsageStats;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;
using XCode.DataAccessLayer;

namespace OpenForgeSelf.Backend.Plugins.Scheduler;

public class SchedulerPlugin : IPlugin
{
    public string Id => "scheduler.plugin";
    public string Name => "定时任务插件";
    public string Version => "1.0.0";
    public string Author => "OpenForgeSelf Team";
    public string Description => "定时任务调度插件，支持Cron表达式、间隔执行、单次执行等多种调度方式，可定时执行工作流、系统命令和HTTP Webhook。";
    public string IconUrl => "https://example.com/scheduler-icon.png";

    public List<IMenuExtension> MenuExtensions { get; private set; } = new();
    public List<IToolFunctionExtension> ToolExtensions { get; private set; } = new();

    private IServiceProvider? _serviceProvider;
    private ITaskScheduler? _taskScheduler;

    public void Initialize(IServiceProvider services)
    {
        try
        {
            XTrace.Log.Info("初始化定时任务插件");
            _serviceProvider = services;

            RegisterMenuExtensions();
            RegisterToolExtensions();

            XTrace.Log.Info("定时任务插件初始化完成");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("定时任务插件初始化失败: {0}", ex.Message);
            throw;
        }
    }

    public void Start()
    {
        try
        {
            XTrace.Log.Info("启动定时任务插件");

            EnsureDatabaseCreated();
            StartScheduler();

            XTrace.Log.Info("定时任务插件启动完成");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("定时任务插件启动失败: {0}", ex.Message);
            throw;
        }
    }

    public void Stop()
    {
        try
        {
            XTrace.Log.Info("停止定时任务插件");

            StopScheduler();

            XTrace.Log.Info("定时任务插件已停止");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("定时任务插件停止失败: {0}", ex.Message);
            throw;
        }
    }

    public void Destroy()
    {
        try
        {
            XTrace.Log.Info("销毁定时任务插件");
            MenuExtensions.Clear();
            ToolExtensions.Clear();
            XTrace.Log.Info("定时任务插件已销毁");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("定时任务插件销毁失败: {0}", ex.Message);
            throw;
        }
    }

    private void RegisterMenuExtensions()
    {
        MenuExtensions.Add(new SchedulerMenuExtension
        {
            Id = "scheduler.menu.main",
            Name = "定时任务",
            PluginId = Id,
            Icon = "fa-clock",
            Path = "/scheduler",
            Order = 90,
            ParentId = null
        });

        XTrace.Log.Debug("定时任务插件已注册菜单扩展点");
    }

    private void RegisterToolExtensions()
    {
        ToolExtensions.Add(new ScheduleTaskToolFunction(_serviceProvider)
        {
            Id = "scheduler.tool.create_scheduled_task",
            Name = "create_scheduled_task",
            PluginId = Id,
            Description = "创建一个定时任务，支持工作流、系统命令和HTTP Webhook的定时执行",
            ParametersJsonSchema = @"
{
    ""type"": ""object"",
    ""properties"": {
        ""name"": {
            ""type"": ""string"",
            ""description"": ""任务名称""
        },
        ""description"": {
            ""type"": ""string"",
            ""description"": ""任务描述""
        },
        ""taskType"": {
            ""type"": ""string"",
            ""description"": ""任务类型：Workflow(工作流)、SystemCommand(系统命令)、HttpWebhook(HTTP请求)"",
            ""enum"": [""Workflow"", ""SystemCommand"", ""HttpWebhook""]
        },
        ""targetId"": {
            ""type"": ""string"",
            ""description"": ""目标ID：工作流ID、命令或URL""
        },
        ""scheduleType"": {
            ""type"": ""string"",
            ""description"": ""调度类型：Cron、Interval(间隔)、Once(单次)、Daily(每日)、Weekly(每周)、Monthly(每月)"",
            ""enum"": [""Cron"", ""Interval"", ""Once"", ""Daily"", ""Weekly"", ""Monthly""]
        },
        ""cronExpression"": {
            ""type"": ""string"",
            ""description"": ""Cron表达式（Cron类型时必填）""
        },
        ""intervalMinutes"": {
            ""type"": ""integer"",
            ""description"": ""间隔分钟数（Interval类型时必填）""
        },
        ""runAt"": {
            ""type"": ""string"",
            ""description"": ""单次执行时间（Once类型时必填，ISO 8601格式）""
        },
        ""timeZone"": {
            ""type"": ""string"",
            ""description"": ""时区，默认Asia/Shanghai"",
            ""default"": ""Asia/Shanghai""
        },
        ""inputParameters"": {
            ""type"": ""string"",
            ""description"": ""输入参数JSON字符串""
        }
    },
    ""required"": [""name"", ""taskType"", ""targetId"", ""scheduleType""]
}"
        });

        ToolExtensions.Add(new ListScheduledTasksToolFunction(_serviceProvider)
        {
            Id = "scheduler.tool.list_scheduled_tasks",
            Name = "list_scheduled_tasks",
            PluginId = Id,
            Description = "列出所有定时任务，支持按关键词和状态筛选",
            ParametersJsonSchema = @"
{
    ""type"": ""object"",
    ""properties"": {
        ""keyword"": {
            ""type"": ""string"",
            ""description"": ""搜索关键词""
        },
        ""status"": {
            ""type"": ""string"",
            ""description"": ""任务状态：Enabled、Disabled、Paused、Error"",
            ""enum"": [""Enabled"", ""Disabled"", ""Paused"", ""Error""]
        },
        ""page"": {
            ""type"": ""integer"",
            ""description"": ""页码，默认1"",
            ""default"": 1
        },
        ""pageSize"": {
            ""type"": ""integer"",
            ""description"": ""每页数量，默认20"",
            ""default"": 20
        }
    },
    ""required"": []
}"
        });

        ToolExtensions.Add(new PauseScheduledTaskToolFunction(_serviceProvider)
        {
            Id = "scheduler.tool.pause_scheduled_task",
            Name = "pause_scheduled_task",
            PluginId = Id,
            Description = "暂停指定的定时任务",
            ParametersJsonSchema = @"
{
    ""type"": ""object"",
    ""properties"": {
        ""taskId"": {
            ""type"": ""integer"",
            ""description"": ""任务ID""
        }
    },
    ""required"": [""taskId""]
}"
        });

        XTrace.Log.Debug("定时任务插件已注册AI工具函数扩展点，共 {0} 个工具", ToolExtensions.Count);
    }

    private void EnsureDatabaseCreated()
    {
        try
        {
            var dal = DAL.Create("Scheduler");
            dal.Session.Query("SELECT 1");

            XTrace.Log.Info("定时任务插件数据库初始化完成");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("定时任务插件数据库初始化失败: {0}", ex.Message);
        }
    }

    private void StartScheduler()
    {
        try
        {
            var cronParser = new CronParser();
            var workflowHandler = new WorkflowTaskHandler(_serviceProvider!);
            var httpHandler = new HttpWebhookHandler();
            var taskExecutor = new TaskExecutor(_serviceProvider!, workflowHandler, httpHandler);

            _taskScheduler = new Services.TaskScheduler(taskExecutor, cronParser);
            _taskScheduler.StartAsync().Wait();

            XTrace.Log.Info("定时任务调度器已启动");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("启动定时任务调度器失败: {0}", ex.Message);
        }
    }

    private void StopScheduler()
    {
        try
        {
            _taskScheduler?.StopAsync().Wait();
            XTrace.Log.Info("定时任务调度器已停止");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("停止定时任务调度器失败: {0}", ex.Message);
        }
    }
}

public class SchedulerMenuExtension : IMenuExtension
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

public class ScheduleTaskToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PluginId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ParametersJsonSchema { get; set; } = string.Empty;

    public ScheduleTaskToolFunction(IServiceProvider? serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[Scheduler] 执行 create_scheduled_task 工具函数，参数: {0}", parameters);

            using var doc = JsonDocument.Parse(parameters);
            var root = doc.RootElement;

            var name = root.GetProperty("name").GetString() ?? string.Empty;
            var taskTypeStr = root.GetProperty("taskType").GetString() ?? "Workflow";
            var targetId = root.GetProperty("targetId").GetString() ?? string.Empty;
            var scheduleTypeStr = root.GetProperty("scheduleType").GetString() ?? "Cron";

            if (string.IsNullOrWhiteSpace(name))
            {
                return JsonSerializer.Serialize(new { success = false, error = "任务名称不能为空" });
            }

            if (string.IsNullOrWhiteSpace(targetId))
            {
                return JsonSerializer.Serialize(new { success = false, error = "任务目标ID不能为空" });
            }

            var taskType = Enum.Parse<ScheduledTaskType>(taskTypeStr);
            var scheduleType = Enum.Parse<ScheduleType>(scheduleTypeStr);

            var description = root.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;
            var cronExpression = root.TryGetProperty("cronExpression", out var cronProp) ? cronProp.GetString() : null;
            int? intervalMinutes = root.TryGetProperty("intervalMinutes", out var intervalProp) ? intervalProp.GetInt32() : null;
            var timeZone = root.TryGetProperty("timeZone", out var tzProp) ? tzProp.GetString() ?? "Asia/Shanghai" : "Asia/Shanghai";
            var inputParameters = root.TryGetProperty("inputParameters", out var inputProp) ? inputProp.GetString() : null;

            DateTime? runAt = null;
            if (root.TryGetProperty("runAt", out var runAtProp) && runAtProp.TryGetDateTime(out var runAtValue))
            {
                runAt = runAtValue;
            }

            var cronParser = new CronParser();
            var workflowHandler = new WorkflowTaskHandler(_serviceProvider!);
            var httpHandler = new HttpWebhookHandler();
            var taskExecutor = new TaskExecutor(_serviceProvider!, workflowHandler, httpHandler);
            var taskScheduler = new Services.TaskScheduler(taskExecutor, cronParser);
            var service = new SchedulerService(cronParser, taskScheduler);

            var request = new CreateScheduledTaskRequest
            {
                Name = name,
                Description = description,
                TaskType = taskType,
                TargetId = targetId,
                ScheduleType = scheduleType,
                CronExpression = cronExpression,
                IntervalMinutes = intervalMinutes,
                RunAt = runAt,
                TimeZone = timeZone,
                InputParameters = inputParameters
            };

            var task = await service.CreateTaskAsync(request);

            var response = new
            {
                success = true,
                task = new
                {
                    id = task.Id,
                    name = task.Name,
                    description = task.Description,
                    taskType = task.TaskType.ToString(),
                    targetId = task.TargetId,
                    scheduleType = task.ScheduleType.ToString(),
                    status = task.Status.ToString(),
                    nextRunTime = task.NextRunTime,
                    createdAt = task.CreatedAt
                },
                message = "定时任务创建成功"
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("create_scheduled_task", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["taskId"] = task.Id,
                ["taskName"] = task.Name,
                ["taskType"] = task.TaskType.ToString()
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[Scheduler] 执行 create_scheduled_task 工具函数失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("create_scheduled_task", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });

            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
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
            XTrace.Log.Warn("[Scheduler] 记录使用统计失败: {0}", ex.Message);
        }
    }
}

public class ListScheduledTasksToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PluginId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ParametersJsonSchema { get; set; } = string.Empty;

    public ListScheduledTasksToolFunction(IServiceProvider? serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[Scheduler] 执行 list_scheduled_tasks 工具函数，参数: {0}", parameters);

            using var doc = JsonDocument.Parse(parameters);
            var root = doc.RootElement;

            string? keyword = null;
            if (root.TryGetProperty("keyword", out var keywordProp))
                keyword = keywordProp.GetString();

            ScheduledTaskStatus? status = null;
            if (root.TryGetProperty("status", out var statusProp))
            {
                var statusStr = statusProp.GetString();
                if (!string.IsNullOrEmpty(statusStr))
                    status = Enum.Parse<ScheduledTaskStatus>(statusStr);
            }

            int page = 1;
            if (root.TryGetProperty("page", out var pageProp))
                page = pageProp.GetInt32();

            int pageSize = 20;
            if (root.TryGetProperty("pageSize", out var pageSizeProp))
                pageSize = pageSizeProp.GetInt32();

            var cronParser = new CronParser();
            var workflowHandler = new WorkflowTaskHandler(_serviceProvider!);
            var httpHandler = new HttpWebhookHandler();
            var taskExecutor = new TaskExecutor(_serviceProvider!, workflowHandler, httpHandler);
            var taskScheduler = new Services.TaskScheduler(taskExecutor, cronParser);
            var service = new SchedulerService(cronParser, taskScheduler);

            var result = await service.ListTasksAsync(keyword, status, page, pageSize);

            var response = new
            {
                success = true,
                total = result.Total,
                page = result.Page,
                pageSize = result.PageSize,
                tasks = result.Items.Select(t => new
                {
                    id = t.Id,
                    name = t.Name,
                    description = t.Description,
                    taskType = t.TaskType.ToString(),
                    targetId = t.TargetId,
                    scheduleType = t.ScheduleType.ToString(),
                    status = t.Status.ToString(),
                    lastRunTime = t.LastRunTime,
                    nextRunTime = t.NextRunTime,
                    runCount = t.RunCount,
                    failureCount = t.FailureCount,
                    createdAt = t.CreatedAt
                }).ToList()
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("list_scheduled_tasks", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["keyword"] = keyword ?? string.Empty,
                ["total"] = result.Total
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[Scheduler] 执行 list_scheduled_tasks 工具函数失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("list_scheduled_tasks", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });

            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
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
            XTrace.Log.Warn("[Scheduler] 记录使用统计失败: {0}", ex.Message);
        }
    }
}

public class PauseScheduledTaskToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PluginId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ParametersJsonSchema { get; set; } = string.Empty;

    public PauseScheduledTaskToolFunction(IServiceProvider? serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[Scheduler] 执行 pause_scheduled_task 工具函数，参数: {0}", parameters);

            using var doc = JsonDocument.Parse(parameters);
            var root = doc.RootElement;
            var taskId = root.GetProperty("taskId").GetInt64();

            var cronParser = new CronParser();
            var workflowHandler = new WorkflowTaskHandler(_serviceProvider!);
            var httpHandler = new HttpWebhookHandler();
            var taskExecutor = new TaskExecutor(_serviceProvider!, workflowHandler, httpHandler);
            var taskScheduler = new Services.TaskScheduler(taskExecutor, cronParser);
            var service = new SchedulerService(cronParser, taskScheduler);

            var task = await service.ToggleTaskStatusAsync(taskId, false);
            if (task == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "任务不存在" });
            }

            var response = new
            {
                success = true,
                task = new
                {
                    id = task.Id,
                    name = task.Name,
                    status = task.Status.ToString()
                },
                message = "任务已暂停"
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("pause_scheduled_task", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["taskId"] = taskId
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[Scheduler] 执行 pause_scheduled_task 工具函数失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("pause_scheduled_task", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });

            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
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
            XTrace.Log.Warn("[Scheduler] 记录使用统计失败: {0}", ex.Message);
        }
    }
}

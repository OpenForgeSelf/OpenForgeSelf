using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;
using OpenForgeSelf.Backend.Plugins.Abstractions;
using OpenForgeSelf.Backend.Plugins.TodoTracker.Models;
using OpenForgeSelf.Backend.Plugins.TodoTracker.Services;
using OpenForgeSelf.Backend.Services.UsageStats;

namespace OpenForgeSelf.Backend.Plugins.TodoTracker;

/// <summary>
/// create_todo 工具函数：创建一个新的待办事项。
/// </summary>
public class CreateTodoToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PluginId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ParametersJsonSchema { get; set; } = string.Empty;

    public CreateTodoToolFunction(IServiceProvider? serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            string? title = null;
            string? remark = null;
            string? dueDate = null;

            if (!string.IsNullOrWhiteSpace(parameters))
            {
                using var doc = JsonDocument.Parse(parameters);
                if (doc.RootElement.TryGetProperty("title", out var titleEl) && titleEl.ValueKind == JsonValueKind.String)
                {
                    title = titleEl.GetString();
                }
                if (doc.RootElement.TryGetProperty("remark", out var remarkEl) && remarkEl.ValueKind == JsonValueKind.String)
                {
                    remark = remarkEl.GetString();
                }
                if (doc.RootElement.TryGetProperty("dueDate", out var dueEl) && dueEl.ValueKind == JsonValueKind.String)
                {
                    dueDate = dueEl.GetString();
                }
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                return JsonSerializer.Serialize(new { success = false, error = "参数 title 不能为空" });
            }

            DateTime? parsedDueDate = null;
            if (!string.IsNullOrWhiteSpace(dueDate) && DateTime.TryParse(dueDate, out var dt))
            {
                parsedDueDate = dt;
            }

            if (_serviceProvider == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "服务提供者未初始化" });
            }

            using var scope = _serviceProvider.CreateScope();
            var todoService = scope.ServiceProvider.GetService<ITodoService>();
            if (todoService == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "ITodoService 未注册" });
            }

            var request = new CreateTodoRequest
            {
                Title = title.Trim(),
                Remark = string.IsNullOrWhiteSpace(remark) ? null : remark,
                DueDate = parsedDueDate
            };

            var created = await todoService.CreateTodoAsync(request);

            stopwatch.Stop();
            await RecordUsageAsync("create_todo", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["todoId"] = created.Id,
                ["title"] = created.Title
            });

            return JsonSerializer.Serialize(new { success = true, data = created });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            XTrace.Log.Warn("[TodoTracker] create_todo 执行失败: {0}", ex.Message);
            await RecordUsageAsync("create_todo", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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
                await usageStatsService.RecordUsageAsync(PluginId, Id, actionType, durationMs, metadata);
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[TodoTracker] 记录使用统计失败: {0}", ex.Message);
        }
    }
}

/// <summary>
/// list_todos 工具函数：列出待办事项，支持按状态过滤与分页。
/// </summary>
public class ListTodosToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PluginId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ParametersJsonSchema { get; set; } = string.Empty;

    public ListTodosToolFunction(IServiceProvider? serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            string? status = null;
            int page = 1;
            int pageSize = 20;

            if (!string.IsNullOrWhiteSpace(parameters))
            {
                using var doc = JsonDocument.Parse(parameters);
                if (doc.RootElement.TryGetProperty("status", out var statusEl) && statusEl.ValueKind == JsonValueKind.String)
                {
                    var s = statusEl.GetString();
                    if (!string.IsNullOrEmpty(s) && (s == "Pending" || s == "Completed"))
                    {
                        status = s;
                    }
                }
                if (doc.RootElement.TryGetProperty("page", out var pageEl) && pageEl.TryGetInt32(out var p) && p > 0)
                {
                    page = p;
                }
                if (doc.RootElement.TryGetProperty("pageSize", out var sizeEl) && sizeEl.TryGetInt32(out var ps) && ps > 0)
                {
                    pageSize = Math.Min(ps, 100);
                }
            }

            if (_serviceProvider == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "服务提供者未初始化" });
            }

            using var scope = _serviceProvider.CreateScope();
            var todoService = scope.ServiceProvider.GetService<ITodoService>();
            if (todoService == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "ITodoService 未注册" });
            }

            var result = await todoService.GetTodosAsync(status, page, pageSize);

            stopwatch.Stop();
            await RecordUsageAsync("list_todos", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["status"] = status ?? "all",
                ["page"] = page,
                ["pageSize"] = pageSize,
                ["returned"] = result.Items.Count,
                ["total"] = result.Total
            });

            return JsonSerializer.Serialize(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            XTrace.Log.Warn("[TodoTracker] list_todos 执行失败: {0}", ex.Message);
            await RecordUsageAsync("list_todos", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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
                await usageStatsService.RecordUsageAsync(PluginId, Id, actionType, durationMs, metadata);
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[TodoTracker] 记录使用统计失败: {0}", ex.Message);
        }
    }
}

/// <summary>
/// complete_todo 工具函数：标记待办事项为已完成。
/// </summary>
public class CompleteTodoToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PluginId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ParametersJsonSchema { get; set; } = string.Empty;

    public CompleteTodoToolFunction(IServiceProvider? serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            int id = 0;
            if (!string.IsNullOrWhiteSpace(parameters))
            {
                using var doc = JsonDocument.Parse(parameters);
                if (doc.RootElement.TryGetProperty("id", out var idEl) && idEl.TryGetInt32(out var parsedId) && parsedId > 0)
                {
                    id = parsedId;
                }
            }

            if (id <= 0)
            {
                return JsonSerializer.Serialize(new { success = false, error = "参数 id 必须为正整数" });
            }

            if (_serviceProvider == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "服务提供者未初始化" });
            }

            using var scope = _serviceProvider.CreateScope();
            var todoService = scope.ServiceProvider.GetService<ITodoService>();
            if (todoService == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "ITodoService 未注册" });
            }

            var updated = await todoService.CompleteTodoAsync(id);
            if (updated == null)
            {
                stopwatch.Stop();
                await RecordUsageAsync("complete_todo", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
                {
                    ["todoId"] = id,
                    ["notFound"] = true
                });
                return JsonSerializer.Serialize(new { success = false, error = $"待办 {id} 不存在" });
            }

            stopwatch.Stop();
            await RecordUsageAsync("complete_todo", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["todoId"] = id,
                ["title"] = updated.Title
            });

            return JsonSerializer.Serialize(new { success = true, data = updated });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            XTrace.Log.Warn("[TodoTracker] complete_todo 执行失败: {0}", ex.Message);
            await RecordUsageAsync("complete_todo", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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
                await usageStatsService.RecordUsageAsync(PluginId, Id, actionType, durationMs, metadata);
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[TodoTracker] 记录使用统计失败: {0}", ex.Message);
        }
    }
}

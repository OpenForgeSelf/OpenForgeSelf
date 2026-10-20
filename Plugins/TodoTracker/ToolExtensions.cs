using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.TodoTracker.Models;
using ForgeSelf.Api.Plugins.TodoTracker.Services;

namespace ForgeSelf.Api.Plugins.TodoTracker;

/// <summary>
/// 老三工具（create/list/complete_todo）。
///
/// DI 解析范式（2026-10-17 修复批 PILOT-062，取代原 <c>TodoToolProvider</c> 构造期 eager 解析）：
/// 构造函数只存原始上下文（插件 ctx），宿主容器一律在 <c>ExecuteAsync</c> 执行期经
/// <see cref="ForgeSelf.Abstractions.PluginContextDiExtensions.CreateHostScope"/> 即时解析——
/// 对 Apply/seed 时序免疫；未就绪时报明确错误并被 catch 转成错误封套。
/// 架构守卫：<c>ForgeSelf.Api.Tests/Architecture/PluginToolConstructorDiGuardTests</c>。
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
        // 构造期零解析：只存原始上下文，执行期才解析宿主容器（PILOT-062）
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

            // 执行期即时解析宿主容器（对 Apply/seed 时序免疫）；未就绪的明确报错由 catch 转错误封套
            using var scope = _serviceProvider.CreateHostScope();
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
            await this.RecordUsageAsync(scope.ServiceProvider, "create_todo", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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
            await this.RecordUsageAsync(_serviceProvider, "create_todo", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
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
        // 构造期零解析：只存原始上下文，执行期才解析宿主容器（PILOT-062）
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

            // 执行期即时解析宿主容器（对 Apply/seed 时序免疫）；未就绪的明确报错由 catch 转错误封套
            using var scope = _serviceProvider.CreateHostScope();
            var todoService = scope.ServiceProvider.GetService<ITodoService>();
            if (todoService == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "ITodoService 未注册" });
            }

            var result = await todoService.GetTodosAsync(status, page, pageSize);

            stopwatch.Stop();
            await this.RecordUsageAsync(scope.ServiceProvider, "list_todos", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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
            await this.RecordUsageAsync(_serviceProvider, "list_todos", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
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
        // 构造期零解析：只存原始上下文，执行期才解析宿主容器（PILOT-062）
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

            // 执行期即时解析宿主容器（对 Apply/seed 时序免疫）；未就绪的明确报错由 catch 转错误封套
            using var scope = _serviceProvider.CreateHostScope();
            var todoService = scope.ServiceProvider.GetService<ITodoService>();
            if (todoService == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "ITodoService 未注册" });
            }

            var updated = await todoService.CompleteTodoAsync(id);
            if (updated == null)
            {
                stopwatch.Stop();
                await this.RecordUsageAsync(_serviceProvider, "complete_todo", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
                {
                    ["todoId"] = id,
                    ["notFound"] = true
                });
                return JsonSerializer.Serialize(new { success = false, error = $"待办 {id} 不存在" });
            }

            stopwatch.Stop();
            await this.RecordUsageAsync(scope.ServiceProvider, "complete_todo", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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
            await this.RecordUsageAsync(_serviceProvider, "complete_todo", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

}

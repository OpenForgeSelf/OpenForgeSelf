using System.Diagnostics;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.TodoTracker.Models;
using ForgeSelf.Api.Plugins.TodoTracker.Services;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.TodoTracker;

/// <summary>
/// agent 侧工具函数（PILOT-054 新增 5 个：<c>get_agent_task</c> / <c>claim_agent_task</c> /
/// <c>append_task_execution</c> / <c>update_task_stage</c> / <c>dispatch_task</c>）。
///
/// 为什么不与既有 3 个工具同写一处：既有 <c>create_todo</c>/<c>list_todos</c>/<c>complete_todo</c>
/// 各自内联了 scope 解析与用量上报（<c>ToolExtensions.cs</c>），本次不做无关重构（规范 §1.5），
/// 新增的一律走本文件基类 —— 同一件事不留三种写法，改动只在增量上收敛。
///
/// 解析回落沿用 <see cref="TodoToolProvider"/>：插件 <c>Apply</c> 拿到的是插件上下文，
/// 直接 <c>CreateScope()</c> 会抛「No service for type 'IServiceScopeFactory'」，须先回落宿主根容器。
/// </summary>
public abstract class TodoAgentToolBase : IToolFunctionExtension
{
    private readonly IServiceProvider? _services;

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PluginId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ParametersJsonSchema { get; set; } = string.Empty;

    protected TodoAgentToolBase(IServiceProvider? services) => _services = TodoToolProvider.Resolve(services);

    /// <summary>工具执行体：作用域由基类负责创建与释放（不得把 scoped 服务存成字段跨请求用）。</summary>
    public abstract Task<string> ExecuteAsync(string parameters);

    /// <summary>在插件作用域内跑一段逻辑，异常统一转成 <c>{success:false,error}</c> 封套。</summary>
    protected async Task<string> RunAsync(Func<IServiceProvider, Task<string>> body, string? errorPrefix = null)
    {
        if (_services == null) return Fail("服务提供者未初始化");

        var watch = Stopwatch.StartNew();
        string result;
        string? error = null;
        try
        {
            using var scope = _services.CreateScope();
            result = await body(scope.ServiceProvider);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            XTrace.Log.Warn("[todo-tracker] {0} 执行失败: {1}", Name, ex.Message);
            result = Fail($"{(errorPrefix == null ? string.Empty : errorPrefix + "：")}{ex.Message}");
        }

        watch.Stop();
        await ReportUsageAsync(watch.ElapsedMilliseconds, error);
        return result;
    }

    /// <summary>用量上报（与既有 3 个工具同一入口 <c>ToolFunctionUsageReportingExtensions</c>）。</summary>
    protected Task ReportUsageAsync(long elapsedMs, string? error)
    {
        var payload = new Dictionary<string, object>();
        if (error != null) payload["error"] = error;
        return this.RecordUsageAsync(_services, Name, elapsedMs, payload);
    }

    /// <summary>解析参数 JSON（坏格式返回空字典，不抛出 —— 由各工具自己给"缺哪个参数"的人话）。</summary>
    protected static Dictionary<string, JsonElement> ParseArgs(string? parameters)
    {
        var args = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(parameters)) return args;

        try
        {
            using var doc = JsonDocument.Parse(parameters);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return args;
            foreach (var prop in doc.RootElement.EnumerateObject()) args[prop.Name] = prop.Value.Clone();
        }
        catch (JsonException)
        {
            // 参数不是合法 JSON：按"没给参数"处理，让必填校验去报准确原因
        }

        return args;
    }

    protected static string? ArgString(IReadOnlyDictionary<string, JsonElement> args, string key)
    {
        if (!args.TryGetValue(key, out var el)) return null;
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.GetRawText(),
            _ => null
        };
    }

    protected static int ArgInt(IReadOnlyDictionary<string, JsonElement> args, string key, int fallback = 0)
    {
        if (!args.TryGetValue(key, out var el)) return fallback;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n)) return n;
        if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var p)) return p;
        return fallback;
    }

    protected static bool ArgBool(IReadOnlyDictionary<string, JsonElement> args, string key, bool fallback = false)
    {
        if (!args.TryGetValue(key, out var el)) return fallback;
        return el.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(el.GetString(), out var b) ? b : fallback,
            _ => fallback
        };
    }

    /// <summary>
    /// 取「改了哪些文件」：数组形状直接反序列化成结构化列表，字符串形状交给服务端归一
    /// （<see cref="FileChangeList"/>）。两种都收，是因为工具调用方常常用后者省事。
    /// </summary>
    protected static void ApplyFilesChanged(IReadOnlyDictionary<string, JsonElement> args, CreateTaskExecutionRequest request)
    {
        if (!args.TryGetValue("filesChanged", out var el)) return;

        if (el.ValueKind == JsonValueKind.Array)
        {
            try
            {
                request.FilesChanged = el.Deserialize<List<ChangedFileDto>>();
                return;
            }
            catch (JsonException)
            {
                // 数组里形状不对：退化成原文本，信息不丢
            }
        }

        if (el.ValueKind == JsonValueKind.String) request.FilesChangedText = el.GetString();
    }

    protected static string Ok(object? data) => JsonSerializer.Serialize(new { success = true, data });

    protected static string Fail(string error) => JsonSerializer.Serialize(new { success = false, error });

    /// <summary>服务缺失的统一答复（不静默返回 null 让调用方以为"没有任务"）。</summary>
    protected static string MissingService(string typeName) => Fail($"{typeName} 未注册");
}

/// <summary><c>get_agent_task</c>：按 taskKey 读完整下发内容（含提示词与已有记录条数）。</summary>
public class GetAgentTaskToolFunction : TodoAgentToolBase
{
    public GetAgentTaskToolFunction(IServiceProvider? services) : base(services) { }

    public override Task<string> ExecuteAsync(string parameters)
    {
        var args = ParseArgs(parameters);
        var taskKey = ArgString(args, "taskKey");
        if (string.IsNullOrWhiteSpace(taskKey)) return Task.FromResult(Fail("参数 taskKey 不能为空"));

        return RunAsync(async sp =>
        {
            var todos = sp.GetService<ITodoService>();
            var dispatch = sp.GetService<ITodoDispatchService>();
            var records = sp.GetService<ITaskExecutionService>();
            if (todos == null) return MissingService(nameof(ITodoService));
            if (dispatch == null) return MissingService(nameof(ITodoDispatchService));
            if (records == null) return MissingService(nameof(ITaskExecutionService));

            var task = await todos.GetTodoByKeyAsync(taskKey);
            if (task == null) return Fail($"任务不存在：{taskKey}");

            var preview = await dispatch.PreviewByKeyAsync(taskKey, null);
            var history = await records.ListAsync(task.Id, 1, 100);

            return Ok(new
            {
                task,
                promptMarkdown = preview?.PromptMarkdown,
                canDispatch = preview?.CanDispatch,
                missing = preview?.Missing,
                warnings = preview?.Warnings,
                recordTotal = history.Total,
                records = history.Items
            });
        });
    }
}

/// <summary><c>claim_agent_task</c>：领取下一条已下发任务（原子置 Running 并留痕）。</summary>
public class ClaimAgentTaskToolFunction : TodoAgentToolBase
{
    public ClaimAgentTaskToolFunction(IServiceProvider? services) : base(services) { }

    public override Task<string> ExecuteAsync(string parameters)
    {
        var args = ParseArgs(parameters);
        var assignee = ArgString(args, "assignee");
        var projectId = ArgInt(args, "projectId");

        return RunAsync(async sp =>
        {
            var dispatch = sp.GetService<ITodoDispatchService>();
            if (dispatch == null) return MissingService(nameof(ITodoDispatchService));

            var result = await dispatch.ClaimNextAsync(assignee, projectId, Actor(args));
            if (!result.Ok) return Fail(result.Error ?? "领取失败");
            if (result.Data == null) return Ok(new { claimed = false, task = (object?)null, note = "当前没有可领取的任务（阶段=Dispatched）" });

            return Ok(new { claimed = true, task = result.Data, note = $"已领取并置为执行中，做完请用 append_task_execution 回报（taskKey={result.Data.TaskKey}）" });
        });
    }

    private static string Actor(IReadOnlyDictionary<string, JsonElement> args) =>
        ArgString(args, "actor") is { Length: > 0 } a ? a : "agent:claim";
}

/// <summary><c>append_task_execution</c>：追加一条执行记录（可同批流转状态）。</summary>
public class AppendTaskExecutionToolFunction : TodoAgentToolBase
{
    public AppendTaskExecutionToolFunction(IServiceProvider? services) : base(services) { }

    public override Task<string> ExecuteAsync(string parameters)
    {
        var args = ParseArgs(parameters);
        var taskKey = ArgString(args, "taskKey");
        if (string.IsNullOrWhiteSpace(taskKey)) return Task.FromResult(Fail("参数 taskKey 不能为空"));

        var request = new CreateTaskExecutionRequest
        {
            Actor = ArgString(args, "actor") ?? "agent:append_task_execution",
            Action = ArgString(args, "action"),
            Result = ArgString(args, "result"),
            Detail = ArgString(args, "detail"),
            Verification = ArgString(args, "verification"),
            Risks = ArgString(args, "risks"),
            Residuals = ArgString(args, "residuals"),
            Evidence = ArgString(args, "evidence"),
            NextStep = ArgString(args, "nextStep"),
            BlockReason = ArgString(args, "blockReason"),
            StageTo = ArgString(args, "stageTo"),
            ElapsedMs = args.ContainsKey("elapsedMs") ? ArgInt(args, "elapsedMs") : null
        };
        ApplyFilesChanged(args, request);

        return RunAsync(async sp =>
        {
            var todos = sp.GetService<ITodoService>();
            var records = sp.GetService<ITaskExecutionService>();
            if (todos == null) return MissingService(nameof(ITodoService));
            if (records == null) return MissingService(nameof(ITaskExecutionService));

            var task = await todos.GetTodoByKeyAsync(taskKey);
            if (task == null) return Fail($"任务不存在：{taskKey}");

            var result = await records.AppendAsync(task.Id, request, request.Actor);
            if (!result.Ok) return Fail(result.Error ?? "写入执行记录失败");

            return Ok(new { task = result.Data, note = "执行记录已追加（append-only，不可改写）" });
        });
    }
}

/// <summary><c>update_task_stage</c>：流转阶段（非法流转返回可达目标清单）。</summary>
public class UpdateTaskStageToolFunction : TodoAgentToolBase
{
    public UpdateTaskStageToolFunction(IServiceProvider? services) : base(services) { }

    public override Task<string> ExecuteAsync(string parameters)
    {
        var args = ParseArgs(parameters);
        var taskKey = ArgString(args, "taskKey");
        var stage = ArgString(args, "stage");
        if (string.IsNullOrWhiteSpace(taskKey)) return Task.FromResult(Fail("参数 taskKey 不能为空"));
        if (string.IsNullOrWhiteSpace(stage)) return Task.FromResult(Fail("参数 stage 不能为空"));

        var request = new StageChangeRequest
        {
            Stage = stage,
            Reason = ArgString(args, "reason"),
            BlockReason = ArgString(args, "blockReason")
        };

        return RunAsync(async sp =>
        {
            var todos = sp.GetService<ITodoService>();
            if (todos == null) return MissingService(nameof(ITodoService));

            var actor = ArgString(args, "actor") ?? "agent:update_task_stage";
            var result = await todos.ChangeStageByKeyAsync(taskKey, request, actor);
            if (!result.Ok) return Fail(result.Error ?? "状态流转失败");

            return Ok(result.Data);
        });
    }
}

/// <summary><c>dispatch_task</c>：下发（校验必填 → Dispatched → 留痕），并回一段可直接用的提示词。</summary>
public class DispatchTaskToolFunction : TodoAgentToolBase
{
    public DispatchTaskToolFunction(IServiceProvider? services) : base(services) { }

    public override Task<string> ExecuteAsync(string parameters)
    {
        var args = ParseArgs(parameters);
        var taskKey = ArgString(args, "taskKey");
        if (string.IsNullOrWhiteSpace(taskKey)) return Task.FromResult(Fail("参数 taskKey 不能为空"));
        var assignee = ArgString(args, "assignee");

        return RunAsync(async sp =>
        {
            var todos = sp.GetService<ITodoService>();
            var dispatch = sp.GetService<ITodoDispatchService>();
            if (todos == null) return MissingService(nameof(ITodoService));
            if (dispatch == null) return MissingService(nameof(ITodoDispatchService));

            var task = await todos.GetTodoByKeyAsync(taskKey);
            if (task == null) return Fail($"任务不存在：{taskKey}");

            var actor = ArgString(args, "actor") ?? "agent:dispatch_task";
            var result = await dispatch.DispatchAsync(task.Id, assignee, actor);
            if (!result.Ok) return Fail(result.Error ?? "下发失败");

            var preview = await dispatch.PreviewByKeyAsync(taskKey, null);
            return Ok(new { task = result.Data, promptMarkdown = preview?.PromptMarkdown });
        });
    }
}

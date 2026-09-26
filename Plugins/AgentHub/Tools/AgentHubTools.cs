using System.Text.Json;
using ForgeSelf.Api.Plugins.AgentHub.Models;
using ForgeSelf.Api.Plugins.AgentHub.Services;
using Microsoft.Extensions.DependencyInjection;
using NewLife;
using TaskStatus = ForgeSelf.Api.Plugins.AgentHub.Services.TaskStatus;

namespace ForgeSelf.Api.Plugins.AgentHub.Tools;

/// <summary>列出已登记的其它 agent（含能力矩阵与健康状态），用于选路决策。</summary>
public class ListAgentsTool : AgentHubToolBase
{
    /// <inheritdoc />
    public override String Id => "agent-hub.list_agents";

    /// <inheritdoc />
    public override String Name => "list_agents";

    /// <inheritdoc />
    public override String Description =>
        "列出本机已登记并可委派任务的其他 AI agent（如 codex / opencode / claude / qodercli），" +
        "返回其擅长标签、能力矩阵、健康状态与授信范围。委派前先用本工具确认有哪些可用目标。";

    /// <inheritdoc />
    public override String ParametersJsonSchema => """
{
    "type": "object",
    "properties": {
        "facet": {
            "type": "string",
            "description": "按能力面过滤，取值 F1_Driving(可驱动执行) / F2_Sessions(可续接会话) / F3_Transcripts(可读历史) / F4_Configure(可改配置) / F5_HealthUsage(可看用量) / F6_Policy(可调权限)"
        },
        "tag": {
            "type": "string",
            "description": "按擅长标签模糊过滤，例如 frontend / code-review / refactor"
        },
        "enabledOnly": {
            "type": "boolean",
            "description": "是否只返回启用的 agent，默认 true",
            "default": true
        }
    }
}
""";

    /// <summary>构造</summary>
    /// <param name="pluginId">插件标识</param>
    /// <param name="services">DI 容器</param>
    public ListAgentsTool(String pluginId, IServiceProvider? services) : base(pluginId, services) { }

    /// <inheritdoc />
    public override Task<String> ExecuteAsync(String parameters)
    {
        var args = ParseArgs(parameters);
        var facet = ArgString(args, "facet");
        var tag = ArgString(args, "tag");
        var enabledOnly = ArgBool(args, "enabledOnly") ?? true;

        var registry = GetService<IAgentRegistry>();
        if (registry == null) return Task.FromResult(Fail("AgentHub 注册表不可用（插件未正确加载）"));

        var agents = (facet.IsNullOrEmpty() && tag.IsNullOrEmpty())
            ? (IReadOnlyList<AgentDto>)registry.List(enabledOnly)
            : registry.SelectCandidates(facet, tag);

        if (enabledOnly && facet.IsNullOrEmpty() && tag.IsNullOrEmpty())
        {
            agents = agents.Where(a => a.Enabled).ToList();
        }

        var data = agents.Select(a => new
        {
            a.Id,
            a.Name,
            a.Vendor,
            a.DisplayName,
            a.Kind,
            a.Enabled,
            a.Health,
            a.Priority,
            a.Tags,
            a.Trusted,
            a.TrustedScopes,
            a.DefaultCwd,
            permissionMode = a.Policy.PermissionMode,
            accessPoints = a.AccessPoints.Select(p => new
            {
                p.Id,
                p.Transport,
                p.Mode,
                p.Executable,
                p.Health,
                p.LastVersion,
                p.IsDefault,
                p.CancelSupported
            }),
            capabilities = a.Capabilities.Facets.ToDictionary(kv => kv.Key, kv => kv.Value)
        }).ToList();

        var summary = agents.Count == 0
            ? "没有可用的 agent。请在「Agent 中枢」页面扫描本机并登记，或检查 agent 是否已启用。"
            : $"找到 {agents.Count} 个可用 agent。" + string.Join("；",
                agents.Select(a => $"{a.Name}({a.Vendor}, 健康={a.Health}, 优先级={a.Priority})"));

        return Task.FromResult(Ok(true, summary, data));
    }
}

/// <summary>把任务委派给指定 agent（或按能力自动选路），同步等待结果。</summary>
public class DelegateTaskTool : AgentHubToolBase
{
    /// <inheritdoc />
    public override String Id => "agent-hub.delegate_task";

    /// <inheritdoc />
    public override String Name => "delegate_task";

    /// <inheritdoc />
    public override String Description =>
        "把一个子任务委派给本机其他 AI agent 执行，并返回它的最终结果与产物摘要。" +
        "适用场景：需要另一个 agent 的专长（如特定代码库经验），或想并行推进。" +
        "默认以只读权限运行；若它需要写文件/执行命令，会进入人工审批。委派有副作用，请勿滥用。";

    /// <inheritdoc />
    public override String ParametersJsonSchema => """
{
    "type": "object",
    "properties": {
        "prompt": {
            "type": "string",
            "description": "交给目标 agent 的完整任务描述（要自包含：背景、目标、验收标准、约束）"
        },
        "agentId": {
            "type": "integer",
            "description": "目标 agent 的 Id（来自 list_agents）。不填则按 facet/tag 自动选路"
        },
        "facet": {
            "type": "string",
            "description": "未指定 agentId 时，按能力面选路，如 F1_Driving"
        },
        "tag": {
            "type": "string",
            "description": "未指定 agentId 时，按擅长标签选路"
        },
        "cwd": {
            "type": "string",
            "description": "让目标 agent 工作的目录（须在该 agent 的允许白名单内；不填用其默认目录）"
        },
        "permissionMode": {
            "type": "string",
            "enum": ["read-only", "workspace-write", "accept-edits"],
            "description": "权限模式，默认继承该 agent 的策略（通常 read-only）"
        },
        "sessionRef": {
            "type": "string",
            "description": "续接某个已有外部会话（来自上次委派返回的 sessionRef）"
        }
    },
    "required": ["prompt"]
}
""";

    /// <summary>构造</summary>
    /// <param name="pluginId">插件标识</param>
    /// <param name="services">DI 容器</param>
    public DelegateTaskTool(String pluginId, IServiceProvider? services) : base(pluginId, services) { }

    /// <inheritdoc />
    public override async Task<String> ExecuteAsync(String parameters)
    {
        var args = ParseArgs(parameters);
        var prompt = ArgString(args, "prompt");

        if (prompt.IsNullOrWhiteSpace()) return Fail("prompt 不能为空");

        var runtime = GetService<DelegationRuntime>();
        if (runtime == null) return Fail("AgentHub 运行时不可用（插件未正确加载）");

        var request = new DelegationRequest
        {
            Prompt = prompt!,
            AgentId = ArgInt(args, "agentId"),
            Cwd = ArgString(args, "cwd"),
            PermissionMode = ArgString(args, "permissionMode"),
            SessionRef = ArgString(args, "sessionRef"),
            Facet = ArgString(args, "facet"),
            Tag = ArgString(args, "tag"),
            CreatedBy = "agent:delegate_task"
        };

        // 工具调用必须同步拿结果；超时由 agent 策略控制
        var task = await runtime.DelegateAsync(request);

        if (task.Status != TaskStatus.Succeeded)
        {
            return Ok(false, $"委派任务 {task.TaskKey} 未成功（{task.Status}）：{task.Message ?? task.ErrorCode}", new
            {
                task.TaskKey,
                task.Status,
                task.ErrorCode,
                task.ExitCode,
                task.ElapsedMs,
                task.ResultText
            });
        }

        return Ok(true, $"委派任务 {task.TaskKey} 执行成功（耗时 {task.ElapsedMs}ms）", new
        {
            task.TaskKey,
            task.Status,
            task.AgentId,
            task.AgentName,
            task.ExitCode,
            task.ElapsedMs,
            task.SessionRef,
            task.ResultText,
            artifacts = task.ArtifactsJson
        });
    }
}

/// <summary>查询某个委派任务的状态与结果（不阻塞）。</summary>
public class GetTaskResultTool : AgentHubToolBase
{
    /// <inheritdoc />
    public override String Id => "agent-hub.get_task_result";

    /// <inheritdoc />
    public override String Name => "get_task_result";

    /// <inheritdoc />
    public override String Description =>
        "按任务标识查询一次委派任务的状态、结果文本与产物。用于查看异步委派（UI 发起）的进展。";

    /// <inheritdoc />
    public override String ParametersJsonSchema => """
{
    "type": "object",
    "properties": {
        "taskKey": {
            "type": "string",
            "description": "委派任务的对外标识（GUID），来自 delegate_task 返回值或任务列表"
        },
        "includeEvents": {
            "type": "boolean",
            "description": "是否附带事件流（默认 false；事件可能较多）",
            "default": false
        }
    },
    "required": ["taskKey"]
}
""";

    /// <summary>构造</summary>
    /// <param name="pluginId">插件标识</param>
    /// <param name="services">DI 容器</param>
    public GetTaskResultTool(String pluginId, IServiceProvider? services) : base(pluginId, services) { }

    /// <inheritdoc />
    public override Task<String> ExecuteAsync(String parameters)
    {
        var args = ParseArgs(parameters);
        var key = ArgString(args, "taskKey");
        if (key.IsNullOrEmpty()) return Task.FromResult(Fail("taskKey 不能为空"));

        var runtime = GetService<DelegationRuntime>();
        if (runtime == null) return Task.FromResult(Fail("AgentHub 运行时不可用"));

        var task = runtime.GetByKey(key!);
        if (task == null) return Task.FromResult(Fail($"任务 {key} 不存在"));

        Object data = task;
        if (ArgBool(args, "includeEvents") == true)
        {
            var events = DelegationRuntime.ListEvents(task.Id)
                .Select(e => new { e.Seq, e.Type, e.PayloadJson, e.Timestamp })
                .ToList();
            data = new { task, events };
        }

        return Task.FromResult(Ok(true, $"任务 {key} 当前状态：{task.Status}", data));
    }
}

/// <summary>取消一个正在执行的委派任务。</summary>
public class CancelTaskTool : AgentHubToolBase
{
    /// <inheritdoc />
    public override String Id => "agent-hub.cancel_task";

    /// <inheritdoc />
    public override String Name => "cancel_task";

    /// <inheritdoc />
    public override String Description =>
        "取消一个尚未结束的委派任务（会终止外部 agent 进程树）。用于任务跑偏或不再需要时止损。";

    /// <inheritdoc />
    public override String ParametersJsonSchema => """
{
    "type": "object",
    "properties": {
        "taskKey": {
            "type": "string",
            "description": "要取消的任务标识（GUID）"
        }
    },
    "required": ["taskKey"]
}
""";

    /// <summary>构造</summary>
    /// <param name="pluginId">插件标识</param>
    /// <param name="services">DI 容器</param>
    public CancelTaskTool(String pluginId, IServiceProvider? services) : base(pluginId, services) { }

    /// <inheritdoc />
    public override async Task<String> ExecuteAsync(String parameters)
    {
        var args = ParseArgs(parameters);
        var key = ArgString(args, "taskKey");
        if (key.IsNullOrEmpty()) return Fail("taskKey 不能为空");

        var runtime = GetService<DelegationRuntime>();
        if (runtime == null) return Fail("AgentHub 运行时不可用");

        var task = runtime.GetByKey(key!);
        if (task == null) return Fail($"任务 {key} 不存在");

        if (TaskStatus.IsTerminal(task.Status))
            return Ok(true, $"任务 {key} 已是终态（{task.Status}），无需取消");

        var cancelled = await runtime.CancelAsync(task.Id);
        return Ok(cancelled, cancelled ? $"已请求取消任务 {key}" : $"取消任务 {key} 失败（可能已结束）");
    }
}

/// <summary>向某个 agent 就其已有会话追问一句（续接会话）。</summary>
public class AskAgentTool : AgentHubToolBase
{
    /// <inheritdoc />
    public override String Id => "agent-hub.ask_agent";

    /// <inheritdoc />
    public override String Name => "ask_agent";

    /// <inheritdoc />
    public override String Description =>
        "就某个 agent 的**已有会话**继续追问，复用它的上下文（不重新开始）。" +
        "适合先 delegate_task 让它做事，再多次 ask_agent 追问细节。" +
        "目标 agent 必须支持会话续接（能力矩阵 F2_Sessions 为 true）。";

    /// <inheritdoc />
    public override String ParametersJsonSchema => """
{
    "type": "object",
    "properties": {
        "sessionRef": {
            "type": "string",
            "description": "要续接的外部会话 id（来自之前委派结果里的 sessionRef）"
        },
        "question": {
            "type": "string",
            "description": "追问内容"
        },
        "agentId": {
            "type": "integer",
            "description": "目标 agent 的 Id（必须与 sessionRef 所属 agent 一致）"
        }
    },
    "required": ["sessionRef", "question", "agentId"]
}
""";

    /// <summary>构造</summary>
    /// <param name="pluginId">插件标识</param>
    /// <param name="services">DI 容器</param>
    public AskAgentTool(String pluginId, IServiceProvider? services) : base(pluginId, services) { }

    /// <inheritdoc />
    public override async Task<String> ExecuteAsync(String parameters)
    {
        var args = ParseArgs(parameters);
        var sessionRef = ArgString(args, "sessionRef");
        var question = ArgString(args, "question");
        var agentId = ArgInt(args, "agentId");

        if (sessionRef.IsNullOrEmpty()) return Fail("sessionRef 不能为空");
        if (question.IsNullOrWhiteSpace()) return Fail("question 不能为空");
        if (agentId is null or <= 0) return Fail("agentId 不能为空");

        var registry = GetService<IAgentRegistry>();
        var runtime = GetService<DelegationRuntime>();
        if (registry == null || runtime == null) return Fail("AgentHub 服务不可用");

        var agent = registry.Get(agentId.Value);
        if (agent == null) return Fail($"agent #{agentId} 不存在");

        // 能力矩阵门禁：不支持会话续接的 agent 直接拒绝，别让调用方白等
        if (!agent.Capabilities.Supports(AgentFacets.Sessions))
            return Fail($"agent「{agent.Name}」不支持会话续接（F2_Sessions = false），无法按 sessionRef 追问");

        var task = await runtime.DelegateAsync(new DelegationRequest
        {
            Prompt = question!,
            AgentId = agentId.Value,
            SessionRef = sessionRef,
            CreatedBy = "agent:ask_agent"
        });

        if (task.Status != TaskStatus.Succeeded)
            return Ok(false, $"追问失败（{task.Status}）：{task.Message ?? task.ErrorCode}", new { task.TaskKey, task.ResultText });

        return Ok(true, "追问完成", new
        {
            task.TaskKey,
            task.SessionRef,
            task.ResultText,
            task.ElapsedMs
        });
    }
}

using System.Text.Json.Serialization;

namespace ForgeSelf.Api.Plugins.AIAgent.Models;

/// <summary>统一 Agent 能力入口的请求与事件模型（/api/agent）。</summary>
public static class AgentSurfaceKeys
{
    public const string Default = "default";
}

public sealed class AgentEmbedContext
{
    public string PluginId { get; set; } = string.Empty;
    public string? SurfaceKey { get; set; }
    public string? ProjectId { get; set; }
    public string? Cwd { get; set; }
    public string? ObjectType { get; set; }
    public string? ObjectId { get; set; }
    public string? Title { get; set; }
    /// <summary>仅展示与提示注入，不扩大工具权限。</summary>
    public Dictionary<string, string>? Extras { get; set; }

    public string ResolvedSurfaceKey() =>
        string.IsNullOrWhiteSpace(SurfaceKey) ? AgentSurfaceKeys.Default : SurfaceKey.Trim();
}

public sealed class AgentToolScope
{
    public List<string>? AllowTools { get; set; }
    public List<string>? DenyTools { get; set; }
    /// <summary>前端字段 allow，等同 allowTools。</summary>
    public List<string>? Allow { get; set; }
    /// <summary>前端字段 deny，等同 denyTools。</summary>
    public List<string>? Deny { get; set; }
    public List<string>? AllowCapabilities { get; set; }

    public List<string>? ResolvedAllow() =>
        AllowTools is { Count: > 0 } ? AllowTools : Allow;

    public List<string>? ResolvedDeny() =>
        DenyTools is { Count: > 0 } ? DenyTools : Deny;
}

public sealed class AgentEmbedOptions
{
    public string? Surface { get; set; }
    /// <summary>可写在 embed 根上，缺省时回填到 context。</summary>
    public string? PluginId { get; set; }
    /// <summary>可写在 embed 根上，缺省时回填到 context。</summary>
    public string? SurfaceKey { get; set; }
    public AgentEmbedContext? Context { get; set; }
    public AgentToolScope? ToolScope { get; set; }

    public AgentEmbedContext ResolvedContext()
    {
        var ctx = Context ?? new AgentEmbedContext();
        if (string.IsNullOrWhiteSpace(ctx.PluginId) && !string.IsNullOrWhiteSpace(PluginId))
            ctx.PluginId = PluginId.Trim();
        if (string.IsNullOrWhiteSpace(ctx.SurfaceKey) && !string.IsNullOrWhiteSpace(SurfaceKey))
            ctx.SurfaceKey = SurfaceKey.Trim();
        return ctx;
    }
}

public sealed class CreateAgentSessionRequest
{
    public string Mode { get; set; } = "open";
    public string? AgentId { get; set; }
    public string? ModelId { get; set; }
    public string? Title { get; set; }
    public AgentEmbedOptions? Embed { get; set; }
}

public sealed class PatchAgentSessionRequest
{
    public string? Mode { get; set; }
    public string? AgentId { get; set; }
    public string? ModelId { get; set; }
    public string? Title { get; set; }
}

public sealed class PostAgentMessageRequest
{
    public string? Text { get; set; }
    public List<AgentAttachment>? Attachments { get; set; }
    public AgentEmbedOptions? Embed { get; set; }
}

public sealed class AgentAttachment
{
    public string Kind { get; set; } = string.Empty;
    public string? Id { get; set; }
    public string? Name { get; set; }
}

public sealed class RevisePlanRequest
{
    public string? Text { get; set; }
    /// <summary>前端修订说明字段，等同 text。</summary>
    public string? Note { get; set; }
    public List<AgentPlanStepDto>? Steps { get; set; }

    public string? ResolvedText() =>
        !string.IsNullOrWhiteSpace(Text) ? Text : Note;
}

public sealed class DecideApprovalRequest
{
    public bool Approved { get; set; }
    public string? Note { get; set; }
}

public sealed class InterveneRunRequest
{
    /// <summary>continue / skip / override / retry / cancel</summary>
    public string Action { get; set; } = string.Empty;
    public string? Input { get; set; }
    public string? StepId { get; set; }
    public string? Output { get; set; }
}

public sealed class WorkspaceWritebackRequest
{
    public string? Content { get; set; }
    public string? Title { get; set; }
}

public sealed class AgentPlanDto
{
    public string Goal { get; set; } = string.Empty;
    public List<AgentPlanStepDto> Steps { get; set; } = new();
    public List<string>? Constraints { get; set; }
    /// <summary>计划卡说明（规划失败回退时=「规划失败，已用回退计划」；正常计划为空。随 plan.proposed 载荷透传前端）。</summary>
    public string? Note { get; set; }
}

public sealed class AgentPlanStepDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    // 前端计划卡片读 title/detail，与 name/objective 对齐。
    public string Title
    {
        get => string.IsNullOrWhiteSpace(_title) ? Name : _title!;
        set => _title = value;
    }
    public string Detail
    {
        get => string.IsNullOrWhiteSpace(_detail) ? Objective : _detail!;
        set => _detail = value;
    }
    private string? _title;
    private string? _detail;
}

public sealed class AgentEventDto
{
    public string Type { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string? RunId { get; set; }
    public long Seq { get; set; }
    public DateTimeOffset Ts { get; set; }
    public object? Payload { get; set; }
}

public sealed class AgentSessionSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Mode { get; set; } = "open";
    public string Status { get; set; } = "idle";
    public string PluginId { get; set; } = string.Empty;
    public string SurfaceKey { get; set; } = AgentSurfaceKeys.Default;
    public string? AgentId { get; set; }
    public string? ModelId { get; set; }
    public string? Surface { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class AgentMessageDto
{
    public string Id { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AgentFacadeRunDto
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? Turn { get; set; }
    public int? TurnMax { get; set; }
    public AgentPlanDto? Plan { get; set; }
    public string? StuckReason { get; set; }
    public string? FailureReason { get; set; }
    public List<string>? AllowedActions { get; set; }
}

public sealed class AgentWorkspaceStatusDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    /// <summary>前端目录项字段，与 Status 同值。</summary>
    public string StatusText
    {
        get => Status;
        set => Status = value;
    }
}

public sealed class AgentSessionDetailDto
{
    public AgentSessionSummaryDto Session { get; set; } = new();
    public List<AgentMessageDto> Messages { get; set; } = new();
    public AgentFacadeRunDto? CurrentRun { get; set; }
    public List<AgentWorkspaceStatusDto> Workspace { get; set; } = new();
}

public sealed class AcceptedRunResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
}

public sealed class CatalogModelDto
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
}

public sealed class CatalogAgentDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public static class AgentToolScopeRules
{
    /// <summary>在白名单上收缩范围：allow 取交集，deny 优先剔除；不能放宽。</summary>
    public static List<string> Narrow(IEnumerable<string>? whitelist, AgentToolScope? scope)
    {
        var set = new HashSet<string>(whitelist ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var allowList = scope?.ResolvedAllow();
        if (allowList is { Count: > 0 })
        {
            var allow = new HashSet<string>(allowList.Where(s => !string.IsNullOrWhiteSpace(s)), StringComparer.OrdinalIgnoreCase);
            if (set.Count == 0)
                set = allow;
            else
                set.IntersectWith(allow);
        }
        var denyList = scope?.ResolvedDeny();
        if (denyList is { Count: > 0 })
            set.ExceptWith(denyList.Where(s => !string.IsNullOrWhiteSpace(s)));
        return set.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }
}

public sealed class AgentWorkspaceTodoDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("goal")] public string? Goal { get; set; }
    [JsonPropertyName("scope")] public string? Scope { get; set; }
    [JsonPropertyName("acceptance")] public string? Acceptance { get; set; }
    [JsonPropertyName("dispatched")] public bool Dispatched { get; set; }
    [JsonPropertyName("assignee")] public string? Assignee { get; set; }
    [JsonPropertyName("at")] public DateTimeOffset? At { get; set; }
}

public sealed class AgentWorkspaceWorkflowStepDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; set; } = "pending";
    [JsonPropertyName("risk")] public string? Risk { get; set; }
}

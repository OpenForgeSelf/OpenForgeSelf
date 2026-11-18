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
    public List<string>? AllowCapabilities { get; set; }
}

public sealed class AgentEmbedOptions
{
    public string? Surface { get; set; }
    public AgentEmbedContext? Context { get; set; }
    public AgentToolScope? ToolScope { get; set; }
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
    public List<AgentPlanStepDto>? Steps { get; set; }
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
}

public sealed class AgentPlanStepDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
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
    public List<string>? AllowedActions { get; set; }
}

public sealed class AgentWorkspaceStatusDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
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
        if (scope?.AllowTools is { Count: > 0 })
        {
            var allow = new HashSet<string>(scope.AllowTools.Where(s => !string.IsNullOrWhiteSpace(s)), StringComparer.OrdinalIgnoreCase);
            if (set.Count == 0)
                set = allow;
            else
                set.IntersectWith(allow);
        }
        if (scope?.DenyTools is { Count: > 0 })
            set.ExceptWith(scope.DenyTools.Where(s => !string.IsNullOrWhiteSpace(s)));
        return set.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }
}

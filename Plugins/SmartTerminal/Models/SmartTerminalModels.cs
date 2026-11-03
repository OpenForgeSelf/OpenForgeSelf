namespace ForgeSelf.Api.Plugins.SmartTerminal.Models;

/// <summary>
/// 权限模式（对齐 VS Code Agent Approvals 的分级审批 + Open Interpreter approval policy 分层）。
/// 默认 <see cref="AskWrite"/>：只读类自动放行，写/副作用询问，硬拒绝仍拦。
/// </summary>
public enum PermissionMode
{
    /// <summary>自动执行，仅硬 denylist 拒绝。</summary>
    Auto = 0,
    /// <summary>只读自动；写/变更/副作用需询问；高危同样询问；硬 denylist 拒绝。</summary>
    AskWrite = 1,
    /// <summary>多数自动；仅高危询问；硬 denylist 拒绝。</summary>
    AskHighRisk = 2
}

/// <summary>命令风险分级（分类器输出）。</summary>
public enum CommandRiskClass
{
    Read = 0,
    Write = 1,
    HighRisk = 2,
    Deny = 3
}

/// <summary>审批闸门决策。</summary>
public enum ApprovalDecision
{
    Allow = 0,
    Ask = 1,
    Deny = 2
}

public sealed class ShellProfileDto
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Executable { get; init; } = string.Empty;
    public string ArgumentsTemplate { get; init; } = "{command}";
    public string Kind { get; init; } = "unknown"; // cmd | powershell | pwsh | bash | zsh | sh | fish | wt-profile | other
    public bool Available { get; init; }
    public string? Source { get; init; }
    public string? Note { get; init; }
}

public sealed class PermissionClassifyResult
{
    public CommandRiskClass Risk { get; init; }
    public ApprovalDecision Decision { get; init; }
    public string MatchedRule { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

public sealed class TerminalSessionDto
{
    public string SessionId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string ShellId { get; init; } = string.Empty;
    public string ShellDisplayName { get; init; } = string.Empty;
    public string Cwd { get; init; } = string.Empty;
    public PermissionMode PermissionMode { get; init; } = PermissionMode.AskWrite;
    public string Status { get; init; } = "idle"; // idle | running | awaiting_approval | closed
    public string? AgentId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public int CommandCount { get; init; }
}

public sealed class TerminalCommandRecordDto
{
    public string CommandId { get; init; } = string.Empty;
    public string SessionId { get; init; } = string.Empty;
    public string CommandRedacted { get; init; } = string.Empty;
    public CommandRiskClass Risk { get; init; }
    public ApprovalDecision Decision { get; init; }
    public string MatchedRule { get; init; } = string.Empty;
    public string Status { get; init; } = "pending"; // pending | approved | denied | running | completed | failed | cancelled
    public int? ExitCode { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
    public string? ApprovalId { get; init; }
}

public sealed class TerminalOutputChunkDto
{
    public int Index { get; init; }
    public string Stream { get; init; } = "stdout"; // stdout | stderr | system
    public string Text { get; init; } = string.Empty;
    public DateTimeOffset At { get; init; }
}

public sealed class PendingApprovalDto
{
    public string ApprovalId { get; init; } = string.Empty;
    public string SessionId { get; init; } = string.Empty;
    public string CommandId { get; init; } = string.Empty;
    public string CommandRedacted { get; init; } = string.Empty;
    public CommandRiskClass Risk { get; init; }
    public string MatchedRule { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public PermissionMode Mode { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class AuditEventDto
{
    public string EventId { get; init; } = string.Empty;
    public DateTimeOffset Time { get; init; }
    public string Kind { get; init; } = string.Empty;
    public string? SessionId { get; init; }
    public string? AgentId { get; init; }
    public string? ShellId { get; init; }
    public string? Cwd { get; init; }
    public string? CommandRedacted { get; init; }
    public string? Decision { get; init; }
    public string? MatchedRule { get; init; }
    public int? ExitCode { get; init; }
    public string? Detail { get; init; }
}

public sealed class CreateSessionRequest
{
    public string? Title { get; set; }
    public string? ShellId { get; set; }
    public string? Cwd { get; set; }
    public PermissionMode? PermissionMode { get; set; }
    public string? AgentId { get; set; }
}

public sealed class RunCommandRequest
{
    public string Command { get; set; } = string.Empty;
    public bool? AutoApproveIfAllowed { get; set; } = true;
}

public sealed class SetPermissionModeRequest
{
    public PermissionMode Mode { get; set; } = PermissionMode.AskWrite;
}

public sealed class ApprovalActionRequest
{
    public bool Approve { get; set; }
    public string? Note { get; set; }
}

public sealed class RunCommandResponse
{
    public string SessionId { get; init; } = string.Empty;
    public string CommandId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? ApprovalId { get; init; }
    public PermissionClassifyResult? Classify { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class OutputSnapshotDto
{
    public string SessionId { get; init; } = string.Empty;
    public string? ActiveCommandId { get; init; }
    public string Status { get; init; } = "idle";
    public IReadOnlyList<TerminalOutputChunkDto> Chunks { get; init; } = Array.Empty<TerminalOutputChunkDto>();
    public int NextIndex { get; init; }
}

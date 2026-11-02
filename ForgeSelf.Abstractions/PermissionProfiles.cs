namespace ForgeSelf.Abstractions;

/// <summary>B4 权限可配置化契约（按 Agent/工具 allow/ask/deny + 白名单），由 ToolRegistry pre-execute 闸门统一消费。</summary>
/// <remarks>
/// 决策匹配序：agentId 精确 profile（Enabled）→ 全局 profile（AgentId=空）→ 内置默认策略。
/// 内置默认策略 = HighRiskToolPolicy 等价语义的 registry 层沉默（高风险审批由 MAF 侧
/// ApprovalRequiredAIFunction 包装承担）——零配置行为不变（兼容性红线）。
/// 架构铁律 2：本契约是宿主唯一权限决策面；插件本地校验（BrowserGuard 等）保留作纵深防御，
/// 不构成平行 PermissionBroker。
/// </remarks>
public enum PermissionEffect
{
    /// <summary>放行（配置了白名单时须全部命中）。</summary>
    Allow = 0,

    /// <summary>转人工审批（走 IApprovalService，fail-closed）。</summary>
    Ask = 1,

    /// <summary>拒绝（最终结果 ToolOutcome.Denied）。</summary>
    Deny = 2,
}

/// <summary>单工具权限规则（profile 内按 ToolName 精确匹配，"*" 为兜底规则）。</summary>
public sealed class ToolPermissionRule
{
    /// <summary>工具名（OrdinalIgnoreCase；"*" = 兜底规则）。工具可后装，不做存在性校验。</summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>三态效果。</summary>
    public PermissionEffect Effect { get; set; } = PermissionEffect.Allow;

    /// <summary>文件类工具（write/move/rename/delete_file）允许的路径前缀（大小写不敏感）；空 = 不启用。</summary>
    public string[] PathAllowlist { get; set; } = [];

    /// <summary>浏览器类工具（browser_open/navigate/download）允许的域名（含子域）；空 = 不启用。</summary>
    public string[] DomainAllowlist { get; set; } = [];

    /// <summary>终端类工具（run_terminal_command）允许的命令前缀（OrdinalIgnoreCase）；空 = 不启用。</summary>
    public string[] CommandAllowlist { get; set; } = [];
}

/// <summary>权限策略 profile（HTTP camelCase 投影源；AgentId 空 = 全局默认）。</summary>
public sealed class PermissionProfileDto
{
    /// <summary>持久化 id；新建请求传 0。</summary>
    public long Id { get; set; }

    /// <summary>目标 Agent id（如 agent.generalist）；空 = 全局默认 profile。</summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>禁用的 profile 不参与决策（保留配置）。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>工具规则集（保存为全量替换语义）。</summary>
    public IReadOnlyList<ToolPermissionRule> Rules { get; set; } = [];

    /// <summary>备注（给人看）。</summary>
    public string? Remark { get; set; }

    /// <summary>最后更新时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>策略决策结果（审计与 pre-execute 消费的统一投影）。</summary>
public sealed class PermissionDecision
{
    /// <summary>三态效果。</summary>
    public PermissionEffect Effect { get; init; } = PermissionEffect.Allow;

    /// <summary>决策理由（审计给人看；Ask 时作为审批原因）。</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>决策来源："profile:&lt;id&gt;" 或 "default-policy"。</summary>
    public string Source { get; init; } = "default-policy";
}

/// <summary>权限策略服务：决策 + CRUD + 审计查询 + 会话到 Agent 记忆。</summary>
public interface IPermissionProfileService
{
    /// <summary>决策一次工具调用（02-spec §4 语义）。实现绝不抛异常（fail-open：异常视为 Allow 并记日志）。</summary>
    Task<PermissionDecision> DecideAsync(string? sessionId, string toolName, string argsJson, CancellationToken ct = default);

    /// <summary>记忆 sessionId 到 agentId（决策按 Agent 匹配用；由请求入口与 Run 编排调用）。</summary>
    void RememberSessionAgent(string sessionId, string? agentId);

    /// <summary>该会话（按记忆的 Agent，含全局兜底）是否配置了此工具的显式规则——MAF 侧包装让位判断（防双问）。</summary>
    Task<bool> HasRuleForToolAsync(string? sessionId, string toolName, CancellationToken ct = default);

    /// <summary>列出全部 profiles（含禁用）。</summary>
    Task<IReadOnlyList<PermissionProfileDto>> ListAsync(CancellationToken ct = default);

    /// <summary>保存 profile（id=0 新建；AgentId 唯一，冲突返回 null，控制器映射 409）。</summary>
    Task<PermissionProfileDto?> SaveAsync(PermissionProfileDto profile, CancellationToken ct = default);

    /// <summary>删除 profile；不存在返回 false。</summary>
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);

    /// <summary>审计联合查询（策略决策日志 + ToolApproval），Time 倒序分页。</summary>
    Task<(IReadOnlyList<PermissionAuditDto> Items, int Total)> QueryAuditAsync(PermissionAuditQuery query, CancellationToken ct = default);
}

/// <summary>审计查询入参。</summary>
public sealed class PermissionAuditQuery
{
    /// <summary>按会话过滤（可空）。</summary>
    public string? SessionId { get; init; }

    /// <summary>按工具名过滤（可空）。</summary>
    public string? ToolName { get; init; }

    /// <summary>"policy-decision" 或 "tool-approval"；null = 全部。</summary>
    public string? Kind { get; init; }

    /// <summary>时间下界（含）。</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>时间上界（含）。</summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>页码（1 起）。</summary>
    public int Page { get; init; } = 1;

    /// <summary>页大小（默认 50）。</summary>
    public int PageSize { get; init; } = 50;
}

/// <summary>审计条目（策略决策日志与 ToolApproval 的联合投影；camelCase）。</summary>
public sealed class PermissionAuditDto
{
    /// <summary>来源记录 id（决策日志行 id 或 ToolApproval id）。</summary>
    public long Id { get; init; }

    /// <summary>"policy-decision" 或 "tool-approval"。</summary>
    public string Kind { get; init; } = "policy-decision";

    /// <summary>发生时间（决策时间或审批创建时间）。</summary>
    public DateTimeOffset Time { get; init; }

    /// <summary>会话 id。</summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>决策命中的 agentId（决策日志有；审批记录为空）。</summary>
    public string AgentId { get; init; } = string.Empty;

    /// <summary>工具名。</summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>参数哈希（SHA256，审计幂等）。</summary>
    public string ArgsHash { get; init; } = string.Empty;

    /// <summary>决策 Allow/Ask/Deny；审批 Approved/Denied/Expired/Pending/Cancelled。</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>决策来源（profile:id 或 default-policy）或审批风险等级。</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>决策理由或审批说明加 Note。</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>关联审批 id（Ask 决策产生审批时回填）。</summary>
    public long? ApprovalId { get; init; }
}

namespace ForgeSelf.Abstractions;

/// <summary>
/// 工具审批服务（B8/A1 Ask 落点 + P0 MAF ApprovalRequiredAIFunction 持久化）。
/// pre-execute 返回 <c>PreToolDecision.Ask</c> 时，管线调用 <see cref="AskAsync"/> 征询人工批准；
/// MAF 泵在收到 <c>ToolApprovalRequestContent</c> 时调用 <see cref="CreatePendingAsync"/> 落盘并挂起。
/// </summary>
/// <remarks>
/// 红线 4：<see cref="AskAsync"/> <b>必须</b>携带 <paramref name="ct"/> 并自带超时，超时/取消/无响应一律返回 false
/// （fail-closed），绝不挂死 agent loop。
/// </remarks>
public interface IApprovalService
{
    /// <summary>
    /// 征询审批（阻塞等待决策）：返回 true = 放行；false = 拒绝（含超时/取消/服务不可用，fail-closed）。
    /// </summary>
    Task<bool> AskAsync(string sessionId, string toolName, string argsJson, string? reason, CancellationToken ct);

    /// <summary>创建 Pending 审批记录（非阻塞；供 MAF 泵 / HTTP 列表）。</summary>
    Task<ApprovalDto> CreatePendingAsync(ApprovalCreateRequest request, CancellationToken ct = default);

    /// <summary>按会话列出审批记录（含终态）。</summary>
    Task<IReadOnlyList<ApprovalDto>> ListBySessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>人工决策；超时已 Expired 的记录拒绝改写（fail-closed）。</summary>
    Task<ApprovalDto?> DecideAsync(long id, bool approved, string? note, CancellationToken ct = default);

    /// <summary>
    /// 取出本会话已决策、尚未喂回 MAF 的续跑条目（Approved/Denied），标记为已消费。
    /// </summary>
    Task<IReadOnlyList<ApprovalContinuation>> ConsumeContinuationsAsync(string sessionId, CancellationToken ct = default);
}

/// <summary>审批状态（fail-closed：超时→Expired，未决不得执行）。</summary>
public enum ApprovalStatus
{
    Pending = 0,
    Approved = 1,
    Denied = 2,
    Expired = 3,
    Cancelled = 4,
}

/// <summary>创建待审请求的入参。</summary>
public sealed class ApprovalCreateRequest
{
    public string SessionId { get; init; } = string.Empty;
    public string ToolName { get; init; } = string.Empty;
    public string ArgsJson { get; init; } = "{}";
    public string? Description { get; init; }
    public string RiskLevel { get; init; } = HighRiskToolPolicy.RiskHigh;
    /// <summary>MEAI <c>ToolApprovalRequestContent.RequestId</c>（续跑关联）。</summary>
    public string? MeaiRequestId { get; init; }
    /// <summary>原始 <c>FunctionCallContent.CallId</c>。</summary>
    public string? CallId { get; init; }
    public TimeSpan? Timeout { get; init; }
}

/// <summary>审批记录 DTO（HTTP camelCase 投影源）。</summary>
public sealed class ApprovalDto
{
    public long Id { get; init; }
    public string SessionId { get; init; } = string.Empty;
    public string ToolName { get; init; } = string.Empty;
    public string ArgsJson { get; init; } = "{}";
    public string ArgsHash { get; init; } = string.Empty;
    public string RiskLevel { get; init; } = HighRiskToolPolicy.RiskHigh;
    public string Description { get; init; } = string.Empty;
    public string Status { get; init; } = nameof(ApprovalStatus.Pending);
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public string? Note { get; init; }
    public string? MeaiRequestId { get; init; }
    public string? CallId { get; init; }
}

/// <summary>已决策、待 MAF 以 <c>ToolApprovalResponseContent</c> 续跑的条目。</summary>
public sealed class ApprovalContinuation
{
    public long Id { get; init; }
    public string SessionId { get; init; } = string.Empty;
    public string ToolName { get; init; } = string.Empty;
    public string ArgsJson { get; init; } = "{}";
    public bool Approved { get; init; }
    public string? Note { get; init; }
    public string MeaiRequestId { get; init; } = string.Empty;
    public string CallId { get; init; } = string.Empty;
}

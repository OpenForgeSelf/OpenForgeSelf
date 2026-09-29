namespace ForgeSelf.Abstractions;

/// <summary>
/// B8（042 工具管线）：六闸门管线的契约类型。
/// 管线：<c>tools/pre-execute → 单调守卫 → tools/execute → tools/post-execute → finalize → tools/result</c>。
/// </summary>
/// <remarks>
/// <para>
/// 判据（架构师 §2.7）：
/// A1 决策三态 <see cref="PreToolDecision"/>（Ask 无审批服务 fail-closed → Denied）；
/// A2 单调守卫只减不增（无 allow 结果）；
/// A3 tools/execute / tools/post-execute 走 waterfall；
/// A4 model-ordered commit（未执行 call 合成 Skipped，N call 必有 N result）；
/// A5 StreamChunk 携带 Usage/FinishReason/ToolCall。
/// </para>
/// </remarks>

/// <summary>
/// <c>tools/pre-execute</c> 三态决策。无监听器（SerialAsync 返回 null）= Allow。
/// </summary>
public abstract record PreToolDecision
{
    /// <summary>放行本次工具调用。</summary>
    public sealed record Allow : PreToolDecision;

    /// <summary>拒绝本次工具调用（携带理由，最终结果 <see cref="ToolOutcome.Denied"/>）。</summary>
    public sealed record Deny(string Reason) : PreToolDecision;

    /// <summary>需要人工审批（<see cref="Reason"/> 可空）。无审批服务或审批超时/取消 → fail-closed 拒绝。</summary>
    public sealed record Ask(string? Reason) : PreToolDecision;
}

/// <summary>
/// <c>tools/post-execute</c> 决策：对已产出的工具结果做接受/改写/阻断。
/// 无监听器（WaterfallAsync fallback 返回 null）= 原样接受。
/// </summary>
public abstract record PostToolDecision
{
    /// <summary>接受并把结果文本改写为 <paramref name="Content"/>（结果仍算成功）。</summary>
    public sealed record AcceptContent(string Content) : PostToolDecision;

    /// <summary>接受并把结果 JSON 改写为 <paramref name="ValueJson"/>。</summary>
    public sealed record AcceptValue(string ValueJson) : PostToolDecision;

    /// <summary>阻断本次结果：结果转为失败，<paramref name="Feedback"/> 作为错误信息回给模型。</summary>
    public sealed record Block(string Feedback) : PostToolDecision;
}

/// <summary>
/// 工具执行上下文：六闸门管线的贯穿 payload（可变类，中间件可在调用 next() 前替换
/// <see cref="Signal"/> 实现超时注入——门禁 5）。
/// </summary>
public sealed class ToolExecution
{
    /// <summary>模型返回的调用 ID（旧单工具入口为空串）。</summary>
    public required string CallId { get; init; }

    /// <summary>工具名。</summary>
    public required string ToolName { get; init; }

    /// <summary>工具参数 JSON。</summary>
    public required string ArgsJson { get; init; }

    /// <summary>发起会话 ID（旧单工具入口为空串）。</summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// execute 视图的取消信号：默认 = 调用方 ct；tools/execute 中间件<b>可替换</b>（如换上带超时的链接令牌），
    /// 不可移除（红线 1：fallback 以 <c>execution.Signal</c> 调用工具体）。
    /// </summary>
    public CancellationToken Signal { get; set; }

    /// <summary>执行耗时（毫秒），由管线回填。</summary>
    public long DurationMs { get; set; }

    /// <summary>闸门 1 的决策（回填，供观察者审计）。</summary>
    public PreToolDecision? PreDecision { get; set; }

    // ---- 执行视图（闸门 3 → 4 之间回填，供 post-execute 中间件读取） ----

    /// <summary>工具体是否成功。</summary>
    public bool Success { get; set; }

    /// <summary>工具体原始结果 JSON。</summary>
    public string ResultJson { get; set; } = string.Empty;

    /// <summary>工具体错误信息。</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// <c>tools/result</c> 广播载荷：最终结果的<b>冻结快照</b>（与主流程结果对象分离，
/// 监听器改字段无法影响主流程——门禁 9）。广播走 <c>EmitAsync</c>，观测失败被隔离。
/// </summary>
public sealed class ToolResultAnnouncement
{
    /// <summary>调用 ID。</summary>
    public string CallId { get; init; } = string.Empty;

    /// <summary>工具名。</summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>发起会话 ID。</summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>最终是否成功（可变仅供监听器读取后篡改无效——快照与主流程分离）。</summary>
    public bool Success { get; set; }

    /// <summary>最终结果 JSON。</summary>
    public string ResultJson { get; set; } = string.Empty;

    /// <summary>最终错误信息。</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>最终结果定性。</summary>
    public ToolOutcome Outcome { get; set; }

    /// <summary>总耗时（毫秒）。</summary>
    public long DurationMs { get; set; }
}

/// <summary>
/// 流式工具调用增量（B8/A5，<see cref="ILlmRuntime"/> 面）：首块带 <see cref="Id"/> 视为新调用，
/// 后续块向末位追加参数（与 <see cref="UnifiedToolCall"/> 的流式聚合约定一致）。
/// </summary>
public sealed record ToolCallDelta(string? Id, string? Name, string Arguments);

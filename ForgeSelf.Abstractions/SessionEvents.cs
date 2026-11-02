namespace ForgeSelf.Abstractions;

/// <summary>
/// 会话事件基类（append-only 日志的事实单元）。
/// B1（040）将原先「Type 字符串 + Payload 字符串」的裸结构升级为可辨识联合：
/// 编译期靠 record 层级穷举，运行期靠 <see cref="SessionEventMap"/> 强制注册。
/// </summary>
/// <remarks>
/// 约定：Id 全局单调递增；Timestamp 由存储实现在缺省时补齐；子类必须覆写 <see cref="Type"/>。
/// 新增事件三步走：① 定义 record；② 注册进 SessionEventMap；③ 在 DeriveMessages 加 case。
/// </remarks>
public abstract record SessionEvent(long Id, string SessionId, DateTimeOffset Timestamp)
{
    /// <summary>事件类型名，持久化用（如 <c>user/message</c>）。子类必须覆写。</summary>
    public abstract string Type { get; }
}

// ---- 消息类（模型可见） ----

/// <summary>系统提示消息。</summary>
public sealed record SystemMessageEvent(long Id, string SessionId, DateTimeOffset Timestamp,
    string Content) : SessionEvent(Id, SessionId, Timestamp)
{ public override string Type => "system/message"; }

/// <summary>用户消息，带来源标记（Web/API/插件/系统）。</summary>
public sealed record UserMessageEvent(long Id, string SessionId, DateTimeOffset Timestamp,
    string Content, MessageSource Source) : SessionEvent(Id, SessionId, Timestamp)
{ public override string Type => "user/message"; }

/// <summary>助手消息：模型正式产出，可携带工具调用与用量。</summary>
public sealed record AssistantMessageEvent(long Id, string SessionId, DateTimeOffset Timestamp,
    string Content, IReadOnlyList<ToolCallRef>? ToolCalls,
    UsageInfo? Usage, string FinishReason) : SessionEvent(Id, SessionId, Timestamp)
{ public override string Type => "assistant/message"; }

/// <summary>失败/重试/取消的助手产出；不入模型历史，仅供轨迹与诊断。</summary>
public sealed record AssistantAttemptEvent(long Id, string SessionId, DateTimeOffset Timestamp,
    string? Content, string ErrorKind, string ModelId) : SessionEvent(Id, SessionId, Timestamp)
{ public override string Type => "assistant/attempt"; }

// ---- 工具类 ----

/// <summary>模型发起的工具调用请求。</summary>
public sealed record ToolCallEvent(long Id, string SessionId, DateTimeOffset Timestamp,
    string CallId, string ToolName, string ArgsJson) : SessionEvent(Id, SessionId, Timestamp)
{ public override string Type => "tool/call"; }

/// <summary>工具执行结果；四态之一，保证「N 个 call 必有 N 个 result」日志闭合。</summary>
public sealed record ToolResultEvent(long Id, string SessionId, DateTimeOffset Timestamp,
    string CallId, string ToolName, string ResultJson, ToolOutcome Outcome, long DurationMs) : SessionEvent(Id, SessionId, Timestamp)
{ public override string Type => "tool/result"; }

/// <summary>
/// 工具审批请求（P0 / MAF）：<c>ToolApprovalRequestContent</c> 落盘事实。
/// 对模型不可见；挂起回合等待人工决策后以 <c>ToolApprovalResponseContent</c> 续跑。
/// </summary>
public sealed record ToolApprovalRequestEvent(long Id, string SessionId, DateTimeOffset Timestamp,
    long ApprovalId, string MeaiRequestId, string CallId, string ToolName, string ArgsJson, string RiskLevel)
    : SessionEvent(Id, SessionId, Timestamp)
{ public override string Type => "tool/approval-request"; }

// ---- 结构类（turn/step 由 041 正式启用；040 先注册占位，避免二次破坏性变更） ----

/// <summary>回合开始。</summary>
public sealed record TurnStartEvent(long Id, string SessionId, DateTimeOffset Timestamp,
    string TurnId) : SessionEvent(Id, SessionId, Timestamp)
{ public override string Type => "turn/start"; }

/// <summary>回合结束，带结束原因。</summary>
public sealed record TurnEndEvent(long Id, string SessionId, DateTimeOffset Timestamp,
    string TurnId, TurnEndReason Reason) : SessionEvent(Id, SessionId, Timestamp)
{ public override string Type => "turn/end"; }

/// <summary>步骤开始。</summary>
public sealed record StepStartEvent(long Id, string SessionId, DateTimeOffset Timestamp,
    string TurnId, string StepId, int StepIndex) : SessionEvent(Id, SessionId, Timestamp)
{ public override string Type => "step/start"; }

/// <summary>步骤结束，带结束原因。</summary>
public sealed record StepEndEvent(long Id, string SessionId, DateTimeOffset Timestamp,
    string TurnId, string StepId, StepEndReason Reason) : SessionEvent(Id, SessionId, Timestamp)
{ public override string Type => "step/end"; }

// ---- Run 结果侧（B2 · 2026-11-02 maf-runtime-hardening）：Verify 闸门与结果收据轨迹 ----

/// <summary>
/// Run 级 Verify 阶段轨迹（B2②）：Execute 完成后出口验收（exitCriteria）逐条校验结论落盘。
/// 对模型不可见（纯轨迹，与 tool/approval-request 同类）；载荷即 <see cref="RunVerifyItem"/> 序列。
/// </summary>
public sealed record RunVerifyEvent(long Id, string SessionId, DateTimeOffset Timestamp,
    long RunId, string Status, IReadOnlyList<RunVerifyItem> Items) : SessionEvent(Id, SessionId, Timestamp)
{ public override string Type => "run/verify"; }

/// <summary>
/// Run 结果收据冻结事件（B2③）：回合/Run 结束时聚合「改了哪些文件/跑了什么验证/证据计数」，
/// 以 append-only 事件冻结（开放任务 Run 的收据载体；计划任务 Run 另落 AgentRun.ResultReceipt 列）。
/// 对模型不可见。
/// </summary>
public sealed record RunReceiptEvent(long Id, string SessionId, DateTimeOffset Timestamp,
    long RunId, string ReceiptJson) : SessionEvent(Id, SessionId, Timestamp)
{ public override string Type => "run/receipt"; }

// ---- 请求信封（不变量 2：请求是日志的纯函数） ----

/// <summary>请求头：模型 ID、已渲染的系统提示、工具 schema 引用。</summary>
public sealed record RequestHeaderEvent(long Id, string SessionId, DateTimeOffset Timestamp,
    string ModelId, string RenderedSystemPrompt, IReadOnlyList<string> ToolSchemaRefs) : SessionEvent(Id, SessionId, Timestamp)
{ public override string Type => "request/header"; }

/// <summary>请求上下文：工作区指令、时间上下文、引用；对模型可见但独立成事件便于审计。</summary>
public sealed record RequestContextEvent(long Id, string SessionId, DateTimeOffset Timestamp,
    string? WorkspaceInstructions, string? TimeContext, IReadOnlyList<string>? References) : SessionEvent(Id, SessionId, Timestamp)
{ public override string Type => "request/context"; }

// ---- 收件箱投影 ----

/// <summary>收件箱拼接：followup/steer/inject 落地后的投影事件。</summary>
public sealed record InboxSplicedEvent(long Id, string SessionId, DateTimeOffset Timestamp,
    InboxTarget Target, string Op, IReadOnlyList<InboxItem> Items) : SessionEvent(Id, SessionId, Timestamp)
{ public override string Type => "agent/inbox/spliced"; }

// ---- 支撑枚举 / 值对象 ----

/// <summary>消息来源。</summary>
public enum MessageSource { WebUi, Api, Plugin, System }

/// <summary>工具执行结果四态；<c>Skipped</c> 用于未执行调用的合成结果，保证日志闭合。</summary>
public enum ToolOutcome { Ok, Error, Denied, Skipped }

/// <summary>回合结束原因。</summary>
public enum TurnEndReason { Completed, MaxTokens, Aborted, NoInput, Suspended }

/// <summary>
/// <see cref="TurnEndReason.Suspended"/>（B7 统一循环新增）：
/// 回合<b>挂起</b>——request 级无进展超时（上游不可达/冷启动阻塞）等可恢复暂停。
/// 挂起<b>不是</b>失败：日志事实保留现场（不回滚、不确认收件箱输入），
/// 后续 steer/followup 唤醒新回合即可继续（029 的 Stuck 语义映射到状态机层）。
/// </summary>

/// <summary>步骤结束原因。</summary>
public enum StepEndReason { Completed, MaxTokens, Aborted, ToolCallPending, Failed, Suspended, ExitTool }

/// <summary>收件箱投递目标：下一回合或下一步。</summary>
public enum InboxTarget { NextTurn, NextStep }

/// <summary>工具调用引用（模型返回的调用描述）。</summary>
public sealed record ToolCallRef(string CallId, string ToolName, string ArgsJson);

/// <summary>用量信息。</summary>
public sealed record UsageInfo(long PromptTokens, long CompletionTokens, long? CachedTokens);

/// <summary>收件箱条目。</summary>
public sealed record InboxItem(string MessageId, string Content, MessageSource Source);

// ---- Run 结果侧契约（B2②③ · 2026-11-02 maf-runtime-hardening）----
// 跨宿主/插件边界的验收与收据形状（架构 §5.4：契约进 Abstractions）。

/// <summary>
/// Run 出口验收判据（B2②）：验收标准文本 + 断言类型。
/// 断言类型 V1（确定性，零 LLM）：<c>file_exists</c> / <c>output_contains</c> / <c>files_changed_not_empty</c>；
/// 未知类型按失败处理（fail-safe，不静默放行）。
/// </summary>
/// <remarks>
/// <see cref="Value"/> 是断言字面量（匹配串/文件路径），缺省回退 <see cref="Text"/>。B2② 走查实证
/// （2026-11-02）：output_contains 拿整句描述文本当匹配串必不过（「产出不包含「产出包含「验收通过」」」），
/// 故字面量与人类描述分离；裸词判据（Value 缺省）保持直用 Text。
/// </remarks>
public sealed record RunExitCriterion(string Text, string Type, string? Value = null);

/// <summary>Verify 阶段状态（AgentRun.VerifyStatus 持久值 + run/verify 事件 Status 词表）。</summary>
public enum RunVerifyStatus
{
    /// <summary>未配置 exitCriteria（现状行为，无 Verify 阶段）。</summary>
    None = 0,

    /// <summary>已配置，待验证（Run 未到收束点，或中途 Stuck/Failed 保留现场）。</summary>
    Pending = 1,

    /// <summary>全部判据通过。</summary>
    Passed = 2,

    /// <summary>存在未通过判据（Run 不得置 Completed）。</summary>
    Failed = 3
}

/// <summary>单条判据的校验结论。</summary>
public sealed record RunVerifyItem(string Text, string Type, bool Passed, string Detail);

/// <summary>Verify 校验输入（事实投影：产出摘要 + 本次改动的文件清单）。</summary>
public sealed record RunVerifyInputs(string SummaryText, IReadOnlyList<string> FilesChanged);

/// <summary>Verify 校验结论（整体状态 + 逐条明细）。</summary>
public sealed record RunVerifyOutcome(RunVerifyStatus Status, IReadOnlyList<RunVerifyItem> Items);

/// <summary>结果收据里的单文件变更（Path = 绝对或相对工作区路径；Change = A/M/D）。</summary>
public sealed record RunReceiptFile(string Path, string Change);

/// <summary>
/// Run 结果收据（B2③，冻结不可变）：改了哪些文件 / 跑了什么验证 / 证据在哪。
/// 计划任务 Run = AgentRun.ResultReceipt 列（终态一次写入）；开放任务回合 = run/receipt 事件（append-only 天然不可变）。
/// </summary>
public sealed class RunResultReceipt
{
    /// <summary>Run id（计划任务 = AgentRun.Id；开放任务回合 = 0，以 sessionId 定位日志）。</summary>
    public long RunId { get; set; }

    /// <summary>冻结时的状态词表（Completed/Stuck/Failed/Cancelled）。</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>结果摘要（终局合成摘要 / task_complete 声明产出 / 最终答复首段）。</summary>
    public string? Summary { get; set; }

    /// <summary>本次改动的文件清单（从工具轨迹聚合，同路径去重）。</summary>
    public List<RunReceiptFile> FilesChanged { get; set; } = [];

    /// <summary>出口验收结论（未配置 = 空数组）。</summary>
    public List<RunVerifyItem> Verifications { get; set; } = [];

    /// <summary>证据引用（文件清单之外的证据锚点，如命令输出/截图路径；V1 为空占位）。</summary>
    public List<string> Evidence { get; set; } = [];

    /// <summary>工具调用总数（含出口工具声明）。</summary>
    public int ToolCallCount { get; set; }

    /// <summary>工具成功数（Outcome=Ok）。</summary>
    public int ToolOkCount { get; set; }

    /// <summary>冻结时间。</summary>
    public DateTimeOffset FrozenAt { get; set; }
}

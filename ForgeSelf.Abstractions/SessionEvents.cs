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

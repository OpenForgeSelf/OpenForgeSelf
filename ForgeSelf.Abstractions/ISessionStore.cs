namespace ForgeSelf.Abstractions;

/// <summary>
/// 会话存储接缝：仅追加事件日志 + 模型可见历史投影（对标 dsh session store）。
/// </summary>
/// <remarks>
/// 不变量 1（Model-visible means logged）：一切进入模型的消息必须先落本日志，再从日志派生；
/// 但「已记录」不必然「对模型可见」——结构/轨迹类事件落盘而不投影。
/// 写入唯一入口是 <see cref="Append"/>，实现必须在首行调用 <see cref="SessionEventMap.EnsureKnown"/>。
/// </remarks>
public interface ISessionStore
{
    /// <summary>唯一写路径。实现须先做 <see cref="SessionEventMap.EnsureKnown"/> 校验，再分配 Id。</summary>
    long Append(string sessionId, SessionEvent evt);

    /// <summary>按 Id 升序返回该会话的全部事件。</summary>
    IReadOnlyList<SessionEvent> Replay(string sessionId);

    /// <summary>
    /// 模型可见历史投影：仅 system / user / assistant / tool 四类；
    /// attempt / 结构 / context 事件被排除。遇到未覆盖的事件类型必须抛异常（漏投影在开发期炸出）。
    /// </summary>
    IReadOnlyList<Message> DeriveMessages(string sessionId);

    /// <summary>fork：基于 beforeEventId 开新会话，复制前缀事件，返回新会话 ID。</summary>
    string Fork(string sourceSessionId, long beforeEventId, string newSessionId);

    /// <summary>事件流订阅（供投影同步与 UI 推送）。</summary>
    IObservable<SessionEvent> Observe(string sessionId);
}

namespace OpenForgeSelf.Abstractions;

/// <summary>
/// 会话事件（仅追加日志，对标 dsh SessionEvent）。
/// 每个事件代表会话中发生的一件可追溯事实。
/// </summary>
public sealed class SessionEvent
{
    /// <summary>全局自增事件 ID。</summary>
    public long Id { get; init; }

    /// <summary>所属会话 ID。</summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>事件类型（如 user / assistant / tool / system）。</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>事件载荷（可序列化 JSON 字符串）。</summary>
    public string Payload { get; init; } = string.Empty;

    /// <summary>事件发生时间。</summary>
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// 会话存储接缝：仅追加事件日志 + 模型可见历史投影。
/// 实现应保证「模型可见 = 已记录」，即 <see cref="DeriveMessages"/> 能重建全部模型可见历史。
/// </summary>
public interface ISessionStore
{
    /// <summary>追加一条会话事件，返回分配的事件 ID。</summary>
    long Append(string sessionId, SessionEvent evt);

    /// <summary>按追加顺序重放某会话的全部事件。</summary>
    IReadOnlyList<SessionEvent> Replay(string sessionId);

    /// <summary>将事件日志投影为模型可见的消息历史（供 LLM 消费）。</summary>
    IReadOnlyList<Message> DeriveMessages(string sessionId);
}

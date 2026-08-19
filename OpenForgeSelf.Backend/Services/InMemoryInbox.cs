using System.Collections.Concurrent;
using OpenForgeSelf.Abstractions;

namespace OpenForgeSelf.Backend.Services;

/// <summary>
/// 内存版收件箱实现（P4 会话/LLM 接缝接线）。
/// 三种输入语义（followup / steer / inject）的统一入口，消息存储在内存中。
/// 当前为骨架实现，消息仅存储不处理，后续可接入 Agent Loop 或消息队列。
/// </summary>
public sealed class InMemoryInbox : IInbox
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<InboxMessage>> _messages = new();

    /// <summary>获取指定会话的所有消息（按入队顺序）。</summary>
    public IReadOnlyList<InboxMessage> GetMessages(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        return _messages.TryGetValue(sessionId, out var queue)
            ? queue.ToArray()
            : Array.Empty<InboxMessage>();
    }

    /// <summary>清空指定会话的所有消息。</summary>
    public void Clear(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        _messages.TryRemove(sessionId, out _);
    }

    /// <inheritdoc />
    public void Followup(string sessionId, string message)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(message);

        Enqueue(sessionId, new InboxMessage
        {
            Kind = InboxMessageKind.Followup,
            SessionId = sessionId,
            Text = message,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <inheritdoc />
    public void Steer(string sessionId, string input)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(input);

        Enqueue(sessionId, new InboxMessage
        {
            Kind = InboxMessageKind.Steer,
            SessionId = sessionId,
            Text = input,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <inheritdoc />
    public void Inject(string sessionId, object contextBlock)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(contextBlock);

        Enqueue(sessionId, new InboxMessage
        {
            Kind = InboxMessageKind.Inject,
            SessionId = sessionId,
            ContextBlock = contextBlock,
            Timestamp = DateTime.UtcNow
        });
    }

    private void Enqueue(string sessionId, InboxMessage message)
    {
        var queue = _messages.GetOrAdd(sessionId, _ => new ConcurrentQueue<InboxMessage>());
        queue.Enqueue(message);
    }
}

/// <summary>收件箱消息。</summary>
public sealed class InboxMessage
{
    /// <summary>消息种类。</summary>
    public InboxMessageKind Kind { get; init; }

    /// <summary>会话 ID。</summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>文本内容（Followup/Steer 时使用）。</summary>
    public string? Text { get; init; }

    /// <summary>结构化上下文（Inject 时使用）。</summary>
    public object? ContextBlock { get; init; }

    /// <summary>入队时间（UTC）。</summary>
    public DateTime Timestamp { get; init; }
}

/// <summary>收件箱消息种类。</summary>
public enum InboxMessageKind
{
    /// <summary>续聊：用户对当前会话追加一条普通消息。</summary>
    Followup,

    /// <summary>转向：用户给出即时纠偏/指令。</summary>
    Steer,

    /// <summary>注入：外部向会话上下文注入一块结构化上下文。</summary>
    Inject
}

using System.Collections.Concurrent;
using ForgeSelf.Abstractions;
using Message = ForgeSelf.Abstractions.Message;

namespace ForgeSelf.Api.Services;

/// <summary>
/// 内存版会话存储（P4 会话接缝实现）：仅追加事件日志 + 模型可见历史投影。
/// 满足「模型可见 = 已记录」：DeriveMessages 从追加日志重建全部模型可见历史。
/// 生产可替换为持久化实现（同一 ISessionStore 接缝，替换零改动）。
/// </summary>
public sealed class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<string, List<SessionEvent>> _sessions = new();
    private long _nextId;

    public long Append(string sessionId, SessionEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);
        var id = Interlocked.Increment(ref _nextId);
        var stored = new SessionEvent
        {
            Id = id,
            SessionId = sessionId,
            Type = evt.Type,
            Payload = evt.Payload,
            Timestamp = evt.Timestamp == default ? DateTimeOffset.Now : evt.Timestamp
        };
        var list = _sessions.GetOrAdd(sessionId, _ => new List<SessionEvent>());
        lock (list)
        {
            list.Add(stored);
        }
        return id;
    }

    public IReadOnlyList<SessionEvent> Replay(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var list))
        {
            return Array.Empty<SessionEvent>();
        }
        lock (list)
        {
            return list.ToArray();
        }
    }

    public IReadOnlyList<Message> DeriveMessages(string sessionId)
    {
        var events = Replay(sessionId);
        var messages = new List<Message>();
        foreach (var e in events)
        {
            // 仅模型可见角色投影为消息；其它类型事件（如 system 内部事件）跳过
            if (e.Type is "user" or "assistant" or "system" or "tool")
            {
                messages.Add(new Message { Role = e.Type, Content = e.Payload });
            }
        }
        return messages;
    }
}

using System.Collections.Concurrent;
using ForgeSelf.Abstractions;
using Message = ForgeSelf.Abstractions.Message;

namespace ForgeSelf.Api.Services;

/// <summary>
/// 内存版会话存储（B1/040 重写）：仅追加事件日志 + 模型可见历史投影 + fork + 事件流订阅。
/// </summary>
/// <remarks>
/// 写入唯一入口 <see cref="Append"/>：首行即 <see cref="SessionEventMap.EnsureKnown"/>，
/// 未注册的事件类型在此炸出（开发期暴露漏注册，不进生产）。
/// 生产可替换为持久化实现（同一 ISessionStore 接缝，替换零改动）——B2 落 Sqlite/XCode 实现。
/// </remarks>
public sealed class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<string, List<SessionEvent>> _sessions = new();
    private readonly ConcurrentDictionary<string, SessionEventSubject> _subjects = new();
    private long _nextId;

    /// <inheritdoc />
    public long Append(string sessionId, SessionEvent evt)
    {
        // 与 PersistentSessionStore 对齐契约：空 sessionId 直接拒绝（空会话 ID 不应静默落库）
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentNullException.ThrowIfNull(evt);
        // 运行期强制：未注册/类型名与 record 不符，直接抛
        SessionEventMap.EnsureKnown(evt);

        var id = Interlocked.Increment(ref _nextId);
        var timestamp = evt.Timestamp == default ? DateTimeOffset.Now : evt.Timestamp;
        // 重新分配 Id / 规整 SessionId / 补齐 Timestamp；事件内容本身不可变
        var stored = evt with { Id = id, SessionId = sessionId, Timestamp = timestamp };

        var list = _sessions.GetOrAdd(sessionId, _ => new List<SessionEvent>());
        lock (list)
        {
            list.Add(stored);
        }

        if (_subjects.TryGetValue(sessionId, out var subject))
        {
            subject.OnNext(stored);
        }

        return id;
    }

    /// <inheritdoc />
    public IReadOnlyList<SessionEvent> Replay(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var list))
        {
            return Array.Empty<SessionEvent>();
        }

        lock (list)
        {
            return list.OrderBy(e => e.Id).ToArray();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<Message> DeriveMessages(string sessionId)
        // B2（040）：投影逻辑抽到 SessionEventProjection 单一真源，内存实现与持久化实现共用，
        // 杜绝两份 switch 各自演化导致的投影语义漂移。
        => SessionEventProjection.Derive(Replay(sessionId));

    /// <inheritdoc />
    public string Fork(string sourceSessionId, long beforeEventId, string newSessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(newSessionId);

        if (sourceSessionId == newSessionId)
        {
            throw new ArgumentException("fork 的新会话 ID 不能与源会话相同", nameof(newSessionId));
        }

        var source = Replay(sourceSessionId);
        // 复制前缀事件时改写 SessionId，否则新会话查不到（Id 保留，保证 fork 后顺序一致）
        var prefix = source
            .Where(e => e.Id < beforeEventId)
            .Select(e => e with { SessionId = newSessionId })
            .ToList();

        var list = _sessions.GetOrAdd(newSessionId, _ => new List<SessionEvent>());
        lock (list)
        {
            list.AddRange(prefix);
        }

        return newSessionId;
    }

    /// <inheritdoc />
    public IObservable<SessionEvent> Observe(string sessionId)
        // 与 PersistentSessionStore 共用同一个广播器实现，观察流语义一致（B2 抽出）
        => _subjects.GetOrAdd(sessionId, _ => new SessionEventSubject());
}

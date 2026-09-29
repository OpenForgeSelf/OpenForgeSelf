using System.Collections.Concurrent;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Entities;
using Message = ForgeSelf.Abstractions.Message;

namespace ForgeSelf.Api.Services;

/// <summary>
/// 持久化会话存储（B2/040）：会话事件日志落 Sqlite（NewLife.XCode），进程重启可逐字回放。
/// </summary>
/// <remarks>
/// 与内存实现 <see cref="InMemorySessionStore"/> 同属 <see cref="ISessionStore"/> 接缝：
/// <list type="bullet">
/// <item>写入唯一入口 <see cref="Append"/>：首行 <see cref="SessionEventMap.EnsureKnown"/> 校验，未注册类型直接抛。</item>
/// <item>模型可见历史一律经 <see cref="SessionEventProjection.Derive"/> 投影（与内存实现共用同一份逻辑，防语义漂移）。</item>
/// <item>库中存在 map 未注册的类型 → <see cref="Replay"/> 抛 <see cref="UnknownSessionEventException"/>，
///      严禁静默跳过（旧数据类型被删 = 破坏性变更，必须显式迁移）。</item>
/// </list>
/// 表结构：<c>SessionEvent(Id 自增主键, SessionId, Ts, Type, PayloadJson)</c>，含 SessionId / (SessionId,Id) 索引。
/// </remarks>
public sealed class PersistentSessionStore : ISessionStore
{
    private readonly string _connName;
    private readonly ConcurrentDictionary<string, SessionEventSubject> _subjects = new();

    /// <summary>写串行化闸门：SQLite 单写者，避免并发 Append 撞 "database is locked"。</summary>
    private readonly object _writeGate = new();

    /// <summary>是否已为本实例做过建表（惰性，仅在真正读写库时触发一次）。</summary>
    private int _initialized;

    /// <summary>构造持久化会话存储。</summary>
    /// <param name="connName">XCode 连接名（默认宿主库 <c>ForgeSelf</c>）。</param>
    public PersistentSessionStore(string connName = SessionEventEntity.ConnName)
    {
        ArgumentException.ThrowIfNullOrEmpty(connName);
        _connName = connName;
    }

    /// <summary>
    /// 自持建表（惰性、每实例一次）：宿主 <c>InitializeXCodeDatabase</c> 在 Testing 环境直接返回，
    /// 且 XCode 12 的按需自动建表依赖「实体 Meta 首次初始化」，本实体可能晚于该时机被使用。
    /// 惰性化同时避免「只是被 DI 构造出来、尚未读写」时就去动真实库。
    /// </summary>
    private void EnsureTable()
    {
        if (Volatile.Read(ref _initialized) == 1)
        {
            return;
        }

        SessionEventEntity.EnsureCreated(_connName);
        Volatile.Write(ref _initialized, 1);
    }

    /// <inheritdoc />
    public long Append(string sessionId, SessionEvent evt)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentNullException.ThrowIfNull(evt);

        // 运行期强制：未注册 / 类型名与 record 不符，直接抛
        SessionEventMap.EnsureKnown(evt);
        EnsureTable();

        var timestamp = evt.Timestamp == default ? DateTimeOffset.Now : evt.Timestamp;
        // Id 的真源是行主键（自增），载荷里不落 Id，避免 fork/重放时 Id 语义漂移
        var stored = evt with { Id = 0, SessionId = sessionId, Timestamp = timestamp };

        var row = new SessionEventEntity
        {
            SessionId = sessionId,
            Ts = timestamp.ToUnixTimeMilliseconds(),
            Type = stored.Type,
            PayloadJson = JsonSerializer.Serialize(stored, stored.GetType(), SessionEventJsonConverter.Options),
        };

        lock (_writeGate)
        {
            row.Insert();
        }

        // 推给本会话观察流的是「已落盘、带主键」的事件
        var committed = stored with { Id = row.Id };
        if (_subjects.TryGetValue(sessionId, out var subject))
        {
            subject.OnNext(committed);
        }

        return row.Id;
    }

    /// <inheritdoc />
    public IReadOnlyList<SessionEvent> Replay(string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        EnsureTable();

        var rows = SessionEventEntity.FindAllBySessionIdOrdered(sessionId);
        var result = new List<SessionEvent>(rows.Count);
        foreach (var row in rows)
        {
            result.Add(ToEvent(row));
        }

        return result;
    }

    /// <inheritdoc />
    public IReadOnlyList<Message> DeriveMessages(string sessionId)
        => SessionEventProjection.Derive(Replay(sessionId));

    /// <inheritdoc />
    public string Fork(string sourceSessionId, long beforeEventId, string newSessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceSessionId);
        ArgumentException.ThrowIfNullOrEmpty(newSessionId);

        if (sourceSessionId == newSessionId)
        {
            throw new ArgumentException("fork 的新会话 ID 不能与源会话相同", nameof(newSessionId));
        }

        EnsureTable();

        var rows = SessionEventEntity.FindAllBySessionIdOrdered(sourceSessionId)
            .Where(r => r.Id < beforeEventId)
            .ToList();

        foreach (var row in rows)
        {
            // 按新会话重新落一行：SessionId 改写，Id 由自增重排（XCode 自增主键不可指定）
            var copy = ToEvent(row) with { SessionId = newSessionId };
            var newRow = new SessionEventEntity
            {
                SessionId = newSessionId,
                Ts = row.Ts,
                Type = row.Type,
                PayloadJson = JsonSerializer.Serialize(copy, copy.GetType(), SessionEventJsonConverter.Options),
            };

            lock (_writeGate)
            {
                newRow.Insert();
            }
        }

        return newSessionId;
    }

    /// <inheritdoc />
    public IObservable<SessionEvent> Observe(string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        return _subjects.GetOrAdd(sessionId, _ => new SessionEventSubject());
    }

    /// <summary>行 → 事件：按 Type 选 record 类型反序列化；Id / SessionId 以行为准。</summary>
    private static SessionEvent ToEvent(SessionEventEntity row)
    {
        // 库里有、map 里没有 → Resolve 抛 UnknownSessionEventException（不静默丢历史）
        var targetType = SessionEventMap.Resolve(row.Type);

        var evt = (SessionEvent?)JsonSerializer.Deserialize(
            row.PayloadJson, targetType, SessionEventJsonConverter.Options);

        if (evt is null)
        {
            throw new UnknownSessionEventException($"{row.Type} 载荷反序列化结果为空");
        }

        return evt with { Id = row.Id, SessionId = row.SessionId };
    }
}

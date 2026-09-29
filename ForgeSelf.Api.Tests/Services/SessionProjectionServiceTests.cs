using ForgeSelf.Abstractions;
using ForgeSelf.Api.Entities;
using ForgeSelf.Api.Services;
using XCode;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Tests.Services;

/// <summary>
/// B4（040）门禁：<see cref="SessionProjectionService"/> 投影同步器判据。
/// </summary>
/// <remarks>
/// 判据：幂等（同一事件集重跑 N 次行集合不变）、非模型可见事件不投影、孤儿行自愈（投影以日志为准）。
/// 隔离手法与 <c>PersistentSessionStoreTests</c> 一致：临时库 + 连接串交换 + 缓存清空。
/// </remarks>
[Collection("XCode")]
public class SessionProjectionServiceTests
{
    private readonly string _dbDir;
    private readonly InMemorySessionStore _store;
    private readonly SessionProjectionService _projection;
    private readonly string _sessionId = "proj-" + Guid.NewGuid().ToString("N")[..12];

    private static readonly DateTimeOffset Now = new(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);

    public SessionProjectionServiceTests()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfProj_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dbDir);

        DAL.AddConnStr("ForgeSelf", $"Data Source={Path.Combine(_dbDir, "ForgeSelf.db")}", null, "SQLite");
        EntityFactory.InitConnection("ForgeSelf");

        // XCode 实体缓存为进程级全局单例，切换连接串后必须清空，否则读到旧库数据
        ChatMessage.Meta.Cache.Clear("test reset");
        ChatMessage.Meta.Cache.Expire = 0;

        _store = new InMemorySessionStore();
        _projection = new SessionProjectionService(_store, new SilentLogService());
    }

    // 门禁 6：同一事件集连续 SyncAsync 3 次 → ChatMessage 行数不变（幂等）
    [Fact]
    public async Task SyncAsync_IsIdempotent()
    {
        _store.Append(_sessionId, new UserMessageEvent(0, _sessionId, Now, "问题", MessageSource.Api));
        _store.Append(_sessionId, new AssistantMessageEvent(0, _sessionId, Now, "回答", null, null, "stop"));

        await _projection.SyncAsync(_sessionId);
        Assert.Equal(2, CountRows());

        await _projection.SyncAsync(_sessionId);
        await _projection.SyncAsync(_sessionId);

        Assert.Equal(2, CountRows());
        Assert.Equal(new[] { "user", "assistant" }, Rows().Select(m => m.Role).ToArray());
    }

    // 门禁 7：非模型可见事件（attempt/turn/step/context/inbox）不投影进 ChatMessage
    [Fact]
    public async Task SyncAsync_ExcludesNonModelVisibleEvents()
    {
        _store.Append(_sessionId, new UserMessageEvent(0, _sessionId, Now, "问", MessageSource.Api));
        // 以下均为「已落日志但对模型不可见」
        _store.Append(_sessionId, new AssistantAttemptEvent(0, _sessionId, Now, null, "timeout", "gpt-x"));
        _store.Append(_sessionId, new TurnStartEvent(0, _sessionId, Now, "t1"));
        _store.Append(_sessionId, new TurnEndEvent(0, _sessionId, Now, "t1", TurnEndReason.Completed));
        _store.Append(_sessionId, new StepStartEvent(0, _sessionId, Now, "t1", "st1", 0));
        _store.Append(_sessionId, new StepEndEvent(0, _sessionId, Now, "t1", "st1", StepEndReason.Completed));
        _store.Append(_sessionId, new RequestHeaderEvent(0, _sessionId, Now, "gpt-x", "sys", Array.Empty<string>()));
        _store.Append(_sessionId, new RequestContextEvent(0, _sessionId, Now, null, null, null));
        _store.Append(_sessionId, new InboxSplicedEvent(0, _sessionId, Now, InboxTarget.NextTurn, "claimed",
            [new InboxItem("m1", "内容", MessageSource.WebUi)]));
        _store.Append(_sessionId, new AssistantMessageEvent(0, _sessionId, Now, "答", null, null, "stop"));

        await _projection.SyncAsync(_sessionId);

        var rows = Rows();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, m => Assert.Contains(m.Role, new[] { "user", "assistant" }));
    }

    // 附加：孤儿行自愈 —— 「表里有、日志没有」的行被投影以日志为准覆盖
    [Fact]
    public async Task SyncAsync_HealsOrphanRows()
    {
        new ChatMessage
        {
            SessionId = _sessionId,
            Role = "user",
            Content = "ORPHAN-ROW",
            CreateTime = DateTime.Now,
            UpdateTime = DateTime.Now,
        }.Insert();

        _store.Append(_sessionId, new UserMessageEvent(0, _sessionId, Now, "真实消息", MessageSource.Api));

        await _projection.SyncAsync(_sessionId);

        var rows = Rows();
        Assert.Single(rows);
        Assert.Equal("真实消息", rows[0].Content);
    }

    // 附加：多轮累积 —— 日志追加后增量重投影，前缀保留不重写
    [Fact]
    public async Task SyncAsync_MultiturnAccumulates()
    {
        _store.Append(_sessionId, new UserMessageEvent(0, _sessionId, Now, "u1", MessageSource.Api));
        _store.Append(_sessionId, new AssistantMessageEvent(0, _sessionId, Now, "a1", null, null, "stop"));
        await _projection.SyncAsync(_sessionId);

        _store.Append(_sessionId, new UserMessageEvent(0, _sessionId, Now, "u2", MessageSource.Api));
        _store.Append(_sessionId, new AssistantMessageEvent(0, _sessionId, Now, "a2", null, null, "stop"));
        await _projection.SyncAsync(_sessionId);

        var rows = Rows();
        Assert.Equal(4, rows.Count);
        Assert.Equal(new[] { "u1", "a1", "u2", "a2" }, rows.Select(m => m.Content).ToArray());
    }

    private IList<ChatMessage> Rows()
        => ChatMessage.FindAll(ChatMessage._.SessionId == _sessionId).OrderBy(m => m.Id).ToList();

    private int CountRows() => Rows().Count;

    /// <summary>静默日志替身（避免为单测构造整套 ILogService mock）。</summary>
    private sealed class SilentLogService : ILogService
    {
        public void Debug(string message, params object[] args) { }

        public void Info(string message, params object[] args) { }

        public void Warn(string message, params object[] args) { }

        public void Error(string message, params object[] args) { }
    }
}

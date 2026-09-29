using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Abstractions.Tests;
using ForgeSelf.Api.Entities;
using ForgeSelf.Api.Services;
using Microsoft.AspNetCore.Hosting;
using XCode;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Tests.Services;

/// <summary>
/// B2（040）持久化会话存储测试：① 复用 Abstractions 侧契约基类（与内存实现同跑一套断言）；
/// ② 落盘专属判据（重启逐字一致、并发不串号、未知落盘类型必抛、13 种事件往返、索引存在）。
/// </summary>
/// <remarks>
/// 每个用例独立临时库目录 + <see cref="DAL.AddConnStr"/> + <see cref="EntityFactory.InitConnection"/>，
/// 与 <c>ChatControllerIntegrationTests</c> 同款隔离手法；类上标 <c>[Collection("XCode")]</c> 保证串行。
/// </remarks>
[Collection("XCode")]
public class PersistentSessionStoreTests : SessionStoreContractTestsBase
{
    private readonly string _dbDir;

    public PersistentSessionStoreTests()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfSession_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dbDir);

        DAL.AddConnStr("ForgeSelf", $"Data Source={Path.Combine(_dbDir, "ForgeSelf.db")}", null, "SQLite");
        EntityFactory.InitConnection("ForgeSelf");

        // XCode 的实体缓存为进程级全局单例，跨用例共享且不会因连接串切换而失效；
        // 每个用例建完临时库后清空缓存，保证读到的是当前库。
        SessionEventEntity.Meta.Cache.Clear("test reset");
        SessionEventEntity.Meta.Cache.Expire = 0;
    }

    /// <inheritdoc />
    protected override ISessionStore CreateStore() => new PersistentSessionStore("ForgeSelf");

    private static readonly DateTimeOffset Now = new(2026, 9, 28, 0, 0, 0, TimeSpan.Zero);

    // 测试 6：换实例读同一库 → 逐字一致（等价于「进程重启」）
    [Fact]
    public void Replay_AfterProcessRestart()
    {
        var store = CreateStore();
        var id1 = store.Append("s1", new UserMessageEvent(0, "s1", Now, "你好", MessageSource.Api));
        store.Append("s1", new AssistantMessageEvent(0, "s1", Now, "嗨", null, null, "stop"));
        store.Append("s1", new ToolResultEvent(0, "s1", Now, "c1", "search", "{\"ok\":true}", ToolOutcome.Ok, 12));

        // 新实例（模拟进程重启）：只从库里读
        var restarted = new PersistentSessionStore("ForgeSelf");
        var replayed = restarted.Replay("s1");

        Assert.Equal(3, replayed.Count);
        Assert.Equal(id1, replayed[0].Id);
        Assert.Equal("s1", replayed[0].SessionId);
        Assert.Equal(Now, replayed[0].Timestamp);
        Assert.Equal("你好", ((UserMessageEvent)replayed[0]).Content);
        Assert.Equal("嗨", ((AssistantMessageEvent)replayed[1]).Content);
        Assert.Equal("{\"ok\":true}", ((ToolResultEvent)replayed[2]).ResultJson);

        // 投影在重启后同样可重建
        var messages = restarted.DeriveMessages("s1");
        Assert.Equal(new[] { "user", "assistant", "tool" }, messages.Select(m => m.Role).ToArray());
    }

    // 测试 7：两会话并发 Append 不串号，Id 全局单调
    [Fact]
    public void Append_ConcurrentSessions()
    {
        var store = CreateStore();
        const int perSession = 50;

        var ids = new System.Collections.Concurrent.ConcurrentBag<long>();
        Parallel.For(0, perSession * 2, i =>
        {
            var sessionId = i % 2 == 0 ? "sa" : "sb";
            var id = store.Append(sessionId,
                new UserMessageEvent(0, sessionId, Now, $"m{i}", MessageSource.Api));
            ids.Add(id);
        });

        var a = store.Replay("sa");
        var b = store.Replay("sb");

        Assert.Equal(perSession, a.Count);
        Assert.Equal(perSession, b.Count);
        Assert.All(a, e => Assert.Equal("sa", e.SessionId));
        Assert.All(b, e => Assert.Equal("sb", e.SessionId));

        // Id 全局唯一（自增主键保证单调），且两会话的 Id 并集 == 全部返回 Id
        var distinct = ids.Distinct().ToList();
        Assert.Equal(perSession * 2, distinct.Count);
        Assert.Equal(distinct.OrderBy(x => x).ToArray(), a.Concat(b).Select(e => e.Id).OrderBy(x => x).ToArray());
    }

    // 测试 8：库里有、map 里没有的类型 → Replay 抛（防静默丢历史）
    [Fact]
    public void Replay_UnknownPersistedType_Throws()
    {
        var store = CreateStore();
        store.Append("s1", new UserMessageEvent(0, "s1", Now, "好数据", MessageSource.Api));

        // 直接落一行未注册类型（模拟升级后旧类型被删 / 外部写入）
        new SessionEventEntity
        {
            SessionId = "s1",
            Ts = Now.ToUnixTimeMilliseconds(),
            Type = "test/legacy",
            PayloadJson = "{}",
        }.Insert();

        Assert.Throws<UnknownSessionEventException>(() => store.Replay("s1"));
        Assert.Throws<UnknownSessionEventException>(() => store.DeriveMessages("s1"));
    }

    // 测试 9：13 种事件全量往返（多态 JSON 还原器判据）
    [Fact]
    public void SessionEvent_Roundtrip_All13Types()
    {
        var store = CreateStore();
        var all = AllThirteenEvents();

        Assert.Equal(13, all.Count);

        foreach (var evt in all)
        {
            store.Append("s1", evt);
        }

        var replayed = store.Replay("s1");
        Assert.Equal(13, replayed.Count);

        for (var i = 0; i < all.Count; i++)
        {
            var expected = all[i];
            var actual = replayed[i];

            // 类型必须逐字还原（还原器按 Type 选 record 类型）
            Assert.Equal(expected.GetType(), actual.GetType());
            Assert.Equal(expected.Type, actual.Type);

            // 内容逐字一致：Id 由行主键分配，故以「期望事件带上实际 Id」比较；
            // 用同一套序列化选项比字符串，规避 IReadOnlyList 的引用相等语义。
            var expectedJson = JsonSerializer.Serialize(
                expected with { Id = actual.Id }, expected.GetType(), SessionEventJsonConverter.Options);
            var actualJson = JsonSerializer.Serialize(
                actual, actual.GetType(), SessionEventJsonConverter.Options);

            Assert.Equal(expectedJson, actualJson);
        }
    }

    // 测试 10：SessionId 索引存在（PRAGMA index_list）
    [Fact]
    public void Index_IX_SessionEvent_SessionId_Exists()
    {
        // 先落一行，确保表已建
        CreateStore().Append("s1", new UserMessageEvent(0, "s1", Now, "x", MessageSource.Api));

        // dal.Tables 走 SQLite 反向工程（内部即 PRAGMA index_list / index_info），
        // 断言的是「库里真实存在的索引」而非仅实体声明，等价架构师清单里的 PRAGMA 断言。
        var dal = DAL.Create("ForgeSelf");
        dal.Tables = null; // 清反向工程缓存，强制重读当前临时库

        var table = dal.Tables.FirstOrDefault(t =>
            string.Equals(t.Name, "SessionEvent", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(table);

        var indexNames = table!.Indexes.Select(i => i.Name ?? string.Empty).ToList();
        Assert.Contains(indexNames, n => string.Equals(n, "IX_SessionEvent_SessionId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(indexNames, n => string.Equals(n, "IX_SessionEvent_SessionId_Id", StringComparison.OrdinalIgnoreCase));

        // 索引确实覆盖 SessionId 列（不只是名字对）
        Assert.Contains(table.Indexes, i =>
            i.Columns.Any(c => string.Equals(c, "SessionId", StringComparison.OrdinalIgnoreCase)));
    }

    // 测试 11：宿主 DI 解析 ISessionStore 的运行时类型必须是持久化实现
    [Fact]
    public void Di_Resolves_PersistentSessionStore()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
        });

        var store = factory.Services.GetService<ISessionStore>();

        Assert.NotNull(store);
        Assert.IsType<PersistentSessionStore>(store);
    }

    /// <summary>13 种事件各一个（与 SessionEventMap 的注册一一对应）。</summary>
    private static List<SessionEvent> AllThirteenEvents()
    {
        var ts = Now;
        return
        [
            new SystemMessageEvent(0, "s1", ts, "你是助手"),
            new UserMessageEvent(0, "s1", ts, "你好", MessageSource.WebUi),
            new AssistantMessageEvent(0, "s1", ts, "嗨",
                [new ToolCallRef("c1", "search", "{}")], new UsageInfo(1, 2, 3), "stop"),
            new AssistantAttemptEvent(0, "s1", ts, null, "timeout", "gpt-x"),
            new ToolCallEvent(0, "s1", ts, "c1", "search", "{\"q\":1}"),
            new ToolResultEvent(0, "s1", ts, "c1", "search", "{\"ok\":true}", ToolOutcome.Ok, 12),
            new TurnStartEvent(0, "s1", ts, "t1"),
            new TurnEndEvent(0, "s1", ts, "t1", TurnEndReason.Completed),
            new StepStartEvent(0, "s1", ts, "t1", "st1", 0),
            new StepEndEvent(0, "s1", ts, "t1", "st1", StepEndReason.Completed),
            new RequestHeaderEvent(0, "s1", ts, "gpt-x", "sys", ["search"]),
            new RequestContextEvent(0, "s1", ts, "ws", "2026-09-28", ["ref1"]),
            new InboxSplicedEvent(0, "s1", ts, InboxTarget.NextTurn, "claimed",
                [new InboxItem("m1", "内容", MessageSource.WebUi)]),
        ];
    }
}

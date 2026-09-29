using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Entities;
using ForgeSelf.Api.Services;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace ForgeSelf.Api.Tests.Services;

/// <summary>
/// B2（040）QA 独立反驳性测试（与工程师的 PersistentSessionStoreTests 互不复用）：
/// 打重启重建的全量 13 类逐字段比对、同一会话高并发写、直插脏载荷/未知类型必抛、
/// 投影幂等、内存/持久化双实现行为一致性、构造惰性（无 DB 副作用）、观察流与空 sessionId 负面路径。
/// </summary>
/// <remarks>
/// 隔离手法与 <c>PersistentSessionStoreTests</code> 完全一致（临时目录 + AddConnStr 交换 + 缓存清空），
/// 且<b>不做临时目录删除</b>：xunit 跨 collection 并行下，删除动作会让正在查询
/// "ForgeSelf" 连接的并行测试类（如 AgentRegistryServiceTests 构造器）撞上文件消失 ——
/// QA 第 2 轮回归已实证，删除须避免。目录遗留交由 %TEMP% 清理。
/// </remarks>
[Collection("XCode")]
public class PersistentSessionStoreAdversarialTests
{
    private readonly string _dbDir;

    public PersistentSessionStoreAdversarialTests()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfQA_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dbDir);
        DAL.AddConnStr("ForgeSelf", $"Data Source={Path.Combine(_dbDir, "ForgeSelf.db")}", null, "SQLite");
        EntityFactory.InitConnection("ForgeSelf");
        SessionEventEntity.Meta.Cache.Clear("qa reset");
        SessionEventEntity.Meta.Cache.Expire = 0;
    }

    private static readonly DateTimeOffset T0 = new(2026, 9, 28, 1, 2, 3, TimeSpan.Zero);

    private static PersistentSessionStore NewStore() => new("ForgeSelf");

    /// <summary>13 种事件各一个，时间戳各不相同（用于逐字段比对时抓串位）。</summary>
    private static List<SessionEvent> AllThirteenDistinct()
    {
        DateTimeOffset Ts(int s) => T0.AddSeconds(s);
        return
        [
            new SystemMessageEvent(0, "s", Ts(1), "你是助手"),
            new UserMessageEvent(0, "s", Ts(2), "你好", MessageSource.Plugin),
            new AssistantMessageEvent(0, "s", Ts(3), "嗨",
                [new ToolCallRef("c1", "search", "{\"q\":1}"), new ToolCallRef("c2", "calc", "{}")],
                new UsageInfo(11, 22, 33), "tool_calls"),
            new AssistantAttemptEvent(0, "s", Ts(4), "部分输出", "timeout", "model-x"),
            new ToolCallEvent(0, "s", Ts(5), "c1", "search", "{\"q\":1}"),
            new ToolResultEvent(0, "s", Ts(6), "c1", "search", "{\"ok\":false}", ToolOutcome.Error, 250),
            new TurnStartEvent(0, "s", Ts(7), "t1"),
            new TurnEndEvent(0, "s", Ts(8), "t1", TurnEndReason.MaxTokens),
            new StepStartEvent(0, "s", Ts(9), "t1", "st1", 2),
            new StepEndEvent(0, "s", Ts(10), "t1", "st1", StepEndReason.ToolCallPending),
            new RequestHeaderEvent(0, "s", Ts(11), "model-x", "渲染后的系统提示", ["search", "calc"]),
            new RequestContextEvent(0, "s", Ts(12), "工作区指令", "2026-09-28T01:02:03", ["ref-a"]),
            new InboxSplicedEvent(0, "s", Ts(13), InboxTarget.NextStep, "steer",
                [new InboxItem("m9", "插队内容", MessageSource.Api)]),
        ];
    }

    // ---- 1. 重启重建：13 类全量 + 换实例 + 逐字段（含 Timestamp / 枚举 / 嵌套列表） ----
    [Fact]
    public void Replay_AcrossInstances_All13Types_FieldByField()
    {
        var store1 = NewStore();
        var all = AllThirteenDistinct();
        foreach (var evt in all)
        {
            store1.Append("s", evt);
        }

        // 模拟进程重启：全新实例读同一库
        var store2 = NewStore();
        var back = store2.Replay("s");

        Assert.Equal(13, back.Count);
        for (var i = 0; i < all.Count; i++)
        {
            var expected = all[i];
            var actual = back[i];

            Assert.Equal(expected.GetType(), actual.GetType());
            Assert.Equal(expected.Type, actual.Type);
            Assert.Equal(expected.Timestamp, actual.Timestamp); // 时间戳逐字一致
            Assert.Equal("s", actual.SessionId);

            // 除 Id 外全字段一致：序列化后比对（规避 record 引用相等）
            var expectedJson = JsonSerializer.Serialize(
                expected with { Id = actual.Id }, expected.GetType(), SessionEventJsonConverter.Options);
            var actualJson = JsonSerializer.Serialize(
                actual, actual.GetType(), SessionEventJsonConverter.Options);
            Assert.Equal(expectedJson, actualJson);
        }

        // 投影同样可跨实例重建，且角色序列稳定
        var messages = store2.DeriveMessages("s");
        Assert.Equal(new[] { "system", "user", "assistant", "tool" },
            messages.Select(m => m.Role).ToArray());
        Assert.Equal(2, messages[2].ToolCalls!.Count);
        Assert.Equal(33, messages[2].Usage!.CachedTokens);
    }

    // ---- 2. 同一会话高并发 Append：不丢号、不串号、不重号 ----
    [Fact]
    public void Append_ConcurrentSameSession_NoLossNoDuplicate()
    {
        var store = NewStore();
        const int threads = 8;
        const int perThread = 25;

        var ids = new System.Collections.Concurrent.ConcurrentBag<long>();
        Parallel.For(0, threads * perThread, i =>
        {
            ids.Add(store.Append("s-conc",
                new UserMessageEvent(0, "s-conc", default, $"m{i}", MessageSource.Api)));
        });

        var replayed = store.Replay("s-conc");

        Assert.Equal(threads * perThread, replayed.Count);
        var distinct = ids.Distinct().ToArray();
        Assert.Equal(threads * perThread, distinct.Length); // 返回 Id 无重复

        // 库中 Id 与返回 Id 完全一致（无丢号）
        Assert.Equal(distinct.OrderBy(x => x).ToArray(),
            replayed.Select(e => e.Id).ToArray());

        // 内容逐字对应（无串号）：每条内容恰好出现一次
        var contents = replayed.Cast<UserMessageEvent>().Select(e => e.Content).OrderBy(x => x).ToArray();
        var expectedContents = Enumerable.Range(0, threads * perThread).Select(i => $"m{i}").OrderBy(x => x).ToArray();
        Assert.Equal(expectedContents, contents);

        // Replay 按 Id 升序
        Assert.Equal(replayed.Select(e => e.Id).OrderBy(x => x).ToArray(),
            replayed.Select(e => e.Id).ToArray());
    }

    // ---- 3. 直插脏数据：未知类型必抛；合法类型 + 脏 JSON 也必抛（不得静默丢） ----
    [Fact]
    public void DirtyRows_AlwaysThrow_NeverSilent()
    {
        var store = NewStore();
        store.Append("s-dirty", new UserMessageEvent(0, "s-dirty", T0, "好数据", MessageSource.Api));

        // 3a：未注册类型
        new SessionEventEntity
        {
            SessionId = "s-dirty", Ts = T0.ToUnixTimeMilliseconds(),
            Type = "qa/legacy", PayloadJson = "{}",
        }.Insert();

        Assert.Throws<UnknownSessionEventException>(() => store.Replay("s-dirty"));
        Assert.Throws<UnknownSessionEventException>(() => store.DeriveMessages("s-dirty"));

        // 3b：注册类型 + 非 JSON 对象载荷（"just a string"）
        new SessionEventEntity
        {
            SessionId = "s-dirty2", Ts = T0.ToUnixTimeMilliseconds(),
            Type = "user/message", PayloadJson = "\"just a string\"",
        }.Insert();
        Assert.ThrowsAny<JsonException>(() => store.Replay("s-dirty2"));

        // 3c：注册类型 + 截断 JSON
        new SessionEventEntity
        {
            SessionId = "s-dirty3", Ts = T0.ToUnixTimeMilliseconds(),
            Type = "user/message", PayloadJson = "{\"Id\":1,\"Sess",
        }.Insert();
        Assert.ThrowsAny<JsonException>(() => store.Replay("s-dirty3"));
    }

    // ---- 4. 投影幂等：同一输入重复 DeriveMessages 结果逐字一致 ----
    [Fact]
    public void DeriveMessages_Idempotent()
    {
        var store = NewStore();
        foreach (var evt in AllThirteenDistinct())
        {
            store.Append("s-idem", evt);
        }

        var first = store.DeriveMessages("s-idem");
        var second = store.DeriveMessages("s-idem");
        var third = NewStore().DeriveMessages("s-idem"); // 换实例再投影

        Assert.Equal(
            JsonSerializer.Serialize(first, SessionEventJsonConverter.Options),
            JsonSerializer.Serialize(second, SessionEventJsonConverter.Options));
        Assert.Equal(
            JsonSerializer.Serialize(first, SessionEventJsonConverter.Options),
            JsonSerializer.Serialize(third, SessionEventJsonConverter.Options));
    }

    // ---- 5. 双实现行为一致性：同一事件序列在两个实现上投影结果一致 ----
    [Fact]
    public void DeriveMessages_InMemoryAndPersistent_BehaviorallyIdentical()
    {
        var memory = new InMemorySessionStore();
        var persistent = NewStore();
        var sessionId = $"s-parity-{Guid.NewGuid():N}";

        foreach (var evt in AllThirteenDistinct())
        {
            memory.Append(sessionId, evt);
            persistent.Append(sessionId, evt);
        }

        var fromMemory = memory.DeriveMessages(sessionId);
        var fromPersistent = persistent.DeriveMessages(sessionId);

        Assert.Equal(fromMemory.Count, fromPersistent.Count);
        for (var i = 0; i < fromMemory.Count; i++)
        {
            Assert.Equal(fromMemory[i].Role, fromPersistent[i].Role);
            Assert.Equal(fromMemory[i].Content, fromPersistent[i].Content);
            Assert.Equal(fromMemory[i].CallId, fromPersistent[i].CallId);
            Assert.Equal(
                JsonSerializer.Serialize(fromMemory[i].ToolCalls, SessionEventJsonConverter.Options),
                JsonSerializer.Serialize(fromPersistent[i].ToolCalls, SessionEventJsonConverter.Options));
            Assert.Equal(fromMemory[i].Usage, fromPersistent[i].Usage);
        }

        // 事件层面（除 Id 外）也一致
        var memEvents = memory.Replay(sessionId);
        var perEvents = persistent.Replay(sessionId);
        Assert.Equal(memEvents.Count, perEvents.Count);
        for (var i = 0; i < memEvents.Count; i++)
        {
            Assert.Equal(memEvents[i].Type, perEvents[i].Type);
            Assert.Equal(memEvents[i].Timestamp, perEvents[i].Timestamp);
            Assert.Equal(sessionId, perEvents[i].SessionId);
        }
    }

    // ---- 6. 惰性建表：仅构造（DI 解析场景）不得产生任何 DB 副作用 ----
    [Fact]
    public void Constructor_Lazy_NoDbSideEffect()
    {
        var dbPath = Path.Combine(_dbDir, "ForgeSelf.db");

        // 先落一笔让库文件存在（隔离出「纯构造」阶段），再删掉库文件
        NewStore().Append("s-warm", new UserMessageEvent(0, "s-warm", T0, "warm", MessageSource.Api));
        Assert.True(File.Exists(dbPath));
        File.Delete(dbPath);

        // 纯构造（等价 DI 解析单例 new PersistentSessionStore()）：不得重建库文件
        var store = new PersistentSessionStore("ForgeSelf");
        Assert.False(File.Exists(dbPath),
            "仅构造 PersistentSessionStore 就触碰了数据库（建库/建表）——惰性建表失效，DI 构造有 DB 副作用");

        // 真正读写才触库
        store.Append("s-lazy", new UserMessageEvent(0, "s-lazy", T0, "x", MessageSource.Api));
        Assert.True(File.Exists(dbPath), "Append 之后库文件应已重建");
        Assert.Single(store.Replay("s-lazy"));
    }

    // ---- 7. 观察流：推送的是已落盘带主键的事件；退订后不再收 ----
    [Fact]
    public void Observe_ReceivesCommittedEvent_UnsubscribeStopsDelivery()
    {
        var store = NewStore();
        var received = new List<SessionEvent>();
        var sub = store.Observe("s-obs").Subscribe(new CollectorObserver(received));

        var id1 = store.Append("s-obs", new UserMessageEvent(0, "s-obs", T0, "一", MessageSource.Api));
        var id2 = store.Append("s-obs", new UserMessageEvent(0, "s-obs", T0, "二", MessageSource.Api));

        Assert.Equal(2, received.Count);
        Assert.Equal(id1, received[0].Id); // 推送带真实主键
        Assert.Equal(id2, received[1].Id);
        Assert.Equal("一", ((UserMessageEvent)received[0]).Content);

        sub.Dispose();
        store.Append("s-obs", new UserMessageEvent(0, "s-obs", T0, "三", MessageSource.Api));
        Assert.Equal(2, received.Count); // 退订后不再收
    }

    // ---- 8. 空 sessionId：必须抛明确异常（ArgumentException），不得静默落库 ----
    [Fact]
    public void EmptySessionId_Throws()
    {
        var store = NewStore();
        Assert.ThrowsAny<ArgumentException>(() =>
            store.Append("", new UserMessageEvent(0, "", T0, "x", MessageSource.Api)));
        Assert.ThrowsAny<ArgumentException>(() => store.Replay(""));
        Assert.ThrowsAny<ArgumentException>(() => store.DeriveMessages(""));
        Assert.ThrowsAny<ArgumentException>(() => store.Observe(""));
    }

    /// <summary>最小观察者：只收集 OnNext 事件，供断言。</summary>
    private sealed class CollectorObserver(List<SessionEvent> sink) : IObserver<SessionEvent>
    {
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(SessionEvent value) => sink.Add(value);
    }
}

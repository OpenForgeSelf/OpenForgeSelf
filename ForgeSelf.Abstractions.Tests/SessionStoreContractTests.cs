using System.Reflection;
using System.Runtime.CompilerServices;
using ForgeSelf.Abstractions;
using Xunit;

namespace ForgeSelf.Abstractions.Tests;

/// <summary>
/// 会话存储契约测试基类（B1/040）。
/// 目的：把「ISessionStore 接缝必须满足的行为」固化成可复用断言，
/// B2 落持久化实现（Sqlite/XCode）时，同一套契约测试对内存实现与持久化实现各跑一遍（双实现契约）。
/// </summary>
public abstract class SessionStoreContractTestsBase
{
    /// <summary>被测实现。</summary>
    protected abstract ISessionStore CreateStore();

    private static readonly DateTimeOffset Now = new(2026, 9, 28, 0, 0, 0, TimeSpan.Zero);

    // 测试 1：未注册类型 Append 必须抛
    [Fact]
    public void Append_UnknownType_Throws()
    {
        var store = CreateStore();

        Assert.Throws<UnknownSessionEventException>(
            () => store.Append("s1", new UnregisteredTestEvent(0, "s1", Now)));
    }

    // 测试 2：attempt/结构/context 类事件不进入模型投影
    [Fact]
    public void DeriveMessages_OnlyModelVisible()
    {
        var store = CreateStore();
        store.Append("s1", new SystemMessageEvent(0, "s1", Now, "你是助手"));
        store.Append("s1", new UserMessageEvent(0, "s1", Now, "问", MessageSource.Api));
        store.Append("s1", new AssistantMessageEvent(0, "s1", Now, "答", null, null, "stop"));
        // 以下三类：落日志但对模型不可见
        store.Append("s1", new AssistantAttemptEvent(0, "s1", Now, null, "timeout", "gpt-x"));
        store.Append("s1", new TurnStartEvent(0, "s1", Now, "t1"));
        store.Append("s1", new RequestContextEvent(0, "s1", Now, null, null, null));

        var messages = store.DeriveMessages("s1");

        Assert.Equal(3, messages.Count);
        Assert.Equal("system", messages[0].Role);
        Assert.Equal("user", messages[1].Role);
        Assert.Equal("assistant", messages[2].Role);
    }

    // 测试 3：Append 一批 → DeriveMessages 顺序与内容一致
    [Fact]
    public void DeriveMessages_Roundtrip()
    {
        var store = CreateStore();
        store.Append("s1", new SystemMessageEvent(0, "s1", Now, "sys"));
        store.Append("s1", new UserMessageEvent(0, "s1", Now, "u1", MessageSource.WebUi));
        store.Append("s1", new AssistantMessageEvent(0, "s1", Now, "a1", null, null, "stop"));
        store.Append("s1", new ToolCallEvent(0, "s1", Now, "c1", "search", "{}"));
        store.Append("s1", new ToolResultEvent(0, "s1", Now, "c1", "search", "{\"ok\":true}", ToolOutcome.Ok, 12));
        store.Append("s1", new UserMessageEvent(0, "s1", Now, "u2", MessageSource.WebUi));

        var messages = store.DeriveMessages("s1");

        // tool/call 折叠进 assistant.ToolCalls，不单独成条
        Assert.Equal(5, messages.Count);
        Assert.Equal(new[] { "system", "user", "assistant", "tool", "user" },
            messages.Select(m => m.Role).ToArray());
        Assert.Equal("u2", messages[4].Content);
        Assert.Equal("c1", messages[3].CallId);
    }

    // 测试 4：反射遍历所有事件子类，漏注册在此抓出（常绿防回归）
    [Fact]
    public void SessionEventMap_RecordMatchesType()
    {
        var eventTypes = typeof(SessionEvent).Assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(typeof(SessionEvent)) && !t.IsAbstract)
            .ToList();

        Assert.NotEmpty(eventTypes);

        foreach (var type in eventTypes)
        {
            // 不调用构造函数即可取得 Type（override 返回常量，不依赖字段）
            var probe = (SessionEvent)RuntimeHelpers.GetUninitializedObject(type);
            Assert.True(SessionEventMap.IsKnown(probe.Type),
                $"事件 {type.Name} 的 Type={probe.Type} 未注册进 SessionEventMap");
            Assert.Same(type, SessionEventMap.Resolve(probe.Type));
        }
    }

    // 测试 5：fork 只复制 beforeEventId 之前的事件
    [Fact]
    public void Fork_CopiesPrefix()
    {
        var store = CreateStore();
        var id1 = store.Append("s1", new UserMessageEvent(0, "s1", Now, "前", MessageSource.Api));
        store.Append("s1", new AssistantMessageEvent(0, "s1", Now, "后", null, null, "stop"));

        var newSessionId = store.Fork("s1", beforeEventId: id1 + 1, newSessionId: "s2");

        Assert.Equal("s2", newSessionId);
        Assert.Single(store.Replay("s2"));
        Assert.Equal("前", ((UserMessageEvent)store.Replay("s2")[0]).Content);
        Assert.Equal(2, store.Replay("s1").Count);
    }

    /// <summary>测试替身用的未注册事件：用于验证「漏注册即抛」。</summary>
    private sealed record UnregisteredTestEvent(long Id, string SessionId, DateTimeOffset Timestamp)
        : SessionEvent(Id, SessionId, Timestamp)
    {
        public override string Type => "test/unregistered";
    }
}

/// <summary>
/// 契约测试的默认载体：B1 阶段用内存替身跑通契约；
/// B2 落持久化实现后，新增一个继承同一基类的子类即可复用全部断言。
/// </summary>
public class SessionStoreContractTests : SessionStoreContractTestsBase
{
    protected override ISessionStore CreateStore() => new FakeSessionStore();

    /// <summary>最小内存实现：仅用于验证契约本身，不替代 Api 侧 InMemorySessionStore。</summary>
    private sealed class FakeSessionStore : ISessionStore
    {
        private readonly List<SessionEvent> _events = new();
        private long _nextId;

        public long Append(string sessionId, SessionEvent evt)
        {
            SessionEventMap.EnsureKnown(evt);
            var id = ++_nextId;
            _events.Add(evt with { Id = id, SessionId = sessionId });
            return id;
        }

        public IReadOnlyList<SessionEvent> Replay(string sessionId)
            => _events.Where(e => e.SessionId == sessionId).OrderBy(e => e.Id).ToArray();

        public IReadOnlyList<Message> DeriveMessages(string sessionId)
        {
            var result = new List<Message>();
            foreach (var evt in Replay(sessionId))
            {
                switch (evt)
                {
                    case SystemMessageEvent s:
                        result.Add(new Message { Role = "system", Content = s.Content });
                        break;
                    case UserMessageEvent u:
                        result.Add(new Message { Role = "user", Content = u.Content });
                        break;
                    case AssistantMessageEvent a:
                        result.Add(new Message { Role = "assistant", Content = a.Content, ToolCalls = a.ToolCalls, Usage = a.Usage });
                        break;
                    case ToolCallEvent:
                        break;
                    case ToolResultEvent r:
                        result.Add(new Message { Role = "tool", Content = r.ResultJson, CallId = r.CallId });
                        break;
                    case TurnStartEvent:
                    case TurnEndEvent:
                    case StepStartEvent:
                    case StepEndEvent:
                    case RequestHeaderEvent:
                    case RequestContextEvent:
                    case AssistantAttemptEvent:
                    case InboxSplicedEvent:
                        break;
                    default:
                        throw new UnknownSessionEventException(evt.Type);
                }
            }

            return result;
        }

        public string Fork(string sourceSessionId, long beforeEventId, string newSessionId)
        {
            // 复制前缀事件时改写 SessionId，否则新会话 Replay 查不到
            var prefix = Replay(sourceSessionId)
                .Where(e => e.Id < beforeEventId)
                .Select(e => e with { SessionId = newSessionId })
                .ToList();
            _events.AddRange(prefix);
            return newSessionId;
        }

        public IObservable<SessionEvent> Observe(string sessionId) => new NoopObservable();

        private sealed class NoopObservable : IObservable<SessionEvent>
        {
            public IDisposable Subscribe(IObserver<SessionEvent> observer) => new Nop();

            private sealed class Nop : IDisposable
            {
                public void Dispose() { }
            }
        }
    }
}

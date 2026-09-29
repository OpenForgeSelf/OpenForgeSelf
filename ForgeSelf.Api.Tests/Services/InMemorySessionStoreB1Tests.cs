using ForgeSelf.Abstractions;
using ForgeSelf.Api.Services;
using FluentAssertions;
using Xunit;

namespace ForgeSelf.Api.Tests.Services;

/// <summary>
/// B1（040）InMemorySessionStore 实现级测试：契约之外的实现细节（事件流订阅、运行期拒写、fork 隔离）。
/// 契约类断言（漏注册即抛、投影规则、roundtrip）在 Abstractions 侧 SessionStoreContractTestsBase。
/// </summary>
public class InMemorySessionStoreB1Tests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Append_UnregisteredType_Throws()
    {
        var store = new InMemorySessionStore();

        var act = () => store.Append("s1", new UnregisteredEvent(0, "s1", Now));

        act.Should().Throw<UnknownSessionEventException>();
        store.Replay("s1").Should().BeEmpty();
    }

    [Fact]
    public void Append_TypeNameMismatch_Throws()
    {
        var store = new InMemorySessionStore();

        // 类型名「user/message」已注册，但实际 record 不是 UserMessageEvent —— 必须被拒
        var act = () => store.Append("s1", new ForgedTypeNameEvent(0, "s1", Now));

        act.Should().Throw<UnknownSessionEventException>();
    }

    [Fact]
    public void Observe_ReceivesAppendedEvents()
    {
        var store = new InMemorySessionStore();
        var received = new List<SessionEvent>();
        using var subscription = store.Observe("s1").Subscribe(new DelegateObserver(received.Add));

        store.Append("s1", new UserMessageEvent(0, "s1", Now, "你好", MessageSource.Api));
        store.Append("s2", new UserMessageEvent(0, "s2", Now, "别的会话", MessageSource.Api));
        store.Append("s1", new AssistantMessageEvent(0, "s1", Now, "嗨", null, null, "stop"));

        // 只收本会话事件
        received.Should().HaveCount(2);
        received[0].Should().BeOfType<UserMessageEvent>();
        received[1].Should().BeOfType<AssistantMessageEvent>();
    }

    [Fact]
    public void Fork_CopiesPrefixAndKeepsSourceIntact()
    {
        var store = new InMemorySessionStore();
        var firstId = store.Append("s1", new UserMessageEvent(0, "s1", Now, "前", MessageSource.Api));
        store.Append("s1", new AssistantMessageEvent(0, "s1", Now, "后", null, null, "stop"));

        store.Fork("s1", beforeEventId: firstId + 1, newSessionId: "s2");

        store.Replay("s1").Should().HaveCount(2, "源会话不受 fork 影响");
        var forked = store.Replay("s2");
        forked.Should().HaveCount(1);
        forked[0].SessionId.Should().Be("s2", "fork 出来的事件归属新会话");
    }

    [Fact]
    public void Append_NullEvent_Throws()
    {
        var store = new InMemorySessionStore();

        var act = () => store.Append("s1", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>未注册事件（用于验证「漏注册即抛」）。</summary>
    private sealed record UnregisteredEvent(long Id, string SessionId, DateTimeOffset Timestamp)
        : SessionEvent(Id, SessionId, Timestamp)
    {
        public override string Type => "test/unregistered";
    }

    /// <summary>伪造类型名：Type 已注册但与 record 类型不符。</summary>
    private sealed record ForgedTypeNameEvent(long Id, string SessionId, DateTimeOffset Timestamp)
        : SessionEvent(Id, SessionId, Timestamp)
    {
        public override string Type => "user/message";
    }

    private sealed class DelegateObserver(Action<SessionEvent> onNext) : IObserver<SessionEvent>
    {
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(SessionEvent value) => onNext(value);
    }
}

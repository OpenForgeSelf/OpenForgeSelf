using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Backend.Services;

namespace OpenForgeSelf.Backend.Tests.Services;

public class AgentLoopAndInboxTests
{
    [Fact]
    public async Task InMemoryAgentLoop_RunAsync_YieldsFivePhasesInOrder()
    {
        var loop = new InMemoryAgentLoop();
        var request = new AgentRunRequest { SessionId = "s1", Message = "hello" };

        var turns = new List<TurnEvent>();
        await foreach (var turn in loop.RunAsync(request))
        {
            turns.Add(turn);
        }

        turns.Should().HaveCount(5);
        turns.Select(t => t.Phase).Should()
            .ContainInOrder("Claim", "Assemble", "Request", "Execute", "Repeat");

        // Sequence 从 0 递增
        turns.Select(t => t.Sequence).Should().Equal(0, 1, 2, 3, 4);

        // Payload 含请求信息
        turns[0].Payload.Should().Contain("s1").And.Contain("hello");
    }

    [Fact]
    public async Task InMemoryAgentLoop_RunAsync_RespectsCancellation()
    {
        var loop = new InMemoryAgentLoop();
        var request = new AgentRunRequest { SessionId = "s1", Message = "test" };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var turns = new List<TurnEvent>();
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var turn in loop.RunAsync(request, cts.Token))
            {
                turns.Add(turn);
            }
        });

        turns.Should().BeEmpty();
    }

    [Fact]
    public void InMemoryInbox_ThreeInputSemantics_EnqueueCorrectly()
    {
        var inbox = new InMemoryInbox();
        var contextBlock = new { key = "value" };

        inbox.Followup("s1", "continue");
        inbox.Steer("s1", "stop");
        inbox.Inject("s1", contextBlock);

        var messages = inbox.GetMessages("s1");
        messages.Should().HaveCount(3);

        messages[0].Kind.Should().Be(InboxMessageKind.Followup);
        messages[0].Text.Should().Be("continue");
        messages[0].SessionId.Should().Be("s1");

        messages[1].Kind.Should().Be(InboxMessageKind.Steer);
        messages[1].Text.Should().Be("stop");

        messages[2].Kind.Should().Be(InboxMessageKind.Inject);
        messages[2].ContextBlock.Should().BeSameAs(contextBlock);
    }

    [Fact]
    public void InMemoryInbox_SessionIsolation()
    {
        var inbox = new InMemoryInbox();

        inbox.Followup("s1", "msg1");
        inbox.Followup("s2", "msg2");

        inbox.GetMessages("s1").Should().HaveCount(1);
        inbox.GetMessages("s1")[0].Text.Should().Be("msg1");
        inbox.GetMessages("s2").Should().HaveCount(1);
        inbox.GetMessages("s2")[0].Text.Should().Be("msg2");

        // 不存在的会话返回空
        inbox.GetMessages("other").Should().BeEmpty();
    }

    [Fact]
    public void InMemoryInbox_Clear_RemovesSessionMessages()
    {
        var inbox = new InMemoryInbox();

        inbox.Followup("s1", "msg1");
        inbox.Followup("s1", "msg2");
        inbox.GetMessages("s1").Should().HaveCount(2);

        inbox.Clear("s1");
        inbox.GetMessages("s1").Should().BeEmpty();
    }

    [Fact]
    public void InMemoryInbox_Timestamps_AreSet()
    {
        var inbox = new InMemoryInbox();

        inbox.Followup("s1", "test");

        var msg = inbox.GetMessages("s1").Single();
        msg.Timestamp.Should().NotBe(default(DateTime));
    }
}

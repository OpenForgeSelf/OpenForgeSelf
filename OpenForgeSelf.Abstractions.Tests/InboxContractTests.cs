using OpenForgeSelf.Abstractions;
using Xunit;

namespace OpenForgeSelf.Abstractions.Tests;

public class InboxContractTests
{
    private sealed class FakeInbox : IInbox
    {
        public List<(string Kind, string SessionId, string Text)> Calls { get; } = new();
        public List<(string SessionId, object ContextBlock)> Injects { get; } = new();

        public void Followup(string sessionId, string message)
            => Calls.Add(("Followup", sessionId, message));

        public void Steer(string sessionId, string input)
            => Calls.Add(("Steer", sessionId, input));

        public void Inject(string sessionId, object contextBlock)
            => Injects.Add((sessionId, contextBlock));
    }

    [Fact]
    public void ThreeInputSemantics_AreRecorded()
    {
        var inbox = new FakeInbox();
        var block = new object();

        inbox.Followup("s1", "continue");
        inbox.Steer("s1", "stop doing that");
        inbox.Inject("s1", block);

        Assert.Equal(2, inbox.Calls.Count);
        Assert.Equal(("Followup", "s1", "continue"), inbox.Calls[0]);
        Assert.Equal(("Steer", "s1", "stop doing that"), inbox.Calls[1]);

        var (sessionId, injected) = Assert.Single(inbox.Injects);
        Assert.Equal("s1", sessionId);
        Assert.Same(block, injected);
    }
}

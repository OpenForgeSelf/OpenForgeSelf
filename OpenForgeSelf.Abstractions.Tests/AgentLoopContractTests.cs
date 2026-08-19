using System.Runtime.CompilerServices;
using OpenForgeSelf.Abstractions;
using Xunit;

namespace OpenForgeSelf.Abstractions.Tests;

public class AgentLoopContractTests
{
    private sealed class FakeAgentLoop : IAgentLoop
    {
        public async IAsyncEnumerable<TurnEvent> RunAsync(
            AgentRunRequest request,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return new TurnEvent { Phase = "Claim", Sequence = 0 };
            yield return new TurnEvent { Phase = "Assemble", Sequence = 1 };
            yield return new TurnEvent { Phase = "Request", Sequence = 2 };
            yield return new TurnEvent { Phase = "Execute", Sequence = 3 };
        }
    }

    [Fact]
    public async Task RunAsync_YieldsTurnsInReactOrder()
    {
        var loop = new FakeAgentLoop();
        var request = new AgentRunRequest { SessionId = "s1", Message = "hi" };

        var turns = new List<TurnEvent>();
        await foreach (var turn in loop.RunAsync(request))
        {
            turns.Add(turn);
        }

        Assert.Equal(4, turns.Count);
        Assert.Equal(new[] { "Claim", "Assemble", "Request", "Execute" }, turns.Select(t => t.Phase));
        Assert.Equal(new long[] { 0, 1, 2, 3 }, turns.Select(t => t.Sequence));
    }
}

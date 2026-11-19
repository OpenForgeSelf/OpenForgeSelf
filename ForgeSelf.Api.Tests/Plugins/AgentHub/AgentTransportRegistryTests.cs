using FluentAssertions;
using ForgeSelf.Api.Plugins.AgentHub.Models;
using ForgeSelf.Api.Plugins.AgentHub.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AgentHub;

/// <summary>P2：Transport 注册表按 Kind 选路。</summary>
public class AgentTransportRegistryTests
{
    [Fact]
    public void Resolve_按Kind选路_空Kind回落Cli()
    {
        var reg = new AgentTransportRegistry();
        reg.Register(new StubTransport("Cli"));
        reg.Register(new StubTransport("Oar"));

        reg.Resolve("Cli").Kind.Should().Be("Cli");
        reg.Resolve("Oar").Kind.Should().Be("Oar");
        reg.Resolve(null).Kind.Should().Be("Cli");
        reg.Resolve("").Kind.Should().Be("Cli");
        reg.Resolve("unknown-kind").Kind.Should().Be("Cli"); // 未知回落 Cli
    }

    [Fact]
    public void Resolve_无Cli且未知_抛KeyNotFound()
    {
        var reg = new AgentTransportRegistry();
        reg.Register(new StubTransport("Oar"));
        var act = () => reg.Resolve("Cli");
        act.Should().Throw<KeyNotFoundException>();
    }

    private sealed class StubTransport : IAgentTransport
    {
        public StubTransport(string kind) => Kind = kind;
        public string Kind { get; }
        public async IAsyncEnumerable<AgentEvent> StreamAsync(
            AgentAccessPointDto ap, AgentRunRequest req, AgentSession session,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.Yield();
            yield break;
        }

        public Task CancelAsync(AgentSession session) => Task.CompletedTask;
    }
}

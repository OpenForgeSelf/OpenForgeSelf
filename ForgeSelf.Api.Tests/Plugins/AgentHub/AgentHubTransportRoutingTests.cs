using FluentAssertions;
using ForgeSelf.Api.Plugins.AgentHub.Models;
using ForgeSelf.Api.Plugins.AgentHub.Profiles;
using ForgeSelf.Api.Plugins.AgentHub.Services;
using Xunit;
using TaskStatus = ForgeSelf.Api.Plugins.AgentHub.Services.TaskStatus;

namespace ForgeSelf.Api.Tests.Plugins.AgentHub;

/// <summary>P2：DelegationRuntime 按 AccessPoint.Transport 选路（Cli|Oar）。</summary>
[Collection("XCode")]
public class AgentHubTransportRoutingTests : IClassFixture<XCodeTestFixture>
{
    public AgentHubTransportRoutingTests(XCodeTestFixture fixture) => _ = fixture;

    [Fact]
    public async Task Transport为Oar时_走Oar替身不走Cli()
    {
        var loader = new ProfileLoader();
        loader.LoadAll(null);
        var registry = new AgentRegistry(loader);
        var broker = new PermissionBroker(registry);

        var cli = new RecordingTransport("Cli");
        var oar = new RecordingTransport("Oar");
        var transports = new AgentTransportRegistry();
        transports.Register(cli);
        transports.Register(oar);

        var runtime = new DelegationRuntime(registry, loader, broker, transports);

        // 清理同 vendor
        foreach (var a in registry.List().Where(a => a.Vendor == "route-oar").ToList())
            registry.Delete(a.Id);

        var agent = registry.Create(new AgentSaveRequest
        {
            Name = "route-" + Guid.NewGuid().ToString("N")[..8],
            Vendor = "route-oar",
            Enabled = true,
            DefaultCwd = Path.GetTempPath(),
            AccessPoints =
            [
                new AgentAccessPointSaveRequest
                {
                    Mode = "Sessionful",
                    Transport = "Oar",
                    RuntimeId = "scripted",
                    IsDefault = true,
                    CancelSupported = true,
                }
            ]
        });

        var dto = await runtime.CreateAsync(new DelegationRequest
        {
            AgentId = agent.Id,
            Prompt = "hello-oar-route",
            Cwd = Path.GetTempPath(),
            PermissionMode = "read-only",
            CreatedBy = "test"
        });

        dto.Status.Should().Be(TaskStatus.Queued);
        var ran = await runtime.RunAsync(dto.Id);
        ran.Status.Should().BeOneOf(TaskStatus.Succeeded, TaskStatus.Failed);

        oar.StreamCalls.Should().BeGreaterThan(0, "Oar AccessPoint 必须命中 Oar transport");
        cli.StreamCalls.Should().Be(0, "不得误走 Cli");
    }

    private sealed class RecordingTransport : IAgentTransport
    {
        public RecordingTransport(string kind) => Kind = kind;
        public string Kind { get; }
        public int StreamCalls { get; private set; }

        public async IAsyncEnumerable<AgentEvent> StreamAsync(
            AgentAccessPointDto ap, AgentRunRequest req, AgentSession session,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            StreamCalls++;
            await Task.Yield();
            yield return AgentEvent.FromText($"from-{Kind}");
            yield return new AgentEvent { Type = AgentEventTypes.Exit, ExitCode = 0 };
        }

        public Task CancelAsync(AgentSession session) => Task.CompletedTask;
    }
}

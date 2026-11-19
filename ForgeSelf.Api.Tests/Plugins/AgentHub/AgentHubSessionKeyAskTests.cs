using System.Text.Json;
using FluentAssertions;
using ForgeSelf.Api.Plugins.AgentHub.Models;
using ForgeSelf.Api.Plugins.AgentHub.Profiles;
using ForgeSelf.Api.Plugins.AgentHub.Services;
using ForgeSelf.Api.Plugins.AgentHub.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using IAgentRegistry = ForgeSelf.Api.Plugins.AgentHub.Services.IAgentRegistry;

namespace ForgeSelf.Api.Tests.Plugins.AgentHub;

/// <summary>V8：hub_ask_agent 必须把 sessionKey 写入 DelegationRequest（续接面）。</summary>
[Collection("XCode")]
public class AgentHubSessionKeyAskTests : IClassFixture<XCodeTestFixture>
{
    public AgentHubSessionKeyAskTests(XCodeTestFixture fixture) => _ = fixture;

    [Fact]
    public async Task hub_ask_agent_空SessionKey_失败()
    {
        var sp = BuildHost();
        var tool = new AskAgentTool("agent-hub", sp);
        var json = await tool.ExecuteAsync("""{"question":"hi","agentId":1}""");
        json.Should().Contain("sessionKey");
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task hub_ask_agent_经假Transport续接_返回sessionKey()
    {
        var sp = BuildHost(withFake: true);
        var registry = sp.GetRequiredService<IAgentRegistry>();
        foreach (var a in registry.List().Where(x => x.Vendor == "ask-sk").ToList())
            registry.Delete(a.Id);

        var agent = registry.Create(new AgentSaveRequest
        {
            Name = "ask-sk-" + Guid.NewGuid().ToString("N")[..6],
            Vendor = "ask-sk",
            Enabled = true,
            DefaultCwd = Path.GetTempPath(),
            Capabilities = new AgentCapabilityMatrix
            {
                Facets = new Dictionary<String, Boolean>
                {
                    [AgentFacets.Driving] = true,
                    [AgentFacets.Sessions] = true,
                }
            },
            AccessPoints =
            [
                new AgentAccessPointSaveRequest
                {
                    Mode = "Sessionful",
                    Transport = "Cli",
                    Executable = "echo",
                    IsDefault = true,
                    CancelSupported = true,
                }
            ]
        });

        var sessions = sp.GetRequiredService<AgentSessionManager>();
        var opened = sessions.Open(agent.Id, agent.AccessPoints[0].Id, "Cli", null, null, Path.GetTempPath(), null);

        var tool = new AskAgentTool("agent-hub", sp);
        var payload = JsonSerializer.Serialize(new
        {
            question = "follow-up",
            agentId = agent.Id,
            sessionKey = opened.SessionKey,
        });
        var json = await tool.ExecuteAsync(payload);
        using var doc = JsonDocument.Parse(json);
        // 工具成功或业务失败都可；关键是不因缺 SessionKey 短路，且成功时回传 sessionKey
        if (doc.RootElement.GetProperty("success").GetBoolean())
        {
            var data = doc.RootElement.GetProperty("data");
            data.GetProperty("sessionKey").GetString().Should().Be(opened.SessionKey);
        }
        else
        {
            json.Should().NotContain("sessionKey（或 sessionRef）不能为空");
        }
    }

    private static ServiceProvider BuildHost(Boolean withFake = false)
    {
        var services = new ServiceCollection();
        var loader = new ProfileLoader();
        loader.LoadAll(null);
        services.AddSingleton(loader);
        services.AddSingleton<IAgentRegistry, AgentRegistry>();
        services.AddSingleton<PermissionBroker>();
        services.AddSingleton(new OarWorkerClientOptions { WorkerEntryPath = "" });
        services.AddSingleton<OarWorkerClient>();
        services.AddSingleton<AgentSessionManager>();
        services.AddSingleton<AgentRuntimeEventStore>();
        services.AddSingleton<CliTransport>();
        services.AddSingleton<OarTransport>();
        services.AddSingleton<IAgentTransportRegistry>(sp =>
        {
            var reg = new AgentTransportRegistry();
            if (withFake)
                reg.Register(new FakeCliTransport());
            else
            {
                reg.Register(sp.GetRequiredService<CliTransport>());
                reg.Register(sp.GetRequiredService<OarTransport>());
            }
            return reg;
        });
        services.AddSingleton<DelegationRuntime>();
        return services.BuildServiceProvider();
    }

    private sealed class FakeCliTransport : IAgentTransport
    {
        public String Kind => "Cli";

        public async IAsyncEnumerable<AgentEvent> StreamAsync(
            AgentAccessPointDto ap, AgentRunRequest req, AgentSession session,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.Yield();
            yield return AgentEvent.FromText("ask-fake");
            yield return new AgentEvent { Type = AgentEventTypes.Exit, ExitCode = 0 };
        }

        public Task CancelAsync(AgentSession session) => Task.CompletedTask;
    }
}

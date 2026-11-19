using FluentAssertions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AgentHub.Models;
using ForgeSelf.Api.Plugins.AgentHub.Profiles;
using ForgeSelf.Api.Plugins.AgentHub.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using TaskStatus = ForgeSelf.Api.Plugins.AgentHub.Services.TaskStatus;
using IAgentRegistry = ForgeSelf.Api.Plugins.AgentHub.Services.IAgentRegistry;

namespace ForgeSelf.Api.Tests.Plugins.AgentHub;

/// <summary>P3：IAgentRuntime Provider 同实例注册与 Submit/List/Cancel 面。</summary>
[Collection("XCode")]
public class AgentHubRuntimeProviderTests : IClassFixture<XCodeTestFixture>
{
    public AgentHubRuntimeProviderTests(XCodeTestFixture fixture) => _ = fixture;

    [Fact]
    public void 同实例_MS_DI与构造引用一致()
    {
        var services = new ServiceCollection();
        var provider = new AgentHubRuntimeProvider(services.BuildServiceProvider());
        services.AddSingleton(provider);
        services.AddSingleton<IAgentRuntime>(provider);
        var sp = services.BuildServiceProvider();

        var a = sp.GetRequiredService<IAgentRuntime>();
        var b = sp.GetRequiredService<AgentHubRuntimeProvider>();
        ReferenceEquals(a, b).Should().BeTrue();
        ReferenceEquals(a, provider).Should().BeTrue();
    }

    [Fact]
    public async Task ListAgentsAsync_含登记Agent()
    {
        var sp = BuildHost();
        var registry = sp.GetRequiredService<IAgentRegistry>();
        foreach (var a in registry.List().Where(x => x.Vendor == "p3-list").ToList())
            registry.Delete(a.Id);

        registry.Create(new AgentSaveRequest
        {
            Name = "p3-list-" + Guid.NewGuid().ToString("N")[..6],
            Vendor = "p3-list",
            Enabled = true,
            DefaultCwd = Path.GetTempPath(),
        });

        var runtime = new AgentHubRuntimeProvider(sp);
        var list = await runtime.ListAgentsAsync();
        list.Should().Contain(d => d.Vendor == "p3-list");
    }

    [Fact]
    public async Task SubmitAsync_空提示词_返回Failed句柄()
    {
        var sp = BuildHost();
        var runtime = new AgentHubRuntimeProvider(sp);
        var handle = await runtime.SubmitAsync(new AgentTaskRequest { Prompt = "  " });
        handle.Status.Should().Be(TaskStatus.Failed);
        handle.TaskKey.Should().BeEmpty();
    }

    [Fact]
    public async Task CancelAsync_不存在任务_Rejected()
    {
        var sp = BuildHost();
        var runtime = new AgentHubRuntimeProvider(sp);
        var r = await runtime.CancelAsync("no-such-task-key");
        r.Accepted.Should().BeFalse();
        r.Code.Should().Be("not_found");
    }

    [Fact]
    public async Task Submit_经假Transport入队_GetTask可读()
    {
        var sp = BuildHost(withFakeTransport: true);
        var registry = sp.GetRequiredService<IAgentRegistry>();
        foreach (var a in registry.List().Where(x => x.Vendor == "p3-sub").ToList())
            registry.Delete(a.Id);

        var agent = registry.Create(new AgentSaveRequest
        {
            Name = "p3-sub-" + Guid.NewGuid().ToString("N")[..6],
            Vendor = "p3-sub",
            Enabled = true,
            DefaultCwd = Path.GetTempPath(),
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

        var runtime = new AgentHubRuntimeProvider(sp);
        var handle = await runtime.SubmitAsync(new AgentTaskRequest
        {
            Prompt = "hello-p3",
            AgentId = agent.Id,
            Cwd = Path.GetTempPath(),
            PermissionMode = "read-only",
            CreatedBy = "test",
        });

        handle.TaskKey.Should().NotBeNullOrWhiteSpace();
        handle.Status.Should().BeOneOf(TaskStatus.Queued, TaskStatus.Running, TaskStatus.Succeeded, TaskStatus.Failed);

        // 等后台跑完（假 transport 很快）
        AgentTaskSnapshot? snap = null;
        for (var i = 0; i < 50; i++)
        {
            snap = await runtime.GetTaskAsync(handle.TaskKey);
            if (snap?.Terminal == true) break;
            await Task.Delay(50);
        }

        snap.Should().NotBeNull();
        snap!.TaskKey.Should().Be(handle.TaskKey);
    }

    private static ServiceProvider BuildHost(Boolean withFakeTransport = false)
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
        services.AddSingleton<CliTransport>();
        services.AddSingleton<OarTransport>();

        if (withFakeTransport)
        {
            services.AddSingleton<IAgentTransportRegistry>(sp =>
            {
                var reg = new AgentTransportRegistry();
                reg.Register(new FakeCliTransport());
                return reg;
            });
        }
        else
        {
            services.AddSingleton<IAgentTransportRegistry>(sp =>
            {
                var reg = new AgentTransportRegistry();
                reg.Register(sp.GetRequiredService<CliTransport>());
                reg.Register(sp.GetRequiredService<OarTransport>());
                return reg;
            });
        }

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
            yield return AgentEvent.FromText("p3-fake");
            yield return new AgentEvent { Type = AgentEventTypes.Exit, ExitCode = 0 };
        }

        public Task CancelAsync(AgentSession session) => Task.CompletedTask;
    }
}

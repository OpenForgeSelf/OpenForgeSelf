using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AgentHub.Controllers;
using ForgeSelf.Api.Plugins.AgentHub.Models;
using ForgeSelf.Api.Plugins.AgentHub.Profiles;
using ForgeSelf.Api.Plugins.AgentHub.Services;
using ForgeSelf.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using TaskStatus = ForgeSelf.Api.Plugins.AgentHub.Services.TaskStatus;
using IAgentRegistry = ForgeSelf.Api.Plugins.AgentHub.Services.IAgentRegistry;

namespace ForgeSelf.Api.Tests.Plugins.AgentHub;

/// <summary>
/// P4 消费者试点：兄弟插件经 <c>ctx.Get&lt;IAgentRuntime&gt;()</c> 取用能力接缝。
/// 覆盖：每次 Get、缺席降级、Worker 不可用时 Cli 仍可 Submit、Session 对外 DTO 不泄漏 OarSessionId。
/// </summary>
[Collection("XCode")]
public class AgentHubRuntimeConsumerPilotTests : IClassFixture<XCodeTestFixture>
{
    public AgentHubRuntimeConsumerPilotTests(XCodeTestFixture fixture) => _ = fixture;

    /// <summary>消费方样例：禁止字段缓存；每次方法内 Get。</summary>
    private sealed class SampleRuntimeConsumer
    {
        private readonly IContext _ctx;

        public SampleRuntimeConsumer(IContext ctx) => _ctx = ctx;

        public IAgentRuntime? GetRuntime() => _ctx.Get<IAgentRuntime>();

        public async Task<string> ListOrDegradeAsync()
        {
            var rt = _ctx.Get<IAgentRuntime>();
            if (rt == null) return "degraded:no-runtime";
            var list = await rt.ListAgentsAsync();
            return $"ok:{list.Count}";
        }
    }

    [Fact]
    public async Task 消费者_每次Get_同实例且禁止字段缓存语义()
    {
        var (ctx, _) = BuildConsumerHost();
        var consumer = new SampleRuntimeConsumer(ctx);

        var a = consumer.GetRuntime();
        var b = consumer.GetRuntime();
        a.Should().NotBeNull();
        ReferenceEquals(a, b).Should().BeTrue("同实例 Register 后每次 Get 应命中同一提供方");

        var msg = await consumer.ListOrDegradeAsync();
        msg.Should().StartWith("ok:");
    }

    [Fact]
    public async Task 消费者_未Register时_Get为null_走降级()
    {
        var ctx = new Context();
        var consumer = new SampleRuntimeConsumer(ctx);
        consumer.GetRuntime().Should().BeNull();
        (await consumer.ListOrDegradeAsync()).Should().Be("degraded:no-runtime");
    }

    [Fact]
    public async Task Worker不可用时_Cli路径仍可Submit_经Get取得的Runtime()
    {
        // WorkerEntryPath 空 = OAR unavailable；AccessPoint Transport=Cli + Fake transport 不启真实进程
        var (ctx, host) = BuildConsumerHost(withFakeCli: true);
        var registry = host.GetRequiredService<IAgentRegistry>();
        foreach (var a in registry.List().Where(x => x.Vendor == "p4-cli").ToList())
            registry.Delete(a.Id);

        var agent = registry.Create(new AgentSaveRequest
        {
            Name = "p4-cli-" + Guid.NewGuid().ToString("N")[..6],
            Vendor = "p4-cli",
            Enabled = true,
            DefaultCwd = Path.GetTempPath(),
            AccessPoints =
            [
                new AgentAccessPointSaveRequest
                {
                    Mode = "OneShot",
                    Transport = "Cli",
                    Executable = "echo",
                    IsDefault = true,
                    CancelSupported = true,
                }
            ]
        });

        var runtime = ctx.Get<IAgentRuntime>();
        runtime.Should().NotBeNull();

        var listed = await runtime!.ListAgentsAsync();
        listed.Should().Contain(d => d.AgentId == agent.Id && d.Transport == "Cli");

        var handle = await runtime.SubmitAsync(new AgentTaskRequest
        {
            Prompt = "p4-cli-fallback",
            AgentId = agent.Id,
            Cwd = Path.GetTempPath(),
            PermissionMode = "read-only",
            CreatedBy = "consumer-pilot",
        });
        handle.TaskKey.Should().NotBeNullOrWhiteSpace();
        handle.Status.Should().BeOneOf(
            TaskStatus.Queued, TaskStatus.Running, TaskStatus.Succeeded, TaskStatus.Failed);

        AgentTaskSnapshot? snap = null;
        for (var i = 0; i < 50; i++)
        {
            snap = await runtime.GetTaskAsync(handle.TaskKey);
            if (snap?.Terminal == true) break;
            await Task.Delay(50);
        }

        snap.Should().NotBeNull();
        snap!.Terminal.Should().BeTrue();
        snap.Status.Should().Be(TaskStatus.Succeeded);
    }

    [Fact]
    public void Session对外DTO_不得含OarSessionId属性_防泄漏守卫()
    {
        typeof(SessionDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Should().NotContain("OarSessionId");

        typeof(SessionDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Should().Contain("SessionKey");

        var json = JsonSerializer.Serialize(
            new SessionDto { SessionKey = "abc", Status = "Open" },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        json.Should().Contain("sessionKey");
        json.Should().NotContain("oarSessionId");
    }

    [Fact]
    public void SessionKey可与Worker侧OarSessionId解耦_库内存内外不同值()
    {
        // 现状：OarTransport 常把 Worker sessionKey 对齐为对外 SessionKey（便于 cancel）。
        // 契约允许解耦：库内可同时存独立 SessionKey 与 OarSessionId；对外 DTO 只暴露 SessionKey。
        // P4 不落地独立 GUID 映射表，仅守卫此边界。
        var mgr = new AgentSessionManager();
        var opened = mgr.Open(
            agentId: 1,
            accessPointId: 1,
            transport: "Oar",
            runtimeId: "scripted",
            oarSessionId: "worker-inner-xyz",
            cwd: Path.GetTempPath(),
            model: null,
            sessionKey: "public-session-key");

        opened.SessionKey.Should().Be("public-session-key");
        opened.OarSessionId.Should().Be("worker-inner-xyz");

        var dto = new SessionDto
        {
            SessionKey = opened.SessionKey,
            Status = opened.Status,
            RuntimeId = opened.RuntimeId,
        };
        dto.SessionKey.Should().Be("public-session-key");
        typeof(SessionDto).GetProperty("OarSessionId").Should().BeNull();

        mgr.Close(opened.SessionKey);
    }

    /// <summary>
    /// 模拟 PluginManager.ProvideHostServices seed + AgentHubPlugin Apply 注册姿势。
    /// </summary>
    private static (IContext ctx, ServiceProvider host) BuildConsumerHost(bool withFakeCli = false)
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

        if (withFakeCli)
        {
            services.AddSingleton<IAgentTransportRegistry>(_ =>
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
        var host = services.BuildServiceProvider();

        var ctx = new Context();
        ctx.Register(typeof(IServiceProvider), host);

        var runtimeProvider = new AgentHubRuntimeProvider(ctx);
        ctx.Register<IAgentRuntime>(runtimeProvider);

        return (ctx, host);
    }

    private sealed class FakeCliTransport : IAgentTransport
    {
        public string Kind => "Cli";

        public async IAsyncEnumerable<AgentEvent> StreamAsync(
            AgentAccessPointDto ap, AgentRunRequest req, AgentSession session,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.Yield();
            session.SessionRef = "cli-session-" + Guid.NewGuid().ToString("N")[..8];
            yield return AgentEvent.FromText("p4-consumer-fake");
            yield return new AgentEvent { Type = AgentEventTypes.Exit, ExitCode = 0 };
        }

        public Task CancelAsync(AgentSession session) => Task.CompletedTask;
    }
}

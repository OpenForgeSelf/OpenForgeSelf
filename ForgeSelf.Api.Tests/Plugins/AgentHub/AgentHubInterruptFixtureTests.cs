using FluentAssertions;
using ForgeSelf.Api.Plugins.AgentHub.Entities;
using ForgeSelf.Api.Plugins.AgentHub.Models;
using ForgeSelf.Api.Plugins.AgentHub.Profiles;
using ForgeSelf.Api.Plugins.AgentHub.Services;
using Xunit;
using TaskStatus = ForgeSelf.Api.Plugins.AgentHub.Services.TaskStatus;

namespace ForgeSelf.Api.Tests.Plugins.AgentHub;

/// <summary>
/// Gap #25：crash/interrupt fixture。
/// Interrupted Task 不得落成 Succeeded；Session 持久化 resume ≠ 旧进程复活。
/// </summary>
[Collection("XCode")]
public class AgentHubInterruptFixtureTests : IClassFixture<XCodeTestFixture>
{
    public AgentHubInterruptFixtureTests(XCodeTestFixture fixture)
    {
        _ = fixture;
        AgentDefinition.Meta.Cache.Clear("test reset");
        AgentAccessPoint.Meta.Cache.Clear("test reset");
        DelegationTask.Meta.Cache.Clear("test reset");
        AgentHubSession.Meta.Cache.Clear("test reset");
    }

    [Fact]
    public void Interrupted_是终态且不等于Succeeded()
    {
        TaskStatus.IsTerminal(TaskStatus.Interrupted).Should().BeTrue();
        TaskStatus.Interrupted.Should().NotBe(TaskStatus.Succeeded);
        TaskStatus.Terminal.Should().Contain(TaskStatus.Interrupted);
        TaskStatus.Terminal.Should().Contain(TaskStatus.Succeeded);
    }

    [Fact]
    public void RecoverOrphans_遗留Running_标Interrupted_不得Succeeded()
    {
        var runtime = MakeRuntime(out var registry);
        // 用内置 profile vendor，保证 Create 带出默认 AccessPoint
        var agentId = MakeAgent(registry, "opencode");
        var ap = registry.GetDefaultAccessPoint(agentId);
        ap.Should().NotBeNull("opencode profile 应带默认交互口");
        var apId = ap!.Id;

        var entity = new DelegationTask
        {
            AgentId = agentId,
            AccessPointId = apId,
            Prompt = "crash-fixture-running",
            Status = TaskStatus.Running,
            PermissionMode = "read-only",
            CreatedBy = "interrupt-fixture",
        };
        entity.Insert();

        runtime.RecoverOrphans().Should().BeGreaterThan(0);

        var after = DelegationTask.FindById(entity.Id)!;
        after.Status.Should().Be(TaskStatus.Interrupted);
        after.Status.Should().NotBe(TaskStatus.Succeeded);
        after.ErrorCode.Should().Be("host_restart");
        TaskStatus.IsTerminal(after.Status).Should().BeTrue();

        registry.Delete(agentId);
    }

    [Fact]
    public void Session_Interrupt后仍Interrupted_Open新会话不等于进程复活()
    {
        var mgr = new AgentSessionManager();
        var opened = mgr.Open(
            agentId: 42,
            accessPointId: 1,
            transport: "Oar",
            runtimeId: "scripted",
            oarSessionId: "worker-proc-dead-1",
            cwd: "/tmp",
            model: null,
            generation: 1);

        opened.Status.Should().Be(AgentHubSession.SessionStatus.Open);
        var oldOar = opened.OarSessionId;
        var oldKey = opened.SessionKey;
        var oldGen = opened.Generation;

        mgr.InterruptOpenSessions().Should().BeGreaterThanOrEqualTo(1);

        var interrupted = mgr.Find(oldKey)!;
        interrupted.Status.Should().Be(AgentHubSession.SessionStatus.Interrupted);
        interrupted.OarSessionId.Should().Be(oldOar, "库内仍记旧 OarSessionId，但不表示进程仍活");

        // 「resume」= 新开一条 Session 记录（新 SessionKey / Generation），不是把 Interrupted 翻回 Open 假装进程还在
        var resumed = mgr.Open(
            agentId: 42,
            accessPointId: 1,
            transport: "Oar",
            runtimeId: "scripted",
            oarSessionId: "worker-proc-new-2",
            cwd: "/tmp",
            model: null,
            generation: oldGen + 1);

        resumed.SessionKey.Should().NotBe(oldKey, "新会话须新 SessionKey，禁止复用旧键伪装复活");
        resumed.Generation.Should().Be(oldGen + 1);
        resumed.OarSessionId.Should().NotBe(oldOar);
        resumed.Status.Should().Be(AgentHubSession.SessionStatus.Open);

        mgr.Find(oldKey)!.Status.Should().Be(
            AgentHubSession.SessionStatus.Interrupted,
            "旧行保持 Interrupted；resume ≠ process revive");
    }

    private static DelegationRuntime MakeRuntime(out AgentRegistry registry)
    {
        var loader = new ProfileLoader();
        loader.LoadAll(null);
        registry = new AgentRegistry(loader);
        var broker = new PermissionBroker(registry);
        var transports = new AgentTransportRegistry();
        transports.Register(new FakeTransport());
        return new DelegationRuntime(registry, loader, broker, transports);
    }

    private static Int32 MakeAgent(AgentRegistry registry, String vendor)
    {
        foreach (var a in registry.List().Where(x => x.Vendor == vendor).ToList())
            registry.Delete(a.Id);

        var dto = registry.Create(new AgentSaveRequest
        {
            Name = $"interrupt-fx-{Guid.NewGuid():N}"[..20],
            Vendor = vendor,
            Enabled = true,
            DefaultCwd = Path.GetTempPath(),
        });
        return dto.Id;
    }

    private sealed class FakeTransport : IAgentTransport
    {
        public String Kind => "Cli";

        public async IAsyncEnumerable<AgentEvent> StreamAsync(
            AgentAccessPointDto ap, AgentRunRequest req, AgentSession session,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.Yield();
            yield return AgentEvent.FromText("interrupt-fixture");
            yield return new AgentEvent { Type = AgentEventTypes.Exit, ExitCode = 0 };
        }

        public Task CancelAsync(AgentSession session) => Task.CompletedTask;
    }
}

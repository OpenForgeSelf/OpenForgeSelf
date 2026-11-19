using FluentAssertions;
using ForgeSelf.Api.Plugins.AgentHub.Entities;
using ForgeSelf.Api.Plugins.AgentHub.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AgentHub;

/// <summary>P2：AgentHubSession 持久化与 SessionKey。</summary>
[Collection("XCode")]
public class AgentHubSessionManagerTests : IClassFixture<XCodeTestFixture>
{
    public AgentHubSessionManagerTests(XCodeTestFixture fixture)
    {
        _ = fixture;
        AgentHubSession.Meta.Cache.Clear("test reset");
    }

    [Fact]
    public void Open_Find_Close_SessionKey闭环()
    {
        var mgr = new AgentSessionManager();
        var opened = mgr.Open(
            agentId: 1,
            accessPointId: 2,
            transport: "Oar",
            runtimeId: "scripted",
            oarSessionId: "oar-inner-1",
            cwd: "/tmp",
            model: null,
            generation: 1);

        opened.SessionKey.Should().NotBeNullOrWhiteSpace();
        opened.Status.Should().Be(AgentHubSession.SessionStatus.Open);
        opened.RuntimeId.Should().Be("scripted");
        opened.OarSessionId.Should().Be("oar-inner-1");

        var found = mgr.Find(opened.SessionKey);
        found.Should().NotBeNull();
        found!.Id.Should().Be(opened.Id);

        mgr.Touch(found, generation: 2, lastTaskId: 99);
        var again = mgr.Find(opened.SessionKey)!;
        again.Generation.Should().Be(2);
        again.LastTaskId.Should().Be(99);

        mgr.Close(opened.SessionKey);
        mgr.Find(opened.SessionKey)!.Status.Should().Be(AgentHubSession.SessionStatus.Closed);
    }

    [Fact]
    public void InterruptOpenSessions_把遗留Open标Interrupted()
    {
        var mgr = new AgentSessionManager();
        var a = mgr.Open(1, 1, "Oar", "scripted", "x", null, null);
        var n = mgr.InterruptOpenSessions();
        n.Should().BeGreaterThanOrEqualTo(1);
        mgr.Find(a.SessionKey)!.Status.Should().Be(AgentHubSession.SessionStatus.Interrupted);
    }
}

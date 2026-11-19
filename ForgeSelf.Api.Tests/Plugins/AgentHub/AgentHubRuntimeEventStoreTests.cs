using FluentAssertions;
using ForgeSelf.Api.Plugins.AgentHub.Entities;
using ForgeSelf.Api.Plugins.AgentHub.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AgentHub;

/// <summary>gap-close：双层事件最小耐久路径 — AgentHubSequence / OarSequence 单调。</summary>
[Collection("XCode")]
public class AgentHubRuntimeEventStoreTests : IClassFixture<XCodeTestFixture>
{
    public AgentHubRuntimeEventStoreTests(XCodeTestFixture fixture)
    {
        _ = fixture;
        AgentHubSession.Meta.Cache.Clear("test reset");
        AgentRuntimeRecord.Meta.Cache.Clear("test reset");
    }

    [Fact]
    public void Append_分配单调AgentHubSequence并回写Session水位()
    {
        var mgr = new AgentSessionManager();
        var store = new AgentRuntimeEventStore(mgr);
        var session = mgr.Open(1, 1, "Oar", "scripted", "inner", null, null, generation: 1);

        var a = store.Append(session.SessionKey, "meta", """{"stage":"open"}""");
        var b = store.Append(session.SessionKey, "text", "hello");
        var c = store.Append(session.SessionKey, "text", "world", oarSequence: 99);

        a.AgentHubSequence.Should().BeGreaterThan(0);
        b.AgentHubSequence.Should().Be(a.AgentHubSequence + 1);
        c.AgentHubSequence.Should().Be(b.AgentHubSequence + 1);

        a.OarSequence.Should().Be(1);
        b.OarSequence.Should().Be(2);
        c.OarSequence.Should().BeGreaterThan(b.OarSequence);

        var touched = mgr.Find(session.SessionKey)!;
        touched.AgentHubSequence.Should().Be(c.AgentHubSequence);
        touched.OarSequence.Should().Be(c.OarSequence);
        touched.Generation.Should().Be(1);

        var listed = store.List(session.SessionKey, afterAgentHubSequence: a.AgentHubSequence);
        listed.Should().HaveCount(2);
        listed[0].AgentHubSequence.Should().Be(b.AgentHubSequence);
        listed.Select(x => x.SessionKey).Should().OnlyContain(k => k == session.SessionKey);
    }

    [Fact]
    public void Append_长载荷截断()
    {
        var mgr = new AgentSessionManager();
        var store = new AgentRuntimeEventStore(mgr);
        var session = mgr.Open(1, 1, "Oar", "scripted", null, null, null);
        var huge = new string('x', AgentRuntimeEventStore.MaxPayloadChars + 50);

        var rec = store.Append(session.SessionKey, "blob", huge);
        rec.Truncated.Should().BeTrue();
        rec.PayloadJson!.Length.Should().Be(AgentRuntimeEventStore.MaxPayloadChars);
    }
}

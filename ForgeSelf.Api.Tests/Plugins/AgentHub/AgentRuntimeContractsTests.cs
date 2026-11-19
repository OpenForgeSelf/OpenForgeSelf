using FluentAssertions;
using ForgeSelf.Abstractions;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AgentHub;

/// <summary>P1：IAgentRuntime 契约形状与无 OAR 类型泄漏守卫。</summary>
public class AgentRuntimeContractsTests
{
    [Fact]
    public void IAgentRuntime_方法面_对齐架构草图()
    {
        var t = typeof(IAgentRuntime);
        t.IsInterface.Should().BeTrue();
        t.GetMethod("ListAgentsAsync").Should().NotBeNull();
        t.GetMethod("SubmitAsync").Should().NotBeNull();
        t.GetMethod("GetTaskAsync").Should().NotBeNull();
        t.GetMethod("WatchTaskAsync").Should().NotBeNull();
        t.GetMethod("SendInputAsync").Should().NotBeNull();
        t.GetMethod("CancelAsync").Should().NotBeNull();
        t.GetMethod("CloseSessionAsync").Should().NotBeNull();
    }

    [Fact]
    public void AgentControlResult_禁止纯bool语义_提供Ok与Reject()
    {
        var ok = AgentControlResult.Ok("ok", null);
        ok.Accepted.Should().BeTrue();
        ok.Code.Should().Be("ok");

        var reject = AgentControlResult.Reject("unsupported", "no steer");
        reject.Accepted.Should().BeFalse();
        reject.Code.Should().Be("unsupported");
        reject.Reason.Should().Be("no steer");
    }

    [Fact]
    public void AgentRuntimeContracts_源码_不含OAR包名泄漏()
    {
        var path = LocateContractsFile();
        path.Should().NotBeNull("应能定位 AgentRuntimeContracts.cs");
        var text = File.ReadAllText(path!);
        text.Should().NotContain("@botiverse/oar");
        text.Should().NotContain("botiverse");
        text.Should().Contain("IAgentRuntime");
        text.Should().Contain("AgentControlResult");
    }

    private static string? LocateContractsFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "ForgeSelf.Abstractions", "AgentRuntimeContracts.cs");
            if (File.Exists(candidate) && File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx")))
                return candidate;
            dir = dir.Parent;
        }

        return null;
    }
}

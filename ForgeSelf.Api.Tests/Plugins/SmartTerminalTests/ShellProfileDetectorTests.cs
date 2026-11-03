using System.Runtime.InteropServices;
using FluentAssertions;
using ForgeSelf.Api.Plugins.SmartTerminal.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.SmartTerminalTests;

public class ShellProfileDetectorTests
{
    [Fact]
    public void ListProfiles_IncludesPosixShells()
    {
        var detector = new ShellProfileDetector();
        var profiles = detector.ListProfiles();

        profiles.Should().Contain(p => p.Id == "bash");
        profiles.Should().Contain(p => p.Id == "zsh");
        profiles.Should().Contain(p => p.Id == "sh");
        profiles.Should().Contain(p => p.Id == "pwsh");
        profiles.Should().Contain(p => p.Id == "cmd");
        profiles.Should().Contain(p => p.Id == "powershell");
    }

    [Fact]
    public void PreferDefaultShellId_OnLinux_PrefersPosixWhenAvailable()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return; // Windows 偏好在另一条路径验证，本用例专盯 Linux/macOS 盒子

        var detector = new ShellProfileDetector();
        var id = detector.PreferDefaultShellId();
        id.Should().NotBeNullOrWhiteSpace();
        id.Should().BeOneOf("bash", "zsh", "sh", "fish", "dash", "pwsh");

        var bash = detector.Find("bash");
        bash.Should().NotBeNull();
        // 本 CI/BOX 几乎一定有 bash；若有则必须 Available
        if (File.Exists("/bin/bash") || File.Exists("/usr/bin/bash"))
            bash!.Available.Should().BeTrue();
    }

    [Fact]
    public void Linux_WindowsOnlyShells_AreMarkedUnavailable()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        var detector = new ShellProfileDetector();
        detector.Find("cmd")!.Available.Should().BeFalse();
        detector.Find("powershell")!.Available.Should().BeFalse();
    }
}

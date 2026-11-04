using FluentAssertions;
using ForgeSelf.Api.Plugins.SmartTerminal.Models;
using ForgeSelf.Api.Plugins.SmartTerminal.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.SmartTerminalTests;

/// <summary>
/// S5 / custom 拒绝：Launch 在建会话前失败，且文案可识别。
/// </summary>
public class LaunchGuardTests
{
    private sealed class StubShells : IShellProfileDetector
    {
        private readonly ShellProfileDto _bash = new()
        {
            Id = "bash",
            DisplayName = "Bash",
            Executable = "/bin/bash",
            Kind = "bash",
            Available = true
        };

        public IReadOnlyList<ShellProfileDto> ListProfiles() => new[] { _bash };
        public ShellProfileDto? Find(string shellId) =>
            string.Equals(shellId, "bash", StringComparison.OrdinalIgnoreCase) ? _bash : null;
        public string? PreferDefaultShellId() => "bash";
    }

    private sealed class StubClassifier : IPermissionClassifier
    {
        public PermissionMode CurrentMode { get; private set; } = PermissionMode.AskWrite;
        public void SetMode(PermissionMode mode) => CurrentMode = mode;
        public PermissionClassifyResult Classify(string command, PermissionMode? modeOverride = null) =>
            new() { Risk = CommandRiskClass.Read, Decision = ApprovalDecision.Allow, MatchedRule = "test:allow" };
        public void ReloadRules() { }
    }

    private sealed class StubAudit : IAuditLogStore
    {
        public List<AuditEventDto> Events { get; } = new();
        public void Append(AuditEventDto evt) => Events.Add(evt);
        public IReadOnlyList<AuditEventDto> Query(string? sessionId = null, string? kind = null, string? agentId = null, int take = 100, int skip = 0) =>
            Events;
    }

    private static TerminalSessionService CreateService(out StubAudit audit)
    {
        audit = new StubAudit();
        return new TerminalSessionService(new StubShells(), new StubClassifier(), audit);
    }

    [Fact]
    public void Launch_CustomProfile_ThrowsReadableMessage_NoSession()
    {
        var svc = CreateService(out _);
        var act = () => svc.Launch(new LaunchTerminalRequest
        {
            Profile = "custom:build-box",
            Host = "ignored",
            User = "ignored"
        });

        act.Should().Throw<ArgumentException>()
            .WithMessage("*本里程碑不支持 custom*");
        svc.ListSessions().Should().BeEmpty();
    }

    [Fact]
    public void Launch_InteractiveSshWithoutPassword_ThrowsNeedsPassword_NoSession()
    {
        var svc = CreateService(out var audit);
        var act = () => svc.Launch(new LaunchTerminalRequest
        {
            Profile = "ssh",
            Host = "10.0.0.1",
            User = "root",
            Auth = "interactive",
            Password = null
        });

        act.Should().Throw<InteractivePasswordRequiredException>()
            .WithMessage("*先框后标签*");
        svc.ListSessions().Should().BeEmpty("无密 interactive 不得创建会话（先框后标签）");
        audit.Events.Should().BeEmpty("未建会话则不应落 launch 审计");
    }

    [Fact]
    public void Launch_DefaultAuthWithoutPassword_TreatedAsInteractive_NoSession()
    {
        var svc = CreateService(out _);
        var act = () => svc.Launch(new LaunchTerminalRequest
        {
            Type = "ssh",
            Host = "10.0.0.1",
            User = "ops"
            // Auth 缺省 + 无密 → interactive
        });

        act.Should().Throw<InteractivePasswordRequiredException>();
        svc.ListSessions().Should().BeEmpty();
    }
}

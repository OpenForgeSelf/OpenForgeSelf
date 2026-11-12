using FluentAssertions;
using ForgeSelf.Api.Plugins.SmartTerminal.Models;
using ForgeSelf.Api.Plugins.SmartTerminal.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.SmartTerminalTests;

/// <summary>v2 主路径：本机会话、commands、缺密/缺密钥、Agent 不得自批。</summary>
public class SmartTerminalV2Tests
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

        public PermissionClassifyResult Classify(string command, PermissionMode? modeOverride = null)
        {
            var text = command ?? "";
            if (text.Contains("rm -rf", StringComparison.Ordinal) || text.Contains("FORMAT", StringComparison.Ordinal))
            {
                return new PermissionClassifyResult
                {
                    Risk = CommandRiskClass.Deny,
                    Decision = ApprovalDecision.Deny,
                    MatchedRule = "test:deny",
                    Reason = "硬拒绝"
                };
            }
            if (text.Contains("touch ", StringComparison.Ordinal) || text.Contains("sudo ", StringComparison.Ordinal))
            {
                return new PermissionClassifyResult
                {
                    Risk = CommandRiskClass.Write,
                    Decision = ApprovalDecision.Ask,
                    MatchedRule = "test:ask",
                    Reason = "写操作需审批"
                };
            }
            return new PermissionClassifyResult
            {
                Risk = CommandRiskClass.Read,
                Decision = ApprovalDecision.Allow,
                MatchedRule = "test:allow",
                Reason = "只读"
            };
        }

        public void ReloadRules() { }
    }

    private sealed class StubAudit : IAuditLogStore
    {
        public void Append(AuditEventDto ev) { }
        public IReadOnlyList<AuditEventDto> Query(string? sessionId, string? kind, string? agentId, int take, int skip)
            => Array.Empty<AuditEventDto>();
    }

    private static TerminalSessionService NewService()
        => new(new StubShells(), new StubClassifier(), new StubAudit(), new StMemoryCatalog());

    [Fact]
    public void OpenLocalSession_is_connected_and_has_system_block()
    {
        var svc = NewService();
        var session = svc.OpenSession(new CreateSessionRequest { Kind = "local" }, "ui", isAgent: false);

        session.Status.Should().Be("connected");
        session.Kind.Should().Be("local");
        session.PasswordProvided.Should().BeFalse();
        session.Id.Should().StartWith("s-");
        session.Blocks.Should().NotBeNull();
        session.Blocks!.Should().Contain(b => b.Kind == "system" && b.Command == "connected");
        svc.ListOpenSessions().Should().Contain(s => s.Id == session.Id);
    }

    [Fact]
    public void SubmitCommand_echo_returns_output()
    {
        var svc = NewService();
        var session = svc.OpenSession(new CreateSessionRequest { Kind = "local" }, "ui", isAgent: false);

        var result = svc.SubmitCommand(session.Id, new SubmitCommandRequest { Command = "echo st-v2-ok" }, "ui");

        result.Decision.Should().Be("allow");
        result.Risk.Should().Be("read");
        result.Block.Should().NotBeNull();
        result.Block!.Status.Should().Be("done");
        result.Block.Output.Should().Contain("st-v2-ok");
        result.Block.ExitCode.Should().Be(0);
    }

    [Fact]
    public void Ssh_interactive_without_password_is_409_and_creates_no_session()
    {
        var svc = NewService();
        var act = () => svc.OpenSession(new CreateSessionRequest
        {
            Kind = "ssh",
            Host = "db.example",
            User = "ops",
            Auth = "interactive"
        }, "ui", isAgent: false);

        var ex = act.Should().Throw<SmartTerminalException>().Which;
        ex.Code.Should().Be("password_required");
        ex.StatusCode.Should().Be(409);
        svc.ListOpenSessions().Should().BeEmpty();
    }

    [Fact]
    public void Ssh_key_without_secret_or_identity_is_credential_required()
    {
        var svc = NewService();
        var act = () => svc.OpenSession(new CreateSessionRequest
        {
            Kind = "ssh",
            Host = "db.example",
            User = "ops",
            Auth = "key"
        }, "ui", isAgent: false);

        var ex = act.Should().Throw<SmartTerminalException>().Which;
        ex.Code.Should().Be("credential_required");
        ex.StatusCode.Should().Be(400);
        svc.ListOpenSessions().Should().BeEmpty();
    }

    [Fact]
    public void Agent_cannot_self_approve()
    {
        var svc = NewService();
        var session = svc.OpenSession(new CreateSessionRequest { Kind = "local" }, "ui", isAgent: false);
        var pending = svc.SubmitCommand(session.Id, new SubmitCommandRequest { Command = "touch /tmp/st-v2-x" }, "agent");

        pending.Status.Should().Be("awaiting_approval");
        pending.Approval.Should().NotBeNull();

        var act = () => svc.ResolveApprovalV2(pending.Approval!.Id, "approve", "agent", isAgent: true);
        var ex = act.Should().Throw<SmartTerminalException>().Which;
        ex.Code.Should().Be("forbidden");
        ex.StatusCode.Should().Be(403);

        var approved = svc.ResolveApprovalV2(pending.Approval!.Id, "approve", "ui", isAgent: false);
        approved.Decision.Should().Be("allow");
        approved.Block.Should().NotBeNull();
        approved.Block!.Command.Should().Contain("touch");
    }

    [Fact]
    public void Profile_rejects_password_and_key_without_handle()
    {
        var svc = NewService();
        var withPassword = () => svc.CreateProfile(new UpsertProfileRequest
        {
            Kind = "ssh",
            Host = "h",
            User = "u",
            Auth = "interactive",
            Password = "secret"
        }, "ui");
        withPassword.Should().Throw<SmartTerminalException>().Which.Code.Should().Be("password_not_persistable");

        var keyMissing = () => svc.CreateProfile(new UpsertProfileRequest
        {
            Kind = "ssh",
            Host = "h",
            User = "u",
            Auth = "key"
        }, "ui");
        keyMissing.Should().Throw<SmartTerminalException>().Which.Code.Should().Be("credential_required");
    }

    [Fact]
    public void Disconnect_then_command_is_session_disconnected()
    {
        var svc = NewService();
        var session = svc.OpenSession(new CreateSessionRequest { Kind = "local" }, "ui", isAgent: false);
        var disconnected = svc.Disconnect(session.Id);
        disconnected.Status.Should().Be("disconnected");
        disconnected.ProcessAlive.Should().BeFalse();

        var act = () => svc.SubmitCommand(session.Id, new SubmitCommandRequest { Command = "echo x" }, "ui");
        act.Should().Throw<SmartTerminalException>().Which.Code.Should().Be("session_disconnected");
    }

    [Fact]
    public void Close_removes_from_open_list()
    {
        var svc = NewService();
        var session = svc.OpenSession(new CreateSessionRequest { Kind = "local" }, "ui", isAgent: false);
        svc.CloseV2(session.Id);
        svc.ListOpenSessions().Should().BeEmpty();
        var act = () => svc.GetOpenSession(session.Id);
        act.Should().Throw<SmartTerminalException>().Which.Code.Should().Be("not_found");
    }

    [Fact]
    public void Denied_command_is_rejected_hard()
    {
        var svc = NewService();
        var session = svc.OpenSession(new CreateSessionRequest { Kind = "local" }, "ui", isAgent: false);
        var act = () => svc.SubmitCommand(session.Id, new SubmitCommandRequest { Command = "rm -rf /" }, "ui");
        var ex = act.Should().Throw<SmartTerminalException>().Which;
        ex.Code.Should().Be("rejected_hard");
        ex.StatusCode.Should().Be(400);
    }
}

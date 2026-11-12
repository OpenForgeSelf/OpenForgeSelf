using System.Diagnostics;
using FluentAssertions;
using ForgeSelf.Api.Plugins.SmartTerminal.Models;
using ForgeSelf.Api.Plugins.SmartTerminal.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.SmartTerminalTests;

/// <summary>
/// PC-011 改造回归（2026-11-05 51888 实测 P0）：泵改造曾吞掉 process.Start()，
/// 一切命令报「StandardOut has not been redirected」exit -1——既有单测全是分类器/守卫级，
/// 无一真正跑过进程。本测试走真实 cmd 子进程：建会话 → echo → 轮询输出 → 断言回显与 exit 0，
/// 顺带验证 PC-011 新范式（BaseStream 原始字节 + ProcessOutputLineBuffer）真能出完整行。
/// </summary>
public class TerminalExecutionIntegrationTests
{
    private sealed class StubShells : IShellProfileDetector
    {
        private static readonly ShellProfileDto Cmd = new()
        {
            Id = "cmd",
            DisplayName = "Command Prompt",
            Executable = "cmd.exe",
            ArgumentsTemplate = "/d /c {command}",
            Kind = "cmd",
            Available = true,
        };

        public IReadOnlyList<ShellProfileDto> ListProfiles() => new[] { Cmd };

        public ShellProfileDto? Find(string shellId) =>
            string.Equals(shellId, Cmd.Id, StringComparison.OrdinalIgnoreCase) ? Cmd : null;

        public string? PreferDefaultShellId() => Cmd.Id;
    }

    private sealed class AllowClassifier : IPermissionClassifier
    {
        public PermissionMode CurrentMode { get; private set; } = PermissionMode.Auto;

        public void SetMode(PermissionMode mode) => CurrentMode = mode;

        public PermissionClassifyResult Classify(string command, PermissionMode? modeOverride = null) =>
            new()
            {
                Risk = CommandRiskClass.Read,
                Decision = ApprovalDecision.Allow,
                MatchedRule = "test-allow",
                Reason = "regression",
            };

        public void ReloadRules() { }
    }

    private sealed class MemoryAudit : IAuditLogStore
    {
        private readonly List<AuditEventDto> _events = new();

        public void Append(AuditEventDto evt) => _events.Add(evt);

        public IReadOnlyList<AuditEventDto> Query(string? sessionId = null, string? kind = null, string? agentId = null, int take = 100, int skip = 0) =>
            _events
                .Where(e => (sessionId == null || e.SessionId == sessionId) && (kind == null || e.Kind == kind))
                .Skip(skip).Take(take).ToList();
    }

    [Fact]
    public async Task Echo_经真实子进程_输出可见且退出码0()
    {
        // 该回归按 51888 走真实 cmd 子进程；Linux 上 cmd.exe 不存在。POSIX 一等壳由 v2 SessionShell 覆盖。
        if (!OperatingSystem.IsWindows())
            return;

        var svc = new TerminalSessionService(new StubShells(), new AllowClassifier(), new MemoryAudit());
        var session = svc.CreateSession(new CreateSessionRequest { Title = "pc011-regression", ShellId = "cmd" });

        svc.ProposeOrRun(session.SessionId, "echo st-pc011-regression-ok");

        // StartExecution 是 fire-and-forget：轮询输出与命令状态（上限 15s）
        var snap = svc.GetOutput(session.SessionId);
        var deadline = Stopwatch.StartNew();
        while (deadline.ElapsedMilliseconds < 15000)
        {
            snap = svc.GetOutput(session.SessionId);
            if (snap.Chunks.Any(c => c.Stream == "stdout" && c.Text.Contains("st-pc011-regression-ok")))
                break;
            await Task.Delay(200);
        }

        var stdout = string.Join("\n", snap.Chunks.Where(c => c.Stream == "stdout").Select(c => c.Text));
        stdout.Should().Contain("st-pc011-regression-ok", "PC-011 泵必须把子进程 stdout 行送进输出快照。全部块: " + string.Join(" || ", snap.Chunks.Select(k => k.Stream + ":" + k.Text)));

        var record = svc.ListCommands(session.SessionId)
            .FirstOrDefault(c => c.CommandRedacted.Contains("st-pc011-regression-ok"));
        record.Should().NotBeNull();
        record!.Status.Should().Be("completed");
        record.ExitCode.Should().Be(0);
    }
}
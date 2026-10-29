using System.Text.Json;
using FluentAssertions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.McpCenter.Services;
using ForgeSelf.Core;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.McpCenterTests;

/// <summary>
/// PILOT-075（2026-10-29 用户拍板「放开管道和链式，每一个管道或者链式都走同样的拦截校验」）：
/// run_terminal_command 多段命令（| &amp;&amp; || ; 换行）经 pwsh -NoProfile -NonInteractive -Command
/// 执行完整命令串；守卫逐段过同一套白名单+破坏性红线，任一段被拒则整条拒绝且零子进程。
/// 重定向/反引号/裸 & 仍未放开。单段行为零变化（由既有各测试覆盖）。
/// </summary>
public class RunTerminalCommandChainExecutionTests
{
    private static RunTerminalCommandTool CreateTool() =>
        new(Mock.Of<IContext>());

    private static async Task<JsonElement> ExecuteAsync(string parameters)
    {
        var json = await CreateTool().ExecuteAsync(parameters);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static string NewTempDir()
    {
        var dir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "forgeself-chain-" + Guid.NewGuid().ToString("N"))).FullName;
        return dir;
    }

    [Fact]
    public async Task ChainedAllowlistedCommands_RunViaPwsh_AndReturnLastExitCode()
    {
        var dir = NewTempDir();
        try
        {
            var root = await ExecuteAsync(JsonSerializer.Serialize(new
            {
                command = "git --version && node --version",
                cwd = dir,
                timeoutSeconds = 30,
            }));

            root.GetProperty("success").GetBoolean().Should().BeTrue(
                because: "两段均在白名单内应执行，error: " + (root.TryGetProperty("error", out var e) ? e.GetString() : "<无>"));
            root.GetProperty("exitCode").GetInt32().Should().Be(0);
            (root.GetProperty("stdout").GetString() ?? string.Empty)
                .Should().Contain("git version").And.Contain("v", "node --version 的输出形如 v20.x");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task NewlineSegments_ExecuteInOrder_InSameCwd()
    {
        var dir = NewTempDir();
        try
        {
            // 段 1 写文件、段 2 读回——证明同 cwd 顺序执行且退出码取最后一段。
            var root = await ExecuteAsync(JsonSerializer.Serialize(new
            {
                command = "pwsh -NoProfile -Command Set-Content chain-probe.txt 'p075'\npwsh -NoProfile -Command Get-Content chain-probe.txt",
                cwd = dir,
                timeoutSeconds = 30,
            }));

            root.GetProperty("success").GetBoolean().Should().BeTrue(
                because: "两段 pwsh 均白名单内应执行，error: " + (root.TryGetProperty("error", out var e) ? e.GetString() : "<无>"));
            root.GetProperty("exitCode").GetInt32().Should().Be(0);
            (root.GetProperty("stdout").GetString() ?? string.Empty).Should().Contain("p075");
            File.Exists(Path.Combine(dir, "chain-probe.txt")).Should().BeTrue();
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task RealPipe_BetweenTwoNodeSegments_PassesData()
    {
        var dir = NewTempDir();
        try
        {
            // 真管道：node 段 1 输出 → 段 2 从 stdin 读回（pwsh 解析管道符，非手工拼接）。
            var root = await ExecuteAsync(JsonSerializer.Serialize(new
            {
                command = "node -e \"console.log('p075pipe')\" | node -e \"let d='';process.stdin.on('data',c=>d+=c).on('end',()=>console.log('got:'+d.trim()))\"",
                cwd = dir,
                timeoutSeconds = 30,
            }));

            root.GetProperty("success").GetBoolean().Should().BeTrue(
                because: "两段 node 均白名单内应执行，error: " + (root.TryGetProperty("error", out var e) ? e.GetString() : "<无>"));
            (root.GetProperty("stdout").GetString() ?? string.Empty).Should().Contain("got:p075pipe");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task ChainSecondSegmentNotAllowlisted_RejectedWithSegmentPrefix_ZeroSubprocess()
    {
        var dir = NewTempDir();
        try
        {
            var probe = Path.Combine(dir, "probe-must-survive.txt");
            File.WriteAllText(probe, "still here");

            // 若守卫漏放，多段路径会起 pwsh 执行整串，rm 会删掉探针文件。
            var root = await ExecuteAsync(JsonSerializer.Serialize(new
            {
                command = "git --version && rm -f probe-must-survive.txt",
                cwd = dir,
                timeoutSeconds = 30,
            }));

            root.GetProperty("success").GetBoolean().Should().BeFalse();
            (root.GetProperty("error").GetString() ?? string.Empty)
                .Should().Contain("segment 2/2").And.Contain("allowlist");
            File.Exists(probe).Should().BeTrue("被拒链必须零子进程——探针文件还在即为证据");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task ChainSecondSegmentDestructive_RejectedWithSegmentPrefix_ZeroSubprocess()
    {
        var dir = NewTempDir();
        try
        {
            var probe = Path.Combine(dir, "probe-must-survive.txt");
            File.WriteAllText(probe, "still here");

            var root = await ExecuteAsync(JsonSerializer.Serialize(new
            {
                command = "git --version && pwsh -Command Remove-Item probe-must-survive.txt",
                cwd = dir,
                timeoutSeconds = 30,
            }));

            root.GetProperty("success").GetBoolean().Should().BeFalse();
            (root.GetProperty("error").GetString() ?? string.Empty)
                .Should().Contain("segment 2/2").And.Contain("destructive");
            File.Exists(probe).Should().BeTrue();
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task Redirect_StillRejected_Integral()
    {
        var dir = NewTempDir();
        try
        {
            var root = await ExecuteAsync(JsonSerializer.Serialize(new
            {
                command = "git log > out.txt",
                cwd = dir,
                timeoutSeconds = 30,
            }));

            root.GetProperty("success").GetBoolean().Should().BeFalse();
            (root.GetProperty("error").GetString() ?? string.Empty)
                .Should().Contain("chained/piped").And.Contain("contains '>'");
            File.Exists(Path.Combine(dir, "out.txt")).Should().BeFalse("重定向未放开，不得落盘");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}

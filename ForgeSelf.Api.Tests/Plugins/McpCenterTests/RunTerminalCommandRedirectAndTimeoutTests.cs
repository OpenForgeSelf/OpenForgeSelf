using System.Text.Json;
using FluentAssertions;
using ForgeSelf.Api.Plugins.McpCenter.Services;
using ForgeSelf.Core;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.McpCenterTests;

/// <summary>
/// PILOT-079 P2①（2026-11-07 输入7 缺陷②③）：run_terminal_command 重定向放行 + 超时部分输出。
/// ① 重定向 <c>&gt; &gt;&gt; 1&gt; 2&gt; 2&gt;&amp;1</c> 不再整串拒绝：含重定向的命令整体路由
/// pwsh -NoProfile -NonInteractive -Command（ArgumentList 转义），输出真实落盘（PC-018：
/// 工具报成功 ⇒ 副作用必须真实存在——用读回文件内容作证）。
/// ② <c>&lt;</c> 输入重定向与反引号**仍拒绝**（本批未放开，防止误开任意读入口）。
/// ③ 超时/取消分支不再丢弃已捕获输出：杀进程后等待管道收尾 ≤5s，信封带 stdout/stderr
/// （「agent 自跑构建 >30s 假失败」的直接修复面——错误现场必须可观察）。
/// ④ timeoutSeconds 落 [1, MaxTimeoutSeconds]（下界钳制可观测；上界常量=1800）。
/// </summary>
public class RunTerminalCommandRedirectAndTimeoutTests
{
    private static async Task<JsonElement> ExecuteAsync(string parameters)
    {
        var json = await new RunTerminalCommandTool(Mock.Of<IContext>()).ExecuteAsync(parameters);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static string NewTempDir() =>
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "forgeself-rt-" + Guid.NewGuid().ToString("N"))).FullName;

    private static string Str(JsonElement root, string name) =>
        root.TryGetProperty(name, out var v) ? (v.GetString() ?? string.Empty) : string.Empty;

    [Fact]
    public async Task RedirectToFile_PersistsOutput()
    {
        var dir = NewTempDir();
        try
        {
            var target = Path.Combine(dir, "out.txt");
            var root = await ExecuteAsync(JsonSerializer.Serialize(new
            {
                command = $"git --version > \"{target}\"",
                cwd = dir,
                timeoutSeconds = 60,
            }));

            root.GetProperty("success").GetBoolean().Should().BeTrue(
                because: "重定向应放行并真实落盘，error: " + Str(root, "error"));
            File.Exists(target).Should().BeTrue("PC-018：工具报成功 ⇒ 重定向副作用必须真实存在");
            File.ReadAllText(target).Should().Contain("git version");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task RedirectAppend_AndStdErrMerge_Persist()
    {
        var dir = NewTempDir();
        try
        {
            var target = Path.Combine(dir, "out.txt");
            File.WriteAllText(target, "PRE\n");

            var append = await ExecuteAsync(JsonSerializer.Serialize(new
            {
                command = $"git --version >> \"{target}\"",
                cwd = dir,
                timeoutSeconds = 60,
            }));
            append.GetProperty("success").GetBoolean().Should().BeTrue(
                because: ">> 追加重定向应放行，error: " + Str(append, "error"));

            var merge = await ExecuteAsync(JsonSerializer.Serialize(new
            {
                command = $"git --version 2>&1",
                cwd = dir,
                timeoutSeconds = 60,
            }));
            merge.GetProperty("success").GetBoolean().Should().BeTrue(
                because: "2>&1 合并流重定向应放行，error: " + Str(merge, "error"));
            Str(merge, "stdout").Should().Contain("git version", "2>&1 后版本号应出现在合并输出");

            File.ReadAllText(target).Should().Contain("PRE").And.Contain("git version", ">> 应在原文件上追加而非覆盖");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task Input_And_Backtick_StillRejected()
    {
        var dir = NewTempDir();
        try
        {
            var inbound = Path.Combine(dir, "in.txt");
            File.WriteAllText(inbound, "seed");

            var inputRedir = await ExecuteAsync(JsonSerializer.Serialize(new
            {
                command = $"pwsh -Command Get-Content < \"{inbound}\"",
                cwd = dir,
                timeoutSeconds = 30,
            }));
            inputRedir.GetProperty("success").GetBoolean().Should().BeFalse("< 输入重定向本批未放开");
            Str(inputRedir, "error").Should().Contain("contains '<'");

            var backtick = await ExecuteAsync(JsonSerializer.Serialize(new
            {
                command = "pwsh -Command Write-Output `a",
                cwd = dir,
                timeoutSeconds = 30,
            }));
            backtick.GetProperty("success").GetBoolean().Should().BeFalse("反引号本批未放开");
            Str(backtick, "error").Should().Contain("contains '`'");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task Timeout_KillsProcess_ButReturnsPartialStdout()
    {
        var dir = NewTempDir();
        try
        {
            var root = await ExecuteAsync(JsonSerializer.Serialize(new
            {
                // 引号必须包住含 ; 的 pwsh 命令串：分号在引号外会被分段器拆成两段，
                // 第二段首 token Start-Sleep 不在白名单 → 因 allowlist 被拒而非超时被杀（假红）。
                command = "pwsh -NoProfile -Command \"Write-Output p079-before; Start-Sleep -Seconds 30\"",
                cwd = dir,
                timeoutSeconds = 2,
            }));

            root.GetProperty("success").GetBoolean().Should().BeFalse("2s 超时必杀 30s 进程");
            root.GetProperty("timedOut").GetBoolean().Should().BeTrue();
            Str(root, "stdout").Should().Contain("p079-before",
                "超时中止不再丢弃已捕获输出——错误现场必须可观察（PILOT-079 P2①③）");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task TimeoutSeconds_ClampedToLowerBound()
    {
        var dir = NewTempDir();
        try
        {
            // timeoutSeconds=0 非法 → 钳到下界 1s：3s 睡眠必被杀（若钳制失效，0/负数会变成
            // CancelAfter(0s)/负 TimeSpan 的未定义行为或永不超时）。
            var root = await ExecuteAsync(JsonSerializer.Serialize(new
            {
 command = "pwsh -NoProfile -Command Start-Sleep -Seconds 3",
                cwd = dir,
                timeoutSeconds = 0,
            }));

            root.GetProperty("success").GetBoolean().Should().BeFalse();
            root.GetProperty("timedOut").GetBoolean().Should().BeTrue("0 必须钳到下界 1s，3s 命令必超时");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    [Fact]
    public void MaxTimeoutSeconds_WidenedTo1800()
    {
        TerminalCommandGuard.MaxTimeoutSeconds.Should().Be(1800,
            "PILOT-079 P2①：30s 上限使 dotnet build 必假失败（缺陷②根因），上界放宽到 30 分钟");
    }
}

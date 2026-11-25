using System.Diagnostics;
using System.Text;
using FluentAssertions;

namespace ForgeSelf.Api.Tests;

/// <summary>
/// pilot feature/回写校验脚本的行为守卫（scripts/verify-pilot-feature-writeback.ps1）。
/// 2026-11-24 修复：轻量任务（mini-task.md、无 04-task.md）原硬 exit 1 导致假红，被迫 --no-verify。
/// 本测试以临时仓库实跑脚本，证明：mini-task 无 YAML 头部放行、完整任务照常校验、缺失来源仍拒绝。
/// </summary>
public class PilotWritebackGuardTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "scripts")))
            dir = dir.Parent;
        dir.Should().NotBeNull("测试需能从 bin 目录向上定位仓库根的 scripts/");
        return dir!.FullName;
    }

    private sealed record ScriptRun(int ExitCode, string Output);

    /// <summary>以 pwsh 实跑回写校验脚本，返回退出码与输出。</summary>
    private static ScriptRun Run(string repoRoot, string taskId)
    {
        var root = FindRepoRoot();
        var script = Path.Combine(root, "scripts", "verify-pilot-feature-writeback.ps1");
        File.Exists(script).Should().BeTrue("回写校验脚本必须在位");

        var psi = new ProcessStartInfo
        {
            FileName = "pwsh",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -TaskId \"{taskId}\" -RepoRoot \"{repoRoot}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(psi);
        process.Should().NotBeNull("pwsh 必须可用");
        var stdout = process!.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit(60_000).Should().BeTrue("脚本应在 1 分钟内结束");
        stdout.Wait(5_000);
        stderr.Wait(5_000);
        return new ScriptRun(process.ExitCode, stdout.Result + stderr.Result);
    }

    private static string NewFixtureRoot()
    {
        var root = Path.Combine(FindRepoRoot(), ".temp", "pilot-writeback-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "docs", "ai", "pilot"));
        return root;
    }

    [Fact]
    public void MiniTaskWithoutYamlHeader_PassesWithoutFalseRed()
    {
        var root = NewFixtureRoot();
        var task = "2026-11-23-mini-fixture";
        var dir = Path.Combine(root, "docs", "ai", "pilot", task);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "mini-task.md"), "# 轻量任务\n\n无 YAML 头部。\n", new UTF8Encoding(false));

        var run = Run(root, task);

        run.ExitCode.Should().Be(0, "轻量任务无 04-task.md 不应假红");
        run.Output.Should().Contain("OK", "须输出放行结论");
    }

    [Fact]
    public void FullTaskWith04YamlHeader_IsStillValidated()
    {
        var root = NewFixtureRoot();
        var task = "2026-11-24-full-fixture";
        var dir = Path.Combine(root, "docs", "ai", "pilot", task);
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            Path.Combine(dir, "04-task.md"),
            "---\nfeature: none\nrequirements: []\n---\n\n# Task\n",
            new UTF8Encoding(false));

        var run = Run(root, task);

        run.ExitCode.Should().Be(0, "带合法 04 头部的完整任务必须通过");
        run.Output.Should().Contain("PASS");
    }

    [Fact]
    public void MissingBothSourceFiles_Fails()
    {
        var root = NewFixtureRoot();
        var task = "2026-11-24-missing-fixture";
        Directory.CreateDirectory(Path.Combine(root, "docs", "ai", "pilot", task));

        var run = Run(root, task);

        run.ExitCode.Should().Be(1, "既无 04-task.md 又无 mini-task.md 必须拒绝");
        run.Output.Should().Contain("FAIL");
    }
}

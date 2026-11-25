using System.Diagnostics;
using System.Text;
using FluentAssertions;

namespace ForgeSelf.Api.Tests;

/// <summary>
/// hook 安装脚本行为守卫（scripts/install-git-hooks.ps1）。
/// 2026-11-24 痛点：Windows 下 UnixFileMode/POSIX 探测抢跑导致安装中断、commit-msg 漏装。
/// 本测试在临时 git 仓库实跑安装脚本，证明 Windows 上 pre-commit 与 commit-msg 均安装成功且非空。
/// </summary>
public class InstallGitHooksGuardTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "scripts")))
            dir = dir.Parent;
        dir.Should().NotBeNull("测试需能从 bin 目录向上定位仓库根的 scripts/");
        return dir!.FullName;
    }

    private sealed record RunResult(int ExitCode, string Output);

    private static RunResult RunInstall(string workDir)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "pwsh",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{Path.Combine(workDir, "scripts", "install-git-hooks.ps1")}\"",
            WorkingDirectory = workDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(psi);
        process.Should().NotBeNull("pwsh 必须可用");
        var stdout = process!.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit(60_000).Should().BeTrue("安装脚本应在 1 分钟内结束");
        stdout.Wait(5_000);
        stderr.Wait(5_000);
        return new RunResult(process.ExitCode, stdout.Result + stderr.Result);
    }

    [Fact]
    public void InstallGitHooks_OnWindows_InstallsBothHooksWithoutUnixFileModeThrow()
    {
        var root = FindRepoRoot();
        var workDir = Path.Combine(root, ".temp", "install-hooks-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(workDir, "scripts", "hooks"));

        // 复制脚本与 hook 源文件到临时仓库，避免污染真实 .git/hooks
        File.Copy(Path.Combine(root, "scripts", "install-git-hooks.ps1"), Path.Combine(workDir, "scripts", "install-git-hooks.ps1"));
        foreach (var hook in new[] { "pre-commit", "commit-msg" })
        {
            File.Copy(Path.Combine(root, "scripts", "hooks", hook), Path.Combine(workDir, "scripts", "hooks", hook));
        }

        // 初始化临时 git 仓库（脚本依赖 git rev-parse --git-common-dir）
        var init = Process.Start(new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "init",
            WorkingDirectory = workDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        });
        init.Should().NotBeNull("git 必须可用");
        init!.WaitForExit(30_000).Should().BeTrue("git init 应快速结束");
        init.ExitCode.Should().Be(0, "临时仓库初始化必须成功");

        var run = RunInstall(workDir);

        run.ExitCode.Should().Be(0, "安装脚本在 Windows 上必须成功，不得因 UnixFileMode/POSIX 探测中断");
        run.Output.Should().Contain("已安装 commit-msg", "commit-msg 必须被安装（痛点：漏装）");

        var hooksDir = Path.Combine(workDir, ".git", "hooks");
        foreach (var hook in new[] { "pre-commit", "commit-msg" })
        {
            var path = Path.Combine(hooksDir, hook);
            File.Exists(path).Should().BeTrue($"{hook} 必须已安装");
            new FileInfo(path).Length.Should().BeGreaterThan(0, $"{hook} 不得为空");
        }
    }
}

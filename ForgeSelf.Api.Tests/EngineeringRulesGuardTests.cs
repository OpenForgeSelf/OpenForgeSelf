using System.Diagnostics;
using System.Text;
using FluentAssertions;

namespace ForgeSelf.Api.Tests;

/// <summary>
/// 工程规则守卫（scripts/check-engineering-rules.cjs）行为测试。
/// 覆盖三条下沉规则：temp/*.cjs 禁令、pwsh-only、.ps1 UTF-8 BOM。
/// </summary>
public class EngineeringRulesGuardTests
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

    private static RunResult RunGuard(string fixtureRoot)
    {
        var root = FindRepoRoot();
        var script = Path.Combine(root, "scripts", "check-engineering-rules.cjs");
        File.Exists(script).Should().BeTrue("工程规则守卫脚本必须在位");

        var psi = new ProcessStartInfo
        {
            FileName = "node",
            Arguments = $"\"{script}\" --root \"{fixtureRoot}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(psi);
        process.Should().NotBeNull("node 必须可用");
        var stdout = process!.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit(30_000).Should().BeTrue("守卫应在 30 秒内结束");
        stdout.Wait(5_000);
        stderr.Wait(5_000);
        return new RunResult(process.ExitCode, stdout.Result + stderr.Result);
    }

    private static string NewFixtureRoot()
    {
        var root = Path.Combine(FindRepoRoot(), ".temp", "eng-rules-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "scripts"));
        Directory.CreateDirectory(Path.Combine(root, ".github", "workflows"));
        return root;
    }

    [Fact]
    public void TempProbeCjs_IsReported()
    {
        var root = NewFixtureRoot();
        var tempDir = Path.Combine(root, "temp");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "probe.cjs"), "// 违规探针\n", new UTF8Encoding(false));

        var run = RunGuard(root);

        run.ExitCode.Should().Be(1, "temp/*.cjs 必须被拦截");
        run.Output.Should().Contain("eng.no-temp-probe");
    }

    [Fact]
    public void Powershell51Invocation_IsReported()
    {
        var root = NewFixtureRoot();
        var file = Path.Combine(root, "scripts", "bad.ps1");
        File.WriteAllText(file, "# 注释可提及 PowerShell 7\npowershell.exe -Command \"Get-Date\"\n", new UTF8Encoding(false));

        var run = RunGuard(root);

        run.ExitCode.Should().Be(1, "powershell 5.1 调用必须被拦截");
        run.Output.Should().Contain("eng.pwsh-only");
    }

    [Fact]
    public void Ps1WithoutBom_IsReported()
    {
        var root = NewFixtureRoot();
        var file = Path.Combine(root, "scripts", "nobom.ps1");
        File.WriteAllText(file, "Write-Host 'no bom'\n", new UTF8Encoding(false));

        var run = RunGuard(root);

        run.ExitCode.Should().Be(1, ".ps1 缺少 UTF-8 BOM 必须被拦截");
        run.Output.Should().Contain("eng.ps1-bom");
    }

    [Fact]
    public void CleanFixture_Passes()
    {
        var root = NewFixtureRoot();
        // 带 BOM 的合规 .ps1
        var file = Path.Combine(root, "scripts", "ok.ps1");
        var bom = new UTF8Encoding(true);
        File.WriteAllText(file, "pwsh -NoProfile -File x.ps1\n", bom);

        var run = RunGuard(root);

        run.ExitCode.Should().Be(0, "无违规时必须通过");
        run.Output.Should().Contain("PASS");
    }
}

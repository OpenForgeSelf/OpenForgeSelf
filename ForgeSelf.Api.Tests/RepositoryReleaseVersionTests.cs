using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace ForgeSelf.Api.Tests;

/// <summary>
/// 发行版本行守卫（PILOT-076，2026-10-30 输入3 用户拍板）：
/// 版本行真源 = ForgeSelf.Api.csproj 的 VersionPrefix 属性（三段固定部分，如 2.4.22）；
/// release-local.ps1 本地缺省（不传 -Version 且无 GITHUB_REF_NAME）读它再补时间码，
/// CI tag 与显式 -Version 行为不变；csproj 版本行 bump 必须经用户允许，agent 不得擅自改。
/// 静态层锁 csproj/脚本形态；pwsh 行为层做真实解析互证——测试侧持有一份独立第二解析器，
/// 防「同源自洽的断言不算证据」（AGENTS §5.0）。
/// </summary>
public class RepositoryReleaseVersionTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "scripts")))
            dir = dir.Parent;
        dir.Should().NotBeNull("测试需能从 bin 目录向上定位仓库根");
        return dir!.FullName;
    }

    // ---------- 静态层：csproj 形态 ----------

    [Fact]
    public void ApiCsproj_VersionPrefix_MustBeFullThreeSegmentReleaseLine()
    {
        var csproj = File.ReadAllText(Path.Combine(FindRepoRoot(), "ForgeSelf.Api", "ForgeSelf.Api.csproj"));
        var match = Regex.Match(csproj, @"<VersionPrefix>\s*([^<]+?)\s*</VersionPrefix>");
        match.Success.Should().BeTrue("ForgeSelf.Api.csproj 必须有 VersionPrefix 属性（版本行真源载体）");

        match.Groups[1].Value.Trim().Should().MatchRegex(@"^\d+\.\d+\.\d+$",
            "VersionPrefix 必须承载完整三段发行线（如 2.4.22）——两段值（如旧值 2.3）会让 release-local " +
            "缺省链抛错、兜底 Version 退回旧双段结构；bump 版本行须经用户允许（2026-10-30 输入3）");
    }

    [Fact]
    public void ApiCsproj_VersionSuffix_MustBePureDateCode()
    {
        var csproj = File.ReadAllText(Path.Combine(FindRepoRoot(), "ForgeSelf.Api", "ForgeSelf.Api.csproj"));
        var match = Regex.Match(csproj, @"<VersionSuffix>\s*([^<]+?)\s*</VersionSuffix>");
        match.Success.Should().BeTrue("ForgeSelf.Api.csproj 必须有 VersionSuffix 属性");

        var value = match.Groups[1].Value.Trim();
        value.Should().Contain("yyMMddHHmm", "兜底 Version 第 4 段必须仍是时间码（与发行串同构）");
        value.Should().NotStartWith("0.",
            "旧结构『0.<时间码>』在 VersionPrefix 承载三段后会把 Version 撑成 5 段（非法），已废止");
    }

    // ---------- 静态层：release-local 缺省链 ----------

    [Fact]
    public void ReleaseLocal_MustDefaultToCsprojVersionLine()
    {
        var root = FindRepoRoot();
        var scriptPath = Path.Combine(root, "scripts", "release", "release-local.ps1");
        var script = File.ReadAllText(scriptPath);

        script.Should().Contain("Get-CsprojReleaseVersion",
            "本地缺省必须读 csproj 版本行（2026-10-30 输入3：发布 zip 不需指定版本）");
        script.Should().NotContain("0.0.0-local",
            "本地 dry-run 缺省串 0.0.0-local 已废止——它产出低于任何已发行线的包，且掩盖『忘传 -Version』");
        script.Should().Contain("GITHUB_REF_NAME",
            "CI tag 通道必须保留：GITHUB_REF_NAME 有值时 tag 原样使用、绝不改写（R10 ⑤：产物版本必须 = tag）");

        new FileInfo(scriptPath).Length.Should().BeGreaterThan(1000, "阳性对照：本判据扫的不是空文件");
    }

    [Fact]
    public void ReleaseLib_MustExposeCsprojReleaseVersionReader()
    {
        var lib = File.ReadAllText(Path.Combine(FindRepoRoot(), "scripts", "release", "release-lib.ps1"));
        lib.Should().Contain("function Get-CsprojReleaseVersion",
            "『缺省读 csproj』的解析函数必须落共享层 release-lib.ps1（发布脚本唯一函数库）");
    }

    // ---------- 行为层：pwsh 实跑（真实 csproj + 合成夹具） ----------

    [Fact]
    public void GetCsprojReleaseVersion_FromRealCsproj_MatchesIndependentParse()
    {
        var root = FindRepoRoot();
        var csproj = File.ReadAllText(Path.Combine(root, "ForgeSelf.Api", "ForgeSelf.Api.csproj"));
        var expected = IndependentParseVersionPrefix(csproj);

        var (found, stdout, stderr) = RunPwsh(
            "[Console]::OutputEncoding=[System.Text.Encoding]::UTF8; " +
            $". '{Path.Combine(root, "scripts", "release", "release-lib.ps1")}'; " +
            $"Get-CsprojReleaseVersion -RepoRoot '{root}'");

        found.Should().BeTrue("pwsh 必须可用（本仓库脚本入口统一 pwsh，AGENTS §2.3）");
        stderr.Trim().Should().BeEmpty("函数对真实仓库必须成功，不应有错误输出");
        stdout.Trim().Should().Be(expected,
            "ps1 解析结果必须与测试侧独立第二解析器一致（同源自洽断言不算证据，AGENTS §5.0）");
    }

    [Fact]
    public void GetCsprojReleaseVersion_FourSegmentPrefix_ReturnsFirstThree()
    {
        var root = FindRepoRoot();
        var fixture = CreateCsprojFixture("<VersionPrefix>2.4.22.9</VersionPrefix>");

        var (found, stdout, stderr) = RunPwsh(
            "[Console]::OutputEncoding=[System.Text.Encoding]::UTF8; " +
            $". '{Path.Combine(root, "scripts", "release", "release-lib.ps1")}'; " +
            $"Get-CsprojReleaseVersion -CsprojPath '{fixture}'");

        found.Should().BeTrue("pwsh 必须可用");
        stderr.Trim().Should().BeEmpty("四段输入属于合法形态（解析固定部分），不应报错");
        stdout.Trim().Should().Be("2.4.22",
            "『解析固定的部分』= 超出三段的输入取前三段（2026-10-30 用户原话）");
    }

    [Theory]
    [InlineData("两段 prefix（旧形态 2.3）", "<VersionPrefix>2.3</VersionPrefix>", "3-segment")]
    [InlineData("缺 VersionPrefix 标签", "<NothingHere/>", "Version")]
    public void GetCsprojReleaseVersion_InvalidCsproj_ThrowsWithExplanation(
        string caseName, string prefixLine, string expectedKeyword)
    {
        var root = FindRepoRoot();
        var fixture = CreateCsprojFixture(prefixLine);

        var (found, stdout, stderr) = RunPwsh(
            "[Console]::OutputEncoding=[System.Text.Encoding]::UTF8; " +
            $". '{Path.Combine(root, "scripts", "release", "release-lib.ps1")}'; " +
            $"Get-CsprojReleaseVersion -CsprojPath '{fixture}'");

        found.Should().BeTrue("pwsh 必须可用");
        stderr.Should().Contain(expectedKeyword,
            $"case『{caseName}』必须显式抛错并说明原因；报错关键词用 ASCII（子进程 stderr 捕获不做码页假设，PC-011 lens）");
    }

    [Fact]
    public void GetCsprojReleaseVersion_MissingFile_ThrowsWithExplanation()
    {
        var root = FindRepoRoot();
        var missing = Path.Combine(root, "ForgeSelf.Api", "Does.Not.Exist.csproj");

        var (found, stdout, stderr) = RunPwsh(
            "[Console]::OutputEncoding=[System.Text.Encoding]::UTF8; " +
            $". '{Path.Combine(root, "scripts", "release", "release-lib.ps1")}'; " +
            $"Get-CsprojReleaseVersion -RepoRoot '{root}' -CsprojPath '{missing}'");

        found.Should().BeTrue("pwsh 必须可用");
        stderr.Should().Contain("not found", "csproj 缺失必须显式抛错并带上路径");
    }

    // ---------- 帮手 ----------

    /// <summary>测试侧独立第二解析器：与 ps1 实现互证（防两边同写错也同绿）。</summary>
    private static string IndependentParseVersionPrefix(string csprojText)
    {
        var match = Regex.Match(csprojText, @"<VersionPrefix>\s*([^<]+?)\s*</VersionPrefix>");
        if (!match.Success)
            throw new InvalidOperationException("independent parser: VersionPrefix tag not found in csproj");
        var segments = match.Groups[1].Value.Trim().Split('.');
        if (segments.Length < 3)
            throw new InvalidOperationException(
                "independent parser: VersionPrefix must carry 3 segments, got '" + match.Groups[1].Value.Trim() + "'");
        return string.Join('.', segments[0], segments[1], segments[2]);
    }

    /// <summary>合成 csproj 夹具落系统 TEMP（跑测试前按 §5.0 重定向到仓库 .temp，不入库、不自动删除遵循无删除铁律）。</summary>
    private static string CreateCsprojFixture(string versionPrefixLine)
    {
        var dir = Directory.CreateTempSubdirectory("forge-release-version-");
        var path = Path.Combine(dir.FullName, "Fixture.csproj");
        File.WriteAllText(path,
            "<Project>\n  <PropertyGroup>\n    " + versionPrefixLine + "\n  </PropertyGroup>\n</Project>\n",
            new UTF8Encoding(false));
        return path;
    }

    private static (bool PwshFound, string StdOut, string StdErr) RunPwsh(string script)
    {
        var psi = new ProcessStartInfo { FileName = "pwsh", UseShellExecute = false, CreateNoWindow = true };
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add(script);
        psi.StandardOutputEncoding = Encoding.UTF8;
        psi.StandardErrorEncoding = Encoding.UTF8;

        Process? process;
        try
        {
            process = Process.Start(psi);
            if (process == null) return (false, string.Empty, "pwsh process start returned null");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return (false, string.Empty, "pwsh not found on PATH");
        }

        using (process)
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            process.WaitForExit(60000);
            return (true, stdoutTask.GetAwaiter().GetResult(), stderrTask.GetAwaiter().GetResult());
        }
    }
}

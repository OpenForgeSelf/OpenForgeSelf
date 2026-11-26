using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace ForgeSelf.Api.Tests;

/// <summary>
/// 提交内容卫生守卫的端到端行为测试（规范真源：agent-workflow b-engineering-platform.md §B13）。
/// 以子进程真实运行 scripts/check-commit-hygiene.ps1（B2 模式：外层 pwsh 执行并重定向到日志，
/// 末尾追加 GUARD_EXIT 行，按日志正文判据，不拿 exit code 当唯一证据）。
/// 自律约束：本文件自身也必须通过该守卫——所有违规形态一律用字符串拼接构造（源文件不出现连续违规字面量），
/// 断言只锚规则 ID、相对文件路径与行号，绝不把违规原文带进断言消息。
/// </summary>
public class CommitHygieneGuardTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "scripts")))
            dir = dir.Parent;
        dir.Should().NotBeNull("测试需能从 bin 目录向上定位仓库根的 scripts/");
        return dir!.FullName;
    }

    // ---- 违规形态常量（拼接构造，源文件自身保持干净；四类形态定义见 §B13） ----
    private const string DriveLine = "D" + @":\src\tmp-fixture.cs";
    private const string PosixLine = "/" + "home" + "/u";
    private const string UncLine = "\\\\" + "srv01" + "\\share";
    private const string FileUrlLine = "file" + ":///" + "D" + ":/x";

    private sealed record GuardFail(string Rule, string File, int Line);

    private sealed record GuardRun(
        int ExitCode, string SummaryLine, IReadOnlyList<GuardFail> Fails, string Notes, string RawLog)
    {
        public bool Passed => ExitCode == 0;
    }

    /// <summary>子进程跑守卫：外层 pwsh 执行加重定向到日志并追加 GUARD_EXIT 行，解析出安全断言面。</summary>
    private static GuardRun RunGuard(string arguments)
    {
        var root = FindRepoRoot();
        var guard = Path.Combine(root, "scripts", "check-commit-hygiene.ps1");
        File.Exists(guard).Should().BeTrue("守卫脚本必须在位");
        var workDir = Path.Combine(root, ".temp", "commit-hygiene-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        var log = Path.Combine(workDir, "run.log");

        var inner = "& pwsh -NoProfile -ExecutionPolicy Bypass -File '" + guard + "' " + arguments +
                    " *> '" + log + "'; " +
                    "'GUARD_EXIT=' + $LASTEXITCODE | Add-Content '" + log + "'";
        var psi = new ProcessStartInfo
        {
            FileName = "pwsh",
            Arguments = "-NoProfile -NoLogo -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(inner)),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(psi);
        process.Should().NotBeNull("pwsh 必须可用（AGENTS §2.3：仓库脚本一律 pwsh 执行）");
        // 并发排空两路管道防死锁；子进程输出仅吞掉不入断言（可能含违规原文）
        var outTask = process!.StandardOutput.ReadToEndAsync();
        var errTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit(180_000).Should().BeTrue("守卫子进程应在 3 分钟内结束");
        outTask.Wait(5_000);
        errTask.Wait(5_000);

        var logText = File.Exists(log) ? File.ReadAllText(log) : string.Empty;
        var exitMatch = Regex.Match(logText, @"GUARD_EXIT=(-?\d+)");
        exitMatch.Success.Should().BeTrue("日志必须携带 GUARD_EXIT 行（B2 判据锚文本）");
        var exitCode = int.Parse(exitMatch.Groups[1].Value);

        var fails = new List<GuardFail>();
        foreach (var rawLine in logText.Split('\n'))
        {
            var m = Regex.Match(rawLine.TrimEnd('\r'), @"^\[hygiene\] FAIL (\S+) (.*):(\d+): ");
            if (m.Success)
                fails.Add(new GuardFail(m.Groups[1].Value, m.Groups[2].Value, int.Parse(m.Groups[3].Value)));
        }

        var summary = Regex.Match(logText, @"^check-commit-hygiene: (PASS|FAIL|ERROR) .*$", RegexOptions.Multiline).Value;
        if (exitCode is 0 or 1)
            summary.Should().NotBeEmpty("PASS/FAIL 路径必须含摘要行（按文本判定）；用法错误路径（exit 2）只回显用法说明，无摘要行");
        var notes = string.Join('\n', logText.Split('\n')
            .Where(l => l.TrimStart().StartsWith("[hygiene] 注", StringComparison.Ordinal)));

        return new GuardRun(
            exitCode, summary, fails, notes, logText);
    }

    private static string NewFixtureDir()
    {
        var dir = Path.Combine(FindRepoRoot(), ".temp", "commit-hygiene-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Level1Rules_FourAbsoluteAddressForms_AreEachReportedOnce()
    {
        var file = Path.Combine(NewFixtureDir(), "violations.md");
        File.WriteAllLines(file, new[] { DriveLine, PosixLine, UncLine, FileUrlLine });

        var run = RunGuard("-Files '" + file + "'");

        run.Passed.Should().BeFalse("四类绝对地址形态必须全数拦截");
        run.Fails.Select(f => f.Rule).Should().HaveCount(4, "四条 L1 规则各报一类形态，恰好各一次");
        run.Fails.Select(f => f.Rule).Should().BeEquivalentTo(
            "hygiene.abs-drive", "hygiene.abs-posix", "hygiene.abs-unc", "hygiene.abs-fileurl");
        run.Fails.Should().OnlyHaveUniqueItems("同一违规不得重复报（file URL 盘符形态不再叠加报盘符规则）");
        run.SummaryLine.Should().Contain("hits=4", "file URL 行不叠加报盘符规则，恰好 4 命中");
    }

    [Fact]
    public void BenignContent_PassesWithZeroHits()
    {
        var file = Path.Combine(NewFixtureDir(), "benign.md");
        File.WriteAllLines(file, new[]
        {
            "相对路径 scripts/check-commit-hygiene.ps1 属安全引用",
            "规范真源见 docs/04-standards/agent-workflow/b-engineering-platform.md §B13",
            "证据锚点写法 docs/demo.md:2 属安全记法",
            "行内只提相对路径与锚点写法的行不应被误报",
        });

        var run = RunGuard("-Files '" + file + "'");

        run.Passed.Should().BeTrue("安全内容不得误报");
        run.Fails.Should().BeEmpty();
        run.SummaryLine.Should().Contain("PASS");
        run.SummaryLine.Should().Contain("hits=0");
    }

    [Fact]
    public void DiffFileMode_ReportsAddedLineWithTargetFileAndLineNumber()
    {
        var dir = NewFixtureDir();
        var diffPath = Path.Combine(dir, "new-file.diff");
        var diff = string.Join('\n',
            "diff --git a/docs/demo.md b/docs/demo.md",
            "new file mode 100644",
            "index 0000000..1111111",
            "--- /dev/null",
            "+++ b/docs/demo.md",
            "@@ -0,0 +1,3 @@",
            "+第一行安全",
            "+" + DriveLine,
            "+第三行安全");
        File.WriteAllText(diffPath, diff, new UTF8Encoding(false));

        var run = RunGuard("-DiffFile '" + diffPath + "'");

        run.Passed.Should().BeFalse("diff 新增行含违规形态必须拦截");
        run.Fails.Should().ContainSingle("diff 模式只报新增行");
        var fail = run.Fails[0];
        fail.File.Should().Be("docs/demo.md", "报告按目标文件相对路径计");
        fail.Line.Should().Be(2, "行号按新增行在目标文件中的落点计，违规行是第 2 行");
        fail.Rule.Should().Be("hygiene.abs-drive");
    }

    [Fact]
    public void Denylist_SubstringAndRegexEntries_BothReportedUnderDenylistRule()
    {
        var dir = NewFixtureDir();
        var denylist = Path.Combine(dir, "denylist.txt");
        File.WriteAllLines(denylist, new[] { "示例昵称", @"re:示例\d+" });
        var file = Path.Combine(dir, "content.md");
        File.WriteAllLines(file, new[] { "这行带 示例昵称 字样", "这行匹配 示例123 字样", "这行安全" });

        var run = RunGuard("-Files '" + file + "' -DenylistPath '" + denylist + "'");

        run.Passed.Should().BeFalse("拒绝列表命中必须拦截");
        run.Fails.Should().HaveCount(2, "子串词条与正则词条各命中一次");
        run.Fails.Select(f => f.Rule).Should().OnlyContain(rule => rule == "hygiene.denylist");
    }

    [Fact]
    public void MissingDenylistFile_SkipsLevel2WithNoteInsteadOfErroring()
    {
        var dir = NewFixtureDir();
        var file = Path.Combine(dir, "benign.md");
        File.WriteAllLines(file, new[] { "安全内容一行" });
        var absent = Path.Combine(dir, "absent-denylist.txt");

        var run = RunGuard("-Files '" + file + "' -DenylistPath '" + absent + "'");

        run.Passed.Should().BeTrue("拒绝列表缺失是降级跳过（注记），不是错误");
        run.Notes.Should().Contain("拒绝列表文件不存在");
        run.Notes.Should().Contain("L2 已跳过");
        run.SummaryLine.Should().Contain("PASS");
    }

    [Fact]
    public void SingleLineFile_DenylistTerm_IsMatchedPerLineNotPerChar()
    {
        // 回归守卫：Read-TextLines 曾把单行文件当成字符串逐字符扫描，导致单行内容拒绝列表不命中。
        var dir = NewFixtureDir();
        var denylist = Path.Combine(dir, "denylist.txt");
        File.WriteAllLines(denylist, new[] { "示例昵称" });
        var file = Path.Combine(dir, "single-line.md");
        File.WriteAllText(file, "这行带 示例昵称 字样", new UTF8Encoding(false));

        var run = RunGuard("-Files '" + file + "' -DenylistPath '" + denylist + "'");

        run.Passed.Should().BeFalse("单行文件中的拒绝列表词条必须命中");
        run.Fails.Should().ContainSingle();
        run.Fails[0].Rule.Should().Be("hygiene.denylist");
    }

    [Fact]
    public void MutuallyExclusiveModeArgs_AreUsageErrors()
    {
        var dir = NewFixtureDir();
        var file = Path.Combine(dir, "any.md");
        File.WriteAllLines(file, new[] { "占位内容" });

        var run = RunGuard("-All -Files '" + file + "'");

        run.ExitCode.Should().Be(2, "模式互斥属用法错误，退出码为 2");
        run.RawLog.Should().Contain("用法", "用法错误必须回显用法说明");
        run.SummaryLine.Should().NotContain("PASS");
    }
}

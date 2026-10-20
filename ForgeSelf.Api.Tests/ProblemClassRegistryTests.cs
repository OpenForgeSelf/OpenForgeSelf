using System.Text.RegularExpressions;
using FluentAssertions;

namespace ForgeSelf.Api.Tests;

/// <summary>
/// 守卫的守卫（problem-class-closure 机制，2026-10-17）：断言问题类台账自身的完整性——
/// front-matter 必填键、守卫路径真实性、planned 守卫带可检索 TODO、case↔PC 双向引用闭合、
/// 两处 README 索引与档案一一对应、allowlist 到期日未过。
/// 没有本测试，台账三个月后必然腐烂（index.md 停更 2026-08-12 的前车之鉴）。
/// </summary>
public class ProblemClassRegistryTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "scripts")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return dir!.FullName;
    }

    private static string PcDir() => Path.Combine(FindRepoRoot(), @"docs\11-troubleshooting\problem-classes");
    private static string CaseDir() => Path.Combine(FindRepoRoot(), @"docs\11-troubleshooting\cases");

    /// <summary>取 front-matter 原始行（--- 之间）。</summary>
    private static List<string> FrontMatter(string file)
    {
        var fm = new List<string>();
        var inFm = false;
        foreach (var l in File.ReadAllLines(file))
        {
            if (l.Trim() == "---") { if (inFm) break; inFm = true; continue; }
            if (inFm) fm.Add(l);
        }
        return fm;
    }

    private static string Scalar(List<string> fm, string key) =>
        fm.FirstOrDefault(l => Regex.IsMatch(l, @"^" + key + @"\s*:")) is { } l ? Regex.Replace(l, @"^" + key + @"\s*:\s*", "").Trim() : "";

    private static List<string> ListItems(List<string> fm, string key)
    {
        var items = new List<string>();
        var inBlock = false;
        foreach (var l in fm)
        {
            if (Regex.IsMatch(l, @"^" + key + @"\s*:")) { inBlock = true; continue; }
            if (!inBlock) continue;
            if (Regex.IsMatch(l, @"^\s+-\s+(.*)")) items.Add(Regex.Match(l, @"^\s+-\s+(.*)").Groups[1].Value);
            else if (Regex.IsMatch(l, @"^\S")) break;
        }
        return items;
    }

    private static string FlowVal(string map, string key)
    {
        var m = Regex.Match(map, @"\b" + key + @"\s*:\s*(?:""([^""]*)""|([^,}]+))");
        if (!m.Success) return "";
        return (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value).Trim();
    }

    private static IEnumerable<string> PcFiles() => Directory.GetFiles(PcDir(), "PC-*.md").OrderBy(f => f);
    private static IEnumerable<string> CaseFiles() => Directory.GetFiles(CaseDir(), "20*.md").OrderBy(f => f);

    [Fact]
    public void EveryPc_FrontMatter_HasRequiredKeysAndValidStatus()
    {
        var required = new[] { "id", "title", "tags", "invariant", "status", "first_seen", "signature", "allowlist", "guards", "lens" };
        var problems = new List<string>();
        var scanned = 0;
        foreach (var f in PcFiles())
        {
            scanned++;
            var fm = FrontMatter(f);
            foreach (var k in required)
                if (!fm.Any(l => Regex.IsMatch(l, @"^" + k + @"\s*:")))
                    problems.Add($"{Path.GetFileName(f)} 缺必填键 {k}");
            var status = Scalar(fm, "status");
            if (status != "open" && status != "guarded-partial" && status != "guarded" && status != "structural")
                problems.Add($"{Path.GetFileName(f)} status 非法: '{status}'");
            var id = Scalar(fm, "id");
            if (!Regex.IsMatch(Path.GetFileName(f), @"^" + Regex.Escape(id) + @"-"))
                problems.Add($"{Path.GetFileName(f)} 文件名与 front-matter id {id} 不一致");
        }
        scanned.Should().BeGreaterThanOrEqualTo(7, "首批种子 PC ≥7（阳性对照）");
        problems.Should().BeEmpty();
    }

    [Fact]
    public void Guards_ActivePaths_Exist_And_PlannedGuards_CarrySearchableTodo()
    {
        var root = FindRepoRoot();
        var todoText = File.ReadAllText(Path.Combine(root, "TODO.md"));
        var problems = new List<string>();
        var guardCount = 0;
        foreach (var f in PcFiles())
        {
            var id = Scalar(FrontMatter(f), "id");
            foreach (var g in ListItems(FrontMatter(f), "guards"))
            {
                guardCount++;
                var path = FlowVal(g, "path");
                var status = FlowVal(g, "status");
                if (status == "active")
                {
                    if (!File.Exists(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar))))
                        problems.Add($"{id} active 守卫路径不存在: {path}");
                }
                else if (status == "planned")
                {
                    var todo = FlowVal(g, "todo");
                    var expires = FlowVal(g, "expires");
                    if (string.IsNullOrEmpty(todo) || string.IsNullOrEmpty(expires))
                        problems.Add($"{id} planned 守卫必须带 todo+expires（悬空守卫）: {g}");
                    else
                    {
                        if (!todoText.Contains(todo, StringComparison.Ordinal))
                            problems.Add($"{id} planned 守卫的 todo 在 TODO.md 逐字检索不到: {todo}");
                        if (DateOnly.TryParse(expires, out var exp) && exp < DateOnly.FromDateTime(DateTime.Today))
                            problems.Add($"{id} planned 守卫已过期: {expires}");
                    }
                }
                else problems.Add($"{id} 守卫状态非法（只允许 active|planned）: {g}");
            }
        }
        guardCount.Should().BeGreaterThanOrEqualTo(8, "台账守卫条目阳性对照（防解析空转）");
        problems.Should().BeEmpty();
    }

    [Fact]
    public void AllowlistEntries_HaveFutureExpiry()
    {
        var problems = new List<string>();
        foreach (var f in PcFiles())
        {
            var id = Scalar(FrontMatter(f), "id");
            foreach (var a in ListItems(FrontMatter(f), "allowlist"))
            {
                var expires = FlowVal(a, "expires");
                if (string.IsNullOrEmpty(expires)) { problems.Add($"{id} allowlist 缺 expires（豁免必带到期日）: {a}"); continue; }
                if (DateOnly.TryParse(expires, out var exp) && exp < DateOnly.FromDateTime(DateTime.Today))
                    problems.Add($"{id} allowlist 已过期 {expires}: {FlowVal(a, "path")}");
            }
        }
        problems.Should().BeEmpty();
    }

    [Fact]
    public void Cases_ResolveToExistingPc_And_PcCaseRefs_ResolveToFiles()
    {
        var problems = new List<string>();
        var pcIds = PcFiles().Select(f => Scalar(FrontMatter(f), "id")).ToHashSet();
        pcIds.Should().Contain("PC-001", "阳性对照");

        foreach (var cf in CaseFiles())
        {
            var fm = FrontMatter(cf);
            var cid = Scalar(fm, "id");
            var fileName = Path.GetFileNameWithoutExtension(cf);
            if (cid != fileName) problems.Add($"case {fileName} 的 front-matter id '{cid}' 与文件名不一致");
            var pc = Scalar(fm, "pc");
            if (!pcIds.Contains(pc)) problems.Add($"case {fileName} 的 pc: {pc} 在台账不存在（悬空 case）");
        }

        foreach (var f in PcFiles())
        {
            var fm = FrontMatter(f);
            var id = Scalar(fm, "id");
            foreach (var key in new[] { "cases", "recurrences" })
                foreach (var refId in Regex.Matches(Scalar(fm, key), @"20\d{2}-\d{2}-\d{2}-[^,\]\s]+").Select(m => m.Value))
                    if (!File.Exists(Path.Combine(CaseDir(), refId + ".md")))
                        problems.Add($"{id} 的 {key} 引用不存在的 case: {refId}");
        }
        CaseFiles().Count().Should().BeGreaterThanOrEqualTo(4, "首批 case ≥4（阳性对照）");
        problems.Should().BeEmpty();
    }

    [Fact]
    public void Ledgers_ListEveryPc_And_EveryCase()
    {
        var root = FindRepoRoot();
        var pcLedger = File.ReadAllText(Path.Combine(PcDir(), "README.md"));
        var caseLedger = File.ReadAllText(Path.Combine(CaseDir(), "README.md"));
        var problems = new List<string>();
        foreach (var f in PcFiles())
        {
            var id = Scalar(FrontMatter(f), "id");
            if (!Regex.IsMatch(pcLedger, @"\|\s*" + Regex.Escape(id) + @"\s*\|"))
                problems.Add($"problem-classes/README.md 台账缺 {id} 行");
        }
        foreach (var cf in CaseFiles())
        {
            var cid = Path.GetFileNameWithoutExtension(cf);
            if (!caseLedger.Contains(cid, StringComparison.Ordinal))
                problems.Add($"cases/README.md 索引缺 case 行: {cid}");
        }
        problems.Should().BeEmpty("三处登记缺一即腐烂——见技能 references/07-recording-spec.md");
        File.ReadAllText(Path.Combine(root, @"docs\11-troubleshooting\index.md")).Should().Contain("problem-classes/", "index.md 已升级为知识库入口");
    }
}

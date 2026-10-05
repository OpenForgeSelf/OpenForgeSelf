using System.IO.Compression;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ForgeSelf.Api.Plugins.DesignSystem;
using ForgeSelf.Api.Plugins.DesignSystem.Controllers;
using ForgeSelf.Api.Plugins.DesignSystem.Data;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;
using ForgeSelf.Api.Plugins.DesignSystem.Services;
using Microsoft.AspNetCore.Mvc;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>
/// M3 AC16：UX 规范进了导出没有、进去的东西是不是真的（brief 章 / design-md 章 / bundle 两文件 / Manifest / agent-rules）。
///
/// 三条"看着像其实不是"的坑，本类逐条钉：
/// ① **章写了但值是假的**：规范正文只存令牌路径，数字由渲染端现查。所以导出的括注值必须等于 `tokens/effective`
///    —— 这里用**控制器**取 effective 值来比对，而不是用导出自己的取值函数（自己比自己=空转）。
/// ② **空规范开了一章**：没有规范的项目导出里出现「## UX 规范（0 条）」，下游会以为这套系统没有 UX 要求。
/// ③ **清单里有、包里没有**：Manifest 列了 `guidelines/GUIDELINES.md` 但 zip 里没这个条目 = 假交付。
/// 另外 Stardust 十类实体必须不变（AC16 最后一句）：规范是新投影面，不是新实体类。
/// </summary>
[Collection("XCode")]
public class GuidelineExportTests : IDisposable
{
    static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    readonly String _dbDir;
    readonly DesignProjectService _projects = new();
    readonly TokenRepository _tokens = new();
    readonly CatalogRepository _catalog = new();
    readonly AuditRepository _audits = new();
    readonly GuidelineRepository _repo = new();
    readonly GuidelineService _svc;
    readonly ExportService _export;
    readonly DesignBriefBuilder _brief;
    readonly GenerationService _generation;
    readonly DesignSystemController _ctl;
    readonly Int64 _projectId;

    public GuidelineExportTests()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfDsGuidelineExport_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dbDir);
        DAL.AddConnStr(DesignSystemTables.ConnName, $"Data Source={Path.Combine(_dbDir, "DesignSystem.db")}", null, "SQLite");
        EntityFactory.InitConnection(DesignSystemTables.ConnName);

        DesignProject.Meta.Cache.Expire = 0;
        DesignTheme.Meta.Cache.Expire = 0;
        DesignToken.Meta.Cache.Expire = 0;
        DesignShadowLayer.Meta.Cache.Expire = 0;
        DesignComponent.Meta.Cache.Expire = 0;
        DesignComponentVariant.Meta.Cache.Expire = 0;
        DesignIcon.Meta.Cache.Expire = 0;
        DesignAsset.Meta.Cache.Expire = 0;
        DesignScreen.Meta.Cache.Expire = 0;
        DesignFontFace.Meta.Cache.Expire = 0;
        DesignAudit.Meta.Cache.Expire = 0;
        DesignRelease.Meta.Cache.Expire = 0;
        DesignGuideline.Meta.Cache.Expire = 0;

        var auditEngine = new AuditEngine(_tokens, _projects, _audits);
        _svc = new GuidelineService(_projects, _tokens, _repo);
        _export = new ExportService(_tokens, _projects, _catalog) { Guidelines = _svc };
        _brief = new DesignBriefBuilder(_export, _projects, _catalog, new DesignReviewService(_export, _tokens, _projects));
        _export.BriefBuilder = _brief;
        _generation = new GenerationService(_tokens, _projects, _catalog, auditEngine) { Guidelines = _svc };
        _ctl = new DesignSystemController(_projects, _tokens, _catalog, _audits, auditEngine, _export,
            new ReleaseService(_tokens, _projects, _audits, auditEngine, new DesignSystemPaths(_dbDir), _catalog),
            _generation, new AgentAccess(new DesignSystemPaths(_dbDir)),
            new DesignReviewService(_export, _tokens, _projects),
            new QuickCreateService(_projects, _generation), _brief, new PreviewCssService(_export), _svc);

        _projectId = NewProject("exp", seedGuidelines: true).Id;
    }

    public void Dispose() => GC.SuppressFinalize(this);

    DesignProject NewProject(String tag, Boolean seedGuidelines)
    {
        var p = _projects.Create(new ProjectInput { Code = $"e-{tag}-{Guid.NewGuid():N}"[..22], Name = $"规范导出 {tag}" });
        if (seedGuidelines)
        {
            _generation.Run(p.Id, new GenerationRequest { SeedColor = "#7c3aed", Themes = ["light", "dark"] }, false);
            return p;
        }

        // "没有规范"的项目：只走令牌与目录种子，绕开生成链路里的规范播种（测试里也不做物理删除，铁律 10）
        var gen = DesignGenerator.ApplyToProject(_tokens, _projects, p.Id,
            new GenerationRequest { SeedColor = "#7c3aed", Themes = ["light", "dark"] }, false);
        DesignGenerator.SeedComponentCatalog(_catalog, p.Id, gen);
        DesignGenerator.SeedBrandCatalog(_catalog, p.Id, gen);
        return p;
    }

    String Get(String format, String? theme = "light", Int64? projectId = null) =>
        _export.Produce(projectId ?? _projectId, format, theme).Text;

    [Fact]
    public void AC16_brief_开规范章_只列MUST_且排在checklist之前()
    {
        var outcome = _brief.Build(_projectId, "light", null, 60000, DesignBriefBuilder.Markdown);

        outcome.Sections.Should().Contain("guidelines");
        Array.IndexOf(DesignBriefBuilder.SectionOrder, "guidelines")
            .Should().BeLessThanOrEqualTo(Array.IndexOf(DesignBriefBuilder.SectionOrder, "checklist") - 1,
                "章节声明序必须 guidelines 在 checklist 前（与 design_context 默认序一致）");
        var order = outcome.Sections.ToList();
        order.IndexOf("guidelines").Should().BeLessThan(order.IndexOf("checklist"),
            "brief 里规范章必须排在交付清单前（先看约束，再核对清单）");

        var md = outcome.Markdown!;
        md.Should().Contain("## UX 规范");
        md.Should().Contain("- [ ]", "紧凑章的 MUST 要能当勾选清单用");
        md.Should().Contain("只列 MUST");

        // 紧凑=只有 MUST：用"行数与级别"这条自洽判据，而不是拿某条 SHOULD 文本去撞（不同级别规则文本会共用词，会假红/假绿）
        var all = _repo.List(_projectId).SelectMany(g => GuidelineRepository.ReadRules(g.RulesJson)).ToList();
        var musts = all.Where(r => r.Level == "MUST").ToList();
        var nonMust = all.Count - musts.Count;
        nonMust.Should().BeGreaterThan(0, "规范里若只有 MUST，这条「紧凑=只列 MUST」的判据就没内容可验");

        var chapter = md[md.IndexOf("## UX 规范", StringComparison.Ordinal)..];
        var lines = chapter.Split('\n')
            .Where(l => l.StartsWith("- [ ] ", StringComparison.Ordinal))
            .Select(l => l["- [ ] ".Length..].Trim())
            .ToList();
        lines.Should().HaveCount(musts.Count, $"紧凑章应恰好列出 {musts.Count} 条 MUST（SHOULD/MAY 共 {nonMust} 条不该进来）");
        var prefixes = musts.Select(r => Prefix(r.Text)).ToList();
        foreach (var line in lines)
            prefixes.Should().Contain(p => line.StartsWith(p, StringComparison.Ordinal),
                $"这条勾选项不是 MUST 规则：{line}");
    }

    /// <summary>规则文本的可比前缀：括注会在第一个反引号路径后插入值，所以只取到它之前</summary>
    static String Prefix(String text)
    {
        var cut = text.IndexOf('`');
        return (cut > 0 ? text[..cut] : text).Trim();
    }

    [Fact]
    public void AC16_brief_预算挤不下时记omitted_而不是截半章()
    {
        // 先量出"规范章自己要多大"，再用比它小的预算请求：必须整章进 omitted，不能出现半截章
        var full = _brief.Build(_projectId, "light", ["guidelines"], 60000, DesignBriefBuilder.Markdown);
        var need = full.Markdown!.Length;
        need.Should().BeGreaterThan(200);

        var tight = _brief.Build(_projectId, "light", null, Math.Clamp(need / 2 + 1, 2000, 60000), DesignBriefBuilder.Markdown);
        if (!tight.Omitted.Contains("guidelines"))
        {
            // 没被挤掉就必须真的在里面且渲染完整（不许"省略了但没声明"）
            tight.Sections.Should().Contain("guidelines");
            tight.Markdown.Should().Contain("## UX 规范");
        }
        else
        {
            tight.Sections.Should().NotContain("guidelines");
            tight.Markdown!.Should().NotContain("## UX 规范", "omitted 的章节不能留下半截正文");
            tight.Truncated.Should().BeTrue();
        }
    }

    [Fact]
    public void AC16_design_md_完整章_含SHOULD与MAY与引用令牌值()
    {
        var md = Get(ExportFormats.DesignMd);

        md.Should().Contain("## UX 规范");
        md.Should().Contain($"正文只写**令牌路径**");
        var row = _repo.Find(_projectId, "buttons")!;
        var rules = GuidelineRepository.ReadRules(row.RulesJson);
        foreach (var r in rules)
            md.Should().Contain($"**{r.Level}**", $"规则 {r.Id} 的级别标记没出来");
        md.Should().Contain("引用令牌：", "RenderFull 的引用令牌行必须进 design-md（下游据此核对值）");
        md.Should().Contain("生成器 v" + GuidelineGenerator.Version);
    }

    [Fact]
    public void AC16_bundle_两文件真在zip里_Manifest也列了()
    {
        var bytes = _export.Produce(_projectId, ExportFormats.Bundle, null).Bytes;
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var names = zip.Entries.Select(e => e.FullName).ToList();

        names.Should().Contain("guidelines/GUIDELINES.md");
        names.Should().Contain("guidelines/guidelines.json");

        var md = Read(zip, "guidelines/GUIDELINES.md");
        md.Should().Contain($"UX 规范（{GuidelineGenerator.Codes.Length} 条）");
        // 有标题没内容 = 假章：至少扫到若干处"路径（当前值）"括注，证明值是现查出来的
        Regex.Matches(md, @"`([a-z][a-z0-9.-]*)`（[^）]+）").Count
            .Should().BeGreaterThan(GuidelineGenerator.Codes.Length, $"每条规范至少要有一处真实括注，实测 {Regex.Matches(md, @"`([a-z0-9.-]+)`（[^）]+）").Count} 处");

        var json = JsonDocument.Parse(Read(zip, "guidelines/guidelines.json")).RootElement;
        json.GetProperty("count").GetInt32().Should().Be(GuidelineGenerator.Codes.Length);
        var items = json.GetProperty("guidelines").EnumerateArray().ToList();
        items.Should().HaveCount(GuidelineGenerator.Codes.Length);
        items.Select(i => i.GetProperty("code").GetString()).Should().BeEquivalentTo(GuidelineGenerator.Codes);
        items.All(i => i.GetProperty("brokenRefs").GetArrayLength() == 0).Should().BeTrue("新项目的规范工件不该带断链");

        var manifest = Read(zip, "README.md");
        manifest.Should().Contain("`guidelines/GUIDELINES.md`", "Manifest 必须列出包内文件");
        manifest.Should().Contain($"{GuidelineGenerator.Codes.Length} 条");

        // 包内两份与单格式导出的同一函数产物一致（同一份渲染，不存在第二套口径）
        var snap = _export.Load(_projectId, null);
        md.TrimEnd().Should().Be(_export.GuidelinesMd(snap).TrimEnd());
    }

    [Fact]
    public void AC16_agent_rules_指路规范_且只在有规范时指()
    {
        var rules = Get(ExportFormats.AgentRules);
        rules.Should().Contain("guidelines/GUIDELINES.md");
        rules.Should().Contain("\"sections\":[\"guidelines\"]", "agent-rules 必须给出 design_context 的规范章节入口");
        rules.Should().Contain("design_context");

        var bare = NewProject("bare", seedGuidelines: false).Id;
        var none = _export.Produce(bare, ExportFormats.AgentRules, "light").Text;
        none.Should().NotContain("guidelines/GUIDELINES.md", "没有规范却指路 = 指向包里不存在的文件");
    }

    [Fact]
    public void AC16_空规范不开章_不写文件_不列清单()
    {
        var bare = NewProject("empty", seedGuidelines: false).Id;

        var outcome = _brief.Build(bare, "light", null, 60000, DesignBriefBuilder.Markdown);
        outcome.Sections.Should().NotContain("guidelines");
        outcome.Markdown!.Should().NotContain("## UX 规范");

        var md = Get(ExportFormats.DesignMd, "light", bare);
        md.Should().NotContain("## UX 规范");

        using var zip = new ZipArchive(new MemoryStream(_export.Produce(bare, ExportFormats.Bundle, null).Bytes), ZipArchiveMode.Read);
        var names = zip.Entries.Select(e => e.FullName).ToList();
        names.Should().NotContain("guidelines/GUIDELINES.md");
        names.Should().NotContain("guidelines/guidelines.json");
        Read(zip, "README.md").Should().NotContain("guidelines/GUIDELINES.md");
    }

    [Fact]
    public void AC16_contentHash_随规范变化_同内容重算幂等()
    {
        String Hash() => DesignBriefBuilder.ContentHash(_export.Load(_projectId, "light"), "light");

        var before = Hash();
        Hash().Should().Be(before, "同样内容重算必须得同一个 hash（幂等）");

        _svc.Save(_projectId, "layout-grid", new GuidelinePatch { Title = "改了标题" });
        var after = Hash();
        after.Should().NotBe(before, "改了规范而 hash 不变 = 下游无法发现交付物与规范不同步");

        // 归档也算改动：清单少了一条，hash 必须跟着变
        _svc.Archive(_projectId, "motion");
        Hash().Should().NotBe(after);
    }

    [Fact]
    public void AC16_compact主题不抛_且不出现假断链()
    {
        // 密度主题按现状没有语义层（TODO「生成器主题覆盖缺陷」）：规范章必须补默认主题取值，而不是满屏「已不存在」
        foreach (var format in new[] { ExportFormats.DesignMd, ExportFormats.Brief, ExportFormats.AgentRules })
        {
            var text = _export.Produce(_projectId, format, "compact").Text;
            if (format == ExportFormats.AgentRules) { text.Should().NotContain("令牌已不存在"); continue; }
            text.Should().Contain("## UX 规范");
            text.Should().NotContain("令牌已不存在", $"{format}@compact 的规范章把主题层令牌判成断链了");
        }
    }

    [Fact]
    public void AC16_导出括注值等于tokens_effective的值()
    {
        var effective = Effective("light");
        effective.Should().NotBeEmpty();

        var md = Get(ExportFormats.DesignMd);
        var json = JsonDocument.Parse(ReadBundleJson()).RootElement;

        var checkedValues = 0;
        var mdChecked = 0;
        foreach (var item in json.GetProperty("guidelines").EnumerateArray())
        {
            var code = item.GetProperty("code").GetString()!;
            foreach (var token in item.GetProperty("tokenValues").EnumerateObject())
            {
                if (token.Value.ValueKind != JsonValueKind.String) continue;
                var exported = token.Value.GetString();
                // 与令牌页比：不是与导出自己的取值函数比（自己比自己=空转）
                effective.Should().ContainKey(token.Name, $"规范引用了 effective 里没有的令牌 {token.Name}");
                exported.Should().Be(effective[token.Name], $"{code}：导出 json 里的 {token.Name} 与令牌页不是同一个值");
                checkedValues++;

                // design-md 的「引用令牌」行是 `路径`＝值 形态，同一份值必须出现在那里
                if (md.Contains($"`{token.Name}`＝"))
                {
                    md.Should().Contain($"`{token.Name}`＝{exported}", $"{code}：design-md 的引用令牌行与 json/令牌页不同值");
                    mdChecked++;
                }
            }
        }
        checkedValues.Should().BeGreaterThan(0, "一条值都没比对到 = 这条用例是空转");
        mdChecked.Should().BeGreaterThan(0, "design-md 里一条引用令牌行都没比对到 = 那条线没被走到");
    }

    [Fact]
    public void AC16_Stardust投影仍是十类实体_规范不是新实体类()
    {
        ExportService.StardustEntities.Should().HaveCount(10, "AC16：规范是新投影面，不许混进 Stardust 实体清单");
        var index = JsonDocument.Parse(Get(ExportFormats.StardustJson)).RootElement;
        var entities = index.GetProperty("entities").EnumerateArray().Select(e => e.GetProperty("entity").GetString()).ToList();
        entities.Should().HaveCount(10);
        entities.Should().NotContain("guideline", "Stardust 十类不变 = 清单里不能多出规范这一类");
    }

    Dictionary<String, String?> Effective(String theme)
    {
        var res = _ctl.EffectiveTokens(_projectId, theme) as JsonResult;
        var root = JsonDocument.Parse(JsonSerializer.Serialize(res!.Value, JsonOpts)).RootElement;
        var map = new Dictionary<String, String?>(StringComparer.Ordinal);
        foreach (JsonElement item in root.GetProperty("data").GetProperty("items").EnumerateArray())
            map[item.GetProperty("path").GetString()!] = item.GetProperty("value").GetString();
        return map;
    }

    /// <summary>
    /// V10 前半（06 预注册验收清单点名的就是**三种交付物**）：**抽 3 个令牌改值 → brief / design-md / bundle 里现查的数
    /// 跟着变，而库里存的规范文本一字不动**。
    ///
    /// 为什么必须单独有这一条：既有的同源用例比的是"**同一时刻的两个视图**"（导出括注 == `tokens/effective`）。
    /// 那种比法对"数字被抄进规范正文"这种第二份真相**永远不会红**——抄进去了两边照样相等。
    /// 决策 D5 的承诺是"改值不必改规范"，只有**动态**判据钉得住：改值 → 再渲染 → 数跟着变（现查）+ 文本零改动（没被回写）。
    /// 为什么要三种：三条投影的取值出口不同（design-md 走 `引用令牌：path＝值` 行、bundle 走 `tokenValues` JSON、
    /// brief 走紧凑形态的行内括注），只验 design-md 等于放过另两条。
    /// 抽样口径写死在代码里，不让判据自己挑软柿子：候选须①被规范引用、②共享层 px 型且值可解析、③**当前交付里就以这个共享值出**
    /// ——③专门排掉主题层有覆盖的令牌：那种改共享值不会变交付，拿它判"跟着变"是我自己造出来的假红。
    /// </summary>
    [Fact]
    public void V10_抽三个令牌改值_三种交付物里现查的数跟着变_规范文本一字不动()
    {
        var refs = _repo.List(_projectId).SelectMany(g => GuidelineRepository.ReadPaths(g.TokenRefsJson)).Distinct().ToList();
        refs.Should().NotBeEmpty("生成器产出的规范本来就该引用令牌");

        var beforeMd = Get(ExportFormats.DesignMd);

        // 紧凑章只渲染「标题 + 摘要 + 全部 MUST」：引用不在这几处里，brief 就没有"该处括注"可判
        var inlineRefs = _repo.List(_projectId).SelectMany(g =>
        {
            var texts = new List<String> { g.Summary ?? "" };
            texts.AddRange(GuidelineRepository.ReadRules(g.RulesJson)
                .Where(r => r.Level.Equals("MUST", StringComparison.OrdinalIgnoreCase)).Select(r => r.Text));
            return texts.SelectMany(GuidelineRenderer.ConcreteRefs);
        }).Distinct(StringComparer.Ordinal).ToList();

        var pool = _tokens.FindShared(_projectId)
            .Where(t => refs.Contains(t.Path) && t.Value?.EndsWith("px", StringComparison.Ordinal) == true
                        && Double.TryParse(t.Value[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v > 0)
            .Where(t => beforeMd.Contains($"`{t.Path}`＝{t.Value}", StringComparison.Ordinal))
            // 进得了紧凑章的排前面：否则三个全只验到 design-md 与 bundle，brief 这条产物等于没扫
            .OrderBy(t => inlineRefs.Contains(t.Path) ? 0 : 1).ThenBy(t => t.Path, StringComparer.Ordinal)
            .ToList();
        pool.Count.Should().BeGreaterThanOrEqualTo(3, "候选不足 3 个 = 抽样判据无从下手（宁可红，不悄悄退回「一个令牌一种交付」）");

        var chosen = pool.Take(3).Select(t => (Token: t, Old: t.Value!,
            Next: $"{(Double.Parse(t.Value![..^2], CultureInfo.InvariantCulture) * 3).ToString("0.##", CultureInfo.InvariantCulture)}px")).ToList();
        foreach (var c in chosen) c.Next.Should().NotBe(c.Old, "新值必须与旧值不同，否则'跟着变'没有对照");
        chosen.Should().Contain(c => inlineRefs.Contains(c.Token.Path),
            "三个令牌没有一个进得了 brief 的紧凑章 ⇒ brief 这条交付等于没被本判据扫到");

        // 规范行的"指纹"：改值前后必须逐字段相同（含 UpdatedAt——被回写过就会动）
        List<String> Fingerprint() => _repo.List(_projectId)
            .OrderBy(g => g.Code, StringComparer.Ordinal)
            .Select(g => $"{g.Code}|{g.Title}|{g.Summary}|{g.Body}|{g.RulesJson}|{g.TokenRefsJson}|{g.UpdatedAt:O}|{g.GeneratorVersion}|{g.Status}|{g.SortOrder}")
            .ToList();
        var beforeText = Fingerprint();
        beforeText.Should().HaveCountGreaterThan(0);

        String? BundleValue(String path)
        {
            using var doc = JsonDocument.Parse(ReadBundleJson());
            foreach (var item in doc.RootElement.GetProperty("guidelines").EnumerateArray())
                if (item.GetProperty("tokenValues").TryGetProperty(path, out var v))
                    return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            return null;
        }

        foreach (var c in chosen)
        {
            c.Token.Value = c.Next;
            c.Token.Save();
        }

        try
        {
            var afterMd = Get(ExportFormats.DesignMd);
            var brief = _brief.Build(_projectId, "light", null, 60000, DesignBriefBuilder.Markdown).Markdown!;
            brief.Should().Contain("## UX 规范", "brief 不开规范章，行内括注就无从谈起");

            var briefChecked = 0;
            foreach (var c in chosen)
            {
                afterMd.Should().Contain($"`{c.Token.Path}`＝{c.Next}",
                    $"改了令牌值（{c.Old} → {c.Next}），design-md 里现查的数必须跟着变——D5「改值不必改规范」的全部意义");
                afterMd.Should().NotContain($"`{c.Token.Path}`＝{c.Old}",
                    "旧值还挂在渲染结果里 = 那个数是被存进规范文本的，不是现查的（第二份真相）");

                BundleValue(c.Token.Path).Should().Be(c.Next,
                    $"{c.Token.Path}：bundle 的 tokenValues 没跟着改值走 ⇒ 它不是现查的");

                if (inlineRefs.Contains(c.Token.Path))
                {
                    brief.Should().Contain($"`{c.Token.Path}`（{c.Next}）",
                        $"{c.Token.Path} 在紧凑章里出现，括注必须跟着新值——brief 与 design-md 共用同一个渲染器，这里分叉就是两套口径");
                    brief.Should().NotContain($"`{c.Token.Path}`（{c.Old}）", $"{c.Token.Path}：brief 里仍挂着旧值");
                    briefChecked++;
                }
            }

            briefChecked.Should().BeGreaterThan(0, "紧凑章一条都没核对 = 循环空转（自查表 #56）");
            Fingerprint().Should().Equal(beforeText,
                "规范行必须一字不动（标题/摘要/正文/规则/引用/UpdatedAt/GeneratorVersion/Status/SortOrder 全同）：数值现查就不该因为改值而回写规范");
        }
        finally
        {
            // 还原：铁律 10 不做物理删除，这里只把值改回去（同库同项目，不留脏数据给后续用例）
            foreach (var c in chosen)
            {
                var back = _tokens.Find(_projectId, DesignSystemConstants.SharedThemeId, c.Token.Path);
                if (back != null)
                {
                    back.Value = c.Old;
                    back.Save();
                }
            }
        }
    }

    /// <summary>
    /// V10 后半（02-spec FR8）：**数字守卫只约束生成器模板**，手写规范里写死数字不是错误。
    /// 两半判据：① 含 `8px` / `#0f172a` 的手写正文必须**存得下**（误拒 = 守卫越界）；② 存进去的必须**原样**出现在
    /// design-md 与 bundle 里（`bodyRaw`/`textRaw` 逐字相等），而不是被守卫改写或悄悄去掉数字。
    /// </summary>
    [Fact]
    public void V10_手写规范正文带数字_不被误拒_原样保存与渲染()
    {
        const String Body = "正文行高按 8px 网格对齐，卡片底色取 #0f172a，大标题固定 24px。";
        const String RuleText = "同一屏内边距 16px 起，不得各写各的。";

        var saved = _svc.Save(_projectId, "manual-numeric", new GuidelinePatch
        {
            Title = "手写含数字口径",
            Summary = "口径由人写死，数值不做现查",
            Category = "content",
            Status = "adopted",
            Body = Body,
            Rules = [new GuidelineRuleInput(null, "MUST", RuleText)],
        });

        saved.Source.Should().Be("manual", "走用户编辑路径必须标成 manual，否则重新生成会把它当模板产出覆盖掉");
        saved.Body.Should().Be(Body, "手写正文里带数字不是错误：守卫只约束生成器模板（FR8），这里改掉一个字就是误拒");

        var md = Get(ExportFormats.DesignMd);
        md.Should().Contain(Body, "design-md 里手写正文要原样出现（不被守卫改写、不被截断）");
        md.Should().Contain(RuleText, "MUST 规则要原样进完整形态投影");

        using var doc = JsonDocument.Parse(ReadBundleJson());
        var rows = doc.RootElement.GetProperty("guidelines").EnumerateArray()
            .Where(i => i.GetProperty("code").GetString() == "manual-numeric").ToList();
        rows.Should().HaveCount(1, "手写规范没进 bundle = 库里有、交付里没有（假交付）");
        var item = rows[0];
        item.GetProperty("bodyRaw").GetString().Should().Be(Body, "bundle 的 bodyRaw 是原文出口，与库里的不一致=交付与库不同源");
        item.GetProperty("body").GetString().Should().Be(Body, "渲染形态把写死的数字弄丢了或改了口径（这条正文没有反引号路径，括注不该动它）");
        var rule = item.GetProperty("rules").EnumerateArray().Single();
        rule.GetProperty("textRaw").GetString().Should().Be(RuleText, "规则原文出口被改写");
    }

    String ReadBundleJson()
    {
        using var zip = new ZipArchive(new MemoryStream(_export.Produce(_projectId, ExportFormats.Bundle, null).Bytes), ZipArchiveMode.Read);
        return Read(zip, "guidelines/guidelines.json");
    }

    static String Read(ZipArchive zip, String name)
    {
        var entry = zip.GetEntry(name) ?? throw new Xunit.Sdk.XunitException($"包里缺 {name}");
        using var sr = new StreamReader(entry.Open(), Encoding.UTF8);
        return sr.ReadToEnd();
    }

}

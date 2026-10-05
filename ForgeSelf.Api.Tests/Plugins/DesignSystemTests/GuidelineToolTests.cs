using System.Text.Json;
using ForgeSelf.Api.Plugins.DesignSystem;
using ForgeSelf.Api.Plugins.DesignSystem.Agent;
using ForgeSelf.Api.Plugins.DesignSystem.Data;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;
using ForgeSelf.Api.Plugins.DesignSystem.Services;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>
/// M3 AC18：8 个 design_* 工具的规范增量 —— <c>design_edit action=guideline</c>、
/// <c>design_lookup kind=guideline</c>、<c>design_context sections=guidelines</c>、
/// <c>design_review mode=checklist</c> 的派生条目。
///
/// 三条工具纪律每条都有用例：
/// ① **工具总数仍是 8**（只增动作/类别，不新增工具）；schema 是 agent 唯一的契约面，
///    枚举一律由后端词表生成（<see cref="GuidelineCategories.SchemaProperties"/>），不手抄第二份；
/// ② **写动作过写开关**：apply=true 才算写，关闭写入时零行落库；
/// ③ **写完必须读回得到同一份**（写了什么不由调用方声明，由库里回读）。
/// </summary>
[Collection("XCode")]
public class GuidelineToolTests : IDisposable
{
    static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    readonly String _dbDir;
    readonly DesignProjectService _projects = new();
    readonly TokenRepository _tokens = new();
    readonly CatalogRepository _catalog = new();
    readonly AuditRepository _audits = new();
    readonly GuidelineRepository _repo = new();
    readonly GuidelineService _svc;
    readonly AuditEngine _auditEngine;
    readonly ExportService _export;
    readonly AgentAccess _agentAccess;
    readonly GenerationService _generation;
    readonly DesignToolKit _kit;
    readonly Int64 _projectId;
    readonly String _projectCode;

    public GuidelineToolTests()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfDsGuidelineTool_{Guid.NewGuid():N}");
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

        _auditEngine = new AuditEngine(_tokens, _projects, _audits);
        _export = new ExportService(_tokens, _projects, _catalog);
        _svc = new GuidelineService(_projects, _tokens, _repo);
        _export.Guidelines = _svc;
        _generation = new GenerationService(_tokens, _projects, _catalog, _auditEngine) { Guidelines = _svc };
        _agentAccess = new AgentAccess(new DesignSystemPaths(_dbDir));
        var review = new DesignReviewService(_export, _tokens, _projects);
        var brief = new DesignBriefBuilder(_export, _projects, _catalog, review);
        _export.BriefBuilder = brief;
        _kit = new DesignToolKit(_projects, _tokens, _catalog, _audits, _auditEngine, _export,
            new ReleaseService(_tokens, _projects, _audits, _auditEngine, new DesignSystemPaths(_dbDir), _catalog, _repo),
            _generation, _agentAccess, review, new QuickCreateService(_projects, _generation), brief, _svc);

        var p = _projects.Create(new ProjectInput { Code = "tl-" + Guid.NewGuid().ToString("N")[..8], Name = "规范工具项目" });
        _projectId = p.Id;
        _projectCode = p.Code;
        _generation.Run(p.Id, new GenerationRequest { Hue = 210, Themes = ["light", "dark"] }, false);
    }

    public void Dispose() => GC.SuppressFinalize(this);

    JsonElement Data(String toolName, String args)
    {
        var tool = Tool(toolName);
        var raw = tool.ExecuteAsync(args).GetAwaiter().GetResult();
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement.Clone();
        root.GetProperty("success").GetBoolean().Should().BeTrue($"{toolName} 返回失败：{raw}");
        return root.GetProperty("data");
    }

    (Boolean Success, String Error) TryRaw(String toolName, String args)
    {
        var raw = Tool(toolName).ExecuteAsync(args).GetAwaiter().GetResult();
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement.Clone();
        return (root.GetProperty("success").GetBoolean(),
                root.TryGetProperty("error", out var e) ? e.GetString() ?? "" : "");
    }

    DesignToolBase Tool(String name) => name switch
    {
        DesignToolIndex.Guide => new DesignGuideTool(_kit),
        DesignToolIndex.Context => new DesignContextTool(_kit),
        DesignToolIndex.Lookup => new DesignLookupTool(_kit),
        DesignToolIndex.Review => new DesignReviewTool(_kit),
        DesignToolIndex.Edit => new DesignEditTool(_kit),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [Fact]
    public void AC18_工具总数仍是8_动作与类别枚举含规范()
    {
        DesignToolIndex.All.Should().HaveCount(8, "M3 只给既有工具增动作/类别，不新增第 9 个工具");
        DesignToolIndex.All.Select(e => e.Name).Should().Equal(
            ["design_guide", "design_context", "design_lookup", "design_review", "design_audit",
             "design_presets", "design_create", "design_edit"]);

        var edit = JsonDocument.Parse(DesignToolIndex.Of(DesignToolIndex.Edit).Schema).RootElement;
        edit.GetProperty("properties").GetProperty("action").GetProperty("enum").EnumerateArray()
            .Select(e => e.GetString()).Should().Contain("guideline");

        var lookup = JsonDocument.Parse(DesignToolIndex.Of(DesignToolIndex.Lookup).Schema).RootElement;
        lookup.GetProperty("properties").GetProperty("kind").GetProperty("enum").EnumerateArray()
            .Select(e => e.GetString()).Should().Contain("guideline");

        var context = JsonDocument.Parse(DesignToolIndex.Of(DesignToolIndex.Context).Schema).RootElement;
        context.GetProperty("properties").GetProperty("sections").GetProperty("description").GetString()
            .Should().Contain("guidelines");

        // 枚举由词表生成，不手抄：schema 里的分类/状态必须与后端校验用的完全一致
        var props = edit.GetProperty("properties");
        props.GetProperty("category").GetProperty("enum").EnumerateArray().Select(e => e.GetString())
            .Should().Equal(GuidelineCategories.All);
        props.GetProperty("status").GetProperty("enum").EnumerateArray().Select(e => e.GetString())
            .Should().Equal(GuidelineCategories.Statuses);
        props.GetProperty("rules").GetProperty("items").GetProperty("properties").GetProperty("level")
            .GetProperty("enum").EnumerateArray().Select(e => e.GetString()).Should().Equal(GuidelineCategories.Levels);
    }

    [Fact]
    public void AC18_edit_guideline_干跑不写_写入后读回是同一份()
    {
        var before = _repo.Count(_projectId);

        var dry = Data(DesignToolIndex.Edit, $$"""{"project":"{{_projectCode}}","action":"guideline","code":"layout-test","title":"栅格干跑","body":"外边距用 `space.6`"}""");
        dry.GetProperty("applied").GetBoolean().Should().BeFalse();
        dry.GetProperty("wouldCreate").GetBoolean().Should().BeTrue();
        _repo.Count(_projectId).Should().Be(before, "apply=false 却写了行 = 干跑不是干跑");

        var wrote = Data(DesignToolIndex.Edit, $$"""{"project":"{{_projectCode}}","action":"guideline","apply":true,"code":"layout-test","title":"栅格特例","summary":"一句话","body":"外边距用 `space.6`","category":"layout","tokens":["space.6"],"rules":[{"level":"MUST","text":"外边距只用 `space.6`，不自写像素"}]}""");
        wrote.GetProperty("applied").GetBoolean().Should().BeTrue();
        wrote.GetProperty("created").GetBoolean().Should().BeTrue();

        var row = _repo.Find(_projectId, "layout-test")!;
        row.Title.Should().Be("栅格特例");
        row.Body.Should().Be("外边距用 `space.6`");
        row.Source.Should().Be("manual", "工具写入与界面写入同一口径：都要被重新生成保护");
        GuidelineRepository.ReadRules(row.RulesJson).Single().Level.Should().Be("MUST");

        // 读回：走另一个工具（lookup），必须与写进去的一字不差
        var read = Data(DesignToolIndex.Lookup, $$"""{"kind":"guideline","project":"{{_projectCode}}","code":"layout-test"}""");
        var g = read.GetProperty("guideline");
        g.GetProperty("title").GetString().Should().Be("栅格特例");
        g.GetProperty("bodyRaw").GetString().Should().Be("外边距用 `space.6`");
        g.GetProperty("rules").EnumerateArray().Single().GetProperty("textRaw").GetString()
            .Should().Be("外边距只用 `space.6`，不自写像素");

        // 第二次同 code = 更新而不是新建（upsert 判重走库，不靠调用方声明）
        Data(DesignToolIndex.Edit, $$"""{"project":"{{_projectCode}}","action":"guideline","apply":true,"code":"layout-test","title":"改成二号"}""");
        _repo.Find(_projectId, "layout-test")!.Title.Should().Be("改成二号");
        _repo.Count(_projectId).Should().Be(before + 1, "upsert 却多建一行 = 唯一判重失效");
    }

    [Fact]
    public void AC18_edit_guideline_空tokens与rules不清空既有内容()
    {
        Data(DesignToolIndex.Edit, $$"""{"project":"{{_projectCode}}","action":"guideline","apply":true,"code":"forms-test","title":"表单","body":"间距用 `space.3`","tokens":["space.3"],"rules":[{"level":"MUST","text":"错误文案写原因"}]}""");
        var before = _repo.Find(_projectId, "forms-test")!;
        var refsBefore = GuidelineRepository.ReadPaths(before.TokenRefsJson);
        var rulesBefore = GuidelineRepository.ReadRules(before.RulesJson);
        refsBefore.Should().Contain("space.3");

        // 只改标题，但按 agent 常见写法把 tokens/rules 传成空数组：一律当"这次不改"，不能清空
        Data(DesignToolIndex.Edit, $$"""{"project":"{{_projectCode}}","action":"guideline","apply":true,"code":"forms-test","title":"只改标题","tokens":[],"rules":[]}""");

        var after = _repo.Find(_projectId, "forms-test")!;
        after.Title.Should().Be("只改标题");
        GuidelineRepository.ReadPaths(after.TokenRefsJson).Should().Equal(refsBefore); // 空数组若被当成「清空清单」写进去，这条就红
        GuidelineRepository.ReadRules(after.RulesJson).Select(r => r.Text).Should().Equal(rulesBefore.Select(r => r.Text));
    }

    [Fact]
    public void AC18_edit_guideline_过写开关_关闭时零写入()
    {
        _agentAccess.Set(false);
        var (ok, error) = TryRaw(DesignToolIndex.Edit, $$"""{"project":"{{_projectCode}}","action":"guideline","apply":true,"code":"blocked","title":"该被拦"}""");
        ok.Should().BeFalse("写开关关闭时 apply=true 必须被拦");
        error.Should().Contain("写入");
        _repo.Find(_projectId, "blocked").Should().BeNull("被拦的写入却还是落了行");

        // 只读类别不受影响
        var list = Data(DesignToolIndex.Lookup, $$"""{"kind":"guideline","project":"{{_projectCode}}"}""");
        list.GetProperty("total").GetInt32().Should().Be(GuidelineGenerator.Codes.Length);

        // apply=false 不算写动作：开关关着也能干跑
        var dry = Data(DesignToolIndex.Edit, $$"""{"project":"{{_projectCode}}","action":"guideline","code":"ok-dry","title":"干跑仍可用"}""");
        dry.GetProperty("applied").GetBoolean().Should().BeFalse();

        _agentAccess.Set(true);
        Data(DesignToolIndex.Edit, $$"""{"project":"{{_projectCode}}","action":"guideline","apply":true,"code":"ok-dry","title":"开关恢复后可写"}""")
            .GetProperty("created").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void AC18_lookup_guideline_列表带值与归档计数_详情带原文_未知code给候选()
    {
        _svc.Archive(_projectId, "motion");
        var list = Data(DesignToolIndex.Lookup, $$"""{"kind":"guideline","project":"{{_projectCode}}"}""");
        list.GetProperty("total").GetInt32().Should().Be(GuidelineGenerator.Codes.Length - 1, "归档行不进默认清单");
        list.GetProperty("archived").GetInt32().Should().Be(1, "归档条数必须说出来，不然像漏了");
        var codes = list.GetProperty("guidelines").EnumerateArray().Select(x => x.GetProperty("code").GetString()).ToList();
        codes.Should().NotContain("motion");
        codes.Should().HaveCount(GuidelineGenerator.Codes.Length - 1);

        // 每条都带当前值：tokenValues 与导出/令牌页同一来源，且不允许出现"取不到值"的静默
        var buttons = list.GetProperty("guidelines").EnumerateArray().First(x => x.GetProperty("code").GetString() == "buttons");
        var values = buttons.GetProperty("tokenValues").EnumerateObject().ToList();
        values.Should().NotBeEmpty("buttons 规范必须引用真令牌");
        values.All(p => p.Value.ValueKind == JsonValueKind.String).Should().BeTrue("新项目里取值不该为空");
        buttons.GetProperty("brokenRefs").GetArrayLength().Should().Be(0);

        var filtered = Data(DesignToolIndex.Lookup, $$"""{"kind":"guideline","project":"{{_projectCode}}","q":"禁用态"}""");
        filtered.GetProperty("total").GetInt32().Should().BeGreaterThan(0);
        filtered.GetProperty("guidelines").EnumerateArray().Select(x => x.GetProperty("code").GetString())
            .Should().Contain("buttons");
        filtered.GetProperty("omittedByQuery").GetInt32().Should().BeGreaterThan(0, "过滤没减任何行 = q 参数没生效");

        var missing = Data(DesignToolIndex.Lookup, $$"""{"kind":"guideline","project":"{{_projectCode}}","code":"no-such-code"}""");
        missing.GetProperty("error").GetString().Should().Contain("no-such-code");
        missing.GetProperty("candidates").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public void AC18_context_sections_guidelines_回紧凑章_空项目不开章()
    {
        var data = Data(DesignToolIndex.Context, $$"""{"project":"{{_projectCode}}","sections":["identity","guidelines"],"maxChars":60000}""");
        var sections = data.GetProperty("sections").EnumerateArray().Select(e => e.GetString()).ToList();
        sections.Should().Contain("guidelines");
        var md = data.GetProperty("markdown").GetString()!;
        md.Should().Contain("## UX 规范");
        md.Should().Contain("- [ ]", "紧凑章要能当勾选清单用");
        md.Should().Contain("只列 MUST");
        md.Should().NotContain("elevation-2（令牌已不存在）", "规范章取值必须走主题视图");

        // 只要规范章时不该把别的章节塞进来（identity 恒含除外）
        sections.Should().Equal(["identity", "guidelines"]);

        var empty = _projects.Create(new ProjectInput { Code = "tl2-" + Guid.NewGuid().ToString("N")[..8], Name = "无规范项目" });
        _export.Guidelines = null;   // 未接规范服务：生成链路不种规范，context 也不该凭空开章
        DesignGenerator.ApplyToProject(_tokens, _projects, empty.Id, new GenerationRequest { Hue = 210 }, false);
        _export.Guidelines = _svc;
        var none = Data(DesignToolIndex.Context, $$"""{"project":"{{empty.Code}}","sections":["identity","guidelines"]}""");
        none.GetProperty("sections").EnumerateArray().Select(e => e.GetString()).Should().NotContain("guidelines");
        none.GetProperty("markdown").GetString().Should().NotContain("## UX 规范");
    }

    [Fact]
    public void AC18_checklist_派生条目_id与级别映射_tokens只含真存在的路径()
    {
        var data = Data(DesignToolIndex.Review, $$"""{"mode":"checklist","project":"{{_projectCode}}","page":"any"}""");
        var items = data.GetProperty("items").EnumerateArray().ToList();
        var derived = items.Where(i => i.GetProperty("id").GetString()!.StartsWith("g:", StringComparison.Ordinal)).ToList();

        // 派生条目 = 全部未归档规范的每一条规则（14 条规范共 43 条规则上下，取"不少于规则总数的一半"这种自洽下限没有意义，
        // 所以这里用库里真数逐条对：库里几条规则，清单就必须有几条派生条目）
        var expected = _repo.List(_projectId).Sum(g => GuidelineRepository.ReadRules(g.RulesJson).Count);
        derived.Should().HaveCount(expected, "规范里的规则没全部进清单 = 一半要求悄悄掉了");

        var existing = _tokens.FindShared(_projectId).Select(t => t.Path).ToHashSet(StringComparer.Ordinal);
        foreach (var theme in _projects.ListThemes(_projectId))
            foreach (var t in _tokens.FindThemed(_projectId, theme.Id)) existing.Add(t.Path);

        var byLevel = new Dictionary<String, String>(StringComparer.Ordinal);
        foreach (var g in _repo.List(_projectId))
            foreach (var r in GuidelineRepository.ReadRules(g.RulesJson))
                byLevel[$"g:{g.Code}:{r.Id}"] = r.Level;

        foreach (var item in derived)
        {
            var id = item.GetProperty("id").GetString()!;
            byLevel.Should().ContainKey(id, $"清单里出现了库里没有的派生条目 {id}");
            var level = byLevel[id];
            var expect = level == "MUST" ? "error" : level == "SHOULD" ? "warning" : "info";
            item.GetProperty("severity").GetString().Should().Be(expect, $"{id} 级别 {level} 的严重度映射错了");
            foreach (JsonElement p in item.GetProperty("tokens").EnumerateArray())
                existing.Should().Contain(p.GetString(), $"派生条目 {id} 列了不存在的令牌 {p.GetString()}");
        }

        // 静态基线不能因为派生条目而被挤掉（M1 的 ≥8 条是既有契约）
        items.Count(i => !i.GetProperty("id").GetString()!.StartsWith("g:"))
            .Should().BeGreaterThanOrEqualTo(8, "静态交付前清单条目数量少于 M1 契约");
        items.Select(i => i.GetProperty("id").GetString()).Should().OnlyHaveUniqueItems("清单条目 id 重复 = 下游勾选状态会串");
    }

    /// <summary>
    /// AC18 的脏数据下限：<c>level</c> 缺失/为 null 的规则行（绕过写入校验的历史行、或直接改库塞进来的）不许把审查工具打崩。
    /// 两半判据：① 仓储**读侧**把空级别规一成 <c>SHOULD</c>——这条一旦掉下去，下面的清单调用就会 NRE，所以判据能红；
    /// ② checklist 照常出该条，且严重度按 SHOULD 映射成 <c>warning</c>（口径与正常行同一条路）。
    /// </summary>
    [Fact]
    public void AC18_脏规则行_级别为null_checklist不抛且回落SHOULD()
    {
        var target = _repo.List(_projectId).OrderBy(g => g.Code, StringComparer.Ordinal).First();
        var clean = target.RulesJson;
        target.RulesJson = """[{"id":"dirty-1","level":null,"text":"级别缺失的历史规则行"}]""";
        target.Save();

        try
        {
            var read = GuidelineRepository.ReadRules(target.RulesJson);
            read.Should().HaveCount(1, "脏行不该被静默丢掉（丢了就等于规范少一条）");
            read[0].Level.Should().Be("SHOULD", "级别缺失必须规一成 SHOULD —— 留 null 就是把崩溃推给每个消费方");

            var data = Data(DesignToolIndex.Review, $$"""{"mode":"checklist","project":"{{_projectCode}}","page":"any"}""");
            var hit = data.GetProperty("items").EnumerateArray()
                .Where(i => i.GetProperty("id").GetString() == $"g:{target.Code}:dirty-1").ToList();
            hit.Should().HaveCount(1, "脏行没进清单 = 规范与审查两套读法各读各的（第二份真相）");
            hit[0].GetProperty("severity").GetString().Should().Be("warning", "SHOULD 的严重度必须是 warning");
        }
        finally
        {
            // 还原：铁律 10 不做物理删除，这里只把规则行改回原样（隔离库里的本项目自有数据）
            var back = _repo.Find(_projectId, target.Code);
            if (back != null)
            {
                back.RulesJson = clean;
                back.Save();
            }
        }
    }

    [Fact]
    public void AC18_edit_guideline_不合形入参_错误文案指出具体值()
    {
        var (ok1, err1) = TryRaw(DesignToolIndex.Edit, $$"""{"project":"{{_projectCode}}","action":"guideline","apply":true,"title":"没有 code"}""");
        ok1.Should().BeFalse();
        err1.Should().Contain("code");

        var (ok2, err2) = TryRaw(DesignToolIndex.Edit, $$"""{"project":"{{_projectCode}}","action":"guideline","apply":true,"code":"布局 Grid","title":"中文 code"}""");
        ok2.Should().BeFalse();
        err2.Should().Contain("布局 Grid");

        var (ok3, err3) = TryRaw(DesignToolIndex.Edit, $$"""{"project":"{{_projectCode}}","action":"guideline","apply":true,"code":"forms-x","category":"typography"}""");
        ok3.Should().BeFalse();
        err3.Should().Contain("typography").And.Contain("layout");

        var (ok4, err4) = TryRaw(DesignToolIndex.Edit, $$"""{"project":"{{_projectCode}}","action":"guideline","apply":true,"code":"forms-y","tokens":["color.never.exists"]}""");
        ok4.Should().BeFalse();
        err4.Should().Contain("color.never.exists");
        _repo.Find(_projectId, "forms-y").Should().BeNull("校验失败却落了行");
    }
}

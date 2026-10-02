using System.Text.Json;
using ForgeSelf.Api.Plugins.DesignSystem;
using ForgeSelf.Api.Plugins.DesignSystem.Agent;
using ForgeSelf.Api.Plugins.DesignSystem.Data;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;
using ForgeSelf.Api.Plugins.DesignSystem.Services;
using ForgeSelf.Abstractions;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>§B 工具契约：8 个 design_* 工具的名/读-写归类/描述/schema 合法性与铁律（description≤400、禁 oneOf/anyOf、required⊆properties）。</summary>
public class DesignAgentToolContractTests
{
    static readonly IReadOnlyList<DesignToolIndex.Entry> All = DesignToolIndex.All;

    static IToolFunctionExtension Tool(DesignToolIndex.Entry e, DesignToolKit kit) => e.Name switch
    {
        DesignToolIndex.Guide => new DesignGuideTool(kit),
        DesignToolIndex.Context => new DesignContextTool(kit),
        DesignToolIndex.Lookup => new DesignLookupTool(kit),
        DesignToolIndex.Review => new DesignReviewTool(kit),
        DesignToolIndex.Audit => new DesignAuditTool(kit),
        DesignToolIndex.Presets => new DesignPresetsTool(kit),
        DesignToolIndex.Create => new DesignCreateTool(kit),
        DesignToolIndex.Edit => new DesignEditTool(kit),
        _ => throw new ArgumentOutOfRangeException(e.Name),
    };

    [Fact]
    public void 工具索引_恰好8个_名字与归类()
    {
        All.Select(e => e.Name).Should().Equal(
            ["design_guide", "design_context", "design_lookup", "design_review",
             "design_audit", "design_presets", "design_create", "design_edit"]);
        All.Count(e => e.Kind == "read").Should().Be(6);   // §B：B1–B6 读（audit 读；run=true 才写）
        All.Count(e => e.Kind == "write").Should().Be(2);  // B7 create / B8 edit
    }

    [Fact]
    public void 每个工具_元数据合规()
    {
        var kit = new DesignToolKit(new DesignProjectService(), new TokenRepository(), new CatalogRepository(),
            new AuditRepository(), null!, null!, null!, null!, null!, null!, null!, null!);
        foreach (var e in All)
        {
            var t = Tool(e, kit);
            t.Id.Should().Be($"design-system.tool.{e.Name}");
            t.Name.Should().Be(e.Name);
            t.PluginId.Should().Be("design-system");
            t.Description.Length.Should().BeLessThanOrEqualTo(400, $"工具 {e.Name} 的 description 超长");

            using var doc = JsonDocument.Parse(t.ParametersJsonSchema);
            var root = doc.RootElement;
            root.GetProperty("type").GetString().Should().Be("object");
            root.TryGetProperty("oneOf", out _).Should().BeFalse($"schema 禁 oneOf（工具 {e.Name}）");
            root.TryGetProperty("anyOf", out _).Should().BeFalse($"schema 禁 anyOf（工具 {e.Name}）");

            var props = root.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToHashSet();
            if (root.TryGetProperty("required", out var req))
            {
                foreach (var r in req.EnumerateArray())
                    props.Should().Contain(r.GetString(), $"required 的键必须在 properties 里（工具 {e.Name}）");
            }
        }
    }

    [Fact]
    public void ExecuteAsync_非法JSON_返回错误封套()
    {
        var tool = new DesignGuideTool(Kit());
        var r = tool.ExecuteAsync("{{{{").Result;
        using var doc = JsonDocument.Parse(r);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.TryGetProperty("error", out _).Should().BeTrue();
    }

    [Fact]
    public void ExecuteAsync_空参数_不抛()
    {
        var tool = new DesignGuideTool(Kit());
        var r = tool.ExecuteAsync("").Result;
        using var doc = JsonDocument.Parse(r);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
    }

    static DesignToolKit Kit() => new(new DesignProjectService(), new TokenRepository(),
        new CatalogRepository(), new AuditRepository(), null!, null!, null!, null!,
        new AgentAccess(new DesignSystemPaths(Path.Combine(Path.GetTempPath(), $"forge-ds-agt-contract_{Guid.NewGuid():N}"))),
        null!, null!, null!);
}

/// <summary>§B 工具行为：真实隔离库（XCode）+ 真实生成，零 mock。只建不删（铁律 10）。</summary>
[Collection("XCode")]
public class DesignAgentToolBehaviorTests : IDisposable
{
    readonly String _dbDir;
    readonly DesignProjectService _projects = new();
    readonly TokenRepository _tokens = new();
    readonly CatalogRepository _catalog = new();
    readonly AuditRepository _audits = new();
    readonly AuditEngine _auditEngine;
    readonly ExportService _export;
    readonly ReleaseService _releases;
    readonly GenerationService _generation;
    readonly AgentAccess _agentAccess;
    readonly DesignReviewService _review;
    readonly QuickCreateService _quickCreate;
    readonly DesignBriefBuilder _brief;
    readonly DesignToolKit _kit;

    public DesignAgentToolBehaviorTests()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfAgt_{Guid.NewGuid():N}");
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

        _auditEngine = new AuditEngine(_tokens, _projects, _audits);
        _generation = new GenerationService(_tokens, _projects, _catalog, _auditEngine);
        _export = new ExportService(_tokens, _projects, _catalog);
        _releases = new ReleaseService(_tokens, _projects, _audits, _auditEngine, new DesignSystemPaths(_dbDir), _catalog);
        _agentAccess = new AgentAccess(new DesignSystemPaths(_dbDir));
        _review = new DesignReviewService(_export, _tokens, _projects);
        _quickCreate = new QuickCreateService(_projects, _generation);
        _brief = new DesignBriefBuilder(_export, _projects, _catalog, _review);
        _export.BriefBuilder = _brief;
        _kit = new DesignToolKit(_projects, _tokens, _catalog, _audits, _auditEngine, _export, _releases,
            _generation, _agentAccess, _review, _quickCreate, _brief);
    }

    public void Dispose() => GC.SuppressFinalize(this);

    static JsonElement Ok(String r)
    {
        using var doc = JsonDocument.Parse(r);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue($"结果应成功：{r[..Math.Min(r.Length, 200)]}");
        return doc.RootElement.GetProperty("data").Clone();
    }

    static JsonElement Err(String r)
    {
        using var doc = JsonDocument.Parse(r);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse("结果应失败");
        return doc.RootElement.GetProperty("error").Clone();
    }

    String SeedProject()
    {
        BuiltinIcons.Seed(_catalog);   // 真实宿主启动行为：首植内置图标库（幂等）
        var preview = _quickCreate.Create("演示后台", "demo-back", "console", "后台管理系统", "admin-calm",
            new GenerationRequest { Brief = "后台管理系统" }, true,
            out var uiRoute, out var applied, out var error);
        error.Should().BeNull();
        applied.Should().NotBeNull();
        return "demo-back";
    }

    // ---- design_guide ----

    [Fact]
    public void Guide_版本_工具清单_工作流_入口()
    {
        var data = Ok(new DesignGuideTool(_kit).ExecuteAsync("{}").Result);
        data.GetProperty("version").GetString().Should().Be(DesignSystemConstants.ModelVersion);
        data.GetProperty("tools").EnumerateArray().Select(t => t.GetProperty("name").GetString()).Should().Contain(
            ["design_guide", "design_context", "design_lookup", "design_review",
             "design_audit", "design_presets", "design_create", "design_edit"]);
        data.GetProperty("workflows").EnumerateArray().Count().Should().BeGreaterThanOrEqualTo(3);
        data.GetProperty("access").GetProperty("allowWrite").GetBoolean().Should().BeTrue();
        data.GetProperty("discovery").GetString().Should().Contain("list_tools");
    }

    // ---- design_context ----

    [Fact]
    public void Context_无项目_给出引导错误()
    {
        var err = Err(new DesignContextTool(_kit).ExecuteAsync("{}").Result);
        err.GetString().Should().Contain("design_presets");
    }

    [Fact]
    public void Context_说明书_颜色同源()
    {
        SeedProject();
        var data = Ok(new DesignContextTool(_kit).ExecuteAsync(@"{""project"":""demo-back"",""format"":""json""}").Result);
        data.GetProperty("contentHash").GetString().Should().NotBeNullOrEmpty();

        // 同源：json.colors 的 markdown 里，semantic.surface-bg 的 hex == ExportService.Load 的 ColorHex；变量名 == CssVarName
        // context 缺省 theme=项目默认主题（light）→ 快照含 semantic 层
        var snap = _export.Load(_projects.FindByCode("demo-back")!.Id, "light");
        var bg = snap.Tokens.First(t => t.Path == "semantic.surface-bg");
        var colorsMd = data.GetProperty("json").GetProperty("sectionsContent").GetProperty("colors").GetString();
        colorsMd.Should().Contain(ExportService.CssVarName("semantic.surface-bg"));
        colorsMd.Should().Contain(bg.ColorHex);
        data.GetProperty("sections").EnumerateArray().Select(s => s.GetString()).Should().Contain("identity");
    }

    // ---- design_lookup ----

    [Fact]
    public void Lookup_token_分页_前缀()
    {
        SeedProject();
        var data = Ok(new DesignLookupTool(_kit).ExecuteAsync(@"{""project"":""demo-back"",""kind"":""token"",""prefix"":""semantic.surface"",""limit"":10}").Result);
        data.GetProperty("total").GetInt32().Should().BeGreaterThan(0);
        data.GetProperty("items").EnumerateArray().Count().Should().BeLessThanOrEqualTo(10);
    }

    [Fact]
    public void Lookup_nearest_按值反查()
    {
        SeedProject();
        var data = Ok(new DesignLookupTool(_kit).ExecuteAsync(@"{""project"":""demo-back"",""kind"":""nearest"",""value"":""#0ea5e9"",""property"":""color"",""limit"":3}").Result);
        data.GetProperty("matches").EnumerateArray().Count().Should().BeGreaterThan(0);
        data.GetProperty("error").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void Lookup_component_详情含anatomy()
    {
        SeedProject();
        var data = Ok(new DesignLookupTool(_kit).ExecuteAsync(@"{""project"":""demo-back"",""kind"":""component"",""code"":""card""}").Result);
        data.GetProperty("code").GetString().Should().Be("card");
        data.GetProperty("anatomy").ValueKind.Should().Be(JsonValueKind.Array);
        data.GetProperty("tokens").EnumerateArray().Count().Should().BeGreaterThan(0);
    }

    [Fact]
    public void Lookup_export_css_有真声明()
    {
        SeedProject();
        var data = Ok(new DesignLookupTool(_kit).ExecuteAsync(
            @"{""project"":""demo-back"",""kind"":""export"",""format"":""css"",""maxChars"":60000}").Result);
        data.GetProperty("content").GetString().Should().Contain("--ds-semantic-surface-bg");
        data.GetProperty("length").GetInt32().Should().BeGreaterThan(500);
        data.GetProperty("truncated").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void Lookup_icon_枚举内置图标()
    {
        SeedProject();
        var data = Ok(new DesignLookupTool(_kit).ExecuteAsync(@"{""kind"":""icon"",""collection"":""forge"",""includeSvg"":false}").Result);
        data.GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(24);
    }

    // ---- design_review ----

    [Fact]
    public void Review_代码反例_命中硬编码()
    {
        SeedProject();
        var data = Ok(new DesignReviewTool(_kit).ExecuteAsync(
            @"{""project"":""demo-back"",""code"":""body { background: #ff0000; color: var(--ds-color-unknown-1) }"",""language"":""css""}").Result);
        data.GetProperty("findings").EnumerateArray().Count().Should().BeGreaterThan(0);
        data.GetProperty("summary").GetProperty("hardcoded").GetInt32().Should().BeGreaterThan(0);
        data.GetProperty("summary").GetProperty("tokenized").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public void Review_checklist_模式返回条目()
    {
        SeedProject();
        var data = Ok(new DesignReviewTool(_kit).ExecuteAsync(@"{""project"":""demo-back"",""mode"":""checklist""}").Result);
        data.GetProperty("items").EnumerateArray().Count().Should().BeGreaterThan(0);
    }

    // ---- design_audit ----

    [Fact]
    public void Audit_读不落库_run落库()
    {
        SeedProject();
        var read = Ok(new DesignAuditTool(_kit).ExecuteAsync(@"{""project"":""demo-back""}").Result);
        read.GetProperty("ran").GetBoolean().Should().BeFalse();
        read.GetProperty("summary").GetProperty("total").GetInt32().Should().BeGreaterThan(0);

        var run = Ok(new DesignAuditTool(_kit).ExecuteAsync(@"{""project"":""demo-back"",""run"":true}").Result);
        run.GetProperty("ran").GetBoolean().Should().BeTrue();
        run.GetProperty("summary").GetProperty("total").GetInt32().Should().BeGreaterThan(0);
        _audits.List(_projects.FindByCode("demo-back")!.Id, 0, null, null).Count.Should().BeGreaterThan(0);
    }

    // ---- design_presets ----

    [Fact]
    public void Presets_list_recommend()
    {
        var list = Ok(new DesignPresetsTool(_kit).ExecuteAsync(@"{""action"":""list""}").Result);
        list.GetProperty("presets").EnumerateArray().Count().Should().Be(8);

        var rec = Ok(new DesignPresetsTool(_kit).ExecuteAsync(
            @"{""action"":""recommend"",""brief"":""后台管理控制台"",""kind"":""console"",""limit"":3}").Result);
        rec.GetProperty("matches").EnumerateArray().Count().Should().BeGreaterThan(0);
    }

    // ---- design_create ----

    [Fact]
    public void Create_干跑_不落库()
    {
        var beforeProjects = DesignProject.FindCount();
        var beforeTokens = DesignToken.FindCount();
        var data = Ok(new DesignCreateTool(_kit).ExecuteAsync(
            @"{""name"":""干跑项目"",""brief"":""演示"",""apply"":false}").Result);
        data.GetProperty("preview").GetProperty("code").GetString().Should().NotBeNullOrEmpty();
        DesignProject.FindCount().Should().Be(beforeProjects);
        DesignToken.FindCount().Should().Be(beforeTokens);
    }

    [Fact]
    public void Create_apply_落库_带审计与路由()
    {
        var data = Ok(new DesignCreateTool(_kit).ExecuteAsync(
            @"{""name"":""落地项目"",""code"":""landed"",""apply"":true}").Result);
        data.GetProperty("project").GetProperty("code").GetString().Should().Be("landed");
        data.GetProperty("uiRoute").GetString().Should().Be("/design-system");
        DesignProject.FindByCode("landed").Should().NotBeNull();
    }

    [Fact]
    public void Create_未知预设_错误列出全部()
    {
        var err = Err(new DesignCreateTool(_kit).ExecuteAsync(@"{""name"":""x"",""preset"":""nope""}").Result);
        err.GetString().Should().Contain("未知预设");
    }

    // ---- design_edit ----

    [Fact]
    public void Edit_set_token_写单令牌()
    {
        SeedProject();
        var data = Ok(new DesignEditTool(_kit).ExecuteAsync(
            @"{""project"":""demo-back"",""action"":""set_token"",""path"":""semantic.agent-test"",""value"":""#123456"",""type"":""color""}").Result);
        data.GetProperty("created").GetBoolean().Should().BeTrue();
        var t = _tokens.Find(_projects.FindByCode("demo-back")!.Id, 0, "semantic.agent-test");
        t.Should().NotBeNull();
        t!.Value.Should().Be("#123456");
    }

    [Fact]
    public void Edit_publish_版本推进()
    {
        SeedProject();
        // 发布有审计门禁：先跑审计确认无 critical（生成产物自身应过门禁，design-system-verify #24）
        var audit = Ok(new DesignAuditTool(_kit).ExecuteAsync(@"{""project"":""demo-back"",""run"":true}").Result);
        audit.GetProperty("blocking").GetBoolean().Should().BeFalse("生成产物应自带无 critical 审计");
        var before = _projects.FindByCode("demo-back")!.Version;
        var data = Ok(new DesignEditTool(_kit).ExecuteAsync(
            @"{""project"":""demo-back"",""action"":""publish"",""version"":""1.1.0"",""notes"":""agent 发布""}").Result);
        data.GetProperty("release").GetProperty("version").GetString().Should().Be("1.1.0");
        _projects.FindByCode("demo-back")!.Version.Should().NotBe(before);
    }

    [Fact]
    public void Edit_regenerate_覆盖生成()
    {
        SeedProject();
        var data = Ok(new DesignEditTool(_kit).ExecuteAsync(
            @"{""project"":""demo-back"",""action"":""regenerate"",""hue"":200,""apply"":true}").Result);
        data.GetProperty("generation").GetProperty("industry").ValueKind.Should().Be(JsonValueKind.String);
    }

    // ---- 写开关 ----

    [Fact]
    public void 写开关关闭_写动作被拦_只读不受影响()
    {
        SeedProject();
        _agentAccess.Set(false);
        try
        {
            var err = Err(new DesignEditTool(_kit).ExecuteAsync(
                @"{""project"":""demo-back"",""action"":""set_token"",""path"":""semantic.x"",""value"":""#000""}").Result);
            err.GetString().Should().Contain("外部写入已被关闭");

            // 只读不受影响
            var read = Ok(new DesignContextTool(_kit).ExecuteAsync(@"{""project"":""demo-back""}").Result);
            read.GetProperty("contentHash").GetString().Should().NotBeNullOrEmpty();
        }
        finally
        {
            _agentAccess.Set(true);
        }
    }

    [Fact]
    public void 归档项目_写拒绝()
    {
        SeedProject();
        var p = _projects.FindByCode("demo-back")!;
        _projects.Archive(p.Id);
        var err = Err(new DesignEditTool(_kit).ExecuteAsync(
            @"{""project"":""demo-back"",""action"":""set_token"",""path"":""semantic.x"",""value"":""#000""}").Result);
        err.GetString().Should().Contain("已归档");
    }

    [Fact]
    public void 项目解析_多项目需指定()
    {
        SeedProject();
        _quickCreate.Create("第二个", "second-proj", "product", null, null,
            new GenerationRequest { Brief = "第二个" }, true, out _, out _, out var err);
        err.Should().BeNull();
        var e = Err(new DesignContextTool(_kit).ExecuteAsync("{}").Result);
        e.GetString().Should().Contain("请指定 project");
    }
}

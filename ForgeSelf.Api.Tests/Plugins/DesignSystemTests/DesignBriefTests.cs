using ForgeSelf.Api.Plugins.DesignSystem;
using ForgeSelf.Api.Plugins.DesignSystem.Data;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;
using ForgeSelf.Api.Plugins.DesignSystem.Services;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>§E 设计说明书（BRIEF）+ agent-rules：章节序、预算 omitted、contentHash 确定性与无时间戳、同源、E5 关键子串。</summary>
[Collection("XCode")]
public class DesignBriefTests : IDisposable
{
    readonly String _dbDir;
    readonly DesignProjectService _projects = new();
    readonly TokenRepository _tokens = new();
    readonly CatalogRepository _catalog = new();
    readonly AuditRepository _audits = new();
    readonly AuditEngine _auditEngine;
    readonly ExportService _export;
    readonly GenerationService _generation;
    readonly GuidelineService _guidelines;
    readonly DesignReviewService _review;
    readonly QuickCreateService _quickCreate;
    readonly DesignBriefBuilder _brief;

    public DesignBriefTests()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfBrf_{Guid.NewGuid():N}");
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
        _generation = new GenerationService(_tokens, _projects, _catalog, _auditEngine);
        _export = new ExportService(_tokens, _projects, _catalog);
        // 规范必须挂进生成与导出两侧（与控制器装配同形）：漏挂的后果是 brief 的 guidelines 章整章消失，
        // 而 E1 那句「Sections == SectionOrder」会把它读成"章节序坏了"——夹具装配与产品装配不同形就是假红。
        _guidelines = new GuidelineService(_projects, _tokens, new GuidelineRepository());
        _generation.Guidelines = _guidelines;
        _export.Guidelines = _guidelines;
        _review = new DesignReviewService(_export, _tokens, _projects);
        _quickCreate = new QuickCreateService(_projects, _generation);
        _brief = new DesignBriefBuilder(_export, _projects, _catalog, _review);
        _export.BriefBuilder = _brief;
    }

    public void Dispose() => GC.SuppressFinalize(this);

    String SeedProject()
    {
        _quickCreate.Create("说明书项目", "brief-proj", "product", "演示", "admin-calm",
            new GenerationRequest { Brief = "演示项目说明书" }, true, out _, out var applied, out var error);
        error.Should().BeNull();
        applied.Should().NotBeNull();
        return "brief-proj";
    }

    [Fact]
    public void E1_章节序_identity恒含()
    {
        SeedProject();
        var o = _brief.Build(_projects.FindByCode("brief-proj")!.Id, null, null, 60000, DesignBriefBuilder.Markdown);
        o.Sections.Should().Equal(DesignBriefBuilder.SectionOrder);   // identity→rules→colors→typography→scales→components→brand→guidelines→checklist
        o.Markdown.Should().Contain("# ");
    }

    [Fact]
    public void E2_预算_identity恒在_其余omitted()
    {
        SeedProject();
        var o = _brief.Build(_projects.FindByCode("brief-proj")!.Id, null, null, 2000, DesignBriefBuilder.Markdown);
        o.Sections.Should().Contain("identity");
        o.Omitted.Count.Should().BeGreaterThan(0);
        o.Truncated.Should().BeTrue();
        o.Markdown!.Length.Should().BeLessThanOrEqualTo(2000 + 500);   // identity 恒入，整体仍贴近预算
    }

    [Fact]
    public void E3_contentHash_确定性_无时间戳_令牌变更则变()
    {
        SeedProject();
        var id = _projects.FindByCode("brief-proj")!.Id;
        var a = _brief.Build(id, null, null, 60000, DesignBriefBuilder.Markdown).ContentHash;
        var b = _brief.Build(id, null, null, 60000, DesignBriefBuilder.Markdown).ContentHash;
        a.Should().Be(b);                       // 同输入同 hash（两次调用之间若含时间戳必不等）
        a.Should().MatchRegex("^[0-9a-f]{16}$"); // SHA-256 前 16 位小写 hex

        // 令牌变更 → hash 变
        _tokens.UpsertBatch(id, [new TokenPatch { Path = "semantic.brief-hash-test", ThemeId = DesignSystemConstants.SharedThemeId, Value = "#112233", Type = TokenTypes.Color, Generator = "manual" }], overwrite: true);
        var c = _brief.Build(id, null, null, 60000, DesignBriefBuilder.Markdown).ContentHash;
        c.Should().NotBe(a);
    }

    [Fact]
    public void E4_同源_hex等于Load_变量名等于CssVarName()
    {
        SeedProject();
        var id = _projects.FindByCode("brief-proj")!.Id;
        var o = _brief.Build(id, "light", null, 60000, DesignBriefBuilder.Markdown);   // light 主题快照含 semantic 层
        var snap = _export.Load(id, "light");
        var bg = snap.Tokens.First(t => t.Path == "semantic.surface-bg");
        o.Markdown.Should().Contain(ExportService.CssVarName("semantic.surface-bg"));
        o.Markdown.Should().Contain(bg.ColorHex);
    }

    [Fact]
    public void E5_agentRules_关键子串()
    {
        SeedProject();
        var id = _projects.FindByCode("brief-proj")!.Id;
        var rules = _brief.BuildAgentRules(id, null);
        rules.Should().Contain("brief-proj");          // 项目 code
        rules.Should().Contain("v" + _projects.FindByCode("brief-proj")!.Version);  // 版本号
        rules.Should().Contain("design_context");
        rules.Should().Contain("design_review");
        rules.Should().Contain("MUST NOT");
        rules.Should().Contain("list_tools");
    }
}

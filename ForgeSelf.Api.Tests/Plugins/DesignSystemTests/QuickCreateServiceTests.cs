using ForgeSelf.Api.Plugins.DesignSystem;
using ForgeSelf.Api.Plugins.DesignSystem.Data;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;
using ForgeSelf.Api.Plugins.DesignSystem.Services;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>§G 快速创建：AllocateCode 确定性规则 + Create 干跑/落库 + code 冲突错误。隔离库只建不删（铁律 10）。</summary>
[Collection("XCode")]
public class QuickCreateServiceTests : IDisposable
{
    readonly String _dbDir;
    readonly DesignProjectService _projects = new();
    readonly TokenRepository _tokens = new();
    readonly CatalogRepository _catalog = new();
    readonly AuditRepository _audits = new();
    readonly AuditEngine _auditEngine;
    readonly GenerationService _generation;
    readonly QuickCreateService _quick;

    public QuickCreateServiceTests()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfQuick_{Guid.NewGuid():N}");
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
        _quick = new QuickCreateService(_projects, _generation);
    }

    public void Dispose() => GC.SuppressFinalize(this);

    // ---- AllocateCode ----

    [Fact]
    public void AllocateCode_lower_替换_合并_去首尾()
    {
        _quick.AllocateCode("  Acme 商城!?  ").Should().Be("acme");       // 非 [a-z0-9]（含中文）→ '-'
        _quick.AllocateCode("My Project").Should().Be("my-project");
        _quick.AllocateCode("你好").Should().Match("ds-??????");           // 无 ASCII 字母数字 → ds- + 6 位 hex
    }

    [Fact]
    public void AllocateCode_确定性_同输入同输出()
    {
        _quick.AllocateCode("我的品牌站").Should().Be(_quick.AllocateCode("我的品牌站"));
    }

    [Fact]
    public void AllocateCode_截40()
    {
        var code = _quick.AllocateCode(new String('a', 60) + "项目");
        code.Length.Should().BeLessThanOrEqualTo(40);
        code.Should().MatchRegex("^[a-z0-9][a-z0-9-]{0,39}$");
    }

    [Fact]
    public void AllocateCode_判重试_2到99()
    {
        _projects.Create(new ProjectInput { Code = "acme", Name = "Acme 一" });
        _projects.Create(new ProjectInput { Code = "acme-2", Name = "Acme 二" });
        _quick.AllocateCode("Acme").Should().Be("acme-3");
    }

    // ---- Create ----

    [Fact]
    public void Create_干跑_不落库()
    {
        var before = DesignProject.FindCount();
        var preview = _quick.Create("演示后台", null, "console", null, null,
            new GenerationRequest { Brief = "后台管理系统" }, false,
            out var uiRoute, out var applied, out var error);

        error.Should().BeNull();
        applied.Should().BeNull();
        uiRoute.Should().Be(DesignSystemConstants.FrontendRoute);
        preview.Should().NotBeNull();
        preview!.CodeAvailable.Should().BeTrue();
        preview.Code.Should().MatchRegex("^[a-z0-9][a-z0-9-]{0,39}$");
        preview.Seed.Should().NotBeNullOrWhiteSpace();
        preview.Industry.Should().Be("general");
        preview.Tokens.Should().BeGreaterThan(100);
        preview.Themes.Should().NotBeEmpty();
        DesignProject.FindCount().Should().Be(before);   // 干跑不建项目
    }

    [Fact]
    public void Create_预设_参数继承且显式覆盖()
    {
        var preview = _quick.Create("金融后台", null, "console", null, null,
            new GenerationRequest { Brief = "支付账单系统", Density = "compact" }, false,
            out _, out _, out var error);

        error.Should().BeNull();
        preview!.Industry.Should().Be("finance");          // brief 推断 → finance-trust 命中
    }

    [Fact]
    public void Create_未知预设_报错并列出id()
    {
        var preview = _quick.Create("x", null, null, null, "不存在的预设", new GenerationRequest(), false,
            out _, out _, out var error);

        preview.Should().BeNull();
        error.Should().Contain("未知预设").And.Contain("admin-calm");
    }

    [Fact]
    public void Create_code冲突_报错()
    {
        _projects.Create(new ProjectInput { Code = "taken", Name = "占位" });
        var preview = _quick.Create("演示", "taken", null, null, null, new GenerationRequest(), false,
            out _, out _, out var error);

        preview.Should().BeNull();
        error.Should().Contain("已存在");
    }

    [Fact]
    public void Create_非法code_报错()
    {
        var preview = _quick.Create("演示", "ABC 大写", null, null, null, new GenerationRequest(), false,
            out _, out _, out var error);

        preview.Should().BeNull();
        error.Should().Contain("code 只能含小写字母");
    }

    [Fact]
    public void Create_空name_报错()
    {
        var preview = _quick.Create("   ", null, null, null, null, new GenerationRequest(), false,
            out _, out _, out var error);

        preview.Should().BeNull();
        error.Should().Contain("name 必填");
    }

    [Fact]
    public void Create_落库_项目与生成物都在()
    {
        var preview = _quick.Create("快捷电商", null, "marketing", null, null,
            new GenerationRequest { Brief = "电商促销商城" }, true,
            out var uiRoute, out var applied, out var error);

        error.Should().BeNull();
        preview.Should().NotBeNull();
        applied.Should().NotBeNull();
        uiRoute.Should().Be(DesignSystemConstants.FrontendRoute);

        var p = _projects.FindByCode(preview!.Code)!;
        p.Should().NotBeNull();
        p.Name.Should().Be("快捷电商");
        applied!.Generation.Result.Total.Should().BeGreaterThan(100);
        applied.Generation.Audit.Total.Should().BeGreaterThan(0);
        applied.Generation.Audit.Critical.Should().Be(0);   // 自产产物必须过门禁（design-system-verify #24）
        DesignProject.FindCount().Should().BeGreaterThan(0);
    }

    [Fact]
    public void Create_生成阶段失败_项目软归档且错误含code()
    {
        // §J 注入：req.Themes 含项目里不存在的主题 → Generate（纯计算不查库）干跑预览正常，
        // 落库后 ApplyToProject 在主题解析处抛 KeyNotFoundException —— 正是"生成阶段失败"。
        var preview = _quick.Create("失败演示", "fail-demo", null, null, null,
            new GenerationRequest { Themes = ["light", "不存在的主题"] }, true,
            out _, out var applied, out var error);

        preview.Should().BeNull();
        applied.Should().BeNull();
        error.Should().Contain("生成失败").And.Contain("fail-demo").And.Contain("归档");
        _projects.FindByCode("fail-demo")!.Status.Should().Be(ProjectStatus.Archived);   // 软删：数据保留
    }

    [Fact]
    public void Create_审计有critical_项目保留且带warnings()
    {
        var preview = _quick.Create("阻断演示", "block-demo", null, null, null,
            new GenerationRequest { Brief = "后台" }, true,
            out _, out var applied, out var error);

        error.Should().BeNull();
        applied.Should().NotBeNull();
        applied!.Warnings.Should().BeEmpty();          // 自产产物必须过门禁 → blocking=false → warnings 空

        // 模拟用户手改：把 semantic.text-1 的解析值改成与 surface-bg 相同 → 对比度 1.0 → critical
        var project = _projects.FindByCode("block-demo")!;
        var light = _projects.FindTheme(project.Id, "light")!;
        var graph = _tokens.LoadGraph(project.Id, light.Id, "light");
        var bgValue = graph.Resolve("semantic.surface-bg").Value;
        var text1 = DesignToken.FindAll(DesignToken._.ProjectId == project.Id
            & DesignToken._.ThemeId == light.Id & DesignToken._.Path == "semantic.text-1").FirstOrDefault();
        text1.Should().NotBeNull();
        text1!.Value = bgValue;
        text1.AliasPath = "";
        text1.Save();

        var summary = _auditEngine.Run(project.Id);
        summary.Blocking.Should().BeTrue();
        // 项目保留（未被归档）：
        _projects.Find(project.Id)!.Status.Should().Be(ProjectStatus.Draft);
        // warnings 文案（§G）：
        QuickCreateService.BuildWarnings(summary).Should().Contain($"审计存在 {summary.Critical} 条 critical，发布前需处理");
    }

    [Fact]
    public void Create_brief缺省_取description或name()
    {
        // description 兜底（brief=description → 行业推断命中 finance）：
        var preview1 = _quick.Create("演示", null, null, "支付网关", null, new GenerationRequest(), false,
            out _, out _, out var error1);
        error1.Should().BeNull();
        preview1!.Industry.Should().Be("finance");

        // 无 description → brief 兜底为 name：
        var preview2 = _quick.Create("金融后台", null, null, null, null, new GenerationRequest(), false,
            out _, out _, out var error2);
        error2.Should().BeNull();
        preview2!.Industry.Should().Be("finance");
    }

    [Fact]
    public void BuildWarnings_审计不阻断_空列表()
    {
        QuickCreateService.BuildWarnings(new AuditSummary(10, 10, 0, 0, 0)).Should().BeEmpty();
    }
}

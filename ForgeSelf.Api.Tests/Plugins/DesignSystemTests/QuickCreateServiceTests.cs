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
    readonly ExportService _export;
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
        _export = new ExportService(_tokens, _projects, _catalog);
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

    /// <summary>
    /// e2e V3 撞出来的形状：界面那条链（S2）发的是"整份请求 + 轴"，而调用方也可能只发**半份**
    /// （`request: { shadowStyle: 'crisp' }`，其余留空让预设给）。两种都必须真的落到库里的令牌上——
    /// 落不下去就是"轴是装饰"，所以这里直接读回 `shadow.elevation-3` 逐字比。
    /// </summary>
    [Fact]
    public void Create_只带轴的半份请求_轴必须落到库里令牌()
    {
        var plain = _quick.Create("轴对照", "qc-axis-plain", "console", "运维监控后台", "admin-calm",
            new GenerationRequest(), true, out _, out var appliedPlain, out var e1);
        var crisp = _quick.Create("轴锐利", "qc-axis-crisp", "console", "运维监控后台", "admin-calm",
            new GenerationRequest { ShadowStyle = "crisp" }, true, out _, out var appliedCrisp, out var e2);

        e1.Should().BeNull();
        e2.Should().BeNull();
        plain.Should().NotBeNull();
        crisp.Should().NotBeNull();
        appliedPlain.Should().NotBeNull();
        appliedCrisp.Should().NotBeNull();

        var a = ShadowValue(_projects.FindByCode("qc-axis-plain")!.Id, "shadow.elevation-3");
        var b = ShadowValue(_projects.FindByCode("qc-axis-crisp")!.Id, "shadow.elevation-3");
        a.Should().NotBeNullOrWhiteSpace("对照项目也该有 elevation-3（拿不到值 = 下面的比对是空转）");
        b.Should().NotBe(a, "crisp 与预设默认的 soft 阴影必须不同（相同 = 轴在快速创建里被丢掉，V3 实测红过）");
    }

    /// <summary>
    /// 轴必须落到**交付 CSS 的整族投影变量**上（自查表 #48 同口径：生成层绿 ≠ 投影层绿）。
    ///
    /// 为什么单独立一条：批 C 全目录 e2e 里出现过一次「quick-create 新建项目的导出 CSS 整条
    /// <c>--ds-shadow-elevation-3</c> 都不见」（同批复跑与单跑都是绿的，机制未定：候选有 XCode 读缓存、
    /// SQLite 单写者并发、读时机）。不可复现的"产物缺一族"不能靠 e2e 撞运气，必须有常驻机器判据——
    /// 这条同时在两个方向上收口：① **整族齐全**（五档 elevation 一档都不许少，少了就点名是哪一档）；
    /// ② **形状随轴变**（crisp 与默认 soft 的 elevation-3 必须不同，且 crisp 是单层锐利）。
    /// 期望值与 e2e S2/V3 逐字同源（§A3 人工核算：3^1.35×1.8=7.93→7.9px；alpha 0.07+0.05×3=22%）。
    /// </summary>
    [Fact]
    public void Create_带轴项目_导出CSS整族齐全且形状随轴变()
    {
        _quick.Create("轴CSS对照", "qc-css-plain", "console", "运维监控后台", "admin-calm",
            new GenerationRequest(), true, out _, out _, out var e1);
        _quick.Create("轴CSS锐利", "qc-css-crisp", "console", "运维监控后台", "admin-calm",
            new GenerationRequest { ShadowStyle = "crisp" }, true, out _, out _, out var e2);
        e1.Should().BeNull();
        e2.Should().BeNull();

        var plain = _export.Produce(_projects.FindByCode("qc-css-plain")!.Id, ExportFormats.Css, "light").Text;
        var crisp = _export.Produce(_projects.FindByCode("qc-css-crisp")!.Id, ExportFormats.Css, "light").Text;

        foreach (var level in new[] { 1, 2, 3, 4, 5 })
        {
            plain.Should().Contain($"--ds-shadow-elevation-{level}:", $"对照项目缺 elevation-{level} = 落库/投影断链");
            crisp.Should().Contain($"--ds-shadow-elevation-{level}:", $"带轴项目缺 elevation-{level} = 轴路径把这一档写丢了");
        }

        var plainLine = ShadowLine(plain);
        var crispLine = ShadowLine(crisp);
        plainLine.Should().NotBeNullOrWhiteSpace("判据落空：默认档的 elevation-3 行没取到");
        // 只锁"这一档的几何 + 透明度"，不锁颜色：颜色由中性色族给，跨用例复用同一条公式判据即可
        crispLine.Should().StartWith("0px 3px 7.9px 0px color-mix(in oklab, ",
            "crisp 的 elevation-3 是单层锐利（§A3 公式人工核算：blur=3^1.35×1.8=7.9px，spread 0）");
        crispLine.Should().Contain("22%, transparent)", "alpha 必须按 crisp 公式（0.07+0.05×3=0.22）");
        crispLine.Should().NotBe(plainLine, "crisp 与默认 soft 必须不同（相同 = 轴在快速创建里被丢掉）");
        (System.Text.RegularExpressions.Regex.Matches(crispLine, "color-mix\\(").Count)
            .Should().Be(1, "crisp 是单层；出现第二个 color-mix = 又叠了 soft 的第二层");
    }

    /// <summary>导出 CSS 里 <c>shadow.elevation-3</c> 那一行的值部分（投影族的判据都落在最终产物文本上）</summary>
    static String ShadowLine(String css) =>
        System.Text.RegularExpressions.Regex.Match(css, @"--ds-shadow-elevation-3:\s*([^;]+);").Groups[1].Value.Trim();

    /// <summary>某阴影令牌在该项目 light 主题下的"值 + 逐层展开"（阴影真正差在层表里，只比 Value 会漏）</summary>
    String ShadowValue(Int64 projectId, String path)
    {
        var theme = _tokens.FindThemeByCode(projectId, "light") ?? throw new InvalidOperationException($"项目 {projectId} 没有 light 主题");
        var token = _tokens.Find(projectId, theme.Id, path) ?? throw new InvalidOperationException($"项目 {projectId} 缺令牌 {path}");
        var layers = _tokens.FindShadowLayers(token.Id)
            .Select(l => $"{l.Layer}|inset={l.IsInset}|{l.OffsetX},{l.OffsetY} b{l.Blur} s{l.Spread} {l.ColorValue}/{l.Alpha}")
            .Order(StringComparer.Ordinal);
        return $"{token.Value} ## {String.Join(" || ", layers)}";
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

    /// <summary>
    /// M3 FR5：**快速创建（免配置）必须把风格轴带到生成参数里**。
    /// 这条用例是 e2e S2 实测抓到的缺陷回填的：`ApplyOverrides` 手写逐字段，
    /// 界面把 `shadowStyle=flat` 发过来、这里把它丢掉，于是"选了环线阴影却拿到默认软阴影"——
    /// 请求体里明明有轴，产物里却没有，是最难发现的一类假通路。
    /// </summary>
    [Fact]
    public void M3_风格轴必须穿过quick_create到生成参数()
    {
        var req = new GenerationRequest
        {
            Hue = 210,
            ShadowStyle = "crisp", ShadowStrength = 1.3, BorderStrength = "bold",
            NeutralTemp = "cool", FontPairing = "editorial", RadiusStyle = "round",
            AccentStrategy = "split",
        };

        var preview = _quick.Create("风格轴穿透", null, null, null, null, req, true, out _, out _, out var error);

        error.Should().BeNull();
        preview.Should().NotBeNull();
        // 种子后缀是"轴真的进了生成请求"的现场证据：ApplyOverrides 少一行，后缀就少一段
        var seed = preview!.Seed;
        seed.Should().Contain(";sh=crisp;", $"quick-create 丢了阴影风格轴，实得种子 {seed}");
        seed.Should().Contain(";bo=bold;", $"丢了描边强度轴，实得种子 {seed}");
        seed.Should().Contain(";ne=cool;", $"丢了中性色温轴，实得种子 {seed}");
        seed.Should().Contain(";fo=editorial;", $"丢了字体搭配轴，实得种子 {seed}");
        seed.Should().Contain(";ra=round;", $"丢了圆角风格轴，实得种子 {seed}");
        seed.Should().Contain(";ac=split;", $"丢了强调色策略轴，实得种子 {seed}");
        seed.Should().Contain(";st=1.3", $"丢了阴影强度轴，实得种子 {seed}");

        // 轴不只是"传到了"，还要看它把产物改成了什么：editorial 必须产出展示字族
        var tokens = DesignGenerator.Generate(req);
        tokens.Shared.Should().Contain(p => p.Path == "font.display", "字体搭配轴在 quick-create 链路上必须真改产物");
    }

    /// <summary>从测试输出目录回溯到仓库根（读源码做透传守卫用）</summary>
    static String FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("找不到含 ForgeSelf.slnx 的仓库根");
    }

    [Fact]
    public void M3_GenerationRequest的每个可空字段都得是ApplyOverrides的候选()
    {
        // 反射守卫：可空字段 = "调用方可显式传"的字段。新增一个却忘了在 ApplyOverrides 里透传，
        // 就是上面那个缺陷的下次复发。这里不比对私有实现，只把"字段清单"钉住，
        // 让加字段的人被迫回来看这条用例与 QuickCreateService.ApplyOverrides。
        var nullable = typeof(GenerationRequest).GetProperties()
            .Where(p => Nullable.GetUnderlyingType(p.PropertyType) != null ||
                        (p.PropertyType == typeof(String) && p.CanWrite))
            .Select(p => p.Name)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        nullable.Should().Contain(["ShadowStyle", "ShadowStrength", "BorderStrength", "NeutralTemp",
            "FontPairing", "RadiusStyle", "AccentStrategy"],
            "风格轴字段得在这份清单里（它们是 quick-create 的显式覆盖候选）");

        var src = File.ReadAllText(Path.Combine(FindRepoRoot(), "Plugins", "DesignSystem", "Services", "QuickCreateService.cs"));
        var missing = nullable.Where(f => !src.Contains(f + " ?? target." + f, StringComparison.Ordinal)
                                      && !src.Contains("o." + f, StringComparison.Ordinal)).ToList();
        missing.Should().BeEmpty(
            $"这些可空字段没有出现在 ApplyOverrides 的透传里，调用方传了等于没传：{String.Join(", ", missing)}");
    }

    [Fact]
    public void BuildWarnings_审计不阻断_空列表()
    {
        QuickCreateService.BuildWarnings(new AuditSummary(10, 10, 0, 0, 0)).Should().BeEmpty();
    }
}

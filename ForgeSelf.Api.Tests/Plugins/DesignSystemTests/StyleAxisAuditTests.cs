using ForgeSelf.Api.Plugins.DesignSystem;
using ForgeSelf.Api.Plugins.DesignSystem.Data;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;
using ForgeSelf.Api.Plugins.DesignSystem.Services;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>
/// M3 AC4 / AC7 的审计面：**每个轴的每个取值 × 3 个基础请求 × 4 个默认主题，生成后审计 0 critical**。
///
/// 为什么要跑满矩阵而不是抽一个：新轴改的是阴影/描边/字族/圆角这类"看着无害"的东西，
/// 但它们会连带改变语义层的对比度选择（例如中性色温换了，`text-1` 可能落到另一阶）。
/// 只断言"默认产物通过审计"等于没测（自查表 #24）。
/// 隔离方式同 GenerationAuditTests：本类专属随机临时库目录，只创建不删除（铁律 10）。
/// </summary>
[Collection("XCode")]
public class StyleAxisAuditTests : IDisposable
{
    readonly String _dbDir;
    readonly DesignProjectService _projects = new();
    readonly TokenRepository _tokens = new();
    readonly CatalogRepository _catalog = new();
    readonly AuditRepository _audits = new();
    readonly AuditEngine _auditEngine;
    readonly GenerationService _generation;

    public StyleAxisAuditTests()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfDsAxis_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dbDir);

        DAL.AddConnStr(DesignSystemTables.ConnName, $"Data Source={Path.Combine(_dbDir, "DesignSystem.db")}", null, "SQLite");
        EntityFactory.InitConnection(DesignSystemTables.ConnName);

        // 缓存关掉：本类各自一份临时库，Meta.Cache 是执行上下文级的，留着会把上一个类的行读成幽灵
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
    }

    public void Dispose() => GC.SuppressFinalize(this);

    /// <summary>走与控制器/工具完全相同的编排（令牌 + 组件目录 + 品牌目录 + 审计）</summary>
    Int64 SeedProject(GenerationRequest req)
    {
        var p = _projects.Create(new ProjectInput { Code = $"ax-{Guid.NewGuid():N}"[..22], Name = "轴矩阵项目" });
        _generation.Run(p.Id, req, false);
        return p.Id;
    }

    /// <summary>跑一趟并只在"审计不干净"时返回可读诊断（含 critical 行的类别与路径，便于直接定位）</summary>
    String? AuditIssue(GenerationRequest req, String label)
    {
        var pid = SeedProject(req);
        var audit = _audits.Summarize(pid);
        if (audit.Critical == 0) return null;
        var rows = _audits.List(pid, DesignSystemConstants.SharedThemeId, null, false)
            .Where(a => a.Severity.Equals("critical", StringComparison.OrdinalIgnoreCase))
            .Take(4)
            .Select(a => $"{a.Kind}/{a.TargetPath}：{a.Message}");
        return $"{label}：critical {audit.Critical} 条 → " + String.Join("；", rows);
    }

    /// <summary>3 个基础请求（§AC4）：控制台带种子色 / 营销带行业倾向 / 紧凑密度只给色相。四主题齐备</summary>
    static GenerationRequest[] BaseRequests() =>
    [
        new() { SeedColor = "#7c3aed", Brief = "分布式服务治理控制台", Themes = ["light", "dark", "high-contrast", "compact"] },
        new() { Brief = "生活方式品牌官网", Industry = "media", Chroma = 0.22, TypeRatio = 1.4, Themes = ["light", "dark", "high-contrast", "compact"] },
        new() { Hue = 200, Density = "compact", TypeBasePx = 14, RadiusBase = 4, Themes = ["light", "dark", "high-contrast", "compact"] },
    ];

    static GenerationRequest Clone(GenerationRequest src) => PresetRecommender.Copy(src);

    [Fact]
    public void AC4_轴取值矩阵_三基础请求四主题_审计零critical()
    {
        var failures = new List<String>();
        var cases = 0;

        foreach (var axis in new[] { StyleAxes.ShadowStyle, StyleAxes.BorderStrength, StyleAxes.NeutralTemp,
                     StyleAxes.FontPairing, StyleAxes.RadiusStyle, StyleAxes.AccentStrategy })
        {
            foreach (var value in StyleAxes.ValuesOf(axis)!.Skip(1))
            {
                var bi = 0;
                foreach (var baseReq in BaseRequests())
                {
                    var req = Clone(baseReq);
                    Set(req, axis, value);
                    cases++;
                    AddIf(failures, AuditIssue(req, $"{axis}={value} × 基础请求#{bi}"));
                    bi++;
                }
            }
        }

        // 数值轴另测（取值连续，取最小/中间/最大三点）
        foreach (var strength in new[] { 0d, 0.5, 2d })
        {
            var bi = 0;
            foreach (var baseReq in BaseRequests())
            {
                var req = Clone(baseReq);
                req.ShadowStrength = strength;
                cases++;
                AddIf(failures, AuditIssue(req, $"阴影强度={strength} × 基础请求#{bi}"));
                bi++;
            }
        }

        // 数量断言用词表算，不写死：加第八条轴或某轴多一个取值时，这里要跟着变才算"矩阵真的跑满了"
        var enumValues = new[] { StyleAxes.ShadowStyle, StyleAxes.BorderStrength, StyleAxes.NeutralTemp,
            StyleAxes.FontPairing, StyleAxes.RadiusStyle, StyleAxes.AccentStrategy }
            .Sum(axis => StyleAxes.ValuesOf(axis)!.Count - 1);
        cases.Should().Be((enumValues + 3) * BaseRequests().Length,
            $"矩阵必须跑满（6 条枚举轴的全部非默认取值 + 数值轴 3 点）× 全部基础请求，实际 {cases}");
        failures.Should().BeEmpty("生成产物自己必须先过门禁：\n" + String.Join("\n", failures));
    }

    /// <summary>矩阵跑一趟几百个用例，失败不能一红就断——先把全部问题收齐，再一次性报出来</summary>
    static void AddIf(List<String> failures, String? issue)
    {
        if (issue != null) failures.Add(issue);
    }

    static void Set(GenerationRequest req, String axis, String value)
    {
        switch (axis)
        {
            case StyleAxes.ShadowStyle: req.ShadowStyle = value; break;
            case StyleAxes.BorderStrength: req.BorderStrength = value; break;
            case StyleAxes.NeutralTemp: req.NeutralTemp = value; break;
            case StyleAxes.FontPairing: req.FontPairing = value; break;
            case StyleAxes.RadiusStyle: req.RadiusStyle = value; break;
            case StyleAxes.AccentStrategy: req.AccentStrategy = value; break;
            default: throw new ArgumentException($"矩阵测试不知道轴 {axis}");
        }
    }



    [Fact]
    public void AC7_十三个预设_生成落库后审计零critical()
    {
        var failures = new List<String>();
        foreach (var preset in StylePresets.All)
        {
            AddIf(failures, AuditIssue(PresetRecommender.Copy(preset.Request), $"预设 {preset.Id}"));
        }

        StylePresets.All.Should().HaveCount(13);
        failures.Should().BeEmpty("目录里的每件衣服都必须自己过门禁：\n" + String.Join("\n", failures));
    }

    [Fact]
    public void AC4_轴矩阵落库后_别名目标仍然存在()
    {
        // 圆角风格与字体搭配改的是"别名指向哪个档"。指向不存在的档，落库当场会被别名校验拒绝（ApplyToProject 抛），
        // 这里把这条隐式依赖显式测出来：换轴之后库里每条 component.*.radius 仍能解析到值
        var req = new GenerationRequest
        {
            Hue = 210, RadiusStyle = "pill", FontPairing = "editorial", BorderStrength = "bold", ShadowStyle = "layered",
            Themes = ["light", "dark", "high-contrast", "compact"],
        };
        var p = _projects.Create(new ProjectInput { Code = $"ax-{Guid.NewGuid():N}"[..22], Name = "别名存在性" });
        DesignGenerator.ApplyToProject(_tokens, _projects, p.Id, req, false);

        var light = _tokens.LoadGraph(p.Id, _projects.FindTheme(p.Id, "light")!.Id, "light");
        var radii = _tokens.FindShared(p.Id).Where(t => t.Path.StartsWith("component.", StringComparison.Ordinal) && t.Path.EndsWith(".radius", StringComparison.Ordinal)).ToList();
        radii.Should().NotBeEmpty("组件圆角令牌一条都没落库 = 圆角轴整条白名单是空的");
        foreach (var t in radii)
            light.Resolve(t.Path).IsOk.Should().BeTrue($"换轴后 {t.Path} 的别名目标（{t.AliasPath}）解析不到值");

        _tokens.FindShared(p.Id).Should().Contain(t => t.Path == "font.display", "editorial 的展示字族必须真的落库，不只是产物里有");
    }

    [Fact]
    public void Boundary_editorial才登记display字族_默认档不造假登记()
    {
        // 02-spec「边界条件」：`font.display` 仅 `fontPairing=editorial` 时存在，`SeedBrandCatalog` 要**只在令牌存在时**
        // 登记 role=display 的字体行。此前只有产物侧与令牌侧的断言（AC3/AC4），品牌库这一格没人测过——
        // 空登记等于给用户一条加载不到的字体行，缺登记等于交付时不知道要带哪串衬线。
        var editorial = new GenerationRequest
        {
            Hue = 210, FontPairing = "editorial", Themes = ["light", "dark", "high-contrast", "compact"],
        };
        var pe = _projects.Create(new ProjectInput { Code = $"ax-{Guid.NewGuid():N}"[..22], Name = "display 字族登记" });
        var re = DesignGenerator.ApplyToProject(_tokens, _projects, pe.Id, editorial, false);
        DesignGenerator.SeedBrandCatalog(_catalog, pe.Id, re);

        var roles = _catalog.ListFonts(pe.Id).Select(f => f.Role).ToList();
        roles.Should().Contain("display", "editorial 的展示字族没进品牌库 = 交付侧不知道要加载哪串字体");
        roles.Should().Contain("sans", "sans 行必须还在（display 是补登记，不是替换）");

        // 默认档：先自证前提真不成立（没有 font.display），再看品牌库有没有凭空造一条 display
        var plain = new GenerationRequest { Hue = 210, Themes = ["light", "dark", "high-contrast", "compact"] };
        var pd = _projects.Create(new ProjectInput { Code = $"ax-{Guid.NewGuid():N}"[..22], Name = "默认档不登记" });
        var rd = DesignGenerator.ApplyToProject(_tokens, _projects, pd.Id, plain, false);
        _tokens.FindShared(pd.Id).Should().NotContain(t => t.Path == "font.display", "前提不成立：默认档现在也有展示字族，这条守卫要跟着改判据");
        DesignGenerator.SeedBrandCatalog(_catalog, pd.Id, rd);

        var plainRoles = _catalog.ListFonts(pd.Id).Select(f => f.Role).ToList();
        plainRoles.Should().NotContain("display", "令牌不存在还登记 display = 造假登记");
        plainRoles.Should().Contain("sans", "同一次调用确实跑过（sans 行在），display 的缺席才有意义");
    }
}

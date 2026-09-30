using ForgeSelf.Api.Plugins.DesignSystem;
using ForgeSelf.Api.Plugins.DesignSystem.Data;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;
using ForgeSelf.Api.Plugins.DesignSystem.Services;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>
/// 设计系统自身的版本化（AC13/AC14）：不可变快照 + 哈希 + 令牌级 diff + 发布门禁。
/// 隔离方式同 GenerationAuditTests：本类专属随机临时目录，只创建不删除（铁律 10）。
/// </summary>
[Collection("XCode")]
public class ReleaseSnapshotTests : IDisposable
{
    readonly string _dir;
    readonly DesignProjectService _projects = new();
    readonly TokenRepository _tokens = new();
    readonly AuditRepository _audits = new();
    readonly CatalogRepository _catalog = new();
    readonly AuditEngine _engine;
    readonly ReleaseService _releases;

    public ReleaseSnapshotTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"ForgeSelfDsRel_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);

        DAL.AddConnStr(DesignSystemTables.ConnName, $"Data Source={Path.Combine(_dir, "DesignSystem.db")}", null, "SQLite");
        EntityFactory.InitConnection(DesignSystemTables.ConnName);

        DesignProject.Meta.Cache.Expire = 0;
        DesignTheme.Meta.Cache.Expire = 0;
        DesignToken.Meta.Cache.Expire = 0;
        DesignShadowLayer.Meta.Cache.Expire = 0;
        DesignAudit.Meta.Cache.Expire = 0;
        DesignRelease.Meta.Cache.Expire = 0;
        DesignComponent.Meta.Cache.Expire = 0;
        DesignComponentVariant.Meta.Cache.Expire = 0;
        DesignAsset.Meta.Cache.Expire = 0;
        DesignScreen.Meta.Cache.Expire = 0;
        DesignFontFace.Meta.Cache.Expire = 0;

        _engine = new AuditEngine(_tokens, _projects, _audits);
        _releases = new ReleaseService(_tokens, _projects, _audits, _engine, new DesignSystemPaths(_dir), _catalog);
    }

    public void Dispose() => GC.SuppressFinalize(this);

    /// <summary>造一个"能过自家门禁"的真实设计系统（生成 → 审计），返回项目 id</summary>
    Int64 GeneratedProject()
    {
        var p = _projects.Create(new ProjectInput { Code = $"r-{Guid.NewGuid():N}"[..22], Name = "发布项目" });
        DesignGenerator.ApplyToProject(_tokens, _projects, p.Id, new GenerationRequest
        {
            SeedColor = "#7c3aed",
            Brief = "分布式服务治理控制台",
            Themes = ["light", "dark", "high-contrast"],
        }, false);
        _engine.Run(p.Id).Blocking.Should().BeFalse("前置条件：生成的设计系统必须能通过自家审计门禁");
        return p.Id;
    }

    /// <summary>
    /// 与控制器 `POST generate` 同形状的项目：令牌 + 组件目录/矩阵种子 + 品牌三表种子。
    /// 版本化要覆盖"界面上真能看到的东西"，所以测试也得按真实写入路径造数据，不能只塞令牌。
    /// </summary>
    Int64 SeededProject()
    {
        var pid = GeneratedProject();
        var result = DesignGenerator.ApplyToProject(_tokens, _projects, pid, new GenerationRequest
        {
            SeedColor = "#7c3aed",
            Brief = "分布式服务治理控制台",
            Themes = ["light", "dark", "high-contrast"],
        }, false);
        DesignGenerator.SeedComponentCatalog(_catalog, pid, result);
        DesignGenerator.SeedBrandCatalog(_catalog, pid, result);
        _engine.Run(pid).Blocking.Should().BeFalse();
        return pid;
    }

    /// <summary>取一个"没人引用"的令牌：改它/下架它都不会把别名校验搞坏</summary>
    List<DesignToken> Unreferenced(Int64 projectId)
    {
        var rows = DesignToken.FindAll(DesignToken._.ProjectId == projectId);
        var targets = rows.Where(t => !string.IsNullOrEmpty(t.AliasPath)).Select(t => t.AliasPath!).ToHashSet(StringComparer.Ordinal);
        return rows.Where(t => t.ThemeId == DesignSystemConstants.SharedThemeId && !targets.Contains(t.Path)).ToList();
    }

    /// <summary>挑一个改动后不影响任何对比度配对的尺度令牌（非色彩类型）</summary>
    DesignToken UnreferencedScale(Int64 projectId) =>
        Unreferenced(projectId).First(t => t.Type is TokenTypes.Dimension or TokenTypes.Number or TokenTypes.Duration);

    [Fact]
    public void 发布_写不可变快照文件_并回写项目版本与状态()
    {
        var pid = GeneratedProject();

        var release = _releases.Create(pid, "1.0.0", "首个正式版");

        release.Id.Should().BeGreaterThan(0);
        release.TokensHash.Should().MatchRegex("^[0-9a-f]{64}$", "哈希用于判定快照内容是否真的变了");
        release.TokenCount.Should().BeGreaterThan(100);
        release.AuditPassed.Should().BeTrue();
        File.Exists(release.SnapshotFile).Should().BeTrue();

        var snapshot = _releases.Load(release.Id);
        snapshot.SchemaVersion.Should().Be(ReleaseSnapshot.CurrentSchema);
        snapshot.Version.Should().Be("1.0.0");
        snapshot.Tokens.Should().HaveCount(release.TokenCount);
        // 快照必须按主题展开，而不是只有一份共享层
        snapshot.Tokens.Select(t => t.Theme).Should().Contain(new[] { "shared", "light", "dark" }, "每个主题各自成一份可核对的有效值");
        snapshot.Tokens.Should().OnlyContain(t => !string.IsNullOrEmpty(t.Path));

        var project = _projects.Find(pid)!;
        project.Version.Should().Be("1.0.0");
        project.Status.Should().Be(ProjectStatus.Published);
        project.PublishedAt.Should().BeAfter(DateTime.MinValue, "发布必须留下时间戳");
    }

    [Fact]
    public void 同版本号内容未变_幂等返回同一条记录()
    {
        var pid = GeneratedProject();

        var first = _releases.Create(pid, "1.0.0", null);
        var again = _releases.Create(pid, "1.0.0", null);

        again.Id.Should().Be(first.Id);
        again.TokensHash.Should().Be(first.TokensHash);
        _releases.List(pid).Should().ContainSingle();
    }

    [Fact]
    public void 同版本号内容已变_拒绝并提示递增版本号()
    {
        var pid = GeneratedProject();
        _releases.Create(pid, "1.0.0", null);

        var free = UnreferencedScale(pid);
        _tokens.UpsertBatch(pid, [new TokenPatch { Path = free.Path, Value = "42px" }], overwrite: true);

        var act = () => _releases.Create(pid, "1.0.0", null);
        act.Should().Throw<DesignConflictException>().WithMessage("*递增版本号*");
    }

    [Fact]
    public void 存在未通过的critical审计_禁止发布()
    {
        var pid = GeneratedProject();
        var light = _projects.FindTheme(pid, "light")!;
        // 把正文色指向与背景同档的中性色：对比度实测必然不达标，门禁据此拒发
        _tokens.UpsertBatch(pid, [new TokenPatch { Path = "semantic.text-1", ThemeId = light.Id, AliasPath = "color.neutral.100" }], overwrite: true);

        var act = () => _releases.Create(pid, "1.0.0", null);
        act.Should().Throw<DesignConflictException>().WithMessage("*critical*");

        DesignRelease.FindAll(DesignRelease._.ProjectId == pid).Should().BeEmpty("被门禁拦下时不得留下发布记录");
    }

    [Fact]
    public void 空项目_没有令牌时无内容可发布()
    {
        var p = _projects.Create(new ProjectInput { Code = $"e-{Guid.NewGuid():N}"[..22], Name = "空项目" });

        var act = () => _releases.Create(p.Id, "0.1.0", null);
        act.Should().Throw<DesignConflictException>().WithMessage("*还没有任何令牌*");
    }

    [Fact]
    public void 未知项目_发布报不存在() =>
        ((Action)(() => _releases.Create(987654321, "1.0.0", null))).Should().Throw<KeyNotFoundException>();

    [Fact]
    public void diff_报出改值_改hex_改别名_新增_下架()
    {
        var pid = GeneratedProject();
        // 先埋三个自定义令牌，让它们在 v1 里就存在（改别名需要两个可指向的目标）
        var free = UnreferencedScale(pid);
        _tokens.UpsertBatch(pid, [new TokenPatch { Path = "color.quartz.500", Value = "#1f2937", Type = TokenTypes.Color }]);
        _tokens.UpsertBatch(pid, [new TokenPatch { Path = "color.quartz.700", Value = "#334155", Type = TokenTypes.Color }]);
        _tokens.UpsertBatch(pid, [new TokenPatch { Path = "semantic.quartz", Tier = TokenTiers.Semantic, Type = TokenTypes.Color, AliasPath = "color.quartz.500" }]);

        var v1 = _releases.Create(pid, "1.0.0", null);

        // 1) 尺度改值 2) 颜色改值（连带 hex）3) 新增 primitive 4) 别名重指 5) 下架旧 primitive
        _tokens.UpsertBatch(pid, [new TokenPatch { Path = free.Path, Value = "42px" }], overwrite: true);
        _tokens.UpsertBatch(pid, [new TokenPatch { Path = "color.quartz.700", Value = "#0f172a", Type = TokenTypes.Color }], overwrite: true);
        _tokens.UpsertBatch(pid, [new TokenPatch { Path = "color.quartz.600", Value = "#111827", Type = TokenTypes.Color }]);
        _tokens.UpsertBatch(pid, [new TokenPatch { Path = "semantic.quartz", Tier = TokenTiers.Semantic, Type = TokenTypes.Color, AliasPath = "color.quartz.600" }], overwrite: true);
        _tokens.Retire(pid, DesignSystemConstants.SharedThemeId, "color.quartz.500", "color.quartz.600").Should().BeTrue();

        var v2 = _releases.Create(pid, "2.0.0", "改了尺度并换代石英色");
        v2.Id.Should().NotBe(v1.Id);

        var diff = _releases.Diff(v1.Id, v2.Id);
        diff.From.Should().Be("1.0.0");
        diff.To.Should().Be("2.0.0");
        diff.IsEmpty.Should().BeFalse();

        diff.Changed.Should().Contain(c => c.Path == free.Path && c.Field == "value" && c.From != c.To && c.To == "42px");
        diff.Changed.Should().Contain(c => c.Path == "color.quartz.700" && c.Field == "value" && c.To == "#0f172a");
        diff.Changed.Should().Contain(c => c.Path == "color.quartz.700" && c.Field == "hex", "改色必须连带报出 hex，供设计同学肉眼核对");
        diff.Changed.Should().Contain(c => c.Path == "semantic.quartz" && c.Field == "aliasPath"
            && c.From == "color.quartz.500" && c.To == "color.quartz.600");
        diff.Added.Should().Contain(t => t.Path == "color.quartz.600" && t.Value == "#111827");
        diff.Removed.Should().Contain(t => t.Path == "color.quartz.500", "下架的令牌不进新快照，因此必须报成删除");
        diff.MissingThemes.Should().BeEmpty("主题集合没变");

        // 快照按主题自足展开：同一条改动在每个主题的视图里都要看得见，否则"只看 light"的人漏改动
        diff.Removed.Should().OnlyContain(t => t.Path == "color.quartz.500");
        diff.Removed.Select(t => t.Theme).Distinct().Should()
            .Contain(new[] { "shared", "light", "dark" }, "每个主题视图各自报一次，不互相依赖");
    }

    [Fact]
    public void diff_同一快照自比_为空()
    {
        var pid = GeneratedProject();
        var v1 = _releases.Create(pid, "1.0.0", null);

        var diff = ReleaseService.DiffOf(_releases.Load(v1.Id), _releases.Load(v1.Id));

        diff.IsEmpty.Should().BeTrue();
        diff.Total.Should().Be(0);
    }

    [Fact]
    public void 新增主题后_下一个快照报出主题集合变化()
    {
        var pid = GeneratedProject();
        var v1 = _releases.Create(pid, "1.0.0", null);

        _projects.AddTheme(pid, new ThemeInput { Code = "dusk", Name = "黄昏", ModeKind = ThemeModeKinds.Color });
        var light = _projects.FindTheme(pid, "light")!;
        var dusk = _projects.FindTheme(pid, "dusk")!;
        // 新主题要有内容，否则审计先报 theme-empty 警告（不影响门禁，但 diff 应看到新主题）
        foreach (var t in DesignToken.FindAll(DesignToken._.ProjectId == pid & DesignToken._.ThemeId == light.Id))
            _tokens.UpsertBatch(pid, [new TokenPatch { Path = t.Path, ThemeId = dusk.Id, Tier = t.Tier, Type = t.Type, Value = t.Value, AliasPath = t.AliasPath }]);

        var v2 = _releases.Create(pid, "1.1.0", "新增黄昏主题");
        var diff = _releases.Diff(v1.Id, v2.Id);

        diff.MissingThemes.Should().Contain("dusk");
        diff.Added.Should().Contain(t => t.Theme == "dusk");
    }

    [Fact]
    public void 快照文件只增不改_前一版内容保持原样()
    {
        var pid = GeneratedProject();
        var v1 = _releases.Create(pid, "1.0.0", null);
        var before = File.ReadAllText(v1.SnapshotFile);

        var free = UnreferencedScale(pid);
        _tokens.UpsertBatch(pid, [new TokenPatch { Path = free.Path, Value = "7px" }], overwrite: true);
        var v2 = _releases.Create(pid, "2.0.0", null);

        File.ReadAllText(v1.SnapshotFile).Should().Be(before, "已发布快照不可变：后续发布不得覆写前一份文件");
        v2.SnapshotFile.Should().NotBe(v1.SnapshotFile, "每个版本一份文件，谁都能单独取走");
        File.Exists(v2.SnapshotFile).Should().BeTrue();
        _releases.Load(v1.Id).Tokens.Should().HaveCount(v1.TokenCount, "旧快照在后续发布之后仍可完整读回");
    }

    [Fact]
    public void 快照可再投影为DTCG_别名保持花括号引用()
    {
        var pid = GeneratedProject();
        var release = _releases.Create(pid, "1.0.0", null);
        var snapshot = _releases.Load(release.Id);

        var dtcg = _releases.SnapshotToDtcg(snapshot, "light");
        using var doc = System.Text.Json.JsonDocument.Parse(dtcg);
        doc.RootElement.EnumerateObject().Should().NotBeEmpty();

        var alias = snapshot.Tokens.First(t => t.Theme == "light" && !string.IsNullOrEmpty(t.AliasPath));
        dtcg.Should().Contain("{" + alias.AliasPath + "}");
    }

    [Fact]
    public void 读不存在的发布_报不存在()
    {
        ((Action)(() => _releases.Load(123456789))).Should().Throw<KeyNotFoundException>();
        ((Action)(() => _releases.Diff(123456789, 987654321))).Should().Throw<KeyNotFoundException>();
    }

    /* ============================ M9：版本必须覆盖品牌与组件 ============================
     * 过去快照与 diff 只有令牌：换 logo、改字体许可证、加删起手屏、组件目录变化，两版之间"看不出来"，
     * 而且同版本号重发会被当成"内容一致"幂等放行 —— "可版本化"这个卖点只对一半东西成立。
     */

    [Fact]
    public void 快照必须把品牌三表与组件目录一起钉进版本()
    {
        var pid = SeededProject();
        var rel = _releases.Create(pid, "1.0.0", null);
        var snap = _releases.Load(rel.Id);

        snap.SchemaVersion.Should().Be(ReleaseSnapshot.CurrentSchema);
        snap.Specs.Should().NotBeEmpty("版本化只覆盖令牌 = 换了 logo 也看不出两版不同");
        snap.Specs.Select(s => s.Kind).Should().Contain(["asset", "screen", "font", "component", "variant"]);
        snap.Specs.Should().OnlyContain(s => !string.IsNullOrEmpty(s.Key) && s.Fields.Count > 0,
            "每条规格都要带可比的字段集，空壳条目只会让 diff 假干净");
    }

    [Fact]
    public void 换logo必须被版本识别_同版本重发不得幂等放行()
    {
        var pid = SeededProject();
        var r1 = _releases.Create(pid, "1.0.0", null);

        _catalog.SaveAsset(pid, "logo", "品牌标识", "logo", "<path d=\"M1 1h2v2z\" fill=\"currentColor\"/>",
            null, null, "用户换图", "Owned");

        // 旧实现（哈希只含令牌）会在这里"幂等返回"，等于版本没识别出内容变了
        ((Action)(() => _releases.Create(pid, "1.0.0", null))).Should().Throw<DesignConflictException>()
            .WithMessage("*快照不可变*");

        var r2 = _releases.Create(pid, "1.1.0", null);
        r2.TokensHash.Should().NotBe(r1.TokensHash, "品牌内容变了，快照哈希必须变");

        var diff = _releases.Diff(r1.Id, r2.Id);
        diff.SpecsComparable.Should().BeTrue();
        diff.SpecsChanged.Should().Contain(c => c.Kind == "asset" && c.Key == "logo" && c.Field == "svgBody",
            "diff 要能指到「哪一条的哪个字段」，而不是只说「有变化」");
    }

    [Fact]
    public void 内容未变时两版diff不得出现任何假变更()
    {
        var pid = SeededProject();
        var r1 = _releases.Create(pid, "1.0.0", null);
        var r2 = _releases.Create(pid, "1.1.0", null);

        var d = _releases.Diff(r1.Id, r2.Id);
        d.SpecsComparable.Should().BeTrue();
        d.Added.Should().BeEmpty();
        d.Removed.Should().BeEmpty();
        d.Changed.Should().BeEmpty();
        d.SpecsAdded.Should().BeEmpty();
        d.SpecsRemoved.Should().BeEmpty();
        d.SpecsChanged.Should().BeEmpty();
        d.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void 老快照没有品牌节_比对必须报不可比而不是整节新增()
    {
        var pid = SeededProject();
        var rel = _releases.Create(pid, "1.0.0", null);
        var current = _releases.Load(rel.Id);

        // 模拟 v1 快照（schema 1：只有令牌，没有 Specs）
        var legacy = current with { SchemaVersion = 1, Specs = null };
        var d = ReleaseService.DiffOf(legacy, current);

        d.SpecsComparable.Should().BeFalse("一边没有这一节时，「新增 N 条」是编出来的");
        d.SpecsAdded.Should().BeEmpty();
        d.SpecsChanged.Should().BeEmpty();
        d.IsEmpty.Should().BeTrue("令牌没变而品牌节不可比 → 不许报成有变更");
    }

    [Fact]
    public void 加一条起手屏必须在diff里报成新增()
    {
        var pid = SeededProject();
        var r1 = _releases.Create(pid, "1.0.0", null);
        _catalog.SaveScreen(pid, "billing", "账单", "credit-card", "/billing", null, "e2e 新增", null,
            DesignSystemConstants.SharedThemeId, 99);
        var r2 = _releases.Create(pid, "1.1.0", null);

        var d = _releases.Diff(r1.Id, r2.Id);
        d.SpecsAdded.Should().Contain(s => s.Kind == "screen" && s.Key == "billing");
        d.Total.Should().BeGreaterThan(0, "只改了品牌时，总数也得反映出来（否则界面显示「无变化」）");
    }
}

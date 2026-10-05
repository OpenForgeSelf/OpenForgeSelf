using System.Text.Json;
using ForgeSelf.Api.Plugins.DesignSystem;
using ForgeSelf.Api.Plugins.DesignSystem.Data;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;
using ForgeSelf.Api.Plugins.DesignSystem.Services;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>
/// M3 AC17：版本快照升到 schema 3（把 UX 规范钉进版本），且**旧快照文件必须还读得动、还比得了**。
///
/// 这一段最怕的不是"没记规范"，而是"记了规范以后，老版本之间的 diff 开始胡说"：
/// schema 2 的快照里从来没有 guideline 这一类，如果 diff 把它当成"这一版新增了 14 条规格"，
/// 发布记录里就会出现一批根本没发生过的变更 —— 比漏记更坏，因为它看起来像事实。
///
/// 判据按"不可比"处理（AC17）：<see cref="ReleaseSnapshot.KindComparable"/> 由 schema 版本号决定，
/// 不看数据里恰好有没有那一类（旧侧为空 ≠ "旧侧记录了零条"）。
///
/// 隔离：本类专属随机临时库目录 + 随机快照目录，只创建不删除（铁律 10）。
/// </summary>
[Collection("XCode")]
public class GuidelineReleaseTests : IDisposable
{
    readonly String _dir;
    readonly DesignProjectService _projects = new();
    readonly TokenRepository _tokens = new();
    readonly CatalogRepository _catalog = new();
    readonly AuditRepository _audits = new();
    readonly GuidelineRepository _repo = new();
    readonly GuidelineService _svc;
    readonly AuditEngine _auditEngine;
    readonly ReleaseService _releases;
    readonly DesignSystemPaths _paths;

    public GuidelineReleaseTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"ForgeSelfDsGuidelineRelease_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        DAL.AddConnStr(DesignSystemTables.ConnName, $"Data Source={Path.Combine(_dir, "DesignSystem.db")}", null, "SQLite");
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

        _paths = new DesignSystemPaths(_dir);
        _auditEngine = new AuditEngine(_tokens, _projects, _audits);
        _svc = new GuidelineService(_projects, _tokens, _repo);
        _releases = new ReleaseService(_tokens, _projects, _audits, _auditEngine, _paths, _catalog, _repo);
    }

    public void Dispose() => GC.SuppressFinalize(this);

    /// <summary>生成一个可发布的项目（审计必须无 critical，否则发布被门禁拒）</summary>
    Int64 NewProject(Boolean withGuidelines)
    {
        var id = _projects.Create(new ProjectInput { Code = "rl-" + Guid.NewGuid().ToString("N")[..8], Name = "规范版本项目" }).Id;
        var gen = DesignGenerator.ApplyToProject(_tokens, _projects, id, new GenerationRequest { Hue = 210, Themes = ["light", "dark"] }, false);
        DesignGenerator.SeedComponentCatalog(_catalog, id, gen);
        DesignGenerator.SeedBrandCatalog(_catalog, id, gen);
        _auditEngine.Run(id);
        if (withGuidelines) _svc.Generate(id);
        return id;
    }

    [Fact]
    public void AC17_当前schema是3_快照含14条guideline规格()
    {
        ReleaseSnapshot.CurrentSchema.Should().Be(3);
        ReleaseSnapshot.Schema3SpecKinds.Should().Equal("guideline");

        var id = NewProject(true);
        var release = _releases.Create(id, "1.0.0", null);
        var snap = _releases.Load(release.Id);

        snap.SchemaVersion.Should().Be(ReleaseSnapshot.CurrentSchema);
        var kinds = snap.Specs!.Select(s => s.Kind).Distinct().ToList();
        kinds.Should().Contain("guideline");
        snap.Specs.Count(s => s.Kind == "guideline").Should().Be(GuidelineGenerator.Codes.Length,
            "每条规范都要进版本：漏一条 = 这一版到底要求了什么，回读时说不清");
        snap.Specs!.First(s => s.Kind == "guideline" && s.Key == "buttons").Fields["rules"].Should().NotBeNullOrWhiteSpace();

        // 归档行不进快照（口径与交付一致：归档=这一版不再要求它）
        _svc.Archive(id, "motion");
        var r2 = _releases.Create(id, "1.1.0", null);
        _releases.Load(r2.Id).Specs!.Count(s => s.Kind == "guideline").Should().Be(GuidelineGenerator.Codes.Length - 1);
    }

    [Fact]
    public void AC17_schema2与schema3互比_报不可比而不报新增()
    {
        var id = NewProject(true);
        var current = _releases.Load(_releases.Create(id, "1.0.0", null).Id);
        // 旧快照形态：schema 2 根本没有 guideline 这一类（不是"记录了零条"）
        var legacy = current with { SchemaVersion = 2 };

        foreach (var pair in new[] { (legacy, current), (current, legacy) })
        {
            var diff = ReleaseService.DiffOf(pair.Item1, pair.Item2);
            // schema 2↔3 必须申报 guideline 不可比（注意：Equal(params T[]) 会把 because 当成第二个期望值，写注释别传参）
            diff.NotComparableKinds.Should().Equal("guideline");
            diff.SpecsComparable.Should().BeTrue("规格节整体是可比的（两边都有 Specs），只有这一类不可比");
            diff.SpecsAdded.Should().NotContain(s => s.Kind == "guideline", "把不可比的一类报成新增 = 发布记录里出现没发生过的变更");
            diff.SpecsRemoved.Should().NotContain(s => s.Kind == "guideline");
            diff.SpecsChanged.Should().NotContain(c => c.Kind == "guideline");
            diff.IsEmpty.Should().BeTrue("同内容跨 schema 互比不该报出任何差异");
            diff.Total.Should().Be(0);
        }
    }

    [Fact]
    public void AC17_v3与v3互比_规范改动必须报出来()
    {
        var id = NewProject(true);
        var v1 = _releases.Load(_releases.Create(id, "1.0.0", null).Id);

        _svc.Save(id, "buttons", new GuidelinePatch { Title = "按钮层级（本项目特例）", Body = "改了正文" });
        var v2 = _releases.Load(_releases.Create(id, "1.1.0", null).Id);

        var diff = ReleaseService.DiffOf(v1, v2);
        diff.NotComparableKinds.Should().BeEmpty();
        var changed = diff.SpecsChanged.Where(c => c.Kind == "guideline").ToList();
        changed.Select(c => c.Key).Should().Contain("buttons");
        changed.Should().Contain(c => c.Field == "title" && c.To == "按钮层级（本项目特例）");
        changed.Should().Contain(c => c.Field == "body" && c.To == "改了正文");
        diff.IsEmpty.Should().BeFalse();

        // 新增一条规范 = SpecsAdded；归档一条 = SpecsRemoved（都是这一版真实发生的变化）
        _svc.Save(id, "layout-extra", new GuidelinePatch { Title = "本项目特有栅格", Category = "layout" });
        _svc.Archive(id, "motion");
        var v3 = _releases.Load(_releases.Create(id, "1.2.0", null).Id);
        var d2 = ReleaseService.DiffOf(v2, v3);
        d2.SpecsAdded.Select(s => s.Key).Should().Contain("layout-extra");
        d2.SpecsRemoved.Select(s => s.Key).Should().Contain("motion");
    }

    [Fact]
    public void AC17_hash含规范_同版本改了规范不再幂等()
    {
        var id = NewProject(true);
        var first = _releases.Create(id, "1.0.0", null);

        // 什么都没改：同版本重发必须幂等返回同一行
        _releases.Create(id, "1.0.0", null).Id.Should().Be(first.Id);

        _svc.Save(id, "forms", new GuidelinePatch { Body = "只改了规范正文，令牌一个字没动" });
        var again = Record.Exception(() => _releases.Create(id, "1.0.0", null));
        again.Should().BeOfType<DesignConflictException>(
            "规范没进 hash 时这里会静默幂等返回旧快照 —— 改了交付要求却发不出去、也没人报错");
        again!.Message.Should().Contain("1.0.0");

        // 递增版本号即可发布，且新快照带的是改后的文本
        var next = _releases.Load(_releases.Create(id, "1.0.1", null).Id);
        next.Specs!.First(s => s.Kind == "guideline" && s.Key == "forms").Fields["body"]
            .Should().Be("只改了规范正文，令牌一个字没动");
    }

    [Fact]
    public void AC17_无规范项目_重发幂等且hash不受规范层影响()
    {
        var id = NewProject(withGuidelines: false);
        var first = _releases.Create(id, "1.0.0", null);
        var snapshot = _releases.Load(first.Id);

        snapshot.Specs!.Should().NotContain(s => s.Kind == "guideline", "项目没生成过规范时不该凭空出现这一类");
        _releases.Create(id, "1.0.0", null).Id.Should().Be(first.Id);
        first.TokensHash.Should().NotBeNullOrWhiteSpace();

        // 与 schema 2 互比（旧文件读得动）：把快照文件改成 schema 2 的形态再读回来
        var legacy = new ReleaseSnapshot(2, snapshot.Project, snapshot.Version, snapshot.GeneratedAt, snapshot.Tokens,
            snapshot.Specs.Where(s => s.Kind != "guideline").ToList());
        File.WriteAllText(first.SnapshotFile, JsonSerializer.Serialize(legacy));

        var reread = _releases.Load(first.Id);
        reread.SchemaVersion.Should().Be(2);
        reread.Specs!.Should().NotContain(s => s.Kind == "guideline");
        var diff = ReleaseService.DiffOf(reread, _releases.Load(_releases.Create(id, "1.0.1", null).Id));
        diff.Total.Should().Be(0, "无规范项目跨 schema 互比不该报出差异");
    }

    [Fact]
    public void AC17_diff出参带notComparableKinds_界面才有话可说()
    {
        var id = NewProject(true);
        var current = _releases.Load(_releases.Create(id, "1.0.0", null).Id);
        var diff = ReleaseService.DiffOf(current with { SchemaVersion = 2 }, current);

        // 控制器出参必须把这个字段带给界面（否则前端只能显示"没有变化"，把不可比说成没变化）
        diff.NotComparableKinds.Should().Equal("guideline");
        diff.SpecsAdded.Count.Should().Be(0);
        diff.IsEmpty.Should().BeTrue();
    }
}

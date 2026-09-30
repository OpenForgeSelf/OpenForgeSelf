using ForgeSelf.Api.Plugins.DesignSystem;
using ForgeSelf.Api.Plugins.DesignSystem.Data;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;
using ForgeSelf.Api.Plugins.DesignSystem.Services;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>
/// 设计系统库落地测试（AC2/AC3/AC4/AC18/G5/G7/G14）：插件自建 12 张表、批量写入的事务纪律、
/// 人工行保护、变体幂等、内置库只读、退役是软删。
///
/// 隔离：每类一份随机临时目录库（只创建、永不删除 —— plugin-development 铁律 10）；
/// 存在性/唯一性判断在门面里一律 FindAll/FindCount 直查库，不读 Meta.Cache（铁律 11）。
/// </summary>
[Collection("XCode")]
public class DesignSystemStoreTests : IDisposable
{
    readonly string _dbDir;
    readonly DesignProjectService _projects = new();
    readonly TokenRepository _tokens = new();
    readonly CatalogRepository _catalog = new();

    public DesignSystemStoreTests()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfDesignSystem_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dbDir);

        DAL.AddConnStr(DesignSystemTables.ConnName, $"Data Source={Path.Combine(_dbDir, "DesignSystem.db")}", null, "SQLite");
        EntityFactory.InitConnection(DesignSystemTables.ConnName);

        // 缓存是进程级 AsyncLocal 一份，跨测试会互相看见幽灵行；这里禁用缓存，正确性判定全走直查
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
    }

    public void Dispose()
    {
        // 铁律 10：绝不删除测试库目录，交给构建产物清理
        GC.SuppressFinalize(this);
    }

    Int64 NewProject(String tag)
    {
        var p = _projects.Create(new ProjectInput { Code = $"p-{tag}-{Guid.NewGuid():N}"[..24], Name = $"项目 {tag}" });
        return p.Id;
    }

    [Fact]
    public void EnsureCreated_十二张表可用_且重复调用幂等()
    {
        DesignSystemTables.EnsureCreated().Should().BeTrue();
        DesignSystemTables.EnsureCreated().Should().BeTrue();

        DesignSystemTables.EntityTypes.Should().HaveCount(12);

        // 每张表都能直查（表不存在会抛 no such table）
        DesignProject.FindAll(DesignProject._.Id > 0).Should().NotBeNull();
        DesignTheme.FindAll(DesignTheme._.Id > 0).Should().NotBeNull();
        DesignToken.FindAll(DesignToken._.Id > 0).Should().NotBeNull();
        DesignShadowLayer.FindAll(DesignShadowLayer._.Id > 0).Should().NotBeNull();
        DesignComponent.FindAll(DesignComponent._.Id > 0).Should().NotBeNull();
        DesignComponentVariant.FindAll(DesignComponentVariant._.Id > 0).Should().NotBeNull();
        DesignIcon.FindAll(DesignIcon._.Id > 0).Should().NotBeNull();
        DesignAsset.FindAll(DesignAsset._.Id > 0).Should().NotBeNull();
        DesignScreen.FindAll(DesignScreen._.Id > 0).Should().NotBeNull();
        DesignFontFace.FindAll(DesignFontFace._.Id > 0).Should().NotBeNull();
        DesignAudit.FindAll(DesignAudit._.Id > 0).Should().NotBeNull();
        DesignRelease.FindAll(DesignRelease._.Id > 0).Should().NotBeNull();
    }

    [Fact]
    public void 建项目自动配齐默认主题_含明暗高对比与密度()
    {
        var pid = NewProject("themes");

        var codes = _projects.ListThemes(pid).Select(t => t.Code).ToList();
        codes.Should().Contain(["light", "dark", "high-contrast", "compact"]);
        _projects.ListThemes(pid).Single(t => t.IsDefault).Code.Should().Be("light");
        _projects.Find(pid)!.DefaultThemeId.Should().BeGreaterThan(0);
    }

    [Fact]
    public void 项目码重复被拒_归档是软删不丢行()
    {
        var code = $"dup-{Guid.NewGuid():N}"[..20];
        _projects.Create(new ProjectInput { Code = code, Name = "一" });

        var act = () => _projects.Create(new ProjectInput { Code = code, Name = "二" });
        act.Should().Throw<DesignConflictException>();

        var archived = _projects.Archive(_projects.FindByCode(code)!.Id);
        archived.Status.Should().Be(ProjectStatus.Archived);
        _projects.FindByCode(code).Should().NotBeNull("归档只是状态位，行必须还在");
    }

    [Fact]
    public void 令牌批量写入_可读回_同路径重复提交是更新不是新增()
    {
        var pid = NewProject("tokens");
        var r = _tokens.UpsertBatch(pid,
        [
            new TokenPatch { Path = "color.brand.500", Tier = TokenTiers.Primitive, Type = TokenTypes.Color, Value = "#7c3aed", Group = "brand", Generator = TokenGenerators.Ramp },
            new TokenPatch { Path = "semantic.brand", Tier = TokenTiers.Semantic, Type = TokenTypes.Color, AliasPath = "color.brand.500", Group = "semantic", Generator = TokenGenerators.Ramp },
        ]);

        r.Succeeded.Should().BeTrue();
        r.Created.Should().Be(2);

        // 颜色派生列随值刷新（双色值：hex + oklch 三元组）。参考值 = Tailwind violet-600 = oklch(54.13% 0.2445 293.02)
        var t = _tokens.Find(pid, 0, "color.brand.500")!;
        t.ColorHex.Should().Be("#7c3aed");
        t.OklchL.Should().BeApproximately(0.5413, 0.005);
        t.OklchC.Should().BeApproximately(0.2445, 0.01);
        t.OklchH.Should().BeApproximately(293.02, 1.0);

        var again = _tokens.UpsertBatch(pid, [new TokenPatch { Path = "color.brand.500", Value = "#8b5cf6", Generator = TokenGenerators.Ramp }]);
        again.Created.Should().Be(0);
        again.Updated.Should().Be(1);
        DesignToken.FindCount(DesignToken._.ProjectId == pid).Should().Be(2, "同路径重复提交必须是更新，不能插出第二行");
    }

    [Fact]
    public void 别名成环_整批回滚_零行落库()
    {
        var pid = NewProject("cycle");
        _tokens.UpsertBatch(pid, [new TokenPatch { Path = "color.brand.500", Value = "#7c3aed" }]);
        DesignToken.FindCount(DesignToken._.ProjectId == pid).Should().Be(1);

        var r = _tokens.UpsertBatch(pid,
        [
            new TokenPatch { Path = "semantic.a", AliasPath = "semantic.b" },
            new TokenPatch { Path = "semantic.b", AliasPath = "semantic.a" },
            new TokenPatch { Path = "semantic.c", AliasPath = "color.brand.500" },
        ]);

        r.Succeeded.Should().BeFalse();
        r.Diagnostics.Should().NotBeEmpty();
        DesignToken.FindCount(DesignToken._.ProjectId == pid).Should().Be(1, "校验失败必须全或无，不能留下半成品（G11）");
    }

    [Fact]
    public void 悬空别名被拒_且错误文案指出缺哪个路径()
    {
        var pid = NewProject("dangling");
        var r = _tokens.UpsertBatch(pid, [new TokenPatch { Path = "semantic.brand", AliasPath = "color.nope" }]);

        r.Succeeded.Should().BeFalse();
        r.Diagnostics.Single().Status.Should().Be(ResolveStatus.Missing);
        r.Diagnostics.Single().Message.Should().Contain("color.nope");
    }

    [Fact]
    public void 逆向引用被拒_component可指semantic但semantic不可指component()
    {
        var pid = NewProject("layer");
        _tokens.UpsertBatch(pid,
        [
            new TokenPatch { Path = "color.brand.500", Tier = TokenTiers.Primitive, Value = "#7c3aed" },
            new TokenPatch { Path = "semantic.brand", Tier = TokenTiers.Semantic, AliasPath = "color.brand.500" },
        ]).Succeeded.Should().BeTrue();

        _tokens.UpsertBatch(pid, [new TokenPatch { Path = "component.button.bg", Tier = TokenTiers.Component, AliasPath = "semantic.brand" }])
            .Succeeded.Should().BeTrue();

        var bad = _tokens.UpsertBatch(pid, [new TokenPatch { Path = "semantic.oops", Tier = TokenTiers.Semantic, AliasPath = "component.button.bg" }]);
        bad.Diagnostics.Single().Status.Should().Be(ResolveStatus.LayerViolation);
    }

    [Fact]
    public void 批次内同路径重复早判_不依赖数据库约束报错()
    {
        var pid = NewProject("dupbatch");
        var r = _tokens.UpsertBatch(pid,
        [
            new TokenPatch { Path = "color.brand.500", Value = "#111111" },
            new TokenPatch { Path = "color.brand.500", Value = "#222222" },
        ]);

        r.Succeeded.Should().BeFalse();
        r.Diagnostics.Single().Message.Should().Contain("重复");
        DesignToken.FindCount(DesignToken._.ProjectId == pid).Should().Be(0);
    }

    [Fact]
    public void 手改过的令牌不被重新生成覆盖_显式overwrite才覆盖()
    {
        var pid = NewProject("manual");
        _tokens.UpsertBatch(pid, [new TokenPatch { Path = "color.brand.500", Value = "#7c3aed", Generator = TokenGenerators.Ramp }]);
        // 人工改动一经写入即归 manual（受保护）
        _tokens.UpsertOne(pid, new TokenPatch { Path = "color.brand.500", Value = "#1d4ed8" }).Succeeded.Should().BeTrue();

        var regen = _tokens.UpsertBatch(pid, [new TokenPatch { Path = "color.brand.500", Value = "#be123c" }]);
        regen.SkippedProtected.Should().Be(1);
        regen.Conflicts.Should().Contain("color.brand.500");
        _tokens.Find(pid, 0, "color.brand.500")!.Value.Should().Be("#1d4ed8");

        var forced = _tokens.UpsertBatch(pid, [new TokenPatch { Path = "color.brand.500", Value = "#be123c" }], overwrite: true);
        forced.SkippedProtected.Should().Be(0);
        _tokens.Find(pid, 0, "color.brand.500")!.Value.Should().Be("#be123c");
    }

    [Fact]
    public void 乐观并发_过期ExpectUpdatedAt被拒()
    {
        var pid = NewProject("concurrency");
        _tokens.UpsertBatch(pid, [new TokenPatch { Path = "color.brand.500", Value = "#7c3aed" }]);
        var current = _tokens.Find(pid, 0, "color.brand.500")!;

        var r = _tokens.UpsertOne(pid, new TokenPatch { Path = "color.brand.500", Value = "#000000", ExpectUpdatedAt = current.UpdatedAt.AddMinutes(-5) });
        r.Succeeded.Should().BeFalse();
        r.Diagnostics.Single().Message.Should().Contain("已被他人修改");
        _tokens.Find(pid, 0, "color.brand.500")!.Value.Should().Be("#7c3aed");
    }

    [Fact]
    public void 自定义品牌主题可加_同码重复被拒_设为默认会互斥()
    {
        var pid = NewProject("themepick");

        var ocean = _projects.AddTheme(pid, new ThemeInput { Code = "ocean", Name = "海洋", ModeKind = ThemeModeKinds.Brand, IsDefault = true });
        ocean.IsDefault.Should().BeTrue();
        _projects.ListThemes(pid).Count(t => t.IsDefault).Should().Be(1, "同一项目只能有一个默认主题");
        _projects.Find(pid)!.DefaultThemeId.Should().Be(ocean.Id);

        var dup = () => _projects.AddTheme(pid, new ThemeInput { Code = "ocean" });
        dup.Should().Throw<DesignConflictException>();
    }

    [Fact]
    public void 主题覆盖层与共享层合并解析()
    {
        var pid = NewProject("themetrim");
        _tokens.UpsertBatch(pid, [new TokenPatch { Path = "color.brand.500", Value = "#7c3aed", Generator = TokenGenerators.Ramp }]);
        var dark = _projects.FindTheme(pid, "dark") ?? throw new InvalidOperationException("默认主题 dark 未随项目建出");
        _tokens.UpsertBatch(pid, [new TokenPatch { Path = "color.brand.500", ThemeId = dark.Id, Value = "#a78bfa", Generator = TokenGenerators.Ramp }]);

        _tokens.LoadGraph(pid, null).Resolve("color.brand.500").Value.Should().Be("#7c3aed");

        var g = _tokens.LoadGraph(pid, dark.Id, "dark");
        g.Resolve("color.brand.500").Value.Should().Be("#a78bfa");
    }

    [Fact]
    public void 退役令牌是软删_行仍在且可追溯替代者()
    {
        var pid = NewProject("retire");
        _tokens.UpsertBatch(pid, [new TokenPatch { Path = "color.old", Value = "#111111" }]);

        _tokens.Retire(pid, 0, "color.old", "color.brand.500").Should().BeTrue();

        var t = _tokens.Find(pid, 0, "color.old")!;
        t.Lifecycle.Should().Be(TokenLifecycles.Removed);
        t.Deprecated.Should().BeTrue();
        t.ReplacedBy.Should().Be("color.brand.500");
    }

    [Fact]
    public void 变体键序不同经规范化后仍是同一行_幂等upsert()
    {
        var pid = NewProject("variant");
        var c = _catalog.SaveComponent(pid, new ComponentInput { Code = "button", Name = "按钮", Category = "primitive" });

        _catalog.SaveVariant(pid, c, new VariantInput { Code = "primary-lg", VariantJson = """{"size":"lg","tone":"brand"}""", State = "hover" });
        _catalog.SaveVariant(pid, c, new VariantInput { Code = "lg-primary", VariantJson = """{"tone":"brand","size":"lg"}""", State = "hover" });

        DesignComponentVariant.FindCount(DesignComponentVariant._.ComponentId == c.Id).Should().Be(1, "键序不同不得产生重复行（G5）");
        _catalog.ListVariants(c.Id).Single().VariantKey.Should().Be("""{"size":"lg","tone":"brand"}""");
    }

    [Fact]
    public void 内置图标库只读_项目内可自定义()
    {
        var pid = NewProject("icon");

        var act = () => _catalog.SaveIcon(DesignSystemConstants.BuiltinProjectId, new IconInput { Code = "home", SvgBody = "<path d='M1 1'/>" });
        act.Should().Throw<DesignConflictException>();

        _catalog.SaveIcon(pid, new IconInput { Code = "home", SvgBody = "<path d='M3 12h18'/>", Collection = "custom" });
        _catalog.FindIcon(pid, "home").Should().NotBeNull();
    }

    [Fact]
    public void 阴影复合令牌_真源在ValueJson_展开层成对维护()
    {
        var pid = NewProject("shadow");
        var json = """{"x":0,"y":2,"blur":8,"color":"#00000033"}""";
        _tokens.UpsertBatch(pid, [new TokenPatch
        {
            Path = "shadow.elevation-2", Tier = TokenTiers.Semantic, Type = TokenTypes.Shadow, Value = "0 2px 8px #00000033", ValueJson = json,
        }]);

        var token = _tokens.Find(pid, 0, "shadow.elevation-2")!;
        _tokens.ReplaceShadowLayers(token,
        [
            new ShadowLayerInput { OffsetY = 2, Blur = 8, ColorValue = "#000000", Alpha = 0.2 },
            new ShadowLayerInput { OffsetY = 1, Blur = 2, ColorValue = "#000000", Alpha = 0.1 },
        ]);
        _tokens.FindShadowLayers(token.Id).Select(l => l.Layer).Should().Equal(0, 1);

        // 收缩层数时只删本令牌多余的展开行，不动别的令牌
        _tokens.ReplaceShadowLayers(token, [new ShadowLayerInput { OffsetY = 2, Blur = 8, ColorValue = "#000000", Alpha = 0.2 }]);
        _tokens.FindShadowLayers(token.Id).Should().ContainSingle();
        token.ValueJson.Should().Be(json, "ValueJson 是真源，展开层不得反过来改它（G6）");
    }

    [Fact]
    public void 审计按唯一键覆盖_汇总与门禁判据正确()
    {
        var pid = NewProject("audit");
        var audits = new AuditRepository();

        audits.Record(pid, 0, [new AuditItem(AuditKinds.Contrast, "wcag22-1.4.3", "critical", "token", "semantic.text-1", false,
            "semantic.surface-1", "4.5", "3.2", 3.2, "改用 color.brand.700 起的新 tone")]).Should().Be(1);
        audits.HasBlocking(pid).Should().BeTrue();

        // 同键重跑必须覆盖而非堆积
        audits.Record(pid, 0, [new AuditItem(AuditKinds.Contrast, "wcag22-1.4.3", "critical", "token", "semantic.text-1", true,
            "semantic.surface-1", "4.5", "4.9", 4.9)]);
        DesignAudit.FindCount(DesignAudit._.ProjectId == pid).Should().Be(1);
        audits.HasBlocking(pid).Should().BeFalse();

        var summary = audits.Summarize(pid);
        summary.Total.Should().Be(1);
        summary.Passed.Should().Be(1);
        summary.Blocking.Should().BeFalse();
    }
}

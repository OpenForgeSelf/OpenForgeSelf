using System.Reflection;
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
/// 生成 → 落库 → 审计 的端到端后端测试（AC9/AC11/AC18 的数据面）。
/// 隔离方式同 DesignSystemStoreTests：本类专属随机临时库目录，只创建不删除（铁律 10）。
/// </summary>
[Collection("XCode")]
public class GenerationAuditTests : IDisposable
{
    readonly string _dbDir;
    readonly DesignProjectService _projects = new();
    readonly TokenRepository _tokens = new();
    readonly AuditRepository _audits = new();
    readonly CatalogRepository _catalog = new();
    readonly AuditEngine _engine;

    public GenerationAuditTests()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfDsGen_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dbDir);

        DAL.AddConnStr(DesignSystemTables.ConnName, $"Data Source={Path.Combine(_dbDir, "DesignSystem.db")}", null, "SQLite");
        EntityFactory.InitConnection(DesignSystemTables.ConnName);

        DesignProject.Meta.Cache.Expire = 0;
        DesignTheme.Meta.Cache.Expire = 0;
        DesignToken.Meta.Cache.Expire = 0;
        DesignShadowLayer.Meta.Cache.Expire = 0;
        DesignAudit.Meta.Cache.Expire = 0;
        DesignComponent.Meta.Cache.Expire = 0;
        DesignComponentVariant.Meta.Cache.Expire = 0;
        DesignAsset.Meta.Cache.Expire = 0;
        DesignScreen.Meta.Cache.Expire = 0;
        DesignFontFace.Meta.Cache.Expire = 0;

        _engine = new AuditEngine(_tokens, _projects, _audits);
    }

    public void Dispose() => GC.SuppressFinalize(this);

    Int64 NewProject(params String[] themes)
    {
        var p = _projects.Create(new ProjectInput { Code = $"g-{Guid.NewGuid():N}"[..22], Name = "生成项目" });
        foreach (var extra in themes.Where(t => !new[] { "light", "dark", "high-contrast", "compact" }.Contains(t)))
            _projects.AddTheme(p.Id, new ThemeInput { Code = extra, ModeKind = ThemeModeKinds.Color });
        return p.Id;
    }

    GenerationRequest Req() => new() { SeedColor = "#7c3aed", Brief = "分布式服务治理控制台", Themes = ["light", "dark", "high-contrast"] };

    [Fact]
    public void 生成落库_三层齐备_且同一主题解析出的有效值随主题不同()
    {
        var pid = NewProject();
        DesignGenerator.ApplyToProject(_tokens, _projects, pid, Req(), false);

        DesignToken.FindCount(DesignToken._.ProjectId == pid).Should().BeGreaterThan(120);

        var light = _tokens.LoadGraph(pid, _projects.FindTheme(pid, "light")!.Id, "light");
        var dark = _tokens.LoadGraph(pid, _projects.FindTheme(pid, "dark")!.Id, "dark");

        var lightBg = light.ResolveColor("semantic.surface-bg");
        var darkBg = dark.ResolveColor("semantic.surface-bg");
        lightBg.Should().NotBeNull();
        darkBg.Should().NotBeNull();
        lightBg!.Value.L.Should().BeGreaterThan(darkBg!.Value.L, "浅色主题背景必须比暗色主题亮");

        // 组件层不存值，靠别名链到 primitive，因此换主题自动跟随。
        // 注意：brand 填充色两主题故意接近（审美锚点同为 500 阶），真正必须变的是容器底色与正文前景
        var lightCard = light.ResolveColor("component.card.background")!.Value;
        var darkCard = dark.ResolveColor("component.card.background")!.Value;
        Oklch.ToRgb8(lightCard).Should().NotBe(Oklch.ToRgb8(darkCard), "容器底色必须随主题翻转");

        var lightFg = light.ResolveColor("component.button.primary.foreground")!.Value;
        var darkFg = dark.ResolveColor("component.button.primary.foreground")!.Value;
        lightFg.L.Should().BeGreaterThan(darkFg.L, "按钮前景在暗色主题下应更暗（因为容器翻转了）");
        Oklch.ToRgb8(lightFg).Should().NotBe(Oklch.ToRgb8(darkFg), "按钮前景必须随主题翻转");
    }

    [Fact]
    public void 生成必须同时落地组件目录与变体矩阵_且引用的令牌在库里真存在()
    {
        var pid = NewProject();
        var result = DesignGenerator.ApplyToProject(_tokens, _projects, pid, Req(), false);

        var seed = DesignGenerator.SeedComponentCatalog(_catalog, pid, result);
        seed.Components.Should().BeGreaterThanOrEqualTo(10, "按钮/卡片/输入框/徽标/导航/数据表/对话框/提示/选项卡/选择表都要有规格");
        seed.Variants.Should().BeGreaterThanOrEqualTo(30, "矩阵要覆盖角色 × 状态 × 尺寸三条轴，不能只有 base/default");

        var list = _catalog.ListComponents(pid, null);
        list.Should().HaveCount(seed.Components);
        list.Select(c => c.Code).Should().Contain(["button", "card", "input", "dialog", "tabs", "select"]);

        var paths = _tokens.LoadGraph(pid, null, "shared").All().Select(n => n.Path).ToHashSet(StringComparer.Ordinal);
        foreach (var c in list)
        {
            var refs = JsonSerializer.Deserialize<List<string>>(c.TokenRefsJson) ?? [];
            refs.Should().NotBeEmpty($"{c.Code} 的令牌清单不能为空");
            refs.Where(p => !paths.Contains(p)).Should().BeEmpty($"{c.Code} 引用了库里不存在的令牌");
            c.A11yNotes.Should().NotBeNullOrEmpty($"{c.Code} 必须带可达性约束，不能只有外观");
        }

        // 变体格子：每一格引用的令牌必须真存在，且状态名只能来自令牌后缀（不发明状态）
        var button = list.First(c => c.Code == "button");
        var cells = _catalog.ListVariants(button.Id);
        cells.Should().NotBeEmpty("按钮有 primary/secondary/danger 与 hover/active 令牌，矩阵不该空");
        cells.Select(v => v.State).Should().Contain(["default", "hover", "active"]);
        foreach (var v in cells)
        {
            var t = JsonSerializer.Deserialize<List<string>>(v.TokenRefsJson) ?? [];
            t.Should().NotBeEmpty($"{button.Code}/{v.State} 格子必须挂着真令牌");
            t.Where(p => !paths.Contains(p)).Should().BeEmpty("格子里出现库里没有的令牌");
            v.State.Should().MatchRegex("^(default|hover|active|focus|focus-visible|disabled|pressed)$");
        }

        // 尺寸轴必须是真的：三档都在，且 min-height 单调（不是三行同名装饰）
        var graph = _tokens.LoadGraph(pid, null, "shared");
        cells.Where(v => v.VariantJson.Contains("\"size\"")).Select(v => v.VariantKey).ToList()
            .Should().HaveCountGreaterThanOrEqualTo(3, "sm/md/lg 三档都要落格");
        var px = (String path) => int.Parse(graph.Resolve(path).Value.Replace("px", "").Trim());
        px("component.button.sm.min-height").Should().BeLessThan(px("component.button.md.min-height"));
        px("component.button.md.min-height").Should().BeLessThan(px("component.button.lg.min-height"));
        px("component.button.sm.min-height").Should().BeGreaterThanOrEqualTo(24, "WCAG 2.5.8 目标尺寸下限 24px");

        // 交互态按"该组件自己有没有那条令牌"判定：按钮的焦点表现是全局 component.focus.*（焦点环），
        // 不是背景变色 —— 所以按钮矩阵里出现 focus 格才是 bug；输入框有 border-focus，就必须有 focus 格。
        cells.Select(v => v.State).Should().Contain(["default", "hover", "active", "disabled"]);
        cells.Should().NotContain(v => v.State == "focus", "按钮没有焦点背景令牌，不许凭空造一格");
        var input = list.First(c => c.Code == "input");
        _catalog.ListVariants(input.Id).Select(v => v.State).Should().Contain("focus");

        // 幂等：同一次生成再落一次目录，不该堆重复行
        var again = DesignGenerator.SeedComponentCatalog(_catalog, pid, result);
        again.Should().Be(seed);
        DesignComponent.FindCount(DesignComponent._.ProjectId == pid).Should().Be((Int64)seed.Components);
        DesignComponentVariant.FindCount(DesignComponentVariant._.ProjectId == pid).Should().Be((Int64)seed.Variants);
    }

    [Fact]
    public void 生成必须登记字体页面与资产_声明了capabilities就不能永远是空表()
    {
        var pid = NewProject();
        var result = DesignGenerator.ApplyToProject(_tokens, _projects, pid, Req(), false);

        var brand = DesignGenerator.SeedBrandCatalog(_catalog, pid, result);
        brand.Fonts.Should().BeGreaterThan(0, "字体栈里的族名要落成可登记的字体行");
        brand.Screens.Should().BeGreaterThanOrEqualTo(4);
        brand.Assets.Should().Be(2);

        // 许可证不许留空：空许可证等于给自己埋合规雷（用户明确要求"零许可证负担"要看得见）
        _catalog.ListFonts(pid).Should().NotBeEmpty();
        _catalog.ListFonts(pid).Should().OnlyContain(f => !string.IsNullOrWhiteSpace(f.License));

        foreach (var a in _catalog.ListAssets(pid, null))
        {
            a.SvgBody.Should().Contain("currentColor", $"{a.Code} 的颜色必须由令牌/currentColor 决定");
            a.SvgBody.Should().NotMatchRegex("#[0-9a-fA-F]{3,8}", $"{a.Code} 烤死了色值，换肤就无效");
            a.License.Should().Be("Owned");
        }

        var screens = _catalog.ListScreens(pid);
        screens.Should().HaveCountGreaterThanOrEqualTo(brand.Screens);
        screens.Select(s => s.Code).Should().Contain("overview");
        screens.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s.Route), "起手屏没有路由就等于没登记");

        // 幂等：重复种子不得堆行
        DesignGenerator.SeedBrandCatalog(_catalog, pid, result).Should().Be(brand);
        DesignFontFace.FindCount(DesignFontFace._.ProjectId == pid).Should().Be((Int64)brand.Fonts);
        DesignScreen.FindCount(DesignScreen._.ProjectId == pid).Should().Be((Int64)brand.Screens);
        DesignAsset.FindCount(DesignAsset._.ProjectId == pid).Should().Be((Int64)brand.Assets);
    }

    [Fact]
    public void 重跑生成只补空_不得覆盖用户改过的品牌行()
    {
        var pid = NewProject();
        var result = DesignGenerator.ApplyToProject(_tokens, _projects, pid, Req(), false);
        DesignGenerator.SeedBrandCatalog(_catalog, pid, result);

        // 用户动作：换掉 logo、把某个起手屏改成自己产品的路由、给一个字体登记真实许可证
        _catalog.SaveAsset(pid, "logo", "我的标识", "logo", "<path d=\"M2 2h4v4z\" fill=\"currentColor\"/>",
            null, null, "用户自绘", "Owned");
        _catalog.SaveScreen(pid, "overview", "控制台", "dashboard", "/console", null, "用户改过", null,
            DesignSystemConstants.SharedThemeId, 0);
        var seedFont = _catalog.ListFonts(pid).First(f => f.ProjectId == pid);
        _catalog.SaveFont(pid, seedFont.Family, seedFont.Weight, seedFont.Style, "my.woff2", null, "swap",
            seedFont.Role, null, "OFL-1.1", null);

        var after = DesignGenerator.SeedBrandCatalog(_catalog, pid, result);

        var logo = _catalog.ListAssets(pid, null).Single(a => a.Code == "logo");
        logo.Name.Should().Be("我的标识", "重跑生成把用户改过的行写回去 = 删用户的数据");
        logo.SvgBody.Should().Contain("M2 2h4v4z");
        _catalog.ListScreens(pid).Single(s => s.Code == "overview").Route.Should().Be("/console");
        _catalog.ListFonts(pid).Single(f => f.ProjectId == pid && f.Family == seedFont.Family && f.Weight == seedFont.Weight)
            .License.Should().Be("OFL-1.1");

        // 条数不变：种子只补空缺，不重复堆行
        after.Assets.Should().Be(2);
        DesignAsset.FindCount(DesignAsset._.ProjectId == pid).Should().Be(2);
    }

    [Fact]
    public void 生成的设计系统必须通过自己的审计门禁_无critical()
    {
        var pid = NewProject();
        DesignGenerator.ApplyToProject(_tokens, _projects, pid, Req(), false);

        var summary = _engine.Run(pid);

        summary.Blocking.Should().BeFalse($"审计发现 {summary.Critical} 条 critical：{Detail()}");
        DesignAudit.FindCount(DesignAudit._.ProjectId == pid & DesignAudit._.Kind == AuditKinds.Contrast).Should().BeGreaterThan(10);
    }

    [Fact]
    public void 审计能抓到故意做的反例_并给出可执行建议()
    {
        var pid = NewProject();
        DesignGenerator.ApplyToProject(_tokens, _projects, pid, Req(), false);

        var light = _projects.FindTheme(pid, "light")!;
        // 故意把正文色换成几乎与背景同亮的浅灰
        _tokens.UpsertBatch(pid, [new TokenPatch { Path = "semantic.text-1", ThemeId = light.Id, AliasPath = "color.neutral.200" }], overwrite: true);

        var summary = _engine.Run(pid);
        summary.Blocking.Should().BeTrue("正文色对比度不足必须阻断发布");

        var hit = _audits.List(pid, 0, AuditKinds.Contrast, false).Single(a => a.TargetPath.EndsWith("semantic.text-1", StringComparison.Ordinal));
        hit.Severity.Should().Be("critical");
        hit.Suggestion.Should().NotBeNullOrWhiteSpace("不达标必须给出可执行建议，不能只报失败");

        // 故意让组件色直连 primitive → 分层违规
        _tokens.UpsertBatch(pid, [new TokenPatch { Path = "component.button.primary.background", Tier = TokenTiers.Component, Type = TokenTypes.Color, AliasPath = "color.brand.500" }], overwrite: true);
        _engine.Run(pid);
        _audits.List(pid, 0, AuditKinds.TierViolation, false).Should().NotBeEmpty();
    }

    [Fact]
    public void 手改尺度到逆序_门禁必须抓到_且生成产物自己先过()
    {
        var pid = NewProject();
        DesignGenerator.ApplyToProject(_tokens, _projects, pid, Req(), false);
        _engine.Run(pid);
        _audits.List(pid, 0, AuditKinds.Ramp, false).Should().BeEmpty("生成产物必须过这条新门禁，否则新维度一上线就是一片假红");

        // 把 space.6 改得比 space.5 还小：档位号失去含义，按档取间距会忽大忽小
        _tokens.UpsertOne(pid, new TokenPatch { Path = "space.6", Tier = TokenTiers.Primitive, Type = TokenTypes.Dimension, Value = "2px" }, overwrite: true);
        _engine.Run(pid);

        var hit = _audits.List(pid, 0, AuditKinds.Ramp, false).Single(a => a.TargetPath.EndsWith("space.6", StringComparison.Ordinal));
        hit.Severity.Should().Be("warning");
        hit.Suggestion.Should().NotBeNullOrWhiteSpace("抓到就得说清怎么修");
        hit.Message.Should().Contain("space.5", "逆序要指到相邻的那一档，用户才知道是谁被压下去了");
    }

    [Fact]
    public void 手改按钮可点高度到18px_必须报目标尺寸不足_但不拦发布()
    {
        var pid = NewProject();
        DesignGenerator.ApplyToProject(_tokens, _projects, pid, Req(), false);
        _engine.Run(pid);
        _audits.List(pid, 0, AuditKinds.TargetSize, null)
            .Should().NotBeEmpty("三档 min-height 都要被看到，否则这条门禁是空的")
            .And.OnlyContain(a => a.Severity == "info", "生成产物的 28/34/42px 都不低于 24px");

        _tokens.UpsertOne(pid, new TokenPatch
        {
            Path = "component.button.sm.min-height", Tier = TokenTiers.Component, Type = TokenTypes.Dimension, Value = "18px",
        }, overwrite: true);
        _engine.Run(pid);

        var hit = _audits.List(pid, 0, AuditKinds.TargetSize, false).Single(a => a.TargetPath == "component.button.sm.min-height");
        hit.Passed.Should().BeFalse();
        hit.Severity.Should().Be("warning", "WCAG 2.5.8 本身有例外条款（内联/浏览器控制/本质性小目标），报 critical 会误伤并让人无视门禁");
        hit.Message.Should().Contain("24");
    }

    [Fact]
    public void 对比度审计必须按命名约定覆盖全部前景背景对_不是只查一张写死的表()
    {
        var pid = NewProject();
        DesignGenerator.ApplyToProject(_tokens, _projects, pid, Req(), false);
        _engine.Run(pid);

        // 这三对都是生成器真实产出的 `component.<名>.foreground` + `.background`，
        // 但过去审计只查一张硬编码清单 —— 清单没写到的组件，对比度从来没人管。
        var checkedPaths = _audits.List(pid, 0, AuditKinds.Contrast, null)
            .Select(a => a.TargetPath.Split(':')[1]).ToHashSet(StringComparer.Ordinal);
        checkedPaths.Should().Contain(["component.button.primary.foreground", "component.card.foreground"], "原有清单里的对仍要被查到");
        checkedPaths.Should().Contain(["component.dialog.foreground", "component.tooltip.foreground", "component.select.foreground"],
            "同一命名约定的前景/背景对必须一起被查到，否则新增组件等于绕过门禁");
    }

    [Fact]
    public void 用户新增的组件前景背景对_对比度不足必须被审计抓到()
    {
        var pid = NewProject();
        DesignGenerator.ApplyToProject(_tokens, _projects, pid, Req(), false);
        var light = _projects.FindTheme(pid, "light")!;

        // 手工加一对几乎同色的组件（真实场景：用户给新组件写了 #ddd 文字配 #eee 底）
        _tokens.UpsertBatch(pid,
        [
            new TokenPatch { Path = "component.foo.foreground", Tier = TokenTiers.Component, Type = TokenTypes.Color, ThemeId = light.Id, Value = "#dddddd" },
            new TokenPatch { Path = "component.foo.background", Tier = TokenTiers.Component, Type = TokenTypes.Color, ThemeId = light.Id, Value = "#eeeeee" },
        ], overwrite: true);
        var summary = _engine.Run(pid);

        var hit = _audits.List(pid, 0, AuditKinds.Contrast, false).SingleOrDefault(a => a.TargetPath.EndsWith("component.foo.foreground", StringComparison.Ordinal));
        hit.Should().NotBeNull("新组件的前景色没被审计 = 门禁只覆盖写死清单里的八个对");
        hit!.Severity.Should().Be("critical");
        hit.PairedPath.Should().Be("component.foo.background");
        summary.Blocking.Should().BeTrue("未达标的对比度必须拦住发布");
    }

    [Fact]
    public void 禁用态对比度按WCAG豁免不拦发布_但读数必须留在审计里()
    {
        var pid = NewProject();
        DesignGenerator.ApplyToProject(_tokens, _projects, pid, Req(), false);
        var summary = _engine.Run(pid);

        // WCAG 1.4.3 原文豁免"非活动界面构件"：生成器的按钮禁用态实测 3.4:1，若按 4.5 判 critical，
        // 门禁就成了哭狼的孩子 —— 用户第一次看到"发布被一个禁用按钮挡住"就会开始绕过门禁。
        // 但豁免不能写成跳过：那些对必须带着读数留在审计里，否则"没人报"和"没问题"长得一模一样。
        var exempt = _audits.List(pid, 0, AuditKinds.Contrast, null)
            .Where(a => a.Rule.EndsWith("-exempt", StringComparison.Ordinal)).ToList();
        exempt.Should().NotBeEmpty("豁免 ≠ 跳过：禁用态的对仍要算出对比度并留档");
        exempt.Should().OnlyContain(a => a.Severity == "info" || a.Severity == "warning", "1.4.3 豁免项永不能升 critical");
        exempt.Should().OnlyContain(a => a.Ratio > 0, "没算过对比度的行不配叫豁免");
        exempt.Should().OnlyContain(a => a.Passed == (a.Ratio >= 3.0), "豁免项退回 1.4.11 的 3.0 兜底线，判级要由读数推出");
        summary.Blocking.Should().BeFalse($"生成产物必须能过自家门禁，禁用态豁免项不得阻断：{Detail()}");

        // 反例（证明 3.0 兜底线真的会响）：把禁用态前景别名到它禁用背景所用的同一个语义角色 → 对比度 1.0
        var dark = _projects.FindTheme(pid, "dark")!;
        var wr = _tokens.UpsertBatch(pid, [
            new TokenPatch
            {
                Path = "component.button.primary.foreground-disabled", ThemeId = dark.Id,
                Tier = TokenTiers.Component, Type = TokenTypes.Color, AliasPath = "semantic.surface-2",
            },
        ], overwrite: true);
        (wr.Created + wr.Updated).Should().Be(1, $"反例必须真的写进库，否则测的是没发生的事：{String.Join(" / ", wr.Diagnostics.Select(d => d.Message))}");
        _engine.Run(pid);

        var hit = _audits.List(pid, 0, AuditKinds.Contrast, false).Single(a => a.TargetPath == "dark:component.button.primary.foreground-disabled");
        hit.Severity.Should().Be("warning", "低于 3.0 的禁用态连「可辨」都谈不上，必须提示");
        hit.Rule.Should().EndWith("-exempt", "豁免项要带自己的 rule，界面才说得清为什么它不拦发布");
        hit.Message.Should().Contain("1.4.3", "不阻断也得写明依据，否则用户以为是漏判");
    }

    [Fact]
    public void 声明成color却解析不出颜色的令牌_必须自己报无法判定_也不许留旧通过行()
    {
        var pid = NewProject();
        DesignGenerator.ApplyToProject(_tokens, _projects, pid, Req(), false);
        var light = _projects.FindTheme(pid, "light")!;
        _engine.Run(pid);
        const String Target = "light:component.card.foreground";
        _audits.List(pid, 0, AuditKinds.Contrast, null).Should().ContainSingle(a => a.TargetPath == Target, "前置：这一对本来有一条结论");

        // 新判据先断言"生成产物自己必须过"，否则第一次上线就是一片假红（技能 24①）
        _audits.List(pid, 0, AuditKinds.Contrast, null).Should().NotContain(a => a.Rule.EndsWith("-unresolved", StringComparison.Ordinal),
            "生成器产出的 color 令牌必须个个解析得出颜色");

        // 写入端只校验别名能不能解析：space.4 解析得出来（8px），但它不是颜色 —— 这一对这轮判不成
        var wr = _tokens.UpsertOne(pid, new TokenPatch
        {
            Path = "component.card.foreground", ThemeId = light.Id, Tier = TokenTiers.Component, Type = TokenTypes.Color, AliasPath = "space.4",
        }, overwrite: true);
        (wr.Created + wr.Updated).Should().Be(1, "反例必须真的写进库");
        _engine.Run(pid);

        var row = _audits.List(pid, 0, AuditKinds.Contrast, null).SingleOrDefault(a => a.TargetPath == Target);
        row.Should().NotBeNull("判不成就静默消失 = 界面上看不出这里有个没判成的对象");
        row!.Rule.Should().EndWith("-unresolved", "上一轮那条真判过的结论不许留着冒充今天查过");
        row.Passed.Should().BeFalse();
        row.Severity.Should().Be("warning", "无法判定不是可达性失败，拦发布就成了假警报");
        row.Message.Should().Contain("无法判定");
        row.Suggestion.Should().NotBeNullOrWhiteSpace("报了就得说清怎么改");
    }

    [Fact]
    public void 矩阵读序按变体分组_组内按状态档位序_用户补的格子不许跳到最前()
    {
        var pid = NewProject();
        var result = DesignGenerator.ApplyToProject(_tokens, _projects, pid, Req(), false);
        DesignGenerator.SeedComponentCatalog(_catalog, pid, result);
        var comps = _catalog.ListComponents(pid, null);
        var button = comps.SingleOrDefault(c => c.Code == "button");
        button.Should().NotBeNull($"前置：按钮必须在目录里，实际读到 {comps.Count} 条 [{String.Join(",", comps.Select(c => c.Code))}]");

        var generated = _catalog.ListVariants(button!.Id);
        generated.Should().NotBeEmpty("前置：按钮矩阵由生成落地");
        // v2.6.7 起分组序是**轴档位序**（原先是 VariantKey 的 JSON 字母序 —— 那会把 danger 排到 primary 前面）。
        // 分组这一层仍然必须有：只按 SortOrder 排就是流水账，用户补的 0 号格会跳到整张矩阵最前。
        AssertGroupsFollowVocabulary(generated.Select(v => v.VariantKey).ToList(), "按钮矩阵");
        foreach (var g in generated.GroupBy(v => v.VariantKey))
            g.Select(v => VariantAxes.Rank(VariantAxes.State, v.State)).Should().BeInAscendingOrder(
                $"变体 {g.Key} 内的状态序必须等于词表档位序（界面、矩阵、产物同一张表）");

        var last = generated[^1];
        _catalog.SaveVariant(pid, button!, new VariantInput
        {
            Code = "button-user-cell", Name = "用户补的一格", VariantJson = last.VariantJson, State = "default",
        });
        var after = _catalog.ListVariants(button!.Id);
        after.Should().Contain(v => v.Code == "button-user-cell", "补的那一格要看得见");
        AssertGroupsFollowVocabulary(after.Select(v => v.VariantKey).ToList(), "补了一格之后的按钮矩阵");
    }

    [Fact]
    public void 尺度单调性对时长族也必须真的生效_不能是静默空跑()
    {
        var pid = NewProject();
        DesignGenerator.ApplyToProject(_tokens, _projects, pid, Req(), false);
        _engine.Run(pid);

        // duration.* 是 Duration 类型（值带 ms），只认 px 的解析器会让这条判据对时长族悄悄变成空跑
        _tokens.UpsertOne(pid, new TokenPatch
        {
            Path = "duration.macro", Tier = TokenTiers.Primitive, Type = TokenTypes.Duration, Value = "100ms",
        }, overwrite: true);
        _engine.Run(pid);

        var hit = _audits.List(pid, 0, AuditKinds.Ramp, false).SingleOrDefault(a => a.TargetPath.EndsWith("duration.macro", StringComparison.Ordinal));
        hit.Should().NotBeNull("界面门禁口径写着 space/radius/duration 三类，时长族没被检就是名实不符");
        hit!.Message.Should().Contain("duration.base");
    }

    [Fact]
    public void 写出不合形状的路径_审计要抓到_而不是让投影静默产出坏键()
    {
        var pid = NewProject();
        DesignGenerator.ApplyToProject(_tokens, _projects, pid, Req(), false);
        _engine.Run(pid);
        _audits.List(pid, 0, AuditKinds.Naming, false).Should().BeEmpty("生成器产出的路径必须全部合形");

        // 写入端只做 trim+小写，不校验形状：带空格的 path 会一路长成 `--ds-space.4 x` 这种没人能用的键
        _tokens.UpsertOne(pid, new TokenPatch { Path = "space.4 x", Tier = TokenTiers.Primitive, Type = TokenTypes.Dimension, Value = "8px" });
        _engine.Run(pid);

        _audits.List(pid, 0, AuditKinds.Naming, false).Single(a => a.TargetPath == "space.4 x")
            .Severity.Should().Be("warning");
    }

    [Fact]
    public void 退役一个还在被引用的令牌_审计必须指出这条链会断()
    {
        var pid = NewProject();
        DesignGenerator.ApplyToProject(_tokens, _projects, pid, Req(), false);
        _engine.Run(pid);
        _audits.List(pid, 0, AuditKinds.Lifecycle, false).Should().BeEmpty();

        // 软删（removed）不物理删：行还在、还能解析，所以"没人报错"恰恰是最危险的状态
        _tokens.Retire(pid, DesignSystemConstants.SharedThemeId, "color.brand.600", null).Should().BeTrue();
        _engine.Run(pid);

        var hits = _audits.List(pid, 0, AuditKinds.Lifecycle, false);
        hits.Should().NotBeEmpty("semantic.brand 还指着 color.brand.600，下架它必须看得见");
        hits.Should().OnlyContain(a => a.Severity == "warning");
        hits.First().PairedPath.Should().Be("color.brand.600");
    }

    [Fact]
    public void 复合阴影令牌的展开层与真源成对落库()
    {
        var pid = NewProject();
        DesignGenerator.ApplyToProject(_tokens, _projects, pid, Req(), false);

        var light = _projects.FindTheme(pid, "light")!;
        var token = _tokens.Find(pid, light.Id, "shadow.elevation-3");
        token.Should().NotBeNull();
        token!.Type.Should().Be(TokenTypes.Shadow);
        token.ValueJson.Should().Contain("blur");

        var layers = _tokens.FindShadowLayers(token.Id);
        layers.Should().NotBeEmpty();
        layers.First().Layer.Should().Be(0);
        token.Value.Should().Contain("px");
    }

    [Fact]
    public void 重复生成同一参数_令牌行数不翻倍_值一致()
    {
        var pid = NewProject();
        var req = Req();

        DesignGenerator.ApplyToProject(_tokens, _projects, pid, req, true);
        var first = DesignToken.FindCount(DesignToken._.ProjectId == pid);
        var lightText = _tokens.Find(pid, _projects.FindTheme(pid, "light")!.Id, "semantic.text-1")!.AliasPath;

        DesignGenerator.ApplyToProject(_tokens, _projects, pid, req, true);

        DesignToken.FindCount(DesignToken._.ProjectId == pid).Should().Be(first, "同参数重生成应是覆盖而非追加");
        _tokens.Find(pid, _projects.FindTheme(pid, "light")!.Id, "semantic.text-1")!.AliasPath.Should().Be(lightText);
    }

    [Fact]
    public void 手改过的语义角色不被重新生成冲掉_并回报冲突()
    {
        var pid = NewProject();
        DesignGenerator.ApplyToProject(_tokens, _projects, pid, Req(), false);
        var light = _projects.FindTheme(pid, "light")!;

        _tokens.UpsertOne(pid, new TokenPatch { Path = "semantic.text-1", ThemeId = light.Id, Value = "#111827" });

        var result = DesignGenerator.ApplyToProject(_tokens, _projects, pid, Req(), overwrite: false);
        result.Should().NotBeNull();
        _tokens.Find(pid, light.Id, "semantic.text-1")!.Value.Should().Be("#111827", "人工写死的值必须保留（G8）");

        // 显式覆盖才恢复成别名
        DesignGenerator.ApplyToProject(_tokens, _projects, pid, Req(), overwrite: true);
        _tokens.Find(pid, light.Id, "semantic.text-1")!.AliasPath.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void 项目计数与库里真值一致_不把缓存列当事实()
    {
        var pid = NewProject();
        DesignGenerator.ApplyToProject(_tokens, _projects, pid, Req(), false);

        // 回归：TokenCount/ComponentCount 是没人维护的缓存列，直接回给界面就是"令牌数 0"这种假数字
        _projects.CountTokens(pid).Should().Be((int)DesignToken.FindCount(DesignToken._.ProjectId == pid));
        _projects.CountTokens(pid).Should().BeGreaterThan(100);
        _projects.CountComponents(pid).Should().Be((int)DesignComponent.FindCount(DesignComponent._.ProjectId == pid));

        var fresh = new DesignProjectService();
        var dto = DesignMapper.ToDto(fresh.Find(pid)!, fresh.CountTokens(pid), fresh.CountComponents(pid));
        dto.TokenCount.Should().BeGreaterThan(0, "项目列表上的令牌数必须是真的");
    }

    [Fact]
    public void 密度主题是真的换尺度_不是只换个名字的配色()
    {
        var pid = NewProject();
        // 基准密度显式给 default：否则 brief 推出来的行业倾向可能本身就是 compact，测试前提会被悄悄吃掉
        DesignGenerator.ApplyToProject(_tokens, _projects, pid, new GenerationRequest
        {
            SeedColor = "#7c3aed",
            Brief = "密集表格控制台",
            Density = "default",
            Themes = ["light", "compact"],
        }, false);

        var light = _tokens.LoadGraph(pid, _projects.FindTheme(pid, "light")!.Id, "light");
        var compact = _tokens.LoadGraph(pid, _projects.FindTheme(pid, "compact")!.Id, "compact");

        var lightGap = light.Resolve("space.4").Value;
        var compactGap = compact.Resolve("space.4").Value;
        compactGap.Should().NotBeNullOrWhiteSpace("compact 主题必须有自己的 space.* 覆盖，否则密度轴是假的");
        compactGap.Should().NotBe(lightGap, "切到 compact 后同一令牌的有效值必须真的变小");

        // 密度轴不得夹带语义色：mode 是轴，不是"另一套配色"
        DesignToken.FindAll(DesignToken._.ProjectId == pid & DesignToken._.ThemeId == _projects.FindTheme(pid, "compact")!.Id)
            .Should().NotContain(t => t.Type == TokenTypes.Color && t.Path.StartsWith("semantic."), "密度主题只铺尺度覆盖");
    }

    [Fact]
    public void 审计词表与引擎产出必须双向对上_每一类都真能产出()
    {
        // v2.6.6：审计板的筛选下拉改读 /meta.auditKinds（前端不再抄一份 KINDS 常量）。
        // 把词表交给后端的同时要堵两头，否则换了一份真相仍可能是一份假的：
        //   ① 表里有、引擎产不出来 → 界面上多一个永远为空的筛选项（空声明，比没有更坏）；
        //   ② 引擎产出、表里没有 → 那一类在界面上根本筛不出来，门禁结论看不见。
        // 两头都拿真数据核：一次凑齐 11 类各自的触发条件，跑一次审计比对。
        var pid = NewProject();
        DesignGenerator.ApplyToProject(_tokens, _projects, pid, Req(), false);
        var light = _projects.FindTheme(pid, "light")!;

        // 写路径有"全图校验，破损整批拒绝"的守卫（该有：坏别名不该进库）。所以每批只放守卫认可的写入，
        // 而 alias 一类"历史破损"只能由物理删行复现（真实场景 = 直接改库 / 迁移漏了被引用行）。
        // 前提必须被证明成立，否则测的是空气 —— 故逐批核对 Created+Updated 条数。
        var okTheme = _tokens.UpsertBatch(pid,
        [
            // contrast：几乎同色的一对
            new TokenPatch { Path = "component.audit.foreground", Tier = TokenTiers.Component, Type = TokenTypes.Color, ThemeId = light.Id, Value = "#dddddd" },
            new TokenPatch { Path = "component.audit.background", Tier = TokenTiers.Component, Type = TokenTypes.Color, ThemeId = light.Id, Value = "#eeeeee" },
            // tier-violation：component 直连 primitive（方向合法，守卫放行；该不该这么指是门禁的事）
            new TokenPatch { Path = "component.audit.skipping", Tier = TokenTiers.Component, Type = TokenTypes.Color, ThemeId = light.Id, AliasPath = "color.brand.500" },
        ], overwrite: true);
        (okTheme.Created + okTheme.Updated).Should().Be(3,
            $"主题层前提没写进去：{string.Join(" | ", okTheme.Diagnostics.Select(d => $"{d.Path}={d.Status}"))}");

        var okShared = _tokens.UpsertBatch(pid,
        [
            // naming / ramp-monotonic / reduced-motion / target-size / orphan
            new TokenPatch { Path = "space.4 x", Tier = TokenTiers.Primitive, Type = TokenTypes.Dimension, Value = "8px" },
            new TokenPatch { Path = "space.6", Tier = TokenTiers.Primitive, Type = TokenTypes.Dimension, Value = "1px" },
            new TokenPatch { Path = "duration.audit-slow", Tier = TokenTiers.Primitive, Type = TokenTypes.Duration, Value = "400ms" },
            new TokenPatch { Path = "component.audit.min-height", Tier = TokenTiers.Component, Type = TokenTypes.Dimension, Value = "18px" },
            new TokenPatch { Path = "color.audit-orphan.500", Tier = TokenTiers.Primitive, Type = TokenTypes.Color, Value = "#123456" },
        ], overwrite: true);
        (okShared.Created + okShared.Updated).Should().Be(5,
            $"共享层前提没写进去：{string.Join(" | ", okShared.Diagnostics.Select(d => $"{d.Path}={d.Status}"))}");

        // lifecycle-ref：软删一个仍在被引用的族色（行还在、还能解析，"没人报错"正是最危险的状态）
        _tokens.Retire(pid, DesignSystemConstants.SharedThemeId, "color.brand.600", null).Should().BeTrue();
        // alias：物理删掉一条"仍被语义层引用"的族色 → 那条别名悬空（真实场景 = 直接改库 / 迁移漏了被引用行）。
        // 现场挑一条来删而不是写死色阶号：语义层选哪一阶是定向算法的结果，写死就等于把测试绑在实现细节上；
        // 但必须避开 color.brand.500 —— 它是上面 tier-violation 的靶子，删了它 tier-violation 就没靶子了。
        var doomedRef = DesignToken.FindAll(DesignToken._.ProjectId == pid & DesignToken._.ThemeId == light.Id)
            .Select(t => t.AliasPath ?? "")
            .FirstOrDefault(p => p.StartsWith("color.", StringComparison.Ordinal) && p != "color.brand.500");
        doomedRef.Should().NotBeNullOrEmpty("库里没有可删的语义→primitive 引用，alias 这一类根本复现不出来");
        DesignToken.FindAll(DesignToken._.ProjectId == pid & DesignToken._.Path == doomedRef).ToList().ForEach(t => t.Delete());
        // focus：三条描边令牌没了（真实场景 = 手改时被清掉）
        foreach (var p in new[] { "component.focus.outline-width", "component.focus.outline-color", "component.focus.outline-offset" })
            DesignToken.FindAll(DesignToken._.ProjectId == pid & DesignToken._.Path == p).ToList().ForEach(t => t.Delete());

        _engine.Run(pid);
        var produced = _audits.List(pid, 0, null, null).Select(a => a.Kind).Distinct(StringComparer.Ordinal).ToList();

        produced.Should().OnlyContain(k => AuditKinds.All.Contains(k, StringComparer.Ordinal),
            $"引擎产出了词表外的类别（{string.Join(",", produced.Except(AuditKinds.All))}）：/meta 给界面的词表不全，那一类根本筛不出来");
        AuditKinds.All.Should().OnlyContain(k => produced.Contains(k),
            $"词表里有 {string.Join(",", AuditKinds.All.Except(produced))} 却造不出触发场景 = 空声明，界面上是一个永远为空的筛选项（本次实际产出：{string.Join(",", produced)}）");

        // 表本身也不能与常量脱节：加了新常量忘了进 All，上面两条都抓不到（那条类压根没进 /meta）
        var declared = typeof(AuditKinds).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(String)).Select(f => (String)f.GetValue(null)!).ToList();
        AuditKinds.All.Should().BeEquivalentTo(declared,
            $"AuditKinds 的常量（{declared.Count} 个）与 All（{AuditKinds.All.Length} 个）必须一一对应");
    }

    [Fact]
    public void 变体行序按轴档位序_不是VariantKey的JSON字典序()
    {
        // v2.6.7：矩阵的**分组序**以前按 `VariantKey` 排，而那是 JSON 文本的字典序 ——
        // `{"role":"danger"}` 会排在 `{"role":"primary"}` 前面。设计师核对的正是"角色从主到次"这条线，
        // 所以行序必须来自 `VariantAxes` 那张表（与状态维 v2.6.4 同一个口径）。
        var pid = NewProject();
        DesignGenerator.ApplyToProject(_tokens, _projects, pid, Req(), false);
        var comp = _catalog.SaveComponent(pid, new ComponentInput { Code = $"axis-{Guid.NewGuid():N}"[..24], Name = "轴序组件" });

        // 故意按"字典序会错"的顺序写入，并混进一条尺寸轴与一条表外轴
        foreach (var json in new[]
                 {
                     """{"variant":"item"}""",
                     """{"role":"link"}""",
                     """{"role":"danger"}""",
                     """{"size":"lg"}""",
                     """{"role":"primary"}""",
                     """{"role":"secondary"}""",
                     """{"role":"ghost"}""",
                     """{"size":"sm"}""",
                 })
            _catalog.SaveVariant(pid, comp, new VariantInput { VariantJson = json, State = "default" });

        var keys = _catalog.ListVariants(comp.Id).Select(v => v.VariantKey).ToList();
        string[] expected =
        [
            """{"size":"sm"}""", """{"size":"lg"}""",                       // 轴序：size 在 role 前；档位 sm 在 lg 前
            """{"role":"primary"}""", """{"role":"secondary"}""", """{"role":"danger"}""",
            """{"role":"ghost"}""", """{"role":"link"}""",                   // 角色按词表序，不是字母序
            """{"variant":"item"}""",                                        // 表外轴排最后
        ];

        keys.Should().Equal(expected,
            "矩阵行序必须等于 VariantAxes 的轴序 + 档位序；表外轴落到最后（没证据的顺序不编造）");
    }

    /// <summary>
    /// 批量读与逐组件读必须**逐位一致**（v2.6.8）：导出改走一次批量查，是因为它以前逐组件查一次 = N+1，
    /// e2e 里导出页 12 个格式并行预览把上百次查询压到同一个 SQLite 文件上，实测 500（database is locked）。
    /// 批量化的前提是"行序不变"，否则导出矩阵会与界面矩阵分道 —— 这条断言就是那个前提。
    /// </summary>
    [Fact]
    public void 批量取变体与逐组件取_行序逐位一致()
    {
        var pid = NewProject();
        DesignGenerator.ApplyToProject(_tokens, _projects, pid, Req(), false);
        var a = _catalog.SaveComponent(pid, new ComponentInput { Code = $"bx-a-{Guid.NewGuid():N}"[..24], Name = "批量A" });
        var b = _catalog.SaveComponent(pid, new ComponentInput { Code = $"bx-b-{Guid.NewGuid():N}"[..24], Name = "批量B" });
        foreach (var json in new[] { """{"role":"secondary"}""", """{"role":"primary"}""", "{}" })
        {
            _catalog.SaveVariant(pid, a, new VariantInput { VariantJson = json, State = "default" });
            _catalog.SaveVariant(pid, b, new VariantInput { VariantJson = json, State = "hover" });
        }

        var missing = a.Id + 999_999;
        var batch = _catalog.VariantsByComponent([a.Id, b.Id, missing]);

        foreach (var key in new[] { a.Id, b.Id, missing })
            batch.Should().ContainKey(key, "查不到行的组件也要给空组，调用方不许自己补默认");
        foreach (var comp in new[] { a, b })
            batch[comp.Id].Select(v => v.VariantKey).Should()
                .Equal(_catalog.ListVariants(comp.Id).Select(v => v.VariantKey), $"{comp.Code} 的批量行序必须等于逐组件行序");
        batch[missing].Should().BeEmpty();
        string[] wantGroupOrder = ["{}", """{"role":"primary"}""", """{"role":"secondary"}"""];
        batch[a.Id].Select(v => v.VariantKey).Should().Equal(wantGroupOrder,
            "组内仍是 base 在最前 + 角色按词表序（不是字母序）");
    }

    /// <summary>取一条变体键在某轴上的档位（没有该轴返回 null）。性质检查用，不在测试里重实现排序算法。</summary>
    static String? AxisValueOf(String variantKey, String axis)
    {
        using var doc = JsonDocument.Parse(variantKey);
        return doc.RootElement.TryGetProperty(axis, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    /// <summary>
    /// 分组序的词表判据（v2.6.7 起取代"按 VariantKey 字母序"）：base 在最前，
    /// 之后每条**已知轴**的值按该轴的档位序出现、且同轴的行连续成块。
    /// 只检性质不重实现算法 —— 重实现一遍就等于再造一份会漂移的真相。
    /// </summary>
    static void AssertGroupsFollowVocabulary(IReadOnlyList<String> keys, String because)
    {
        keys.Should().NotBeEmpty(because);
        keys[0].Should().Be("{}", because + "：base（无轴）应排在最前");
        foreach (var axis in new[] { VariantAxes.Size, VariantAxes.Role })
        {
            var order = VariantAxes.OrderOf(axis);
            var seen = keys.Select(k => AxisValueOf(k, axis)).Where(v => v != null).Distinct(StringComparer.Ordinal).ToList();
            if (seen.Count < 2) continue;
            seen.Should().Equal(order.Where(seen.Contains),
                $"{because}：{axis} 轴的分组序必须等于词表序（{string.Join('/', order)}），实际 {string.Join(',', seen)}");
        }
    }

    String Detail() => string.Join("; ", _audits.List(DesignToken.FindAll(DesignToken._.Id > 0).First().ProjectId, 0, null, false)
        .Where(a => a.Severity == "critical").Take(6).Select(a => $"{a.TargetPath} {a.Message}"));
}

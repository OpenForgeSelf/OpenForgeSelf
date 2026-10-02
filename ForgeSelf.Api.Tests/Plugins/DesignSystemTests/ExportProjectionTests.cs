using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using ForgeSelf.Api.Plugins.DesignSystem;
using ForgeSelf.Api.Plugins.DesignSystem.Data;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;
using ForgeSelf.Api.Plugins.DesignSystem.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>
/// 导出投影测试（AC12/AC13 + 行业约束：库表形状不得泄漏到工件里）。
/// 断言方式是"结构 + 关键内容"，不是整文件字节比对：字节黄金文件在任何格式化改动时都会假红，
/// 反而掩盖真正的结构回归（此为对 03-plan Test Plan 的有意偏离，已记入偏差表）。
/// </summary>
[Collection("XCode")]
public class ExportProjectionTests : IDisposable
{
    readonly string _dbDir;
    readonly Int64 _projectId;
    readonly DesignProjectService _projects = new();
    readonly TokenRepository _tokens = new();
    readonly CatalogRepository _catalog = new();
    readonly ExportService _export;

    public ExportProjectionTests()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfDsExport_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dbDir);
        XCode.DataAccessLayer.DAL.AddConnStr(DesignSystemTables.ConnName, $"Data Source={Path.Combine(_dbDir, "DesignSystem.db")}", null, "SQLite");
        XCode.EntityFactory.InitConnection(DesignSystemTables.ConnName);

        DesignProject.Meta.Cache.Expire = 0;
        DesignTheme.Meta.Cache.Expire = 0;
        DesignToken.Meta.Cache.Expire = 0;
        DesignAsset.Meta.Cache.Expire = 0;
        DesignScreen.Meta.Cache.Expire = 0;
        DesignFontFace.Meta.Cache.Expire = 0;
        DesignComponent.Meta.Cache.Expire = 0;
        DesignComponentVariant.Meta.Cache.Expire = 0;
        DesignIcon.Meta.Cache.Expire = 0;

        _export = new ExportService(_tokens, _projects, _catalog);
        _export.BriefBuilder = new DesignBriefBuilder(_export, _projects, _catalog,
            new DesignReviewService(_export, _tokens, _projects));
        var p = _projects.Create(new ProjectInput { Code = $"x-{Guid.NewGuid():N}"[..22], Name = "导出验证系统" });
        _projectId = p.Id;
        var generated = DesignGenerator.ApplyToProject(_tokens, _projects, _projectId,
            new GenerationRequest { SeedColor = "#7c3aed", Themes = ["light", "dark"] }, false);
        // 品牌三表要有数据，导出投影才谈得上"供不供给得出"（生成器落种子 = 与界面同一份真源）
        DesignGenerator.SeedBrandCatalog(_catalog, _projectId, generated);
        // 组件目录 + 变体矩阵同理：M10 之前的 registry 只有令牌清单，规格（解剖/状态/a11y）留在库里没出来
        DesignGenerator.SeedComponentCatalog(_catalog, _projectId, generated);
    }

    public void Dispose() => GC.SuppressFinalize(this);

    String Get(String format, String? theme = "light") => _export.Produce(_projectId, format, theme).Text;

    [Fact]
    public void 全部格式都能产出非空工件()
    {
        foreach (var f in ExportFormats.All)
        {
            var file = _export.Produce(_projectId, f, f == ExportFormats.Bundle ? null : "light");
            file.Bytes.Should().NotBeEmpty($"{f} 产出为空");
            file.Name.Should().NotBeNullOrWhiteSpace();
        }
    }

    /// <summary>
    /// 声明了的**每一种格式 × 库里的每一个主题**都要真能导出（v2.6.8 的 e2e 抓出来的：
    /// 浏览器请求 `export?format=less&theme=dark` 直接 500，而既有用例只把全部格式跑在 light 上 ——
    /// "格式都试过"不等于"每种格式在每个主题下都能出来"，暗色档才是别名链最密的地方）。
    /// </summary>
    [Fact]
    public void 每种格式在每个主题下都能导出_不只跑浅色档()
    {
        var themes = _projects.ListThemes(_projectId).Select(t => t.Code).ToList();
        themes.Should().NotBeEmpty("没有主题就谈不上逐档导出");
        themes.Should().Contain("dark", "生成器至少要出浅色/深色两档");

        foreach (var theme in themes)
        {
            foreach (var format in ExportFormats.All.Where(f => f != ExportFormats.Bundle))
            {
                var produced = _export.Produce(_projectId, format, theme);
                produced.Text.Should().NotBeNullOrWhiteSpace($"{format}@{theme} 产出为空文本");
                produced.Text.Length.Should().BeGreaterThan(64, $"{format}@{theme} 只出了一句头注释");
            }
        }
    }

    [Fact]
    public void DTCG_每令牌有_value_别名保留花括号_类型走继承()
    {
        using var doc = System.Text.Json.JsonDocument.Parse(Get(ExportFormats.Dtcg, "light"));
        var root = doc.RootElement;

        root.GetProperty("$schema").GetString().Should().Be("https://designtokens.org/schemas/2025.10/format.json");
        root.GetProperty("$extensions").GetProperty("forgeself").GetProperty("projectionVersion").GetString()
            .Should().Be(DesignSystemConstants.ProjectionVersion);

        var brand = root.GetProperty("color").GetProperty("brand");
        // 种子色 #7c3aed 的明度落在 600 阶（NearestStep），因此它必须逐位出现在那一档上
        brand.GetProperty("600").GetProperty("$value").GetString().Should().Be("#7c3aed");
        brand.GetProperty("500").GetProperty("$type").GetString().Should().Be("color");

        // 语义层是纯别名：值必须是 {path} 引用形式，不能展平成字面量（否则下游工具丢继承能力）
        var alias = root.GetProperty("semantic").GetProperty("brand").GetProperty("$value").GetString();
        alias.Should().StartWith("{").And.EndWith("}");

        // $type 继承：整棵 color 子树只在必要处出现 $type，子节点不重复堆同一个值
        root.GetProperty("space").GetProperty("1").GetProperty("$type").GetString().Should().Be("dimension");
    }

    [Fact]
    public void CSS_保留别名引用_tint编译成colormix_带reduced与focus块()
    {
        var css = Get(ExportFormats.Css);

        css.Should().Contain("--ds-color-brand-600: #7c3aed");
        // 对比度定向可能把品牌填充挪到别的阶（白字要达 AA），所以断言"仍是 var 引用"而不是钉死某阶
        css.Should().MatchRegex(@"--ds-semantic-brand: var\(--ds-color-brand-\d+\)", "别名必须保持 var 引用，换品牌色才全站跟随");
        css.Should().Contain("color-mix(in oklab,", "tint 类令牌编译成 color-mix，而不是提前烤死成 rgba");
        css.Should().Contain("@media (prefers-reduced-motion: reduce)");
        css.Should().Contain(":focus-visible");
    }

    [Fact]
    public void CSS_复合令牌必须展开成真能用的值_不许导出空声明()
    {
        // 回归：shadow/transition 的值只存在 ValueJson，早期 RawValue 直接取 Value，
        // 导出成 `--ds-shadow-elevation-2: ;`，前端换肤与交付方拿到的是失效声明。
        var css = Get(ExportFormats.Css);

        css.Should().MatchRegex(@"--ds-shadow-elevation-\d+: [^;]*\d+px[^;]*;", "阴影层串必须带真实长度值");
        css.Should().NotMatchRegex(@"--ds-shadow-elevation-\d+:\s*;", "复合令牌不得导出空值");

        var transition = System.Text.RegularExpressions.Regex.Match(css, @"--ds-transition-\w+:\s*([^;]+);");
        transition.Success.Should().BeTrue("transition 复合令牌也该展开");
        transition.Groups[1].Value.Should().MatchRegex(@"\S+\s+\S+", "形如 0.2s cubic-bezier(...)");
    }

    [Fact]
    public void Tailwind_v4_用_attheme_而不是旧的_config_js()
    {
        var tw = Get(ExportFormats.Tailwind);
        tw.Should().Contain("@theme {");
        tw.Should().Contain("--color-brand-600: #7c3aed");
        tw.Should().Contain("--spacing-");
        tw.Should().NotContain("module.exports", "v3 时代的 tailwind.config.js 形态已过时");
    }

    [Fact]
    public void 预处理器与TS投影各自成语法()
    {
        Get(ExportFormats.Scss).Should().Contain("$color-brand-500:");
        Get(ExportFormats.Less).Should().Contain("@color-brand-500:");
        var ts = Get(ExportFormats.TypeScript);
        ts.Should().Contain("export const tokens = {").And.Contain("} as const;");
        ts.Should().Contain("export type TokenPath = keyof typeof tokens;");
        ts.Should().Contain("export const colorHex = {");
    }

    [Fact]
    public void TokensStudio_带_themes_与每主题一个_set()
    {
        using var doc = System.Text.Json.JsonDocument.Parse(Get(ExportFormats.TokensStudio, null));
        var themes = doc.RootElement.GetProperty("$themes");
        themes.GetArrayLength().Should().Be(2, "light + dark（密度轴不进颜色主题排列）");
        doc.RootElement.GetProperty("sets").TryGetProperty("themes.dark", out _).Should().BeTrue();
        doc.RootElement.GetProperty("tokens").TryGetProperty("$global", out _).Should().BeTrue();
    }

    [Fact]
    public void DESIGNmd_有可lint的YAML前置与规则正文()
    {
        var md = Get(ExportFormats.DesignMd);
        md.Should().StartWith("---");
        foreach (var key in new[] { "name:", "colors:", "typography:", "rounded:", "spacing:", "components:" })
            md.Should().Contain(key);
        md.Should().Contain("Do / Don't");
        md.Should().Contain("#7c3aed");
    }

    [Fact]
    public void ElementPlus接缝_只引用投影里真存在的变量_且不含字面色值()
    {
        var css = Get(ExportFormats.ElementPlus, "light");
        var defined = System.Text.RegularExpressions.Regex.Matches(Get(ExportFormats.Css, "light"), @"--ds-[a-z0-9-]+(?=\s*:)")
            .Select(m => m.Value).ToHashSet(StringComparer.Ordinal);

        var refs = System.Text.RegularExpressions.Regex.Matches(css, @"var\((--ds-[a-z0-9-]+)\)")
            .Select(m => m.Groups[1].Value).Distinct().ToList();
        refs.Should().NotBeEmpty("接缝一个引用都没有 = 它没在给 EP 上色");
        refs.Where(v => !defined.Contains(v)).Should().BeEmpty("接缝引用了 tokens.css 里没有的变量：那条声明在浏览器里整条失效");

        // 不含字面色值：换肤的唯一来源必须是令牌（这条正是本插件对自己界面的要求）
        System.Text.RegularExpressions.Regex.Matches(css, @"#[0-9a-fA-F]{3,8}\b").Count.Should().Be(0);
        css.Should().NotContain("rgb(").And.NotContain("hsl(");

        css.Should().Contain("--el-color-primary: var(--ds-semantic-brand)");
        css.Should().Contain("--el-bg-color: var(--ds-semantic-surface-1)");
        css.Should().Contain("--el-text-color-primary: var(--ds-semantic-text-1)");
        css.Should().Contain("--el-box-shadow: var(--ds-shadow-elevation-3)");
        // EP 的 light-N 是"与白混合"；这里改成与本页底色混合，深色主题才不会把悬停态洗成灰白
        css.Should().MatchRegex(@"--el-color-primary-light-3: color-mix\(in oklab, var\(--ds-semantic-brand\) 70%, var\(--ds-semantic-surface-bg\)\)");
        // 组件尺寸取我们按钮的 min-height（已过 WCAG 2.5.8 下限），而不是 EP 写死的 32px
        css.Should().Contain("--el-component-size: var(--ds-component-button-md-min-height)");
        // 混合的第二个颜色必须是**颜色值**：写成裸的 `--ds-…` 会让整条声明在 computed-value 阶段失效（本仓已踩过一次）
        css.Should().NotMatchRegex(@"color-mix\([^;]*,\s*--ds-", "color-mix 的混合目标写成裸自定义属性名 = 这条声明不会生效");
        css.Should().MatchRegex(@"--el-color-primary-rgb: 124, 58, 237", "#7c3aed 的三元组要能给 EP 拼 rgba()");
        // 只看声明行：文末"有意不映射"的说明里会提到这些键名，那不算映射了
        var decls = css.Split('\n').Select(l => l.TrimStart()).Where(l => l.StartsWith("--el-", StringComparison.Ordinal)).ToList();
        decls.Should().NotContain(l => l.StartsWith("--el-color-white", StringComparison.Ordinal),
            "EP 把 white 当固定前景，映射到表面色会把文字压成同色");
        decls.Should().NotContain(l => l.StartsWith("--el-index-", StringComparison.Ordinal), "层级不是设计值，不该出现在换肤接缝里");
    }

    [Fact]
    public void ElementPlus接缝_空项目下不发明值_全部如实列为未映射()
    {
        var empty = _projects.Create(new ProjectInput { Code = $"e-{Guid.NewGuid():N}"[..22], Name = "空项目" });
        var css = _export.Produce(empty.Id, ExportFormats.ElementPlus, null).Text;

        // 只看声明行（说明性注释里出现 `--ds-*` 不算引用）
        css.Split('\n').Count(l => l.TrimStart().StartsWith("--el-") && l.Contains("var(--ds-"))
            .Should().Be(0, "库里没有令牌时，接缝不许凭空造引用");
        css.Should().Contain("未映射", "缺的东西必须如实列出来，而不是静默交出一个空文件");
        css.Should().Contain("--el-color-primary ← semantic.brand", "未映射清单要指名道姓，用户才知道去补哪条令牌");
        css.Should().Contain(":root {").And.Contain("}");
    }

    [Fact]
    public void ElementPlus接缝_各主题映射同一批键_差别只在令牌解析到的颜色()
    {
        var keys = (String css) => System.Text.RegularExpressions.Regex.Matches(css, @"^\s*(--el-[a-z0-9-]+):",
                System.Text.RegularExpressions.RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value).ToList();

        var light = keys(Get(ExportFormats.ElementPlus, "light"));
        var dark = keys(Get(ExportFormats.ElementPlus, "dark"));
        light.Should().HaveCountGreaterThanOrEqualTo(60, "颜色/文字/边框/填充/圆角/阴影/排版/动效/尺寸都要覆盖到");
        dark.Should().Equal(light, "两版接缝的键集必须一致，否则切主题会漏一批变量回到 EP 默认值");
    }

    [Fact]
    public void Registry_组件条目把规格带出来_且令牌引用逐条可核对()
    {
        using var doc = System.Text.Json.JsonDocument.Parse(Get(ExportFormats.Registry, null));
        var items = doc.RootElement.GetProperty("items");
        items.GetArrayLength().Should().BeGreaterThanOrEqualTo(6);

        // 工件内自证：产物里列出的每条令牌引用都必须能在令牌工件里查到
        var real = _export.Load(_projectId, null).Tokens.Select(t => t.Path).ToHashSet(StringComparer.Ordinal);
        foreach (var item in items.EnumerateArray())
        {
            var name = item.GetProperty("name").GetString()!;
            item.GetProperty("type").GetString().Should().Be("registry:block");
            item.GetProperty("registryDependencies").GetArrayLength().Should().BeGreaterThan(0);

            foreach (var dep in item.GetProperty("registryDependencies").EnumerateArray())
                real.Should().Contain(dep.GetString(), $"{name} 引用了工件里不存在的令牌");
            foreach (var t in item.GetProperty("meta").GetProperty("tokens").EnumerateArray())
                real.Should().Contain(t.GetString(), $"{name} 的令牌清单指向了不存在的令牌");
            foreach (var bad in item.GetProperty("meta").GetProperty("unresolvedTokenRefs").EnumerateArray())
                real.Should().NotContain(bad.GetString(), $"{name} 的未解析引用被当成已解析混进了依赖里");
        }

        var button = items.EnumerateArray().First(i => i.GetProperty("name").GetString() == "button");
        var meta = button.GetProperty("meta");
        meta.GetProperty("inCatalog").GetBoolean().Should().BeTrue("目录里有按钮，registry 就不该只给一串令牌路径");
        meta.GetProperty("a11yNotes").GetString().Should().NotBeNullOrWhiteSpace("可达性约束是规格的本体之一");
        meta.GetProperty("anatomy").GetArrayLength().Should().BeGreaterThan(0);
        meta.GetProperty("states").EnumerateArray().Select(s => s.GetString()!).ToList()
            .Should().Contain(["default", "hover", "active", "disabled"], "状态矩阵没出来就等于规格没交付");

        var variants = meta.GetProperty("variants").EnumerateArray().ToList();
        variants.Should().NotBeEmpty();
        foreach (var v in variants)
        {
            v.GetProperty("code").GetString().Should().NotBeNullOrWhiteSpace();
            v.GetProperty("state").GetString().Should().NotBeNullOrWhiteSpace();
            v.GetProperty("tokens").GetArrayLength().Should().BeGreaterThan(0, "每格要能看出它由哪条令牌支撑");
            foreach (var t in v.GetProperty("tokens").EnumerateArray())
                real.Should().Contain(t.GetString());
        }
        variants.SelectMany(v => v.GetProperty("axes").EnumerateObject())
            .Where(o => o.Name == "size").Select(o => o.Value.GetString()!).ToList()
            .Should().Contain(["sm", "md", "lg"], "尺寸轴要在产物里真能看见，而不是只在界面里");
    }

    [Fact]
    public void Stardust清单声明的行数_明细端点必须真给得出同样多()
    {
        // 内置图标库由插件启动时播种，单测里没有插件生命周期 —— 自己登记一条真行，才能验证"有行就出得去"
        _catalog.SaveIcon(_projectId, new IconInput
        {
            Code = "qa-star",
            Name = "验证星标",
            Collection = "custom",
            SvgBody = "<path d=\"M12 2 14.9 8.6 22 9.2l-5.4 4.7 1.6 6.9L12 17.2 5.8 20.8l1.6-6.9L2 9.2l7.1-.6z\"/>",
            License = "Owned",
        });

        var snap = _export.Load(_projectId, null);
        using var index = System.Text.Json.JsonDocument.Parse(_export.ToStardustIndex(snap));
        var totals = new Dictionary<String, Int32>(StringComparer.Ordinal);
        foreach (var e in index.RootElement.GetProperty("entities").EnumerateArray())
        {
            var entity = e.GetProperty("entity").GetString()!;
            e.GetProperty("url").GetString().Should().Be($"/api/design-system/{_projectId}/{entity}.json",
                "url 必须指向真存在的路由（控制器 {id:long}/{entity}.json），挂空插座就是假声明");

            var total = e.GetProperty("total").GetInt32();
            using var detail = System.Text.Json.JsonDocument.Parse(_export.ToStardustEntity(snap, entity));
            var root = detail.RootElement;
            root.GetProperty("entity").GetString().Should().Be(entity);
            root.GetProperty("total").GetInt32().Should().Be(total);
            root.GetProperty("data").GetArrayLength().Should().Be(total, $"{entity} 清单说 {total} 行，明细就得给 {total} 行");
            totals[entity] = total;
        }

        // M10 之前这三处是假的：组件数的是 component.* 令牌条数，图标/页面/字体写死 0（库里明明有行）
        totals["design-component"].Should().Be(_catalog.ListComponents(_projectId, null).Count,
            "design-component 数的是目录里的组件，不是组件层令牌");
        totals["design-screen"].Should().BeGreaterThan(0);
        totals["design-font-face"].Should().BeGreaterThan(0);
        totals["design-icon"].Should().BeGreaterThan(0);
    }

    [Fact]
    public void 实体明细不泄漏物理表形状_未知实体如实报错()
    {
        var snap = _export.Load(_projectId, "light");
        var leaks = new[] { "DesignShadowLayer", "DesignComponentVariant", "ProjectId", "ThemeId", "ComponentId",
            "ValueJson", "AliasPath", "GeneratorSeed", "SvgBody", "TokenRefsJson", "GuidanceJson" };
        foreach (var entity in ExportService.StardustEntities)
        {
            var lower = _export.ToStardustEntity(snap, entity).ToLowerInvariant();
            foreach (var l in leaks)
                lower.Should().NotContain(l.ToLowerInvariant(), $"{entity} 的明细里出现了库表/列名 {l}");
        }

        _export.ToStardustEntity(snap, "design-color").Should().Contain("\"alias\"", "别名要能以引用形式给出去，不能提前展平");
        FluentActions.Invoking(() => _export.ToStardustEntity(snap, "design-unknown")).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DESIGN_md_组件节按蓝本列出解剖状态轴与可达性要求()
    {
        var md = Get(ExportFormats.DesignMd);
        md.Should().Contain("### 组件规格");

        var lines = md.Split('\n');
        var i = Array.FindIndex(lines, l => l.StartsWith("- `button`（"));
        i.Should().BeGreaterThan(0, "组件节必须逐蓝本列，而不是两句通用提醒");
        var body = String.Join("\n", lines.Skip(i).Take(8));
        body.Should().Contain("解剖：");
        body.Should().Contain("状态：").And.Contain("hover").And.Contain("disabled");
        body.Should().Contain("变体轴：").And.Contain("size = ");
        body.Should().Contain("可达性要求：");
        body.Should().MatchRegex(@"令牌：\d+ 条");
    }

    [Fact]
    public void 产物里的档位与状态必须按档位序_不是字母序凑的()
    {
        // 字母序会把 size 排成 `lg / md / sm`、状态排成 `active、default、disabled、hover`：
        // 值都对，但设计师据以检查的那条"从最小档往上、从默认态往后"被打乱 —— 规格就只是"有"而不是"能用"。
        var lines = Get(ExportFormats.DesignMd).Split('\n');
        var from = Array.FindIndex(lines, l => l.StartsWith("- `button`（"));
        from.Should().BeGreaterThan(0, "前置：DESIGN.md 里必须真有按钮这一条");
        var block = lines.Skip(from + 1).TakeWhile(l => l.StartsWith("  ", StringComparison.Ordinal)).ToList();

        var size = block.Single(l => l.Contains("size = ")).Split("size = ")[1].Trim().Split('；')[0];
        String.Join("|", size.Split(" / ")).Should().Be("sm|md|lg", "DESIGN.md 的 size 轴必须按档位序");
        var states = block.Single(l => l.Contains("状态：")).Split("状态：")[1].Trim().Split('、');
        String.Join("|", states).Should().Be("default|hover|active|disabled", "状态序取自生成器建矩阵用的同一张表");

        // 三份产物必须同序：否则"顺序"又变成两份真相
        using var reg = System.Text.Json.JsonDocument.Parse(Get(ExportFormats.Registry, null));
        String.Join("|", reg.RootElement.GetProperty("items").EnumerateArray()
            .First(i => i.GetProperty("name").GetString() == "button")
            .GetProperty("meta").GetProperty("states").EnumerateArray().Select(n => n.GetString()))
            .Should().Be(String.Join("|", states), "registry 的 states 必须与 DESIGN.md 同序");

        using var ent = System.Text.Json.JsonDocument.Parse(_export.ToStardustEntity(_export.Load(_projectId, null), "design-component"));
        var row = ent.RootElement.GetProperty("data").EnumerateArray()
            .First(r => r.GetProperty("code").GetString() == "button");
        String.Join("|", row.GetProperty("states").EnumerateArray().Select(n => n.GetString()))
            .Should().Be(String.Join("|", states), "实体明细与两份人读产物必须同序");
        String.Join("|", row.GetProperty("axes").GetProperty("size").EnumerateArray().Select(n => n.GetString()))
            .Should().Be("sm|md|lg", "实体明细的轴档位也必须按档位序");

        String.Join("|", VariantAxes.Sort(VariantAxes.Size, ["xxl", "md", "qq"]))
            .Should().Be("md|qq|xxl", "表外的档位排到最后按字母序：没证据的顺序不编造");
    }

    [Fact]
    public void Stardust兼容投影_形状与参考物一致且SQL可执行()
    {
        using var doc = System.Text.Json.JsonDocument.Parse(Get(ExportFormats.StardustJson, null));
        var entities = doc.RootElement.GetProperty("entities");
        entities.GetArrayLength().Should().Be(10, "与参考物十类逻辑实体对齐");
        foreach (var e in entities.EnumerateArray())
        {
            e.GetProperty("entity").GetString().Should().StartWith("design-");
            e.GetProperty("url").GetString().Should().StartWith("/api/design-system/");
            e.TryGetProperty("data", out _).Should().BeFalse("索引只给清单，明细在各实体 url");
        }

        var sql = Get(ExportFormats.StardustSql);
        // 参考物那份从第 2 张表起是非法 SQL（"DELETE FROM a; b; c;"），我们逐表独立成句
        sql.Should().NotContain("DELETE FROM DesignColor; DesignShadow");
        sql.Split('\n').Where(l => l.TrimStart().StartsWith("INSERT")).Should().NotBeEmpty();
        foreach (var line in sql.Split('\n').Where(l => l.TrimStart().StartsWith("INSERT")))
            line.TrimEnd().Should().EndWith(");");
        sql.Should().Contain("BEGIN TRANSACTION;").And.Contain("COMMIT;");
    }

    [Fact]
    public void 任何工件都不得泄漏我方物理表形状()
    {
        // 判据是"我们的"表/列名不得外泄。Stardust 兼容投影里出现 DesignColor/OklchL 是它的对外契约（照它 schema），不算泄漏。
        var leaks = new[] { "DesignShadowLayer", "DesignComponentVariant", "ProjectId", "ThemeId", "ValueJson", "AliasPath", "GeneratorSeed" };
        foreach (var format in ExportFormats.All.Where(f => f != ExportFormats.Bundle))
        {
            var text = Get(format, null);
            foreach (var l in leaks)
                text.Should().NotContain(l, $"{format} 里出现了库表/列名 {l}：投影层泄漏了存储形状");
        }

        Get(ExportFormats.Dtcg).Should().NotContain("forgeself.design-system.workspace", "v1 的 localStorage 键不该出现在导出里");
    }

    [Fact]
    public void bundle_zip_内含全部主题与格式文件()
    {
        var zip = _export.Produce(_projectId, ExportFormats.Bundle, null).Bytes;
        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        var names = archive.Entries.Select(e => e.FullName).ToList();

        names.Should().Contain(n => n.EndsWith("README.md"));
        names.Should().Contain(n => n.EndsWith(".tokens.json"));
        names.Should().Contain(n => n == "css/tokens.light.css");
        names.Should().Contain(n => n == "css/tokens.dark.css");
        names.Should().Contain(n => n == "tailwind/theme.dark.css");
        names.Should().Contain(n => n == "element-plus/theme.light.css", "换肤接缝要能随包一起交付，而不是只能一个个格式下载");
        names.Should().Contain(n => n == "stardust/design-system.data.sql");
        names.Should().Contain(n => n == "stardust/design-system.xml");
        names.Should().Contain(n => n == "registry.json");
        names.Count.Should().BeGreaterThanOrEqualTo(18);

        // 暗色主题文件必须真的不同（不是把亮色拷一份）
        var light = Read("css/tokens.light.css");
        var dark = Read("css/tokens.dark.css");
        light.Should().NotBe(dark);

        string Read(String name)
        {
            var entry = archive.GetEntry(name)!;
            using var s = entry.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return Encoding.UTF8.GetString(ms.ToArray());
        }
    }

    [Fact]
    public void 品牌三表必须进交付物_svg逐文件_字体与页面清单可消费()
    {
        // 库里登记了却不出现在产物里 = 半套交付（M7 补了写入口，这里补"供给得出"的另一半）
        var zip = _export.Produce(_projectId, ExportFormats.Bundle, null).Bytes;
        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        var names = archive.Entries.Select(e => e.FullName).ToList();

        names.Should().Contain("brand/logo.svg");
        names.Should().Contain("brand/motif-grid.svg");
        names.Should().Contain("brand/fonts.json");
        names.Should().Contain("brand/screens.json");

        var logo = Read("brand/logo.svg");
        logo.Should().StartWith("<svg xmlns=");
        logo.Should().Contain("viewBox=\"0 0 24 24\"", "品牌图形统一 24 网格，外壳与图形本体必须同网格");
        logo.Should().Contain("currentColor");
        logo.Should().NotMatchRegex("#[0-9a-fA-F]{3,8}", "导出的 svg 烤死色值 = 换肤失效");

        var fonts = Read("brand/fonts.json");
        fonts.Should().Contain("\"license\"", "合规要能一眼答上：这套字体能不能带、是谁的");
        fonts.Should().Contain("不随产物分发", "系统栈成员必须写明不分发，别让人以为包里有字体文件");
        var screens = Read("brand/screens.json");
        screens.Should().Contain("\"route\"");
        screens.Should().Contain("不是对产品功能的断言", "起手屏是建议，产物里也要这么说");

        String Read(String name)
        {
            var entry = archive.GetEntry(name)!;
            using var s = entry.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return Encoding.UTF8.GetString(ms.ToArray());
        }
    }

    [Fact]
    public void CSS_只给登记了文件的字体出_fontface_系统栈成员如实注释()
    {
        // 种子全是系统字体栈成员：没有可分发文件，硬写 @font-face 就是假声明
        var before = Get(ExportFormats.Css);
        before.Should().NotContain("@font-face {", "系统字体没有文件可指，出了就是让浏览器去 404");
        before.Should().Contain("不随本产物分发", "但也不能隐身：清单与许可证要看得见");

        _catalog.SaveFont(_projectId, "My Sans", 400, "normal", "my-sans.woff2", null, "swap", "sans", null, "OFL-1.1", null);

        var after = Get(ExportFormats.Css);
        after.Should().Contain("@font-face {");
        after.Should().Contain("font-family: \"My Sans\"");
        after.Should().Contain("src: url(\"my-sans.woff2\")");
        after.Should().Contain("font-display: swap");
    }

    [Fact]
    public void DESIGNmd_必须把品牌与许可证写进契约正文()
    {
        var md = Get(ExportFormats.DesignMd);
        md.Should().Contain("## 品牌与资产");
        md.Should().Contain("字体与许可证");
        md.Should().Contain("系统提供，不随产物分发", "字体行要区分自托管与系统栈，否则合规答不上");
        md.Should().Contain("`/overview`", "起手屏连同路由进契约，前端才知道要建哪几页");
    }

    [Fact]
    public void 整包导出体积与耗时实测_不得是空壳也不得失控()
    {
        // T302 / 规格 U1 一直写"未实测"。这里给出可重复的真数字：
        // 走服务层而不是 HTTP —— 本用例集不依赖浏览器侧那条链路
        // （整包在浏览器侧至今取不到 zip，症状记在 TODO：`Failed to fetch` / `download.path: canceled`，根因未定位）。
        var sw = Stopwatch.StartNew();
        var file = _export.Produce(_projectId, ExportFormats.Bundle, null);
        sw.Stop();

        using var archive = new ZipArchive(new MemoryStream(file.Bytes), ZipArchiveMode.Read);
        var uncompressed = archive.Entries.Sum(e => e.Length);
        Console.WriteLine($"[T302] bundle={file.Bytes.Length / 1024}KiB 解压后={uncompressed / 1024}KiB " +
                          $"条目={archive.Entries.Count} 耗时={sw.ElapsedMilliseconds}ms 令牌数={_tokens.LoadGraph(_projectId, null, "shared").All().Count()}");

        file.Bytes.Length.Should().BeGreaterThan(10_000, "整包不该是空壳");
        archive.Entries.Count.Should().BeGreaterThanOrEqualTo(18);
        file.Bytes.Length.Should().BeLessThan(5 * 1024 * 1024, "整包超 5MiB 说明投影在塞冗余，需要裁剪/分页策略");
        sw.ElapsedMilliseconds.Should().BeLessThan(60_000, "整包超 1 分钟会被代理掐断，用户拿到的是断链而不是文件");
    }

    [Fact]
    public void 未知格式明确报错并列出可用值()
    {
        var act = () => _export.Produce(_projectId, "stylus", null);
        act.Should().Throw<ArgumentException>().WithMessage("*未知导出格式*");
    }
}

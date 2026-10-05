using System.Globalization;
using System.Reflection;
using ForgeSelf.Api.Plugins.DesignSystem.Agent;
using System.Text.Json;
using ForgeSelf.Api.Plugins.DesignSystem.Services;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>
/// M3 风格轴的**行为**契约（AC3/AC5/AC6 + §A2 白名单 + §A3 公式核算）。
///
/// 每条轴都要回答两个问题，缺一不可：
/// ① 它真的改了产物吗（白名单内 ≥ 规定条数变化）；
/// ② 它有没有改到别处去（白名单外逐条不变）。
/// 只断言"产物通过了审计"证明不了新轴在起作用（自查表 #24），所以这里全部按逐条 diff 判。
/// </summary>
public class StyleAxisTests
{
    /// <summary>基础请求：四个主题全覆盖，参数刻意与 8 个预设都不同，避免与黄金基线撞车</summary>
    static GenerationRequest Base() => new()
    {
        Hue = 220,
        Chroma = 0.13,
        Density = "default",
        TypeRatio = 1.2,
        TypeBasePx = 15,
        RadiusBase = 6,
        MotionScale = 1,
        BrandName = "演示",
        Industry = "general",
        Themes = ["light", "dark", "high-contrast", "compact"],
    };

    static GenerationRequest With(String axis, Object? value)
    {
        var r = Base();
        Apply(r, axis, value);
        return r;
    }

    /// <summary>把一条轴的值写进**任意**请求（V6 要在多组不同输入上重复同一判据，故从 `With` 里抽出来）</summary>
    static void Apply(GenerationRequest r, String axis, Object? value)
    {
        switch (axis)
        {
            case StyleAxes.ShadowStyle: r.ShadowStyle = (String?)value; break;
            case StyleAxes.ShadowStrength: r.ShadowStrength = (Double?)value; break;
            case StyleAxes.BorderStrength: r.BorderStrength = (String?)value; break;
            case StyleAxes.NeutralTemp: r.NeutralTemp = (String?)value; break;
            case StyleAxes.FontPairing: r.FontPairing = (String?)value; break;
            case StyleAxes.RadiusStyle: r.RadiusStyle = (String?)value; break;
            case StyleAxes.AccentStrategy: r.AccentStrategy = (String?)value; break;
            default: throw new ArgumentException($"测试不知道轴 {axis}");
        }
    }

    /// <summary>
    /// 两次生成之间**变了哪些行**（键 = `层|路径`）。
    /// 只取键、不取值：值本来就该随输入变，"改的是同一批路径"才是这条轴的身份。
    /// `Line()` 不含 `GeneratorSeed`，所以换输入带来的种子差异不会被算成轴的改动。
    /// </summary>
    static SortedSet<String> ChangedPaths(GenerationRequest a, GenerationRequest b)
    {
        var x = Flatten(DesignGenerator.Generate(a));
        var y = Flatten(DesignGenerator.Generate(b));
        var changed = new SortedSet<String>(StringComparer.Ordinal);
        foreach (var (key, line) in y)
            if (!x.TryGetValue(key, out var before) || before != line) changed.Add(key);
        foreach (var key in x.Keys.Where(k => !y.ContainsKey(k))) changed.Add(key);
        return changed;
    }

    /// <summary>一行的可比字段（§A2：不含 `GeneratorSeed`，也不含 Extensions 里的 seed 字样）</summary>
    static String Line(TokenPatch p) => $"{p.Tier}|{p.Type}|{p.Value}|{p.AliasPath}|{p.ValueJson}";

    /// <summary>把一次生成结果摊成 `层|路径 → 可比字段`（层 = shared 或主题码；同一路径在不同主题层是不同行）</summary>
    static Dictionary<String, String> Flatten(GenerationResult r)
    {
        var map = new Dictionary<String, String>(StringComparer.Ordinal);
        foreach (var p in r.Shared) map[$"shared|{p.Path}"] = Line(p);
        foreach (var (theme, patches) in r.Themed)
            foreach (var p in patches) map[$"{theme}|{p.Path}"] = Line(p);
        return map;
    }

    /// <summary>
    /// 逐条比对默认产物与非默认产物：返回 `不在白名单里却变了的路径`（含新增/消失的行）。
    /// 白名单判定用 `layer|path`，因为 `shadow.elevation-1` 在 light 与 dark 两层里各有一行。
    /// </summary>
    static List<String> OutOfWhitelist(GenerationRequest req, Func<String, String, Boolean> allowed)
    {
        var a = Flatten(DesignGenerator.Generate(Base()));
        var b = Flatten(DesignGenerator.Generate(req));
        var bad = new List<String>();
        foreach (var (key, line) in b)
        {
            var layer = key[..key.IndexOf('|')];
            var path = key[(key.IndexOf('|') + 1)..];
            var changed = !a.TryGetValue(key, out var before) || before != line;
            if (changed && !allowed(layer, path)) bad.Add($"{key}（变了但不在白名单内）");
        }
        foreach (var key in a.Keys.Where(k => !b.ContainsKey(k)))
            bad.Add($"{key}（该行消失了）");
        return bad;
    }

    static IEnumerable<String> NonDefaultValues(String axis) =>
        axis == StyleAxes.ShadowStrength
            ? []
            : StyleAxes.ValuesOf(axis)!.Skip(1);

    // ---------------- AC3：白名单 + 必须改动 ----------------

    [Theory]
    [InlineData("crisp")]
    [InlineData("flat")]
    [InlineData("layered")]
    public void AC3_阴影风格_只改shadow层_且至少三条elevation真变(string style)
    {
        var req = With(StyleAxes.ShadowStyle, style);
        OutOfWhitelist(req, (_, path) => path.StartsWith("shadow.", StringComparison.Ordinal))
            .Should().BeEmpty($"阴影风格 {style} 只允许改 shadow.*");

        var changed = ChangedKeys(Base(), req).Where(k => k.Contains("|shadow.elevation-", StringComparison.Ordinal)).ToList();
        changed.Should().HaveCountGreaterThanOrEqualTo(3, $"阴影风格 {style} 必须真的改掉 ≥3 级 elevation");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.6)]
    [InlineData(2)]
    public void AC3_阴影强度_只改shadow层_且至少三条elevation真变(double strength)
    {
        var req = With(StyleAxes.ShadowStrength, strength);
        OutOfWhitelist(req, (_, path) => path.StartsWith("shadow.", StringComparison.Ordinal))
            .Should().BeEmpty("阴影强度只允许改 shadow.*");

        var changed = ChangedKeys(Base(), req).Where(k => k.Contains("|shadow.elevation-", StringComparison.Ordinal)).ToList();
        changed.Should().HaveCountGreaterThanOrEqualTo(3, $"阴影强度 {strength} 必须真的改掉 ≥3 级 elevation");
    }

    [Fact]
    public void AC3_描边加粗_只改两条描边且两条都变()
    {
        var req = With(StyleAxes.BorderStrength, "bold");
        OutOfWhitelist(req, (_, path) => path is "border.hairline" or "border.thick")
            .Should().BeEmpty("描边强度只允许改 border.hairline / border.thick（component.focus.* 是独立字面宽，不该被带走）");

        var rows = Flatten(DesignGenerator.Generate(req));
        rows["shared|border.hairline"].Should().Contain("|2px|");
        rows["shared|border.thick"].Should().Contain("|3px|");
    }

    [Theory]
    [InlineData("cool")]
    [InlineData("warm")]
    [InlineData("pure")]
    public void AC3_中性色温_只改中性阶与语义别名_且至少六条hex真变(string temp)
    {
        var req = With(StyleAxes.NeutralTemp, temp);
        OutOfWhitelist(req, (_, path) =>
            path.StartsWith("color.neutral.", StringComparison.Ordinal) ||
            path.StartsWith("semantic.", StringComparison.Ordinal))
            .Should().BeEmpty($"中性色温 {temp} 只允许改 color.neutral.* 与选中它们的语义别名");

        var changed = ChangedKeys(Base(), req).Where(k => k.Contains("|color.neutral.", StringComparison.Ordinal)).ToList();
        changed.Should().HaveCountGreaterThanOrEqualTo(6, $"中性色温 {temp} 必须改掉 ≥6 条中性阶");

        // pure 的判据要更硬：彩度 0 时中性阶必须真的是无彩灰，不能只是"换了个色相"
        if (temp == "pure")
            foreach (var row in DesignGenerator.Generate(req).Shared.Where(p => p.Path.StartsWith("color.neutral.", StringComparison.Ordinal)))
                RgbSpread(row.Value!).Should().BeLessOrEqualTo(2, $"{row.Path} 声称纯中性，但 R/G/B 差异 {RgbSpread(row.Value!)} 说明它带色");
    }

    [Theory]
    [InlineData("system")]
    [InlineData("humanist")]
    public void AC3_字体搭配_只改字族且字族真变(string pairing)
    {
        var req = With(StyleAxes.FontPairing, pairing);
        OutOfWhitelist(req, (_, path) => path is "font.sans" or "font.mono")
            .Should().BeEmpty($"字体搭配 {pairing} 只允许改 font.sans / font.mono");

        var rows = Flatten(DesignGenerator.Generate(req));
        rows["shared|font.sans"].Should().NotContain(FontStacks.Sans);
        rows["shared|font.mono"].Should().NotContain(FontStacks.Mono);
    }

    [Fact]
    public void AC3_字体搭配editorial_新增font_display并把标题角色指过去()
    {
        var req = With(StyleAxes.FontPairing, "editorial");
        OutOfWhitelist(req, (_, path) =>
            path is "font.sans" or "font.mono" or "font.display" ||
            path.StartsWith("type.", StringComparison.Ordinal))
            .Should().BeEmpty("editorial 只允许改三条字族 + 用展示字族的 type.* 复合令牌");

        var result = DesignGenerator.Generate(req);
        var display = result.Shared.FirstOrDefault(p => p.Path == "font.display");
        display.Should().NotBeNull("editorial 的产物必须有 font.display，否则衬线标题只是说说");
        display!.Value.Should().Contain("Noto Serif SC");

        // sans/mono 在 editorial 下必须仍是那两串现状栈（正文换衬线是可读性事故，不是风格）
        result.Shared.First(p => p.Path == "font.sans").Value.Should().Be(FontStacks.Sans);
        result.Shared.First(p => p.Path == "font.mono").Value.Should().Be(FontStacks.Mono);

        var serifRoles = new[] { "type.display", "type.h1", "type.h2", "type.h3" };
        foreach (var role in serifRoles)
            result.Shared.First(p => p.Path == role).ValueJson.Should().Contain("{font.display}");

        // 其余角色一律不动：小字/正文用衬线不是"editorial 风格"，是缺陷
        foreach (var role in new[] { "type.h4", "type.h5", "type.body", "type.small", "type.caption", "type.overline" })
            result.Shared.First(p => p.Path == role).ValueJson.Should().Contain("{font.sans}");
    }

    [Fact]
    public void AC3_字体搭配modern_与现状逐字相同()
    {
        // "默认值 = 现状"必须逐字钉住，否则 §A4 里"modern 与现状相同"只是一句承诺
        var rows = Flatten(DesignGenerator.Generate(With(StyleAxes.FontPairing, "modern")));
        rows["shared|font.sans"].Should().Contain(FontStacks.Sans);
        rows["shared|font.mono"].Should().Contain(FontStacks.Mono);
        rows.Keys.Should().NotContain("shared|font.display", "modern 不产出展示字族");
        ChangedKeys(Base(), With(StyleAxes.FontPairing, "modern")).Should().BeEmpty();
    }

    [Theory]
    [InlineData("sharp")]
    [InlineData("round")]
    [InlineData("pill")]
    public void AC3_圆角风格_只改组件圆角别名且至少三条真变(string style)
    {
        var req = With(StyleAxes.RadiusStyle, style);
        OutOfWhitelist(req, (_, path) => path.StartsWith("component.", StringComparison.Ordinal) && path.EndsWith(".radius", StringComparison.Ordinal))
            .Should().BeEmpty($"圆角风格 {style} 只允许改 component.*.radius 的别名目标（radius.* 档本身的值属另一轴）");

        ChangedKeys(Base(), req).Should().HaveCountGreaterThanOrEqualTo(3, $"圆角风格 {style} 必须改掉 ≥3 条组件圆角引用");

        // 表里的具体格子逐个核：pill 的按钮必须全是胶囊，sharp 的卡片必须降到 md
        var rows = Flatten(DesignGenerator.Generate(req));
        var expect = style switch
        {
            "sharp" => ("radius.sm", "radius.xs", "radius.md", "radius.xs"),
            "round" => ("radius.lg", "radius.md", "radius.xl", "radius.pill"),
            _ => ("radius.pill", "radius.pill", "radius.xl", "radius.pill"),
        };
        rows["shared|component.button.radius"].Should().EndWith($"|{expect.Item1}|");
        rows["shared|component.button.sm.radius"].Should().EndWith($"|{expect.Item2}|");
        rows["shared|component.card.radius"].Should().EndWith($"|{expect.Item3}|");
        rows["shared|component.badge.radius"].Should().EndWith($"|{expect.Item4}|");
    }

    [Theory]
    [InlineData("analogous")]
    [InlineData("split")]
    [InlineData("triadic")]
    [InlineData("mono")]
    public void AC3_强调色策略_只改强调与信息族且至少六条hex真变(string strategy)
    {
        var req = With(StyleAxes.AccentStrategy, strategy);
        OutOfWhitelist(req, (_, path) =>
            path.StartsWith("color.accent.", StringComparison.Ordinal) ||
            path.StartsWith("color.info.", StringComparison.Ordinal) ||
            path.StartsWith("semantic.", StringComparison.Ordinal))
            .Should().BeEmpty($"强调色策略 {strategy} 只允许改 color.accent.* / color.info.* 与选中它们的语义别名（品牌族不许跟着动）");

        ChangedKeys(Base(), req).Where(k => k.Contains("|color.accent.", StringComparison.Ordinal))
            .Should().HaveCountGreaterThanOrEqualTo(6, $"强调色策略 {strategy} 必须改掉 ≥6 条强调阶");
    }

    [Fact]
    public void AC3_强调色complement_等价默认偏移()
    {
        // §A1：complement ≡ 168°。它排第一 = 默认，产物与缺省逐字相同，且种子不带后缀
        var req = With(StyleAxes.AccentStrategy, "complement");
        ChangedKeys(Base(), req).Should().BeEmpty();
        DesignGenerator.Generate(req).Seed.Should().Be(DesignGenerator.Generate(Base()).Seed);
    }

    [Fact]
    public void AC3_强调色策略优先于偏移数值()
    {
        var req = Base();
        req.AccentHueOffset = 90;               // 显式给一个非默认偏移
        req.AccentStrategy = "mono";            // 再给策略：策略必须赢（0°），不能两个都生效或取数值
        var rows = Flatten(DesignGenerator.Generate(req));

        var brandHue = HueOf(DesignGenerator.Generate(Base()), "color.brand.500");
        var accentHue = HueOf(DesignGenerator.Generate(Base()), "color.accent.500");
        var monoAccent = HueOf(DesignGenerator.Generate(req), "color.accent.500");
        monoAccent.Should().BeApproximately(brandHue, 2, "mono 策略下强调色相必须回到品牌色相，而不是 hue+90");
        monoAccent.Should().NotBeApproximately(accentHue, 2);
    }

    // ---------------- §A3 公式核算（V6 要求人工核一条） ----------------

    [Fact]
    public void AC3_crisp阴影公式_按L乘1_8且只有一层()
    {
        var result = DesignGenerator.Generate(With(StyleAxes.ShadowStyle, "crisp"));
        var light3 = result.Shadows["light"].First(s => s.Name == "elevation-3");

        light3.Layers.Should().HaveCount(1, "crisp 是锐利单层，叠第二层就退回 soft 的糊");
        var l = light3.Layers[0];
        l.OffsetY.Should().Be(3, "y = level × 密度系数，default 密度系数 1");
        l.Blur.Should().Be(7.9, "blur = round(3^1.35 × 1.8 × 1, 1) = 7.9（人工核算：3^1.35≈4.4066）");
        l.Spread.Should().Be(0);
        l.Alpha.Should().BeApproximately(0.22, 1e-9, "alpha = clamp(1 × (0.07 + 0.05×3), 0, 0.5) = 0.22");
        l.ColorValue.Should().Be("#0f172a");
        light3.Note.Should().Be("锐利单层");

        // 暗色：先插 1px 白 inset 高光，外层色换成纯黑（与现状同形，不是另造一套）
        var dark3 = result.Shadows["dark"].First(s => s.Name == "elevation-3");
        dark3.Layers.Should().HaveCount(2);
        dark3.Layers[0].IsInset.Should().BeTrue();
        dark3.Layers[0].ColorValue.Should().Be("#ffffff");
        dark3.Layers[1].ColorValue.Should().Be("#000000");
    }

    [Fact]
    public void AC3_flat阴影公式_环线无投影且暗色用白环()
    {
        var result = DesignGenerator.Generate(With(StyleAxes.ShadowStyle, "flat"));
        var light4 = result.Shadows["light"].First(s => s.Name == "elevation-4");
        var l = light4.Layers.Single();
        l.OffsetY.Should().Be(0);
        l.Blur.Should().Be(0, "flat 是描边环，有任何模糊就成投影了");
        l.Spread.Should().Be(2, "level>3 的环线加粗到 2px");
        l.Alpha.Should().BeApproximately(0.16, 1e-9, "alpha = clamp(1 × (0.08 + 0.02×4), 0, 0.3) = 0.16");

        var dark2 = result.Shadows["dark"].First(s => s.Name == "elevation-2");
        dark2.Layers.Should().HaveCount(1, "flat 在暗色下不加 inset 高光，否则环线与高光叠成脏边");
        dark2.Layers[0].ColorValue.Should().Be("#ffffff");
        dark2.Layers[0].IsInset.Should().BeFalse();
    }

    [Fact]
    public void AC3_layered阴影_保留soft全部层再叠环境光()
    {
        var soft = DesignGenerator.Generate(With(StyleAxes.ShadowStyle, "soft"));
        var layered = DesignGenerator.Generate(With(StyleAxes.ShadowStyle, "layered"));
        foreach (var level in new[] { 1, 2, 3, 4, 5 })
        {
            var a = soft.Shadows["light"].First(s => s.Name == $"elevation-{level}");
            var b = layered.Shadows["light"].First(s => s.Name == $"elevation-{level}");
            b.Layers.Should().HaveCountGreaterOrEqualTo(a.Layers.Count);
            b.Layers.Take(a.Layers.Count).Select(Shape).Should().Equal(a.Layers.Select(Shape),
                $"layered 必须以 soft 的层为基础（level={level}）");
            if (level >= 2)
                b.Layers.Count.Should().Be(a.Layers.Count + 1, $"level={level} 应多一层环境光");
            else
                b.Layers.Count.Should().Be(a.Layers.Count, "level=1 不叠环境光（贴地的东西没有空气感）");
        }
    }

    static String Shape(ShadowLayerInput l) => $"{l.OffsetX}|{l.OffsetY}|{l.Blur}|{l.Spread}|{l.ColorValue}";

    // ---------------- V6 反作弊：轴的行为不许依赖"测试里那一个基础请求" ----------------

    /// <summary>
    /// 本文件其余 AC3 用例全部走同一个 `Base()`（hue 220 / 无 brief / industry general）。
    /// 如果某条轴的实现偷偷吃那组参数（例如只在"无 brief 的 hue 路径"上生效、或换品牌色族后多改了别处），
    /// 现有用例照样绿——这正是 06 预注册 V6 要抽的东西。这里对三条轴各跑三组**新输入**，
    /// 断言"这条轴改动的**路径集合**与在 `Base()` 下完全相同"且非空：
    /// **多了**＝换输入就改到白名单外，**少了**＝换输入这条轴就不生效。
    /// 不另立第二份白名单定义（"这些路径本身合法"由 AC3 各条钉住），两处共用同一批事实。
    /// 三组新输入都用同一套 `Themes`，否则层集合不同会让比较失去意义。
    /// </summary>
    static IEnumerable<(String Label, Func<GenerationRequest> Make)> FreshRequests()
    {
        yield return ("带 brief 的电商请求（走 seedColor 派生色，不给 hue）", () =>
        {
            var r = Base();
            r.Hue = null;
            r.Chroma = null;
            r.Brief = "跨境电商后台管理系统，需要促销感和清晰的转化路径";
            r.Industry = "ecommerce";
            r.BrandName = "海集";
            return r;
        });
        yield return ("暖色低彩度金融请求（hue 30 + finance + 大字阶）", () =>
        {
            var r = Base();
            r.Hue = 30;
            r.Chroma = 0.06;
            r.Industry = "finance";
            r.TypeRatio = 1.33;
            r.TypeBasePx = 17;
            return r;
        });
        yield return ("显式 seedColor + compact 密度 + 大圆角基", () =>
        {
            var r = Base();
            r.Hue = null;
            r.SeedColor = "#1f6f5c";
            r.Density = "compact";
            r.RadiusBase = 12;
            r.MotionScale = 0.6;
            return r;
        });
    }

    [Theory]
    [InlineData("shadowStyle", "crisp")]
    [InlineData("shadowStyle", "flat")]
    [InlineData("radiusStyle", "pill")]
    [InlineData("fontPairing", "editorial")]
    public void V6_轴改动的路径集合不随输入改变(string axis, string value)
    {
        var ran = 0;
        var reference = ChangedPaths(Base(), With(axis, value));
        reference.Should().NotBeEmpty($"轴 {axis} 在基准请求下本来就该改产物（空集会让本用例变成永真）");

        foreach (var (label, make) in FreshRequests())
        {
            ran++;
            var fresh = make();
            var withAxis = make();
            Apply(withAxis, axis, value);

            var changed = ChangedPaths(fresh, withAxis);
            changed.Should().NotBeEmpty($"轴 {axis}={value} 在「{label}」下一个变量都没改 = 这条轴只在测试那组参数上生效");
            changed.Should().BeEquivalentTo(reference,
                $"轴 {axis}={value} 在「{label}」下改动的路径集合必须与基准一致" +
                $"（多了＝改到白名单外，少了＝换输入不生效）");
        }

        ran.Should().Be(3, "三组新输入必须都被跑到；循环体一旦空转，本用例就退化成只在 Base() 上判过一次");
    }

    /// <summary>
    /// V6 的另一半：**公式的结构**在换输入后必须仍然成立。
    /// 精确算式（`blur = round(L^1.35 × 1.8 × d)`、alpha 钳位等）已由上面三条在 `Base()` 上人工核算钉住；
    /// 这里**故意不重算数**（那等于把公式抄第二遍，抄的那份会漂），只钉"与数值无关的结构"：
    /// crisp=亮色单层 / 暗色"白 inset + 外层"两层；flat=单层、零偏移零模糊的环线，暗色不加高光；
    /// layered=以 soft 的层为基础、level≥2 多一层环境光。
    /// 三组新输入各钉一次 ⇒ "只在测试那组参数上才成立"的分支会被抓出来。
    /// </summary>
    [Theory]
    [InlineData("crisp")]
    [InlineData("flat")]
    [InlineData("layered")]
    public void V6_阴影公式的结构不随输入改变(string style)
    {
        var ran = 0;
        foreach (var (label, make) in FreshRequests())
        {
            ran++;
            var on = make();
            Apply(on, StyleAxes.ShadowStyle, style);
            var gen = DesignGenerator.Generate(on);
            var light3 = gen.Shadows["light"].First(s => s.Name == "elevation-3");
            var light4 = gen.Shadows["light"].First(s => s.Name == "elevation-4");
            var dark3 = gen.Shadows["dark"].First(s => s.Name == "elevation-3");

            switch (style)
            {
                case "crisp":
                    light3.Layers.Should().HaveCount(1, $"「{label}」下 crisp 仍是单层");
                    dark3.Layers.Should().HaveCount(2, $"「{label}」下 crisp 的暗色仍是「白 inset 高光 + 外层」两层");
                    dark3.Layers[0].IsInset.Should().BeTrue();
                    dark3.Layers[0].ColorValue.Should().Be("#ffffff");
                    dark3.Layers[1].ColorValue.Should().Be("#000000");
                    break;
                case "flat":
                    foreach (var s in new[] { light3, light4 })
                    {
                        var only = s.Layers.Single();
                        only.OffsetY.Should().Be(0, $"「{label}」下 flat 是描边环，有偏移就不是环");
                        only.Blur.Should().Be(0, $"「{label}」下 flat 一旦有模糊就退回投影");
                    }

                    dark3.Layers.Should().HaveCount(1, $"「{label}」下 flat 暗色不加 inset 高光（会与环线叠成脏边）");
                    dark3.Layers[0].IsInset.Should().BeFalse();
                    dark3.Layers[0].ColorValue.Should().Be("#ffffff");
                    break;
                default:
                    var soft = DesignGenerator.Generate(make());
                    foreach (var level in new[] { 1, 2, 3, 4, 5 })
                    {
                        var a = soft.Shadows["light"].First(s => s.Name == $"elevation-{level}");
                        var b = gen.Shadows["light"].First(s => s.Name == $"elevation-{level}");
                        b.Layers.Take(a.Layers.Count).Select(Shape).Should().Equal(a.Layers.Select(Shape),
                            $"「{label}」下 layered 必须以 soft 的层为基础（level={level}）");
                        b.Layers.Count.Should().Be(level >= 2 ? a.Layers.Count + 1 : a.Layers.Count,
                            $"「{label}」下 level={level} 的环境光叠层数不对（level=1 贴地，不叠）");
                    }

                    break;
            }
        }

        ran.Should().Be(3, "三组新输入必须都被跑到；循环体一旦空转，本用例就退化成「只在一条输入上判结构」");
    }

    // ---------------- AC5：词表、校验、夹取 ----------------

    [Fact]
    public void AC5_词表_七条轴且首项是默认()
    {
        var vocab = StyleAxes.Vocabulary();
        vocab.Should().HaveCount(7);
        vocab.Select(v => (String)v["axis"]!).Distinct(StringComparer.Ordinal).Should().HaveCount(7);

        foreach (var entry in vocab)
        {
            var axis = (String)entry["axis"]!;
            var kind = (String)entry["kind"]!;
            ((String?)entry["label"]).Should().NotBeNullOrEmpty($"{axis} 要给界面一个中文名，否则控件只能显示英文轴名");
            if (kind == "enum")
            {
                var values = (IReadOnlyList<String>)entry["values"]!;
                values.Count.Should().BeGreaterThanOrEqualTo(2, $"{axis} 只有一个取值 = 假轴");
                values[0].Should().Be((String)entry["default"]!, "§A1 约定：枚举轴首项 = 默认值");
            }
            else
            {
                entry.Should().ContainKeys("min", "max", "step");
                entry["default"].Should().Be(StyleAxes.ShadowStrengthDefault);
            }
        }
    }

    [Fact]
    public void AC5_词表的field必须是GenerationRequest的真实可空属性()
    {
        // 轴名 → 请求字段名这一层映射最容易被写成"文档里对、代码里错"，用反射当场核对
        foreach (var entry in StyleAxes.Vocabulary())
        {
            var field = (String)entry["field"]!;
            // meta 出的是 JSON 字段名（camelCase），属性是 PascalCase：按名不区分大小写找到，才算"词表说的是真字段"
            var prop = typeof(GenerationRequest).GetProperty(field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            prop.Should().NotBeNull($"meta.styleAxes 声明的字段 {field} 在 GenerationRequest 上不存在（界面按它拼请求体会静默丢参数）");
            // 参考类型的可空性是注解（运行时 String? == String），必须用 NullabilityInfoContext 才验得到
            new NullabilityInfoContext().Create(prop!).WriteState.Should().Be(NullabilityState.Nullable,
                $"{field} 必须是可空属性：界面与工具都按『不传 = 默认』发参数，非空属性会把缺省变成空串或 0");
            prop!.PropertyType.Should().Be((String)entry["kind"]! == "enum" ? typeof(String) : typeof(Double?),
                $"{field} 的类型与该轴的 kind 不符");
        }
    }

    [Theory]
    [InlineData(StyleAxes.ShadowStyle, "hard")]
    [InlineData(StyleAxes.BorderStrength, "extra-bold")]
    [InlineData(StyleAxes.NeutralTemp, "teal")]
    [InlineData(StyleAxes.FontPairing, "serif")]
    [InlineData(StyleAxes.RadiusStyle, "square")]
    [InlineData(StyleAxes.AccentStrategy, "complementary")]
    public void AC5_非法取值_抛带词表的ArgumentException(string axis, string value)
    {
        var act = () => DesignGenerator.Generate(With(axis, value));
        var ex = act.Should().Throw<ArgumentException>().Which;
        ex.Message.Should().Contain($"风格轴 {axis} 的取值 {value} 不在 [",
            "400 文案必须同时含轴名、坏值和合法取值清单，否则调用方只能猜");
        ex.Message.Should().Contain(string.Join(", ", StyleAxes.ValuesOf(axis)!));
    }

    [Theory]
    [InlineData("CRISP")]
    [InlineData(" crisp")]
    [InlineData("crisp ")]
    public void AC5_枚举取值大小写与空格宽容但归一为词表值(string raw)
    {
        var rows = Flatten(DesignGenerator.Generate(With(StyleAxes.ShadowStyle, raw)));
        var expected = Flatten(DesignGenerator.Generate(With(StyleAxes.ShadowStyle, "crisp")));
        rows.Should().Equal(expected, $"{raw} 该归一成 crisp，不该当成另一套取值");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void AC5_阴影强度越界_夹取且写进Notes(double raw)
    {
        var result = DesignGenerator.Generate(With(StyleAxes.ShadowStrength, raw));
        var expected = Math.Clamp(raw, StyleAxes.ShadowStrengthMin, StyleAxes.ShadowStrengthMax);

        result.Notes.Should().ContainSingle(n => n.Contains("已夹取"), "夹取必须可见：静默夹取等于告诉用户'你要的强度生效了'");
        result.Notes.Should().Contain(n => n.Contains($"风格轴：阴影强度") || n.Contains("已夹取"));

        // 夹取后的产物必须等于直接用合法值生成的产物（不是"提示一下但算了别的"）
        Flatten(result).Should().Equal(Flatten(DesignGenerator.Generate(With(StyleAxes.ShadowStrength, expected))));
    }

    [Fact]
    public void AC5_全默认时Notes里不出现风格轴行()
    {
        DesignGenerator.Generate(Base()).Notes.Should().NotContain(n => n.Contains("风格轴"),
            "没传轴就别在 Notes 里刷存在感：Notes 是给用户看的，噪声会让人以为改过东西");
    }

    // ---------------- AC6：复现与种子后缀 ----------------

    [Fact]
    public void AC6_非默认取值才带种子后缀()
    {
        DesignGenerator.Generate(Base()).Seed.Should().NotContain(";");

        foreach (var axis in StyleAxes.Order)
        {
            if (axis == StyleAxes.ShadowStrength)
            {
                var s = DesignGenerator.Generate(With(axis, 1.4)).Seed;
                s.Should().Contain(";st=1.4", "强度也进种子后缀，否则两套强度不同的系统看着同一个种子");
                s.Should().MatchRegex(";st=[0-9.]+$", "数值轴按 §A10 排在最后");
                continue;
            }
            foreach (var value in NonDefaultValues(axis))
            {
                var seed = DesignGenerator.Generate(With(axis, value)).Seed;
                // 只设一条轴时后缀就是末尾那一段 `;短键=取值`，取值名必须原样出现
                seed.Should().EndWith($"={value}", $"轴 {axis} 取值 {value} 的种子后缀末尾必须是取值名，实得 {seed}");
                seed.Should().NotBe(DesignGenerator.Generate(Base()).Seed, $"{axis}={value} 换了取值却同一个种子 = 参数没进种子，复现标识是假的");
            }
        }
    }

    [Fact]
    public void AC6_种子后缀顺序按轴序且总长不超列宽()
    {
        // 七个轴全非默认：这是最长的一串。GeneratorSeed 列宽 100 且改既有列属 Forbidden，超了就会写进窄列
        var req = Base();
        req.ShadowStyle = "layered";
        req.BorderStrength = "bold";
        req.NeutralTemp = "cool";
        req.FontPairing = "editorial";
        req.RadiusStyle = "round";
        req.AccentStrategy = "analogous";
        req.ShadowStrength = 1.75;

        var seed = DesignGenerator.Generate(req).Seed;
        seed.Length.Should().BeLessThanOrEqualTo(100, $"种子要进 DesignToken.GeneratorSeed（100 列宽），实测 {seed.Length}：{seed}");

        // 16 位哈希部分不随轴变（轴是"追加"上去的，不是混进哈希的），后缀部分按 §A10 的轴序逐轴带取值名
        var suffix = seed[16..];
        suffix.Should().Be(";sh=layered;bo=bold;ne=cool;fo=editorial;ra=round;ac=analogous;st=1.75");
        seed[..16].Should().Be(DesignGenerator.Generate(Base()).Seed);
    }

    [Fact]
    public void AC6_同请求两次_种子与产物逐字节相同()
    {
        var req = With(StyleAxes.ShadowStyle, "crisp");
        var a = DesignGenerator.Generate(req);
        var b = DesignGenerator.Generate(PresetRecommender.Copy(req));
        b.Seed.Should().Be(a.Seed);
        Flatten(b).Should().Equal(Flatten(a));
    }

    // ---------------- AC9（轴部分）：工具 schema 必须由同一份词表生成 ----------------

    [Theory]
    [InlineData(DesignToolIndex.Create)]
    [InlineData(DesignToolIndex.Edit)]
    public void AC9_工具schema的轴属性与StyleAxes同源(string toolName)
    {
        // 词表第三份真相最容易长在"工具 schema"里：手抄一份 enum，后端改了取值 agent 还在传旧的。
        // 这里不比对"看起来像"，而是把 schema 解析出来逐轴逐值核对。
        var schema = JsonDocument.Parse(DesignToolIndex.Of(toolName).Schema).RootElement
            .GetProperty("properties");
        var missing = new List<String>();
        foreach (var entry in StyleAxes.Vocabulary())
        {
            var field = (String)entry["field"]!;
            if (!schema.TryGetProperty(field, out var prop))
            {
                missing.Add(field);
                continue;
            }
            if ((String)entry["kind"]! == "enum")
            {
                var values = prop.GetProperty("enum").EnumerateArray().Select(e => e.GetString()!).ToList();
                values.Should().Equal(StyleAxes.ValuesOf((String)entry["axis"]!), $"{toolName}.{field} 的 enum 必须逐值等于 StyleAxes");
                prop.GetProperty("default").GetString().Should().Be((String)entry["default"]!, $"{toolName}.{field} 的默认值要与词表一致");
            }
            else
            {
                prop.GetProperty("type").GetString().Should().Be("number");
                prop.GetProperty("minimum").GetDouble().Should().Be((Double)entry["min"]!);
                prop.GetProperty("maximum").GetDouble().Should().Be((Double)entry["max"]!);
            }
        }
        missing.Should().BeEmpty($"{toolName} 的 schema 缺这些风格轴属性：{String.Join(", ", missing)}");
    }

    [Fact]
    public void AC9_agent读到的预设request带出轴取值()
    {
        // design_presets 出参的 request 是 agent 选衣服的根据；轴不带出，agent 就看不出这十件衣服的区别
        var withAxes = StylePresets.Find("editorial-serif")!.Request;
        var dto = DesignGenerator.Generate(withAxes);
        dto.Notes.Should().Contain(n => n.Contains("字体搭配"), "带轴的预设要在 Notes 里看得见用了哪条轴");
    }
    // ---------------- 辅助 ----------------

    /// <summary>默认产物与给定请求的产物之间，可比字段变了/多了/少了的键</summary>
    static List<String> ChangedKeys(GenerationRequest baseline, GenerationRequest req)
    {
        var a = Flatten(DesignGenerator.Generate(baseline));
        var b = Flatten(DesignGenerator.Generate(req));
        var keys = a.Keys.Union(b.Keys).ToList();
        return keys.Where(k => a.TryGetValue(k, out var x) ? !b.TryGetValue(k, out var y) || x != y : true)
            .OrderBy(k => k, StringComparer.Ordinal).ToList();
    }

    static Double RgbSpread(String hex)
    {
        var rgb = Oklch.TryParseRgb8(hex) ?? throw new InvalidOperationException($"不是 hex：{hex}");
        return Math.Max(rgb.R, Math.Max(rgb.G, rgb.B)) - Math.Min(rgb.R, Math.Min(rgb.G, rgb.B));
    }

    static Double HueOf(GenerationResult r, String path)
    {
        var row = r.Shared.First(p => p.Path == path);
        // 组件数组里存的是格式化后的字符串（Num() 产出 0.#### 文本），不是 JSON number
        var json = JsonDocument.Parse(row.ValueJson!).RootElement.GetProperty("components");
        var h = Double.Parse(json[2].GetString()!, CultureInfo.InvariantCulture);
        return ((h % 360) + 360) % 360;
    }
}

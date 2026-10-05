using ForgeSelf.Api.Plugins.DesignSystem.Services;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>
/// M3 AC7（预设部分）：目录 13 个、原 8 个逐字段不变、新 5 个与 §P 一致、轴覆盖矩阵成立。
///
/// 覆盖矩阵这条最要紧：**每个非默认轴取值必须真的被某个预设用到**。
/// 否则轴只是 API 上的装饰——用户翻衣柜看不到"环线阴影"长什么样，就等于没有。
/// 预设"生成→审计 0 critical"的部分需要落库，在 <see cref="StyleAxisAuditTests"/>。
/// </summary>
public class StylePresetAxisTests
{
    /// <summary>原 8 个预设的请求字段快照（M3 开工基线；改任何一个数字都得先过黄金回归并登记偏差）</summary>
    static readonly (String Id, Double? Hue, Double? Chroma, String Density, Double? Ratio, Double? BasePx, Double? Radius, Double? Motion, String Industry)[] Baseline8 =
    [
        ("admin-calm", 255, 0.15, "default", 1.2, 14, 6, 0.9, "general"),
        ("workbench-focus", 275, 0.09, "compact", 1.15, 14, 4, 0.8, "devtools"),
        ("finance-trust", 240, 0.12, "default", 1.15, 14, 4, 0.8, "finance"),
        ("healthcare-gentle", 190, 0.11, "default", 1.25, 16, 10, 1.0, "healthcare"),
        ("commerce-vivid", 35, 0.19, "default", 1.25, 16, 8, 1.0, "commerce"),
        ("media-bold", 350, 0.22, "default", 1.4, 16, 4, 1.2, "media"),
        ("education-friendly", 150, 0.17, "comfortable", 1.3, 16, 12, 1.15, "education"),
        ("mobile-fresh", 205, 0.14, "comfortable", 1.25, 16, 12, 1.0, "general"),
    ];

    static readonly String[] NewPresetIds = ["editorial-serif", "flat-minimal", "warm-craft", "tech-crisp", "kids-playful"];

    [Fact]
    public void AC7_目录恰好13个且id唯一_原8个排在前面()
    {
        StylePresets.All.Should().HaveCount(13);
        StylePresets.All.Select(p => p.Id).Distinct(StringComparer.Ordinal).Should().HaveCount(13);
        StylePresets.All.Take(8).Select(p => p.Id).Should().Equal(Baseline8.Select(b => b.Id),
            "原 8 个的顺序与 id 是外部契约（展厅衣柜、深链 ?outfit= 都按它排）");
        StylePresets.All.Skip(8).Select(p => p.Id).Should().Equal(NewPresetIds);
    }

    [Fact]
    public void AC7_原8个预设_请求字段与开工基线逐字段相等()
    {
        foreach (var (id, hue, chroma, density, ratio, basePx, radius, motion, industry) in Baseline8)
        {
            var p = StylePresets.Find(id)!;
            p.Request.Hue.Should().Be(hue, $"{id}.hue");
            p.Request.Chroma.Should().Be(chroma, $"{id}.chroma");
            p.Request.Density.Should().Be(density, $"{id}.density");
            p.Request.TypeRatio.Should().Be(ratio, $"{id}.typeRatio");
            p.Request.TypeBasePx.Should().Be(basePx, $"{id}.typeBasePx");
            p.Request.RadiusBase.Should().Be(radius, $"{id}.radiusBase");
            p.Request.MotionScale.Should().Be(motion, $"{id}.motionScale");
            p.Request.Industry.Should().Be(industry, $"{id}.industry");
            p.Request.Themes.Should().Equal(["light", "dark", "high-contrast", "compact"], $"{id}.themes");
        }
    }

    [Fact]
    public void AC7_原8个预设_七条轴一律留空_即走默认路径()
    {
        // 这条是"默认逐字节兼容"在目录层的防线：预设里一旦有人顺手填了轴，产物就不再是存量项目认识的那套
        foreach (var p in StylePresets.All.Take(8))
        {
            p.Request.ShadowStyle.Should().BeNull($"{p.Id} 不该带阴影风格轴");
            p.Request.ShadowStrength.Should().BeNull($"{p.Id} 不该带阴影强度");
            p.Request.BorderStrength.Should().BeNull($"{p.Id} 不该带描边强度");
            p.Request.NeutralTemp.Should().BeNull($"{p.Id} 不该带中性色温");
            p.Request.FontPairing.Should().BeNull($"{p.Id} 不该带字体搭配");
            p.Request.RadiusStyle.Should().BeNull($"{p.Id} 不该带圆角风格");
            p.Request.AccentStrategy.Should().BeNull($"{p.Id} 不该带强调色策略");
        }
    }

    /// <summary>
    /// §P 的数值表。唯一一处与规划表不同：**flat-minimal 的彩度 0.05 → 0.08**（偏差登记 03-plan）。
    /// 原因是实测出来的：彩度 0.05 时徽标前景（semantic.success）对它自己的 tint 底只有 3.72:1，
    /// 过不了 4.5:1 的文本对比门禁——极低彩度的"成功绿"太浅，垫一层 15% 自己之后与白底几乎分不开。
    /// §P/§D 的微调规则允许 chroma ±0.03 内改，这里取 +0.03 的最小代价解，"极简低彩度"的性格仍在。
    /// </summary>
    [Theory]
    [InlineData("editorial-serif", 25, 0.12, "default", 1.33, 16, 4, 0.9, "media")]
    [InlineData("flat-minimal", 265, 0.08, "compact", 1.15, 14, 3, 0.8, "devtools")]
    [InlineData("warm-craft", 55, 0.14, "comfortable", 1.25, 16, 10, 1.1, "commerce")]
    [InlineData("tech-crisp", 285, 0.2, "default", 1.2, 16, 4, 0.85, "devtools")]
    [InlineData("kids-playful", 330, 0.2, "comfortable", 1.3, 16, 12, 1.2, "education")]
    public void AC7_新5个预设_数值与P表一致且四主题齐备(string id, Double hue, Double chroma, String density,
        Double ratio, Double basePx, Double radius, Double motion, String industry)
    {
        var p = StylePresets.Find(id);
        p.Should().NotBeNull($"§P 约定的预设 {id} 不在目录里");
        var r = p!.Request;
        r.Hue.Should().Be(hue);
        r.Chroma.Should().Be(chroma);
        r.Density.Should().Be(density);
        r.TypeRatio.Should().Be(ratio);
        r.TypeBasePx.Should().Be(basePx);
        r.RadiusBase.Should().Be(radius);
        r.MotionScale.Should().Be(motion);
        r.Industry.Should().Be(industry);
        r.Themes.Should().Equal(["light", "dark", "high-contrast", "compact"]);

        // 契约字段：新预设也要能被推荐打分命中（关键词/适用范围/性格词不能空着当摆设）
        p.Name.Should().NotBeNullOrWhiteSpace();
        p.Tagline.Should().NotBeNullOrWhiteSpace();
        p.Tones.Should().NotBeEmpty();
        p.Kinds.Should().NotBeEmpty();
        p.Industries.Should().NotBeEmpty();
        p.Keywords.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("editorial-serif")]
    [InlineData("flat-minimal")]
    [InlineData("warm-craft")]
    [InlineData("tech-crisp")]
    [InlineData("kids-playful")]
    public void AC7_新预设的轴取值都在词表内且至少两条非默认(string id)
    {
        var r = StylePresets.Find(id)!.Request;
        var used = new List<(String Axis, String Value)>
        {
            (StyleAxes.ShadowStyle, StyleAxes.Effective(StyleAxes.ShadowStyle, r.ShadowStyle)),
            (StyleAxes.BorderStrength, StyleAxes.Effective(StyleAxes.BorderStrength, r.BorderStrength)),
            (StyleAxes.NeutralTemp, StyleAxes.Effective(StyleAxes.NeutralTemp, r.NeutralTemp)),
            (StyleAxes.FontPairing, StyleAxes.Effective(StyleAxes.FontPairing, r.FontPairing)),
            (StyleAxes.RadiusStyle, StyleAxes.Effective(StyleAxes.RadiusStyle, r.RadiusStyle)),
            (StyleAxes.AccentStrategy, StyleAxes.Effective(StyleAxes.AccentStrategy, r.AccentStrategy)),
        };
        used.Should().OnlyContain(x => StyleAxes.ValuesOf(x.Axis)!.Contains(x.Value));
        used.Count(x => !StyleAxes.IsDefault(x.Axis, x.Value)).Should().BeGreaterThanOrEqualTo(2,
            $"{id} 只叠一条轴就失去『这一件衣服明显是另一套系统』的意义");
    }

    [Fact]
    public void AC7_覆盖矩阵_每个非默认轴取值至少被一个预设用到()
    {
        var covered = new HashSet<String>(StringComparer.Ordinal);
        foreach (var p in StylePresets.All)
        {
            var r = p.Request;
            foreach (var (axis, raw) in new[]
                     {
                         (StyleAxes.ShadowStyle, r.ShadowStyle), (StyleAxes.BorderStrength, r.BorderStrength),
                         (StyleAxes.NeutralTemp, r.NeutralTemp), (StyleAxes.FontPairing, r.FontPairing),
                         (StyleAxes.RadiusStyle, r.RadiusStyle), (StyleAxes.AccentStrategy, r.AccentStrategy),
                     })
            {
                var value = StyleAxes.Effective(axis, raw);
                if (!StyleAxes.IsDefault(axis, value)) covered.Add($"{axis}={value}");
            }
            if (!StyleAxes.IsDefault(StyleAxes.ShadowStrength, r.ShadowStrength))
                covered.Add($"{StyleAxes.ShadowStrength}≠1");
        }

        var missing = new List<String>();
        foreach (var axis in StyleAxes.Order)
        {
            if (axis == StyleAxes.ShadowStrength)
            {
                if (!covered.Contains($"{StyleAxes.ShadowStrength}≠1")) missing.Add($"{axis}（没有任何预设改过强度）");
                continue;
            }
            foreach (var value in StyleAxes.ValuesOf(axis)!.Skip(1))
                if (!covered.Contains($"{axis}={value}")) missing.Add($"{axis}={value}");
        }

        missing.Should().BeEmpty("§P 的覆盖矩阵要求每个非默认取值都被用到，缺项：\n" + String.Join("\n", missing));
    }

    [Fact]
    public void AC7_预设目录里新预设的轴取值_必须真改产物而非摆设()
    {
        // 覆盖矩阵只证明"传了"。这条证明"传了以后产物真的不同"：拿每个新预设与"同参数但去掉全部轴"的版本逐条比
        foreach (var p in StylePresets.All.Skip(8))
        {
            var withAxes = PresetRecommender.Copy(p.Request);
            var without = PresetRecommender.Copy(p.Request);
            without.ShadowStyle = null;
            without.ShadowStrength = null;
            without.BorderStrength = null;
            without.NeutralTemp = null;
            without.FontPairing = null;
            without.RadiusStyle = null;
            without.AccentStrategy = null;

            var a = StyleAxisRows(without);
            var b = StyleAxisRows(withAxes);
            b.Should().NotEqual(a, $"{p.Id} 带了轴取值，产物却与不带轴时逐字相同 = 轴在该预设里没生效");
        }
    }

    static Dictionary<String, String> StyleAxisRows(GenerationRequest req)
    {
        var map = new Dictionary<String, String>(StringComparer.Ordinal);
        var r = DesignGenerator.Generate(req);
        foreach (var p in r.Shared) map[$"shared|{p.Path}"] = $"{p.Value}|{p.AliasPath}|{p.ValueJson}";
        foreach (var (theme, patches) in r.Themed)
            foreach (var p in patches) map[$"{theme}|{p.Path}"] = $"{p.Value}|{p.AliasPath}|{p.ValueJson}";
        return map;
    }

    [Fact]
    public void AC7_Find返回的Request是深拷贝_轴字段也一起拷()
    {
        // M1 的拷贝契约只管旧字段；轴字段漏拷会让"改一个预设的请求"污染整个目录
        var p = StylePresets.Find("editorial-serif")!;
        p.Request.FontPairing = "modern";
        p.Request.RadiusStyle = "round";
        p.Request.ShadowStrength = 2;

        var again = StylePresets.Find("editorial-serif")!;
        again.Request.FontPairing.Should().Be("editorial");
        again.Request.RadiusStyle.Should().Be("sharp");
        again.Request.ShadowStrength.Should().BeNull();
    }

    [Fact]
    public void AC7_拷贝函数覆盖了全部公共可空字段()
    {
        // PresetRecommender.Copy 手写逐字段，加字段忘加行的概率极高，所以逐字段行为核对：
        // 给每个字段填上可辨识的值，拷完必须一个不落地带上
        foreach (var prop in typeof(GenerationRequest).GetProperties())
            prop.PropertyType.Should().NotBeAssignableTo<Array>();
        var req = new GenerationRequest
        {
            Brief = "测试", SeedColor = "#123456", Hue = 12, AccentHueOffset = 34, Chroma = 0.11,
            Density = "compact", TypeRatio = 1.3, TypeBasePx = 17, RadiusBase = 9, MotionScale = 1.1,
            BrandName = "牌子", Industry = "media", Themes = ["light", "dark"],
            ShadowStyle = "crisp", ShadowStrength = 1.6, BorderStrength = "bold", NeutralTemp = "cool",
            FontPairing = "editorial", RadiusStyle = "pill", AccentStrategy = "triadic",
        };
        var copy = PresetRecommender.Copy(req);
        foreach (var prop in typeof(GenerationRequest).GetProperties())
        {
            if (prop.Name == nameof(GenerationRequest.Themes))
            {
                copy.Themes.Should().Equal(req.Themes);
                copy.Themes.Add("extra");
                req.Themes.Should().HaveCount(2, "Themes 必须是新列表，否则拷贝与源共享同一份");
                continue;
            }
            prop.GetValue(copy).Should().Be(prop.GetValue(req), $"Copy 漏了字段 {prop.Name}");
        }
    }
}

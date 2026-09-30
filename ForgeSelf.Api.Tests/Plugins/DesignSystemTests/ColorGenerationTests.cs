using System.Reflection;
using ForgeSelf.Api.Plugins.DesignSystem;
using ForgeSelf.Api.Plugins.DesignSystem.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>
/// 色阶与语义角色生成测试（AC6/AC7/AC8/AC9）——纯逻辑无库。
/// 这些是"从玩具到工具"的分界：v1 用一条写死的 10 阶 L/S 曲线（colorScale.ts:29-49）
/// 且语义色是固定 8 键表，从不验证对比度；这里要求**明度单调、彩度成包络、角色按对比度反查达标**。
/// </summary>
public class ColorGenerationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(45)]
    [InlineData(100)]
    [InlineData(180)]
    [InlineData(262)]
    [InlineData(330)]
    public void 色阶明度严格单调下降_且全部落在sRGB域内(double hue)
    {
        var steps = ColorRampGenerator.Generate(new RampOptions(hue, 0.19));

        steps.Should().OnlyContain(s => s.Step != null);
        for (var i = 1; i < steps.Count; i++)
            steps[i].Oklch.L.Should().BeLessThan(steps[i - 1].Oklch.L, $"{steps[i].Step} 阶必须比 {steps[i - 1].Step} 阶暗");

        foreach (var s in steps)
        {
            Oklch.IsInGamut(s.Oklch).Should().BeTrue($"{s.Step} 阶被域压缩后仍须可达");
            s.Hex.Should().MatchRegex("^#[0-9a-f]{6}$");
        }
    }

    [Fact]
    public void 彩度呈中段峰值_而非两端同样饱满()
    {
        var steps = ColorRampGenerator.Generate(new RampOptions(262, 0.2));
        var peak = steps.OrderByDescending(s => s.Oklch.C).First();

        new[] { "300", "400", "500", "600", "700" }.Should().Contain(peak.Step,
            "最饱满的一档应落在中段；两端贴白贴黑时彩度必须收，否则颜色不可用");
        steps.First(s => s.Step == "50").Oklch.C.Should().BeLessThan(steps.First(s => s.Step == "500").Oklch.C);
        steps.First(s => s.Step == "950").Oklch.C.Should().BeLessThan(steps.First(s => s.Step == "500").Oklch.C);
    }

    [Theory]
    [InlineData("#7c3aed")]
    [InlineData("#0ea5e9")]
    [InlineData("#16a34a")]
    [InlineData("#f59e0b")]
    public void 种子色必须逐位出现在生成的色阶里(String hex)
    {
        var seed = Oklch.ParseHex(hex)!.Value;
        var anchor = ColorRampGenerator.NearestStep(seed.L);
        var steps = ColorRampGenerator.Generate(new RampOptions(Oklch.NormalizeHue(seed.H), seed.C, AnchorStep: anchor, AnchorColor: seed));

        steps.Single(s => s.Step == anchor).Hex.Should().Be(hex,
            "用户给的是真颜色，色阶里若没有它，「种子」就只是装饰，品牌识别会漂");
    }

    [Fact]
    public void 同参数两次生成逐字节一致()
    {
        var a = ColorRampGenerator.Generate(new RampOptions(233, 0.17));
        var b = ColorRampGenerator.Generate(new RampOptions(233, 0.17));
        a.Select(x => $"{x.Step}:{x.Hex}").Should().Equal(b.Select(x => $"{x.Step}:{x.Hex}"));
    }

    [Fact]
    public void 未知色阶名直接报错_不静默兜底()
    {
        var act = () => ColorRampGenerator.Build("650", new RampOptions(262, 0.19));
        act.Should().Throw<ArgumentException>().WithMessage("*未知色阶*");
    }

    [Theory]
    [InlineData(30)]
    [InlineData(140)]
    [InlineData(262)]
    [InlineData(320)]
    public void 中性灰随品牌色相微染_不同色相得到不同灰(double hue)
    {
        var neutral = ColorRampGenerator.GenerateNeutral(hue);
        var other = ColorRampGenerator.GenerateNeutral(hue == 30 ? 140 : 30);

        neutral.Should().NotBeNull();
        neutral.First().Hex.Should().NotBe(other.First().Hex, "灰阶应带品牌色温，而不是所有系统共用同一条 slate");
    }

    [Theory]
    [InlineData(20)]
    [InlineData(90)]
    [InlineData(150)]
    [InlineData(210)]
    [InlineData(262)]
    [InlineData(330)]
    public void 语义角色由对比度反查tone_明暗两套都必须达标(double hue)
    {
        var ramps = BuildRamps(hue);
        foreach (var theme in new[] { "light", "dark" })
        {
            var choices = SemanticResolver.Resolve(ramps, theme);

            foreach (var c in choices.Where(c => c.Required > 0))
                c.Satisfied.Should().BeTrue(
                    $"{theme} 的 {c.Role} 对 {c.AgainstPath} 只有 {c.Ratio:F2}:1，要求 ≥{c.Required}:1");
        }
    }

    [Fact]
    public void 正文色比次要色更暗_亮色主题层级不糊()
    {
        var choices = SemanticResolver.Resolve(BuildRamps(262), "light").ToDictionary(c => c.Role, c => c.Hex);

        var l1 = Oklch.ParseHex(choices["text-1"])!.Value.L;
        var l2 = Oklch.ParseHex(choices["text-2"])!.Value.L;
        l1.Should().BeLessThan(l2, "text-1 必须比 text-2 更重，否则主次层级反了");
    }

    [Fact]
    public void 生成器同请求两次得同一seed与同一令牌集()
    {
        var req = new GenerationRequest { Brief = "分布式服务治理控制台，需要冷静专业", SeedColor = "#7c3aed", Themes = ["light", "dark"] };

        var a = DesignGenerator.Generate(req);
        var b = DesignGenerator.Generate(req);

        a.Seed.Should().Be(b.Seed);
        a.Industry.Should().Be("devtools");
        Fingerprint(a).Should().Be(Fingerprint(b));
    }

    [Fact]
    public void 不同排版比例与密度必须产生不同的非色令牌()
    {
        // v1 的硬伤：spacing/radius/motion 是两处逐字节相同的常量，任何系统都一样
        var tight = DesignGenerator.Generate(new GenerationRequest { SeedColor = "#7c3aed", TypeRatio = 1.125, Density = "compact", MotionScale = 0.8 });
        var loose = DesignGenerator.Generate(new GenerationRequest { SeedColor = "#7c3aed", TypeRatio = 1.5, Density = "comfortable", MotionScale = 1.3 });

        Values(tight, "space.").Should().NotBeEquivalentTo(Values(loose, "space."));
        Values(tight, "radius.").Should().NotBeEquivalentTo(Values(loose, "radius."));
        Values(tight, "duration.").Should().NotBeEquivalentTo(Values(loose, "duration."));
        Values(tight, "size.").Should().NotBeEquivalentTo(Values(loose, "size."), "模块化比例必须真的作用到字号");
    }

    [Fact]
    public void 无种子色时按需求文本稳定取色相_且给出可见提示()
    {
        var a = DesignGenerator.Generate(new GenerationRequest { Brief = "医疗随访管理系统" });
        var b = DesignGenerator.Generate(new GenerationRequest { Brief = "医疗随访管理系统" });
        var c = DesignGenerator.Generate(new GenerationRequest { Brief = "电商中后台订单系统" });

        a.Hue.Should().Be(b.Hue);
        a.Hue.Should().NotBe(c.Hue);
        a.Notes.Should().ContainSingle(n => n.Contains("哈希"), "不带美学判断的取色方式必须对用户可见");
    }

    [Fact]
    public void 三层齐备_组件层只引用语义层_不直连primitive()
    {
        var result = DesignGenerator.Generate(new GenerationRequest { SeedColor = "#0ea5e9", Themes = ["light", "dark"] });

        result.Shared.Count(p => p.Tier == TokenTiers.Primitive).Should().BeGreaterThan(60);
        result.Shared.Count(p => p.Tier == TokenTiers.Component).Should().BeGreaterThan(20);
        result.Themed.Should().ContainKeys("light", "dark");

        var primitivePaths = result.Shared.Where(p => p.Tier == TokenTiers.Primitive).Select(p => p.Path).ToHashSet();
        var bad = result.Shared.Where(p => p.Tier == TokenTiers.Component && p.Type == TokenTypes.Color && primitivePaths.Contains(p.AliasPath ?? "")).ToList();
        bad.Should().BeEmpty("component 颜色令牌必须经 semantic 中转，否则换主题时组件不跟随");
    }

    [Fact]
    public void 动效与排版带可达性与流式派生物()
    {
        var result = DesignGenerator.Generate(new GenerationRequest { SeedColor = "#7c3aed" });

        result.Shared.Should().Contain(p => p.Path == "duration.base-reduced" && p.Value == "0.01ms");
        result.Shared.Should().Contain(p => p.Path == "component.focus.outline-width");
        result.Shared.Should().Contain(p => p.Path == "component.motion.reduced.duration");

        var type = result.Shared.Single(p => p.Path == "type.h1");
        type.Description.Should().Contain("clamp");
        type.ValueJson!.Should().Contain("fontFamily");
    }

    [Fact]
    public void 图表系列色存在_补参考物缺的那一类()
    {
        var result = DesignGenerator.Generate(new GenerationRequest { SeedColor = "#7c3aed" });
        result.Shared.Where(p => p.Path.StartsWith("chart.", StringComparison.Ordinal)).Should().HaveCount(8);
    }

    /// <summary>
    /// v2.6.6：色族清单从"前端手抄一份数组"改成"后端一张表 + 生成器按它逐族产阶"。
    /// 表必须与产出一一对上：多一族没产 = 界面上一条不存在的色阶，产了没列 = 界面把它排到最后并吞掉色族序。
    /// </summary>
    [Fact]
    public void 色族序由ColorFamilies供给_生成器逐族产阶_一族不多也不少()
    {
        var result = DesignGenerator.Generate(new GenerationRequest { SeedColor = "#7c3aed", Brief = "治理控制台", Themes = ["light"] });

        var produced = result.Shared
            .Where(p => p.Tier == TokenTiers.Primitive && p.Type == TokenTypes.Color && p.Path.StartsWith("color.", StringComparison.Ordinal))
            .Select(p => p.Path.Split('.')[1])
            .Distinct(StringComparer.Ordinal)
            .ToList();

        produced.Should().Equal(ColorFamilies.All,
            "生成器产出的族与顺序必须等于 ColorFamilies.All —— 界面色阶条带读的就是这张表（/meta.colorFamilies）");

        // 每族都得是真的有色阶，不能只是表上有个名字
        foreach (var family in ColorFamilies.All)
            result.Shared.Count(p => p.Path.StartsWith($"color.{family}.", StringComparison.Ordinal))
                .Should().BeGreaterThanOrEqualTo(10, $"{family} 族只有一两阶 = 表上有名、产物里没货");

        // 表与常量同源：加了 const 忘了进 All，上面两条都发现不了
        var declared = typeof(ColorFamilies).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(String)).Select(f => (String)f.GetValue(null)!).ToList();
        ColorFamilies.All.Should().BeEquivalentTo(declared,
            $"ColorFamilies 常量（{declared.Count} 个）与 All（{ColorFamilies.All.Length} 个）必须一一对应");
    }

    static Dictionary<String, IReadOnlyList<RampStep>> BuildRamps(Double hue) => new(StringComparer.Ordinal)
    {
        [ColorFamilies.Brand] = ColorRampGenerator.Generate(new RampOptions(hue, 0.19)),
        [ColorFamilies.Accent] = ColorRampGenerator.Generate(new RampOptions(Oklch.NormalizeHue(hue + 168), 0.17)),
        [ColorFamilies.Neutral] = ColorRampGenerator.GenerateNeutral(hue),
        [ColorFamilies.Success] = ColorRampGenerator.Generate(new RampOptions(152, 0.15)),
        [ColorFamilies.Warning] = ColorRampGenerator.Generate(new RampOptions(85, 0.16)),
        [ColorFamilies.Danger] = ColorRampGenerator.Generate(new RampOptions(27, 0.17)),
        [ColorFamilies.Info] = ColorRampGenerator.Generate(new RampOptions(Oklch.NormalizeHue(hue + 168), 0.13)),
    };

    static String Fingerprint(GenerationResult r) =>
        string.Join('|', r.Shared.Select(p => $"{p.Path}={p.Value}→{p.AliasPath}").OrderBy(x => x, StringComparer.Ordinal))
        + "||" + string.Join('|', r.Themed.SelectMany(kv => kv.Value.Select(p => $"{kv.Key}:{p.Path}→{p.AliasPath}").OrderBy(x => x, StringComparer.Ordinal)));

    static IEnumerable<String> Values(GenerationResult r, String prefix) =>
        r.Shared.Where(p => p.Path.StartsWith(prefix, StringComparison.Ordinal)).Select(p => $"{p.Path}={p.Value}");
}

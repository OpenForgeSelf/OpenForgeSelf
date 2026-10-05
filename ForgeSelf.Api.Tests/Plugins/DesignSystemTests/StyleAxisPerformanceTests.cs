using System.Diagnostics;
using System.Text;
using ForgeSelf.Api.Plugins.DesignSystem.Services;
using FluentAssertions;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>
/// 02-spec「非功能要求·性能」的实测位：规格写死 `GuidelineGenerator` &lt; 50ms、非默认轴生成 ≤ 默认档 1.5 倍，
/// 并要求「实测记 Evidence」——这条在前面的收口里只有实现、没有人真跑过数字，所以它一直是纸面承诺。
///
/// 取样纪律（缺一不可，否则数字是假的）：
/// ① 先 warm-up 3 轮再计时（JIT 与首次分配不参与结果）；
/// ② 默认档与对照档**交替**取样：本仓常有并行会话（自查表 #54），交替让机器争用同时压两侧，
///    比值才不被单边抖动推高——这也是 1.5 倍这条紧阈值能常驻不假红的前提；
/// ③ 先证明两侧产物真的不同（FR3 的 `Seed` 后缀），否则测的是同一份东西自比；
/// ④ 中位数为主读，p95 为辅读，两个都写进 `DS_DUMP_PERF=1` 的产出文件（默认不写盘，同黄金基线录制器）。
/// </summary>
public class StyleAxisPerformanceTests
{
    const Int32 Warm = 3;
    const Int32 Rounds = 25;

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

    static void Apply(GenerationRequest r, String axis, String value)
    {
        switch (axis)
        {
            case StyleAxes.ShadowStyle: r.ShadowStyle = value; break;
            case StyleAxes.BorderStrength: r.BorderStrength = value; break;
            case StyleAxes.NeutralTemp: r.NeutralTemp = value; break;
            case StyleAxes.FontPairing: r.FontPairing = value; break;
            case StyleAxes.RadiusStyle: r.RadiusStyle = value; break;
            case StyleAxes.AccentStrategy: r.AccentStrategy = value; break;
            case StyleAxes.ShadowStrength: r.ShadowStrength = Double.Parse(value, System.Globalization.CultureInfo.InvariantCulture); break;
            default: throw new ArgumentException($"未知风格轴 {axis}——新增轴要同时登记到这里，否则性能守卫会漏掉它");
        }
    }

    /// <summary>规范生成器的输入用一份接近真实规模的令牌路径集（36 条，跨 color/space/type/radius/shadow），
    /// 空列表会让 50ms 这条阈值测成"几乎什么都没做"的假绿。</summary>
    static List<String> RealisticPaths() =>
    [
        "color.primary", "color.primary-strong", "color.primary-soft", "color.accent", "color.text-1",
        "color.text-2", "color.surface", "color.surface-raised", "color.border", "color.danger",
        "color.success", "color.warning",
        "space.1", "space.2", "space.4", "space.6", "space.8", "space.12",
        "type.display", "type.h1", "type.h2", "type.h3", "type.body", "type.small", "type.caption",
        "font.sans", "font.mono", "font.display",
        "radius.sm", "radius.md", "radius.lg", "radius.pill",
        "shadow.sm", "shadow.md", "shadow.lg",
    ];

    static Double Median(Double[] xs)
    {
        var s = xs.OrderBy(x => x).ToArray();
        return s.Length % 2 == 1 ? s[s.Length / 2] : (s[s.Length / 2 - 1] + s[s.Length / 2]) / 2;
    }

    static Double Percentile(Double[] xs, Double p)
    {
        var s = xs.OrderBy(x => x).ToArray();
        var idx = (Int32)Math.Ceiling(s.Length * p) - 1;
        return s[Math.Clamp(idx, 0, s.Length - 1)];
    }

    [Fact]
    public void NFR_规范生成器纯函数_实测p95在规格50毫秒内()
    {
        var paths = RealisticPaths();

        // 前提自证：这条轴的值真的进到了生成器（14 条规范不是空跑出来的）
        for (var i = 0; i < Warm; i++)
            GuidelineGenerator.Generate("product", "general", "default", paths).Count.Should().Be(14, "取样对象必须是真产出");

        var samples = new Double[Rounds];
        for (var i = 0; i < Rounds; i++)
        {
            var sw = Stopwatch.StartNew();
            var drafts = GuidelineGenerator.Generate("product", "general", "default", paths);
            sw.Stop();
            drafts.Count.Should().Be(14, $"第 {i} 轮生成器只回 {drafts.Count} 条，测出来的耗时没有意义");
            samples[i] = sw.Elapsed.TotalMilliseconds;
        }

        var p50 = Median(samples);
        var p95 = Percentile(samples, 0.95);
        p95.Should().BeLessThan(50, $"规格 NFR 写 GuidelineGenerator < 50ms，实测 p50={p50:F3}ms、p95={p95:F3}ms（{Rounds} 轮，warm-up {Warm}，输入 {paths.Count} 条令牌路径）");
        Dump($"GuidelineGenerator（{paths.Count} 条路径 → 14 条规范）：p50={p50:F3}ms p95={p95:F3}ms，阈值 50ms，轮次 {Rounds}");
    }

    [Theory]
    [InlineData("shadowStyle", "crisp")]
    [InlineData("shadowStyle", "layered")]
    [InlineData("borderStrength", "bold")]
    [InlineData("neutralTemp", "warm")]
    [InlineData("fontPairing", "editorial")]
    [InlineData("radiusStyle", "pill")]
    [InlineData("accentStrategy", "triadic")]
    [InlineData("shadowStrength", "2")]
    public void NFR_非默认轴生成耗时_交替取样中位数比值不超过规格的1_5倍(String axis, String value)
    {
        var dflt = Base();
        var variant = Base();
        Apply(variant, axis, value);

        var d = DesignGenerator.Generate(dflt);
        var v = DesignGenerator.Generate(variant);
        // 先证明测的是两份不同产物：非默认取值必须给 Seed 追加 ;style=… 后缀（FR3）
        v.Seed.Should().NotBe(d.Seed, $"{axis}={value} 的产物与默认档逐字相同 ⇒ 比值是同一份东西自比，没有意义");
        // 条数守卫容得下规格自己规定的增量：fontPairing=editorial 会**多产一条** `font.display`（AC3 已钉，实测 388→389），
        // 但不容下工作量级变化——差几十条就是"测的根本不是同一件事"。
        Math.Abs(v.Total - d.Total).Should().BeLessThanOrEqualTo(2,
            $"{axis}={value} 的令牌条数从 {d.Total} 变成 {v.Total}：超出「editorial 多一条 font.display」这条已登记的合法增量，耗时比值不再可比");

        for (var i = 0; i < Warm; i++)
        {
            DesignGenerator.Generate(dflt);
            DesignGenerator.Generate(variant);
        }

        var ds = new Double[Rounds];
        var vs = new Double[Rounds];
        for (var i = 0; i < Rounds; i++)
        {
            var sw = Stopwatch.StartNew();
            DesignGenerator.Generate(dflt);
            sw.Stop();
            ds[i] = sw.Elapsed.TotalMilliseconds;

            sw = Stopwatch.StartNew();
            DesignGenerator.Generate(variant);
            sw.Stop();
            vs[i] = sw.Elapsed.TotalMilliseconds;
        }

        var dMed = Median(ds);
        var vMed = Median(vs);
        var ratio = vMed / dMed;
        ratio.Should().BeLessThanOrEqualTo(1.5,
            $"规格 NFR 写「非默认轴生成耗时 ≤ 默认档 1.5 倍」：{axis}={value} 实测默认档中位 {dMed:F2}ms、对照档中位 {vMed:F2}ms，比值 {ratio:F2}×（交替取样 {Rounds} 轮）");
        Dump($"{axis}={value}：默认 p50={dMed:F3}ms p95={Percentile(ds, 0.95):F3}ms；对照 p50={vMed:F3}ms p95={Percentile(vs, 0.95):F3}ms；比值 {ratio:F2}×（阈值 1.5×）");
    }

    /// <summary>与黄金基线录制器/规范清单产出器同一套纪律：默认不产出，只有 DS_DUMP_PERF=1 才写盘。</summary>
    static void Dump(String line)
    {
        if (Environment.GetEnvironmentVariable("DS_DUMP_PERF") != "1") return;

        var file = Path.Combine(FindRepoRoot(), ".temp", "ds-m1", "logs", "perf-m3-measured.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.AppendAllText(file, $"{DateTime.Now:HH:mm:ss} {line}{Environment.NewLine}", new UTF8Encoding(false));
    }

    static String FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("找不到仓库根");
    }
}

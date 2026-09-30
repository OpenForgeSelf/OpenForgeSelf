namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>色阶生成参数（确定性：同参数必得同结果）。</summary>
/// <param name="Hue">种子色相 0~360</param>
/// <param name="Chroma">种子彩度（锚定在 500 阶），0~0.37；0 表示纯中性</param>
/// <param name="Steps">阶名，默认 50..950</param>
/// <param name="HueCycleStrength">彩度被压缩时的色相补偿幅度上限（度）</param>
/// <param name="AnchorStep">锚定阶：该阶必须逐位等于 AnchorColor（种子色进不了色阶 = "种子"是假的）</param>
/// <param name="AnchorColor">锚定色的 oklch 值；空则按包络计算</param>
public sealed record RampOptions(
    Double Hue,
    Double Chroma = 0.19,
    IReadOnlyList<String>? Steps = null,
    Double HueCycleStrength = 12,
    String? AnchorStep = null,
    Oklch.Color? AnchorColor = null);

/// <summary>一阶颜色：阶名 + 落定后的 oklch 与 sRGB。</summary>
public sealed record RampStep(String Step, Oklch.Color Oklch, String Hex, Double MaxChroma, Boolean Clamped);

/// <summary>
/// OKLCH 感知色阶生成器：一个种子色 → 50..950 全阶。
///
/// 三条纪律（每条都有测试钉住）：
/// 1. 明度按锚定曲线走（不是线性插值 hex，那会让中段挤在一起）；
/// 2. 彩度受**实际 sRGB 域**约束——出界时压彩度而不是钳 R/G/B（钳 RGB 会同时改掉明度与色相）；
/// 3. 彩度被压缩时做色相补偿（近似 Bacher hue-cycling），否则恒定色相的梯度在暗端发浑、亮端偏青。
/// </summary>
public static class ColorRampGenerator
{
    /// <summary>阶名 → 目标明度 L。锚定值：500 阶 ≈ 0.62，与参考物 Stardust 的 brand-500 同级</summary>
    static readonly Dictionary<String, Double> ToneLightness = new(StringComparer.Ordinal)
    {
        ["50"] = 0.975,
        ["100"] = 0.945,
        ["200"] = 0.890,
        ["300"] = 0.820,
        ["400"] = 0.735,
        ["500"] = 0.620,
        ["600"] = 0.545,
        ["700"] = 0.475,
        ["800"] = 0.400,
        ["900"] = 0.325,
        ["950"] = 0.245,
    };

    /// <summary>默认阶序</summary>
    public static readonly String[] DefaultSteps = ["50", "100", "200", "300", "400", "500", "600", "700", "800", "900", "950"];

    /// <summary>中性阶（gray）的明度曲线与彩度阶共用，彩度固定 0（纯中性）或极低（带种子色相的微染）</summary>
    public static IReadOnlyList<RampStep> Generate(RampOptions options)
    {
        var steps = options.Steps ?? DefaultSteps;
        var list = new List<RampStep>(steps.Count);
        foreach (var step in steps)
            list.Add(Build(step, options));
        return list;
    }

    /// <summary>生成单阶</summary>
    public static RampStep Build(String step, RampOptions options)
    {
        if (!ToneLightness.TryGetValue(step, out var l))
            throw new ArgumentException($"未知色阶 {step}，可用：{String.Join(",", ToneLightness.Keys)}", nameof(step));

        var hue = Oklch.NormalizeHue(options.Hue);

        // 锚定阶逐位复现种子色：用户给 #7c3aed，色阶里就必须有一档正好是 #7c3aed，
        // 否则"种子色"只是个装饰性输入，品牌识别会漂。
        if (options.AnchorColor != null && step == options.AnchorStep)
        {
            var anchor = Oklch.FitToGamut(options.AnchorColor.Value);
            return new RampStep(step, anchor, Oklch.ToRgb8(anchor).ToHex(), MaxChromaAt(anchor.L, hue), false);
        }

        var anchorStep = options.AnchorStep ?? "500";
        var anchorL = ToneLightness.TryGetValue(anchorStep, out var al) ? al : ToneLightness["500"];
        var maxAtAnchor = Oklch.MaxChroma(anchorL, hue);
        var seedChroma = Math.Clamp(options.Chroma, 0, Math.Max(0.001, maxAtAnchor));

        // 彩度包络：以锚定阶为 1.0 归一，两端收窄、中段最饱满；再按该阶的实际可用彩度封顶
        var envelope = ChromaEnvelope(l) / ChromaEnvelope(anchorL);
        var wanted = seedChroma * envelope;
        var cMax = MaxChromaAt(l, hue);
        var clamped = wanted > cMax;
        var actual = Math.Min(wanted, cMax);

        var h = HueCycle(hue, l, actual, cMax, options.HueCycleStrength);
        var color = Oklch.FitToGamut(new Oklch.Color(l, actual, h));
        return new RampStep(step, color, Oklch.ToRgb8(color).ToHex(), cMax, clamped);
    }

    static Double MaxChromaAt(Double l, Double hue) => l <= 0.0 || l >= 1.0 ? 0 : Oklch.MaxChroma(l, hue);

    /// <summary>
    /// 中性阶：明度沿用同一曲线；彩度取种子色相的极小残留（0.012 量级），
    /// 目的是让灰阶带一点品牌色温而不是死灰——这是参考物 Stardust 冷 slate 灰的做法，但随品牌色相变化。
    /// </summary>
    public static IReadOnlyList<RampStep> GenerateNeutral(Double hue, Double tint = 0.012, IReadOnlyList<String>? steps = null) =>
        Generate(new RampOptions(hue, tint, steps));

    /// <summary>彩度包络：明度 L 处的相对饱满度（0..1），中段最高、两端衰减</summary>
    static Double ChromaEnvelope(Double l)
    {
        var t = Oklch.Clamp01((l - 0.02) / 0.96);
        return 0.25 + 0.75 * Math.Pow(Math.Sin(Math.PI * t), 0.85);
    }

    /// <summary>
    /// 色相补偿（近似 Bacher hue-cycling）：彩度被域压缩得越厉害，色相偏移越大；
    /// 暗端向暖、亮端向冷，用来抵消"同色相不同明度看起来不是同一个颜色"的知觉偏移。
    /// 强度上限由 options 控制，默认 12°，属可复核的设计常量而非拍脑袋数字。
    /// </summary>
    static Double HueCycle(Double hue, Double l, Double chroma, Double chromaMax, Double strength)
    {
        if (chromaMax <= 0.001 || chroma <= 0.002) return hue;

        var compression = Oklch.Clamp(1 - chroma / chromaMax, 0, 1);
        if (compression <= 0.01) return hue;

        // 越靠近中段，补偿越有效；极亮/极暗处压到一半，避免把色相甩成另一种颜色
        var midWeight = 1 - Math.Abs(2 * l - 1);
        var bias = l >= ToneLightness["500"] ? -1 : 1;
        return Oklch.NormalizeHue(hue + bias * strength * compression * (0.4 + 0.6 * midWeight));
    }

    /// <summary>从任意 CSS 颜色（hex 或 oklch）取出色相与彩度作为种子；解析失败返回 null</summary>
    public static (Double Hue, Double Chroma)? SeedFromColor(String? cssColor)
    {
        var c = SeedColorOrNull(cssColor);
        return c == null ? null : (Oklch.NormalizeHue(c.Value.H), c.Value.C);
    }

    /// <summary>解析种子色（供生成器锚定复现）；失败返回 null</summary>
    public static Oklch.Color? SeedColorOrNull(String? cssColor) => Oklch.ParseHex(cssColor) ?? Oklch.ParseOklch(cssColor);

    /// <summary>离给定明度最近的阶名——种子色要落在这一档上逐位复现</summary>
    public static String NearestStep(Double l) =>
        ToneLightness.OrderBy(kv => Math.Abs(kv.Value - l)).First().Key;
}

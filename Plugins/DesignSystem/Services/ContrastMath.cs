namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>
/// WCAG 2.2 对比度数学与判级（纯函数）。
///
/// 判据只用 2.2 规范值：正文 1.4.3 AA 4.5:1 / 1.4.6 AAA 7:1；大字 3:1 / 4.5:1；
/// 非文本 UI 与图形对象、焦点指示 1.4.11 AA 3:1。
/// APCA 不作门禁（2023 年已从 WCAG 3 草案移除，2.x 仍是规范），见 docs/07-decisions/not-taken-decisions.md。
/// </summary>
public static class ContrastMath
{
    /// <summary>对比度用途（决定阈值）</summary>
    public enum Usage
    {
        /// <summary>正文（&lt;24px 常规字重）：AA 4.5 / AAA 7</summary>
        TextNormal,
        /// <summary>大字（≥24px，或 ≥18.66px 且粗体）：AA 3 / AAA 4.5</summary>
        TextLarge,
        /// <summary>非文本 UI 构件、图形对象、焦点指示：AA 3</summary>
        NonTextUi,
        /// <summary>纯装饰/不适用对比度要求：不判</summary>
        Decorative
    }

    /// <summary>达标等级</summary>
    public enum Level
    {
        /// <summary>未计算或不适用</summary>
        None,
        /// <summary>未达 AA</summary>
        Fail,
        /// <summary>达 AA</summary>
        Aa,
        /// <summary>达 AAA</summary>
        Aaa
    }

    /// <summary>AA 阈值</summary>
    public static Double AaThreshold(Usage usage) => usage switch
    {
        Usage.TextNormal => 4.5,
        Usage.TextLarge => 3.0,
        Usage.NonTextUi => 3.0,
        _ => Double.PositiveInfinity,
    };

    /// <summary>AAA 阈值</summary>
    public static Double AaaThreshold(Usage usage) => usage switch
    {
        Usage.TextNormal => 7.0,
        Usage.TextLarge => 4.5,
        // 2.2 对非文本构件只定义 AA(1.4.11)，无 AAA 档
        Usage.NonTextUi => Double.PositiveInfinity,
        _ => Double.PositiveInfinity,
    };

    /// <summary>
    /// 是否属「大字」：CSS px 下 ≥24px，或 ≥18.66px 且字重 ≥700。
    /// 边界取规范原文的 18pt/14pt 换算（1pt = 1.333px）。
    /// </summary>
    public static Boolean IsLargeText(Double sizePx, Int32 weight) => sizePx >= 24 || (sizePx >= 18.66 && weight >= 700);

    /// <summary>对比度比率（1~21）。任一色非法返回 -1，让调用方能区分"没算"与"算出 1"。</summary>
    public static Double Ratio(Oklch.Color a, Oklch.Color b)
    {
        var ya = Oklch.RelativeLuminance(a);
        var yb = Oklch.RelativeLuminance(b);
        var hi = Math.Max(ya, yb);
        var lo = Math.Min(ya, yb);
        return (hi + 0.05) / (lo + 0.05);
    }

    /// <summary>按 hex 字符串算对比度；解析失败返回 -1</summary>
    public static Double Ratio(String? hexA, String? hexB)
    {
        var a = Oklch.TryParseRgb8(hexA);
        var b = Oklch.TryParseRgb8(hexB);
        if (a == null || b == null) return -1;
        return Ratio(a.Value, b.Value);
    }

    /// <summary>按 8bit 分量算对比度</summary>
    public static Double Ratio(Oklch.Rgb8 a, Oklch.Rgb8 b)
    {
        var hi = Math.Max(Oklch.RelativeLuminance(a), Oklch.RelativeLuminance(b));
        var lo = Math.Min(Oklch.RelativeLuminance(a), Oklch.RelativeLuminance(b));
        return (hi + 0.05) / (lo + 0.05);
    }

    /// <summary>判级：先 AAA 再 AA，都不达则 Fail；装饰类返回 None。</summary>
    public static Level Judge(Double ratio, Usage usage)
    {
        if (usage == Usage.Decorative || ratio < 0) return Level.None;
        if (ratio + Eps >= AaaThreshold(usage)) return Level.Aaa;
        if (ratio + Eps >= AaThreshold(usage)) return Level.Aa;
        return Level.Fail;
    }

    /// <summary>是否达 AA（装饰类视为通过，不阻断发布门禁）</summary>
    public static Boolean MeetsAa(Double ratio, Usage usage) => usage == Usage.Decorative || (ratio >= 0 && ratio + Eps >= AaThreshold(usage));

    /// <summary>判级标签（写库用：none|aa|aaa|fail）</summary>
    public static String LevelTag(Level level) => level switch
    {
        Level.Aaa => "aaa",
        Level.Aa => "aa",
        Level.Fail => "fail",
        _ => "none",
    };

    /// <summary>浮点比较容差：避免 4.4999999 被判为不达 4.5</summary>
    const Double Eps = 1e-6;
}

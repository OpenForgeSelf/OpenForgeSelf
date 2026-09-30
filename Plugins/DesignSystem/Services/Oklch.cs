using System.Globalization;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>
/// OKLCH / OKLab / sRGB 色彩数学（纯函数，无 IO，可黄金值单测）。
///
/// 为什么自实现而不引 culori/colorjs.io：本插件的生成与审计必须确定性且可复现，
/// 而依赖体积与新包审批成本更高；矩阵与传递函数是公开定值（Björn Ottosson OKLab）。
/// </summary>
public static class Oklch
{
    /// <summary>OKLCH 颜色：L 明度 0~1、C 彩度 0~约0.4、H 色相 0~360 度</summary>
    public readonly record struct Color(double L, double C, double H)
    {
        /// <summary>CSS oklch() 函数形式（L 以百分数表达，便于人读与 diff）</summary>
        public String ToCss() => FormattableString.Invariant($"oklch({Fmt(L * 100)}% {Fmt(C, 4)} {Fmt(NormalizeHue(H))})");

        public Color With(double? l = null, double? c = null, double? h = null) =>
            new(l ?? L, c ?? C, h ?? H);

        public override String ToString() => ToCss();

        static String Fmt(Double v, Int32 digits = 2) =>
            Math.Round(v, digits, MidpointRounding.AwayFromZero).ToString("0." + new String('#', digits), CultureInfo.InvariantCulture);
    }

    /// <summary>8bit sRGB 分量，各 0~255</summary>
    public readonly record struct Rgb8(Int32 R, Int32 G, Int32 B)
    {
        public String ToHex() => $"#{R:x2}{G:x2}{B:x2}";
    }

    #region 传递函数（sRGB companding）

    /// <summary>sRGB 分量（0~1）转线性光</summary>
    public static Double SrgbToLinear(Double v)
    {
        v = Clamp01(v);
        return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }

    /// <summary>线性光转 sRGB 分量（0~1）</summary>
    public static Double LinearToSrgb(Double v)
    {
        v = Clamp01(v);
        return v <= 0.0031308 ? v * 12.92 : 1.055 * Math.Pow(v, 1.0 / 2.4) - 0.055;
    }

    #endregion

    #region 空间转换

    /// <summary>线性 RGB 转 OKLab（CSS Color 4 定值矩阵；每行之和须≈1，否则纯灰会带上假彩度）</summary>
    public static (Double L, Double A, Double B) LinearRgbToOklab(Double r, Double g, Double b)
    {
        var l = 0.4122214694707658 * r + 0.5363325279447511 * g + 0.0514459992324620 * b;
        var m = 0.2119034881899427 * r + 0.6806995398044187 * g + 0.1073969581285328 * b;
        var s = 0.0883024619229489 * r + 0.2817188147882521 * g + 0.6299787166800563 * b;

        // cbrt 而非幂运算：负值（域外）时 Pow 会得 NaN，Math.Cbrt 保持连续
        l = Math.Cbrt(l);
        m = Math.Cbrt(m);
        s = Math.Cbrt(s);

        return (
            0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
            1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
            0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
    }

    /// <summary>OKLab 转线性 RGB（可能落在 sRGB 域外，由调用方钳制）</summary>
    public static (Double R, Double G, Double B) OklabToLinearRgb(Double l, Double a, Double b)
    {
        var vw = new[] { l + 0.3963377794 * a + 0.2158037573 * b, l - 0.1055613458 * a - 0.0638541728 * b, l - 0.0894841775 * a - 1.2914855480 * b };
        for (var i = 0; i < 3; i++) vw[i] *= vw[i] * vw[i];

        return (
            +4.076741661747987 * vw[0] - 3.307711591053480 * vw[1] + 0.2309699301428529 * vw[2],
            -1.268438019287432 * vw[0] + 2.6092574056513583 * vw[1] - 0.3413193879526928 * vw[2],
            -0.004196087767779 * vw[0] - 0.7034186288499276 * vw[1] + 1.7076805712520067 * vw[2]);
    }

    /// <summary>OKLab 转 OKLCH（极坐标）</summary>
    public static Color OklabToOklch(Double l, Double a, Double b)
    {
        var c = Math.Sqrt(a * a + b * b);
        var h = c < 1e-9 ? Double.NaN : NormalizeHue(Math.Atan2(b, a) * 180.0 / Math.PI);
        return new Color(l, c, double.IsNaN(h) ? 0 : h);
    }

    /// <summary>OKLCH 转 OKLab</summary>
    public static (Double L, Double A, Double B) OklchToOklab(Color o)
    {
        if (o.C <= 0) return (o.L, 0, 0);
        var rad = o.H * Math.PI / 180.0;
        return (o.L, o.C * Math.Cos(rad), o.C * Math.Sin(rad));
    }

    /// <summary>OKLCH 转 8bit sRGB（域外自动按彩度压缩回域内）</summary>
    public static Rgb8 ToRgb8(Color o)
    {
        var inGamut = FitToGamut(o);
        var (l, a, b) = OklchToOklab(inGamut);
        var (r, g, bl) = OklabToLinearRgb(l, a, b);
        return new Rgb8(To255(LinearToSrgb(r)), To255(LinearToSrgb(g)), To255(LinearToSrgb(bl)));
    }

    /// <summary>8bit sRGB 转 OKLCH</summary>
    public static Color FromRgb8(Rgb8 c) => FromLinearRgb(SrgbToLinear(c.R / 255.0), SrgbToLinear(c.G / 255.0), SrgbToLinear(c.B / 255.0));

    /// <summary>线性 RGB 转 OKLCH</summary>
    public static Color FromLinearRgb(Double r, Double g, Double b)
    {
        var (l, a, bb) = LinearRgbToOklab(r, g, b);
        return OklabToOklch(l, a, bb);
    }

    /// <summary>解析 #rgb / #rrggbb（支持不带井号）。解析失败返回 null。</summary>
    public static Color? ParseHex(String? hex)
    {
        var rgb = TryParseRgb8(hex);
        return rgb.HasValue ? FromRgb8(rgb.Value) : null;
    }

    /// <summary>解析 #rgb / #rrggbb 为 8bit 分量；非法输入返回 null</summary>
    public static Rgb8? TryParseRgb8(String? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        var s = hex.Trim().TrimStart('#');
        if (s.Length == 3) s = new String([s[0], s[0], s[1], s[1], s[2], s[2]], 0, 6);
        if (s.Length == 8 && IsHex(s)) s = s[..6]; // 忽略 alpha 段
        if (s.Length != 6 || !IsHex(s)) return null;

        return new Rgb8(Convert.ToInt32(s.Substring(0, 2), 16), Convert.ToInt32(s.Substring(2, 2), 16), Convert.ToInt32(s.Substring(4, 2), 16));
    }

    static Boolean IsHex(String s)
    {
        foreach (var c in s)
            if (!char.IsAsciiHexDigit(c)) return false;
        return true;
    }

    /// <summary>OKLCH 字符串解析：oklch(62% 0.19 285) 与 oklch(0.62 0.19 285) 两种写法都收。</summary>
    public static Color? ParseOklch(String? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var s = text.Trim();
        if (s.StartsWith("oklch", StringComparison.OrdinalIgnoreCase))
        {
            var open = s.IndexOf('(');
            var close = s.LastIndexOf(')');
            if (open < 0 || close <= open) return null;
            s = s[(open + 1)..close];
        }

        var parts = s.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3) return null;
        if (!TryNum(parts[0], out var l) || !TryNum(parts[1], out var c) || !TryNum(parts[2], out var h)) return null;

        return new Color(l > 1.0001 ? l / 100.0 : Clamp01(l), c, NormalizeHue(h));
    }

    static Boolean TryNum(String s, out Double v)
    {
        s = s.TrimEnd('%');
        return Double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
    }

    #endregion

    #region 域（gamut）处理与插值

    /// <summary>该明度/色向在 sRGB 内的最大彩度（二分求解，精度 1e-4）</summary>
    public static Double MaxChroma(Double l, Double h)
    {
        if (l <= 0.0 || l >= 1.0) return 0;

        Double lo = 0, hi = 0.5;
        for (var i = 0; i < 24; i++)
        {
            var mid = (lo + hi) / 2;
            if (IsInGamut(new Color(l, mid, h))) lo = mid;
            else hi = mid;
        }
        return lo;
    }

    /// <summary>颜色是否落在 sRGB 立方体内（容差 1e-6）</summary>
    public static Boolean IsInGamut(Color o)
    {
        var (l, a, b) = OklchToOklab(o);
        var (r, g, bl) = OklabToLinearRgb(l, a, b);
        const double e = 1e-6;
        return r >= -e && r <= 1 + e && g >= -e && g <= 1 + e && bl >= -e && bl <= 1 + e;
    }

    /// <summary>
    /// 把域外颜色按「保明度保色相、只压彩度」压回 sRGB。
    /// 这是色阶生成的关键：域外色不钳 R/G/B（那会同时改掉明度与色相），而是减彩度。
    /// </summary>
    public static Color FitToGamut(Color o)
    {
        if (o.C <= 0 || IsInGamut(o)) return o;
        var max = MaxChroma(o.L, o.H);
        return new Color(o.L, Math.Min(o.C, max), o.H);
    }

    /// <summary>在 OKLab 直线插值（等价于 CSS color-mix(in oklab, a x%, b)）</summary>
    public static Color Mix(Color a, Color b, Double weightOfB)
    {
        var (la, aa, ba) = OklchToOklab(a);
        var (lb, ab, bb) = OklchToOklab(b);
        var w = Clamp01(weightOfB);
        return OklabToOklch(la + (lb - la) * w, aa + (ab - aa) * w, ba + (bb - ba) * w);
    }

    #endregion

    #region 明度与对比度支撑

    /// <summary>相对亮度 Y（WCAG 2.2 定义：线性 RGB 加权和）</summary>
    public static Double RelativeLuminance(Rgb8 c) =>
        0.2126 * SrgbToLinear(c.R / 255.0) + 0.7152 * SrgbToLinear(c.G / 255.0) + 0.0722 * SrgbToLinear(c.B / 255.0);

    /// <summary>OKLCH 颜色的相对亮度（先压回 sRGB 再算，保证与浏览器渲染一致）</summary>
    public static Double RelativeLuminance(Color c) => RelativeLuminance(ToRgb8(c));

    #endregion

    #region 工具

    public static Double Clamp01(Double v) => v < 0 ? 0 : v > 1 ? 1 : v;

    public static Double Clamp(Double v, Double min, Double max) => v < min ? min : v > max ? max : v;

    /// <summary>色相归一到 0~360</summary>
    public static Double NormalizeHue(Double h)
    {
        var v = h % 360.0;
        return v < 0 ? v + 360.0 : v;
    }

    static Int32 To255(Double v) => (Int32)Math.Round(Clamp01(v) * 255.0, MidpointRounding.AwayFromZero);

    #endregion
}

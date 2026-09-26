using ForgeSelf.Api.Plugins.DevTools.Models;
using NewLife.Log;
using System.Drawing;

namespace ForgeSelf.Api.Plugins.DevTools.Services;

public class ColorService : IColorService
{
    public Task<ColorConvertResult> ConvertColorAsync(ColorConvertRequest request)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 颜色转换");

            int r = 0, g = 0, b = 0;
            double a = 1.0;

            if (!string.IsNullOrEmpty(request.Hex))
            {
                (r, g, b, a) = HexToRgba(request.Hex);
            }
            else if (request.R.HasValue && request.G.HasValue && request.B.HasValue)
            {
                r = ClampByte(request.R.Value);
                g = ClampByte(request.G.Value);
                b = ClampByte(request.B.Value);
                a = request.Alpha ?? 1.0;
            }
            else if (request.H.HasValue && request.S.HasValue && request.L.HasValue)
            {
                (r, g, b) = HslToRgb(request.H.Value, request.S.Value, request.L.Value);
                a = request.Alpha ?? 1.0;
            }
            else
            {
                throw new ArgumentException("请提供至少一种颜色格式");
            }

            var (h, s, l) = RgbToHsl(r, g, b);
            var hex = RgbToHex(r, g, b);
            var hexWithAlpha = RgbaToHex(r, g, b, a);

            var result = new ColorConvertResult
            {
                Hex = hex,
                HexWithAlpha = hexWithAlpha,
                R = r,
                G = g,
                B = b,
                A = Math.Round(a, 2),
                H = Math.Round(h, 2),
                S = Math.Round(s, 2),
                L = Math.Round(l, 2),
                RgbString = $"rgb({r}, {g}, {b})",
                RgbaString = $"rgba({r}, {g}, {b}, {Math.Round(a, 2)})",
                HslString = $"hsl({Math.Round(h)}, {Math.Round(s)}%, {Math.Round(l)}%)",
                HslaString = $"hsla({Math.Round(h)}, {Math.Round(s)}%, {Math.Round(l)}%, {Math.Round(a, 2)})"
            };

            return Task.FromResult(result);
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 颜色转换失败: {0}", ex.Message);
            throw new ArgumentException("颜色转换失败: " + ex.Message, nameof(request), ex);
        }
    }

    public Task<ColorPaletteResult> GeneratePaletteAsync(string baseColor, int count = 5, string scheme = "analogous")
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 生成调色板，方案: {0}", scheme);

            var (r, g, b, _) = HexToRgba(baseColor);
            var (h, s, l) = RgbToHsl(r, g, b);

            var colors = new List<string>();
            count = Math.Clamp(count, 2, 12);

            switch (scheme?.ToLowerInvariant())
            {
                case "complementary":
                    colors = GenerateComplementary(h, s, l, count);
                    break;
                case "triadic":
                    colors = GenerateTriadic(h, s, l, count);
                    break;
                case "split-complementary":
                    colors = GenerateSplitComplementary(h, s, l, count);
                    break;
                case "monochromatic":
                    colors = GenerateMonochromatic(r, g, b, count);
                    break;
                case "analogous":
                default:
                    colors = GenerateAnalogous(h, s, l, count);
                    break;
            }

            return Task.FromResult(new ColorPaletteResult
            {
                BaseColor = baseColor,
                Scheme = scheme ?? "analogous",
                Colors = colors
            });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 生成调色板失败: {0}", ex.Message);
            throw new ArgumentException("生成调色板失败: " + ex.Message, nameof(baseColor), ex);
        }
    }

    public Task<ColorContrastResult> CheckContrastAsync(string foreground, string background)
    {
        try
        {
            XTrace.Log.Debug("[DevTools] 检查对比度");

            var (fr, fg, fb, _) = HexToRgba(foreground);
            var (br, bg, bb, _) = HexToRgba(background);

            var ratio = CalculateContrastRatio(fr, fg, fb, br, bg, bb);

            var result = new ColorContrastResult
            {
                Ratio = Math.Round(ratio, 2),
                AANormal = ratio >= 4.5,
                AALarge = ratio >= 3.0,
                AAANormal = ratio >= 7.0,
                AAALarge = ratio >= 4.5,
                Level = GetContrastLevel(ratio)
            };

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] 检查对比度失败: {0}", ex.Message);
            throw new ArgumentException("检查对比度失败: " + ex.Message);
        }
    }

    private static (int r, int g, int b, double a) HexToRgba(string hex)
    {
        if (string.IsNullOrEmpty(hex))
            throw new ArgumentException("颜色值不能为空");

        hex = hex.Trim().TrimStart('#');

        if (hex.Length == 3)
        {
            hex = new string(new[] { hex[0], hex[0], hex[1], hex[1], hex[2], hex[2] });
        }

        if (hex.Length == 6)
        {
            var r = Convert.ToInt32(hex[..2], 16);
            var g = Convert.ToInt32(hex.Substring(2, 2), 16);
            var b = Convert.ToInt32(hex.Substring(4, 2), 16);
            return (r, g, b, 1.0);
        }

        if (hex.Length == 8)
        {
            var r = Convert.ToInt32(hex[..2], 16);
            var g = Convert.ToInt32(hex.Substring(2, 2), 16);
            var b = Convert.ToInt32(hex.Substring(4, 2), 16);
            var a = Convert.ToInt32(hex.Substring(6, 2), 16) / 255.0;
            return (r, g, b, a);
        }

        throw new ArgumentException("无效的HEX颜色格式");
    }

    private static string RgbToHex(int r, int g, int b)
    {
        return $"#{r:X2}{g:X2}{b:X2}".ToLowerInvariant();
    }

    private static string RgbaToHex(int r, int g, int b, double a)
    {
        var alpha = ClampByte((int)Math.Round(a * 255));
        return $"#{r:X2}{g:X2}{b:X2}{alpha:X2}".ToLowerInvariant();
    }

    private static (double h, double s, double l) RgbToHsl(int r, int g, int b)
    {
        double rd = r / 255.0;
        double gd = g / 255.0;
        double bd = b / 255.0;

        var max = Math.Max(rd, Math.Max(gd, bd));
        var min = Math.Min(rd, Math.Min(gd, bd));
        var l = (max + min) / 2.0;

        if (max == min)
        {
            return (0, 0, l * 100);
        }

        var d = max - min;
        var s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);

        double h;
        if (max == rd)
        {
            h = ((gd - bd) / d + (gd < bd ? 6 : 0)) / 6.0;
        }
        else if (max == gd)
        {
            h = ((bd - rd) / d + 2) / 6.0;
        }
        else
        {
            h = ((rd - gd) / d + 4) / 6.0;
        }

        return (h * 360, s * 100, l * 100);
    }

    private static (int r, int g, int b) HslToRgb(double h, double s, double l)
    {
        h = h % 360 / 360.0;
        s = s / 100.0;
        l = l / 100.0;

        if (s == 0)
        {
            var v = ClampByte((int)Math.Round(l * 255));
            return (v, v, v);
        }

        double Hue2Rgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }

        var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        var p = 2 * l - q;

        var r = Hue2Rgb(p, q, h + 1.0 / 3);
        var g = Hue2Rgb(p, q, h);
        var b = Hue2Rgb(p, q, h - 1.0 / 3);

        return (ClampByte((int)Math.Round(r * 255)),
                ClampByte((int)Math.Round(g * 255)),
                ClampByte((int)Math.Round(b * 255)));
    }

    private static List<string> GenerateAnalogous(double h, double s, double l, int count)
    {
        var colors = new List<string>();
        var step = 30.0;
        var start = h - step * (count / 2);

        for (var i = 0; i < count; i++)
        {
            var hue = (start + step * i + 360) % 360;
            var (r, g, b) = HslToRgb(hue, s, l);
            colors.Add(RgbToHex(r, g, b));
        }

        return colors;
    }

    private static List<string> GenerateComplementary(double h, double s, double l, int count)
    {
        var colors = new List<string>();
        var complement = (h + 180) % 360;

        if (count >= 2)
        {
            var (r1, g1, b1) = HslToRgb(h, s, l);
            colors.Add(RgbToHex(r1, g1, b1));

            var (r2, g2, b2) = HslToRgb(complement, s, l);
            colors.Add(RgbToHex(r2, g2, b2));
        }

        if (count > 2)
        {
            var steps = (count - 2) / 2;
            for (var i = 1; i <= steps; i++)
            {
                var lightness = Math.Clamp(l + i * 10, 10, 90);
                var (r1, g1, b1) = HslToRgb(h, s, lightness);
                var (r2, g2, b2) = HslToRgb(complement, s, lightness);
                colors.Insert(0, RgbToHex(r1, g1, b1));
                colors.Add(RgbToHex(r2, g2, b2));
            }
        }

        return colors.Take(count).ToList();
    }

    private static List<string> GenerateTriadic(double h, double s, double l, int count)
    {
        var colors = new List<string>();
        var hues = new[] { h, (h + 120) % 360, (h + 240) % 360 };

        foreach (var hue in hues)
        {
            var (r, g, b) = HslToRgb(hue, s, l);
            colors.Add(RgbToHex(r, g, b));
        }

        if (count > 3)
        {
            for (var i = 1; colors.Count < count; i++)
            {
                var lightness = Math.Clamp(l + i * 15, 10, 90);
                foreach (var hue in hues)
                {
                    if (colors.Count >= count) break;
                    var (r, g, b) = HslToRgb(hue, s, lightness);
                    colors.Add(RgbToHex(r, g, b));
                }
            }
        }

        return colors.Take(count).ToList();
    }

    private static List<string> GenerateSplitComplementary(double h, double s, double l, int count)
    {
        var colors = new List<string>();
        var complement = (h + 180) % 360;
        var split1 = (complement + 150) % 360;
        var split2 = (complement + 210) % 360;

        var baseHues = new[] { h, split1, split2 };

        foreach (var hue in baseHues)
        {
            var (r, g, b) = HslToRgb(hue, s, l);
            colors.Add(RgbToHex(r, g, b));
        }

        if (count > 3)
        {
            for (var i = 1; colors.Count < count; i++)
            {
                var lightness = Math.Clamp(l + i * 15, 10, 90);
                foreach (var hue in baseHues)
                {
                    if (colors.Count >= count) break;
                    var (r, g, b) = HslToRgb(hue, s, lightness);
                    colors.Add(RgbToHex(r, g, b));
                }
            }
        }

        return colors.Take(count).ToList();
    }

    private static List<string> GenerateMonochromatic(int r, int g, int b, int count)
    {
        var colors = new List<string>();
        var (h, s, _) = RgbToHsl(r, g, b);
        var step = 80.0 / (count - 1);

        for (var i = 0; i < count; i++)
        {
            var lightness = 10 + step * i;
            var (cr, cg, cb) = HslToRgb(h, s, lightness);
            colors.Add(RgbToHex(cr, cg, cb));
        }

        return colors;
    }

    private static double CalculateContrastRatio(int fr, int fg, int fb, int br, int bg, int bb)
    {
        var l1 = CalculateRelativeLuminance(fr, fg, fb);
        var l2 = CalculateRelativeLuminance(br, bg, bb);

        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);

        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double CalculateRelativeLuminance(int r, int g, int b)
    {
        double rs = r / 255.0;
        double gs = g / 255.0;
        double bs = b / 255.0;

        rs = rs <= 0.03928 ? rs / 12.92 : Math.Pow((rs + 0.055) / 1.055, 2.4);
        gs = gs <= 0.03928 ? gs / 12.92 : Math.Pow((gs + 0.055) / 1.055, 2.4);
        bs = bs <= 0.03928 ? bs / 12.92 : Math.Pow((bs + 0.055) / 1.055, 2.4);

        return 0.2126 * rs + 0.7152 * gs + 0.0722 * bs;
    }

    private static string GetContrastLevel(double ratio)
    {
        if (ratio >= 7.0) return "AAA (优秀)";
        if (ratio >= 4.5) return "AA (正常)";
        if (ratio >= 3.0) return "AA Large (大文本)";
        return "Fail (不通过)";
    }

    private static int ClampByte(int value)
    {
        return Math.Clamp(value, 0, 255);
    }
}

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using NewLife;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>
/// 最近令牌（FR6 / §F）：输入一个值（颜色/长度/时长/阴影/字体族/字重）+ 可选的 property 线索，
/// 在 <see cref="TokenIndex"/> 的有效值令牌里找最近项，输出可直接替换的 `var(--ds-…)`。
/// 纯函数、确定性：同输入必同输出。审查引擎的建议与 `design_lookup kind=nearest` 共用本类。
/// </summary>
public sealed class NearestTokenFinder
{
    /// <summary>一条候选：path / tier / cssVar / value（令牌值）/ distance / exact / replace（可直接替换文本）/ category（length 无线索时）/ deprecated</summary>
    public sealed record Match(String Path, String Tier, String CssVar, String Value, Double Distance, Boolean Exact,
        String Replace, String? Category = null, Boolean Deprecated = false);

    /// <summary>查找结果：Error 非空 = 值不可解析；Matches 空 + Note = 无候选</summary>
    public sealed record FindResult(String? Error, IReadOnlyList<Match> Matches, String? Note);

    static readonly Dictionary<String, String> NamedHex = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aqua"] = "#00ffff", ["black"] = "#000000", ["blue"] = "#0000ff", ["fuchsia"] = "#ff00ff", ["gray"] = "#808080",
        ["green"] = "#008000", ["lime"] = "#00ff00", ["maroon"] = "#800000", ["navy"] = "#000080", ["olive"] = "#808000",
        ["orange"] = "#ffa500", ["purple"] = "#800080", ["red"] = "#ff0000", ["silver"] = "#c0c0c0", ["teal"] = "#008080",
        ["white"] = "#ffffff", ["yellow"] = "#ffff00",
    };

    static readonly Regex LenRegex = new(@"^-?\d+(\.\d+)?(px|rem|em)?$", RegexOptions.Compiled);
    static readonly Regex DurRegex = new(@"^\d+(\.\d+)?(ms|s)$", RegexOptions.Compiled);
    static readonly Regex WeightRegex = new(@"^(?:100|[1-8]00|900|400)$", RegexOptions.Compiled);

    public static FindResult Find(TokenIndex index, String? value, String? propertyHint, Int32 limit)
    {
        var v = (value ?? "").Trim();
        if (v.Length == 0)
            return new FindResult("无法识别的值 ，支持：颜色（hex/rgb/hsl/oklch）、长度、时长、阴影、字体族、字重", [], null);

        var hint = (propertyHint ?? "").Trim().ToLowerInvariant();

        // property 线索优先：线索明确到类就不做形状猜测（`600` 既像长度又像字重，靠线索消歧）
        if (hint.Contains("font-weight", StringComparison.Ordinal)) return FindWeight(index, v, limit);
        if (hint.Contains("font-family", StringComparison.Ordinal)) return FindFontFamily(index, v, limit);
        if (hint.Contains("duration", StringComparison.Ordinal) || hint.Contains("transition", StringComparison.Ordinal)
            || hint.Contains("animation", StringComparison.Ordinal)) return FindDuration(index, v, limit);
        if (hint.Contains("shadow", StringComparison.Ordinal)) return FindShadow(index, v, limit);
        // 长度类线索（先于颜色：border-radius 是长度不是颜色；border-color 才归颜色）
        if (hint.Contains("radius", StringComparison.Ordinal) || hint.Contains("padding", StringComparison.Ordinal)
            || hint.Contains("margin", StringComparison.Ordinal) || hint.Contains("gap", StringComparison.Ordinal)
            || hint.Contains("font-size", StringComparison.Ordinal) || hint.Contains("border-width", StringComparison.Ordinal)
            || hint == "border") return FindLength(index, v, hint, limit);
        if (hint.Contains("color", StringComparison.Ordinal) || hint.Contains("background", StringComparison.Ordinal)
            || hint.Contains("fill", StringComparison.Ordinal) || hint.Contains("stroke", StringComparison.Ordinal)
            || hint.Contains("accent", StringComparison.Ordinal) || hint.Contains("caret", StringComparison.Ordinal)
            || hint.Contains("outline", StringComparison.Ordinal)) return FindColor(index, v, limit);

        // 无线索：按值形状推断（颜色 → 长度 → 时长 → 字重 → 阴影 → 字体族）
        if (TryParseColor(v, out _)) return FindColor(index, v, limit);
        if (LenRegex.IsMatch(v)) return FindLength(index, v, hint, limit);
        if (DurRegex.IsMatch(v)) return FindDuration(index, v, limit);
        if (v == "normal" || v == "bold" || WeightRegex.IsMatch(v)) return FindWeight(index, v, limit);
        if (LooksLikeShadow(v)) return FindShadow(index, v, limit);
        return FindFontFamily(index, v, limit);
    }

    static FindResult Error(String v) =>
        new($"无法识别的值 {v}，支持：颜色（hex/rgb/hsl/oklch）、长度、时长、阴影、字体族、字重", [], null);

    #region 颜色

    static FindResult FindColor(TokenIndex index, String v, Int32 limit)
    {
        if (!TryParseColor(v, out var color)) return Error(v);
        var lab = Oklch.OklchToOklab(color);

        var matches = index.ColorCandidates()
            .Select(t => (T: t, C: ColorOf(t)))
            .Where(x => x.C != null)
            .Select(x =>
            {
                var tlab = Oklch.OklchToOklab(x.C!.Value);
                var d = Math.Round(Math.Sqrt(Sq(lab.A - tlab.A) + Sq(lab.B - tlab.B) + Sq(lab.L - tlab.L)), 4);
                return new Match(x.T.Path, x.T.Tier, ExportService.CssVarName(x.T.Path), HexOf(x.T), d,
                    d < 0.0005, "var(" + ExportService.CssVarName(x.T.Path) + ")", Deprecated: index.LifecycleOf(x.T.Path) == "deprecated");
            })
            .OrderBy(m => m.Distance)
            .ThenBy(m => m.Tier == TokenTiers.Semantic ? 0 : 1)      // semantic 先于 component
            .ThenBy(m => m.Deprecated ? 1 : 0)                        // deprecated 排后
            .ThenBy(m => m.Path, StringComparer.Ordinal)
            .Take(limit).ToList();

        return new FindResult(null, matches, matches.Count == 0 ? "该项目没有 semantic/component 颜色令牌" : null);
    }

    /// <summary>解析输入颜色：hex（3/4/6/8 位）、rgb(a)/hsl(a)（逗号或空格语法）、oklch()、17 个基础命名色。</summary>
    public static Boolean TryParseColor(String? text, out Oklch.Color color)
    {
        color = default;
        if (String.IsNullOrWhiteSpace(text)) return false;
        var s = text.Trim();

        if (NamedHex.TryGetValue(s, out var hex) && Oklch.TryParseRgb8(hex) is { } named)
        {
            color = Oklch.FromRgb8(named);
            return true;
        }

        if (s.StartsWith('#'))
        {
            var rgb = ParseHexRgb(s);
            if (rgb == null) return false;
            color = Oklch.FromRgb8(rgb.Value);
            return true;
        }
        if (s.StartsWith("rgb", StringComparison.OrdinalIgnoreCase) || s.StartsWith("hsl", StringComparison.OrdinalIgnoreCase))
        {
            var rgb = ParseFunctionColor(s);
            if (rgb == null) return false;
            color = Oklch.FromRgb8(rgb.Value);
            return true;
        }
        if (s.StartsWith("oklch", StringComparison.OrdinalIgnoreCase) && Oklch.ParseOklch(s) is { } c)
        {
            color = c;
            return true;
        }

        return false;
    }

    static Oklch.Rgb8? ParseHexRgb(String s)
    {
        var t = s.TrimStart('#');
        if (t.Length == 3) t = new String([t[0], t[0], t[1], t[1], t[2], t[2]], 0, 6);
        if (t.Length == 4) t = new String([t[0], t[0], t[1], t[1], t[2], t[2], t[3], t[3]], 0, 8);
        return Oklch.TryParseRgb8("#" + t);
    }

    static Oklch.Rgb8? ParseFunctionColor(String s)
    {
        var open = s.IndexOf('(');
        var close = s.LastIndexOf(')');
        if (open < 0 || close <= open) return null;
        var body = s[(open + 1)..close].Trim();
        if (body.Length == 0) return null;
        var isHsl = s.StartsWith("hsl", StringComparison.OrdinalIgnoreCase);

        // 逗号语法：rgb(124, 58, 237[, 0.5])；空格语法：rgb(124 58 237 / 0.5)
        var parts = body.Contains(',')
            ? body.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            : body.Split([' ', '/', '\t'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 3) return null;
        if (!TryComponent(parts[0], out var c0) || !TryComponent(parts[1], out var c1) || !TryComponent(parts[2], out var c2)) return null;

        if (isHsl) return HslToRgb(c0, c1, c2);
        return new Oklch.Rgb8(ClampByte(c0), ClampByte(c1), ClampByte(c2));
    }

    /// <summary>分量：0-255 数值或 0-100% 百分比；hsl 的 h 是 0-360 数值、s/l 是百分比</summary>
    static Boolean TryComponent(String raw, out Double value)
    {
        value = 0;
        var s = raw.Trim();
        if (s.EndsWith('%'))
        {
            if (!Double.TryParse(s[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var pct)) return false;
            value = pct; // 保留百分比语义由调用方解释（rgb 分量 / hsl 的 s/l）
            return true;
        }
        return Double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    static Oklch.Rgb8 HslToRgb(Double h, Double sPct, Double lPct)
    {
        var hh = ((h % 360.0) + 360.0) % 360.0 / 360.0;
        var s = Oklch.Clamp(sPct / 100.0, 0, 1);
        var l = Oklch.Clamp(lPct / 100.0, 0, 1);
        if (s <= 0) return new Oklch.Rgb8(ToByte(l), ToByte(l), ToByte(l));
        var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        var p = 2 * l - q;
        return new Oklch.Rgb8(ToByte(Hue2Rgb(p, q, hh + 1.0 / 3.0)), ToByte(Hue2Rgb(p, q, hh)), ToByte(Hue2Rgb(p, q, hh - 1.0 / 3.0)));
    }

    static Double Hue2Rgb(Double p, Double q, Double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2.0) return q;
        if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
        return p;
    }

    static Byte ToByte(Double v) => (Byte)Math.Round(Oklch.Clamp01(v) * 255.0, MidpointRounding.AwayFromZero);

    static Byte ClampByte(Double v) => (Byte)Math.Round(Oklch.Clamp(v, 0, 255), MidpointRounding.AwayFromZero);

    /// <summary>从 Snap 解析令牌颜色：优先 ColorHex（已是解析结果），回退原始值</summary>
    static Oklch.Color? ColorOf(ExportService.Snap t)
    {
        if (!t.ColorHex.IsNullOrEmpty() && Oklch.TryParseRgb8(t.ColorHex) is { } rgb) return Oklch.FromRgb8(rgb);
        return TryParseColor(t.Value, out var c) ? c : null;
    }

    static String HexOf(ExportService.Snap t)
    {
        if (!t.ColorHex.IsNullOrEmpty()) return t.ColorHex.ToLowerInvariant();
        return TryParseColor(t.Value, out var c) ? Oklch.ToRgb8(c).ToHex().ToLowerInvariant() : t.Value;
    }

    #endregion

    #region 长度

    static FindResult FindLength(TokenIndex index, String v, String hint, Int32 limit)
    {
        if (!TryParseLength(v, out var px)) return Error(v);
        var categories = CategoryFor(hint);
        var matches = categories.SelectMany(c => index.LengthCandidates(c).Select(t => (T: t, C: c)))
            .Select(x =>
            {
                if (!TryParseLength(x.T.Value, out var tv)) return (Match?)null;
                var d = Math.Abs(tv - px);
                return new Match(x.T.Path, x.T.Tier, ExportService.CssVarName(x.T.Path), x.T.Value, d, d == 0,
                    "var(" + ExportService.CssVarName(x.T.Path) + ")", Category: x.C, Deprecated: index.LifecycleOf(x.T.Path) == "deprecated");
            })
            .Where(m => m != null).Select(m => m!)
            .OrderBy(m => m.Distance)
            .ThenBy(m => CategoryRank(m.Category))                     // space → radius → size → border
            .ThenBy(m => m.Path, StringComparer.Ordinal)
            .Take(limit).ToList();
        return new FindResult(null, matches, matches.Count == 0 ? "该项目没有匹配的长度令牌" : null);
    }

    static Int32 CategoryRank(String? c) => c switch
    {
        "space" => 0,
        "radius" => 1,
        "size" => 2,
        "border" => 3,
        _ => 4,
    };

    static String[] CategoryFor(String hint)
    {
        if (hint.Contains("radius", StringComparison.Ordinal)) return ["radius"];
        if (hint.Contains("padding", StringComparison.Ordinal) || hint.Contains("margin", StringComparison.Ordinal)
            || hint.Contains("gap", StringComparison.Ordinal)) return ["space"];
        if (hint.Contains("font-size", StringComparison.Ordinal)) return ["size"];
        if (hint.Contains("border", StringComparison.Ordinal)) return ["border"];
        return ["space", "radius", "size", "border"];
    }

    public static Boolean TryParseLength(String? text, out Double px)
    {
        px = 0;
        if (String.IsNullOrWhiteSpace(text)) return false;
        var s = text.Trim();
        if (s.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            return Double.TryParse(s[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out px);
        if (s.EndsWith("rem", StringComparison.OrdinalIgnoreCase))
        {
            if (!Double.TryParse(s[..^3], NumberStyles.Float, CultureInfo.InvariantCulture, out var r)) return false;
            px = r * 16;
            return true;
        }
        if (s.EndsWith("em", StringComparison.OrdinalIgnoreCase))
        {
            if (!Double.TryParse(s[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var e)) return false;
            px = e * 16;
            return true;
        }
        // 无单位数字按 px（§F）
        return Double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out px);
    }

    #endregion

    #region 时长

    static FindResult FindDuration(TokenIndex index, String v, Int32 limit)
    {
        if (!TryParseDuration(v, out var ms)) return Error(v);
        var matches = index.DurationCandidates()
            .Select(t => TryParseDuration(t.Value, out var tv) ? (Match?)new Match(t.Path, t.Tier, ExportService.CssVarName(t.Path), t.Value,
                Math.Abs(tv - ms), Math.Abs(tv - ms) == 0, "var(" + ExportService.CssVarName(t.Path) + ")",
                Deprecated: index.LifecycleOf(t.Path) == "deprecated") : null)
            .Where(m => m != null).Select(m => m!)
            .OrderBy(m => m.Distance).ThenBy(m => m.Path, StringComparer.Ordinal)
            .Take(limit).ToList();
        return new FindResult(null, matches, matches.Count == 0 ? "该项目没有 duration 令牌" : null);
    }

    public static Boolean TryParseDuration(String? text, out Double ms)
    {
        ms = 0;
        if (String.IsNullOrWhiteSpace(text)) return false;
        var s = text.Trim();
        if (s.EndsWith("ms", StringComparison.OrdinalIgnoreCase))
            return Double.TryParse(s[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out ms);
        if (s.EndsWith("s", StringComparison.OrdinalIgnoreCase))
        {
            if (!Double.TryParse(s[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var sec)) return false;
            ms = sec * 1000;
            return true;
        }
        return false;
    }

    #endregion

    #region 阴影

    static FindResult FindShadow(TokenIndex index, String v, Int32 limit)
    {
        if (!TryParseShadow(v, out var first)) return Error(v);
        var matches = index.ShadowCandidates()
            .Select(t => FirstLayerOf(t.ValueJson) is { } l ? (Match?)new Match(t.Path, t.Tier, ExportService.CssVarName(t.Path), t.Value,
                DistanceShadow(first, l), DistanceShadow(first, l) == 0, "var(" + ExportService.CssVarName(t.Path) + ")",
                Deprecated: index.LifecycleOf(t.Path) == "deprecated") : null)
            .Where(m => m != null).Select(m => m!)
            .OrderBy(m => m.Distance).ThenBy(m => m.Path, StringComparer.Ordinal)
            .Take(limit).ToList();
        var note = matches.Count == 0 ? "该项目没有 shadow.elevation-* 令牌" : "只比较第一层";
        return new FindResult(null, matches, note);
    }

    /// <summary>box-shadow 第一层：[inset] x y blur [spread] color。层分隔符与 rgba() 参数都是逗号，
    /// 必须按「括号外的顶层逗号」切层（naive Split(',')[0] 会把 rgba(0,0,0,.2) 截成 rgba(0）。</summary>
    public static Boolean TryParseShadow(String? text, out (Boolean Inset, Double X, Double Y, Double Blur, Double Spread) layer)
    {
        layer = default;
        if (String.IsNullOrWhiteSpace(text)) return false;
        var first = FirstLayer(text);
        var parts = first.Split([' ', '\t'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4) return false;                 // 至少 x y blur color

        var inset = parts[0] == "inset";
        var vals = new List<Double>();
        foreach (var p in parts.Skip(inset ? 1 : 0).Take(parts.Length - (inset ? 2 : 1)))
        {
            if (!TryParseLength(p, out var px)) return false;
            vals.Add(px);
        }
        if (vals.Count < 2 || vals.Count > 4) return false;
        layer = (inset, vals[0], vals[1], vals.Count > 2 ? vals[2] : 0, vals.Count > 3 ? vals[3] : 0);
        return true;
    }

    /// <summary>阴影令牌 ValueJson 的第一层（形状与 ExportService.ShadowCss 同源：{inset,offsetX,offsetY,blur,spread,…}）</summary>
    static (Boolean Inset, Double X, Double Y, Double Blur, Double Spread)? FirstLayerOf(String? valueJson)
    {
        if (valueJson.IsNullOrEmpty()) return null;
        try
        {
            var arr = JsonNode.Parse(valueJson)?.AsArray();
            if (arr == null || arr.Count == 0 || arr[0]?.AsObject() is not JsonObject o) return null;
            var inset = o["inset"]?.GetValueKind() == JsonValueKind.True;
            return (inset, Dbl(o["offsetX"]), Dbl(o["offsetY"]), Dbl(o["blur"]), Dbl(o["spread"]));
        }
        catch (JsonException) { return null; }
    }

    static Double DistanceShadow((Boolean Inset, Double X, Double Y, Double Blur, Double Spread) a,
        (Boolean Inset, Double X, Double Y, Double Blur, Double Spread) b) =>
        (a.Inset != b.Inset ? 1000.0 : 0.0) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Blur - b.Blur) + Math.Abs(a.Spread - b.Spread);

    static Boolean LooksLikeShadow(String v)
    {
        if (v.Contains("inset", StringComparison.OrdinalIgnoreCase)) return true;
        var first = FirstLayer(v);
        var parts = first.Split([' ', '\t'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) return false;
        // 前 2-4 段是数值，末段是颜色 → 阴影
        var tail = parts[^1];
        var numerics = parts.Take(parts.Length - 1).ToList();
        return numerics.Count is >= 2 and <= 4 && numerics.All(p => TryParseLength(p, out _)) && TryParseColor(tail, out _);
    }

    /// <summary>取 box-shadow 第一层：只在括号深度 0 的逗号处分层（rgba()/hsl() 内部的逗号不算）</summary>
    static String FirstLayer(String text)
    {
        var depth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == ',' && depth == 0) return text[..i].Trim();
        }
        return text.Trim();
    }

    static Double Dbl(JsonNode? n)
    {
        if (n == null) return 0;
        return n.GetValueKind() == JsonValueKind.Number ? n.GetValue<Double>() : 0;
    }

    #endregion

    #region 字体族 / 字重

    static FindResult FindFontFamily(TokenIndex index, String v, Int32 limit)
    {
        var first = FirstFamily(v);
        // 族名只允许字母/数字/空格/连字符/下划线：`not-a-value!` 这类垃圾不该被当成字体族接受
        if (first.Length == 0 || !FamilyNameRegex.IsMatch(first)) return Error(v);
        // 等宽判定看整个值（`"Fira Code", monospace` 首族不含 mono，但整值是等宽栈）
        var isMono = v.ToLowerInvariant().Contains("mono", StringComparison.Ordinal);

        var matches = index.FontFamilyCandidates()
            .Select(t =>
            {
                var tf = FirstFamily(t.Value);
                if (tf.Length == 0) return (Match?)null;
                var d = tf == first ? 0 : 1;
                return new Match(t.Path, t.Tier, ExportService.CssVarName(t.Path), t.Value, d, d == 0,
                    "var(" + ExportService.CssVarName(t.Path) + ")");
            })
            .Where(m => m != null).Select(m => m!)
            .OrderBy(m => m.Distance)
            // 等宽输入优先 font.mono，非等宽输入优先 font.sans（并列时不再按字母序把 mono 排到 sans 前）
            .ThenBy(m => isMono ? (m.Path == "font.mono" ? 0 : 1) : (m.Path == "font.sans" ? 0 : 1))
            .ThenBy(m => m.Path, StringComparer.Ordinal)
            .Take(limit).ToList();
        return new FindResult(null, matches, matches.Count == 0 ? "该项目没有 font.* 令牌" : null);
    }

    static readonly Regex FamilyNameRegex = new(@"^[\p{L}\p{N} _-]+$", RegexOptions.Compiled);

    static String FirstFamily(String? v)
    {
        if (String.IsNullOrWhiteSpace(v)) return "";
        var f = v.Trim().Split(',')[0].Trim().Trim('"', '\'');
        return f.ToLowerInvariant();
    }

    static FindResult FindWeight(TokenIndex index, String v, Int32 limit)
    {
        if (!TryParseWeight(v, out var w)) return Error(v);
        var matches = index.FontWeightCandidates()
            .Select(t => TryParseWeight(t.Value, out var tw) ? (Match?)new Match(t.Path, t.Tier, ExportService.CssVarName(t.Path), t.Value,
                Math.Abs(tw - w), Math.Abs(tw - w) == 0, "var(" + ExportService.CssVarName(t.Path) + ")",
                Deprecated: index.LifecycleOf(t.Path) == "deprecated") : null)
            .Where(m => m != null).Select(m => m!)
            .OrderBy(m => m.Distance).ThenBy(m => m.Path, StringComparer.Ordinal)
            .Take(limit).ToList();
        return new FindResult(null, matches, matches.Count == 0 ? "该项目没有 weight.* 令牌" : null);
    }

    public static Boolean TryParseWeight(String? text, out Double weight)
    {
        weight = 0;
        if (String.IsNullOrWhiteSpace(text)) return false;
        var s = text.Trim().ToLowerInvariant();
        if (s == "normal") { weight = 400; return true; }
        if (s == "bold") { weight = 700; return true; }
        return Double.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out weight) && weight is >= 100 and <= 900;
    }

    #endregion

    static Double Sq(Double x) => x * x;
}

using System.Globalization;
using System.Text.Json;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>排版生成参数。</summary>
/// <param name="BasePx">正文基准字号</param>
/// <param name="Ratio">模块化比例（1.125/1.2/1.25/1.333/1.5/1.618）</param>
/// <param name="MinVw">流式排版视口下限 px</param>
/// <param name="MaxVw">流式排版视口上限 px</param>
/// <param name="Collapse">小视口压缩系数 0~1：越大则小屏字号越接近基准（层级越平）</param>
/// <param name="FontSans">正文字族栈</param>
/// <param name="FontMono">等宽字族栈</param>
public sealed record TypeOptions(
    Double BasePx = 16,
    Double Ratio = 1.25,
    Int32 MinVw = 320,
    Int32 MaxVw = 1280,
    Double Collapse = 0.35,
    String FontSans = "\"Inter\", \"Noto Sans SC\", system-ui, sans-serif",
    String FontMono = "\"JetBrains Mono\", ui-monospace, monospace");

/// <summary>
/// 排版生成：模块化比例（不是 v1 的写死 display 64→micro 12）+ 每档 line-height / letter-spacing
/// + fluid <code>clamp()</code>（小屏压层级、大屏展开）。
/// </summary>
public static class TypographyGenerator
{
    /// <summary>角色 → 比例指数（0 = 正文）与字族</summary>
    static readonly (String Role, Double Step, String Family)[] Roles =
    [
        ("display", 5, "sans"),
        ("h1", 4, "sans"),
        ("h2", 3, "sans"),
        ("h3", 2, "sans"),
        ("h4", 1, "sans"),
        ("h5", 0.5, "sans"),
        ("body", 0, "sans"),
        ("small", -1, "sans"),
        ("caption", -2, "sans"),
        ("overline", -2, "sans"),
        ("code", 0, "mono"),
        ("code-small", -1, "mono"),
    ];

    /// <summary>常用比例（供界面下拉选择，值即 ratio，标签含音程名）</summary>
    public static readonly (Double Ratio, String Label)[] PresetRatios =
    [
        (1.125, "Minor Second 1.125"), (1.2, "Major Second 1.2"), (1.25, "Minor Third 1.25"),
        (1.333, "Major Third 1.333"), (1.414, "Perfect Fourth 1.414"), (1.5, "Perfect Fifth 1.5"),
        (1.618, "Golden Ratio 1.618"),
    ];

    public static IReadOnlyList<TokenPatch> Generate(TypeOptions o)
    {
        var list = new List<TokenPatch>
        {
            new() { Path = "font.sans", Tier = TokenTiers.Primitive, Type = TokenTypes.FontFamily, Value = o.FontSans, Group = "font", Generator = TokenGenerators.Derived, Description = "正文字族栈" },
            new() { Path = "font.mono", Tier = TokenTiers.Primitive, Type = TokenTypes.FontFamily, Value = o.FontMono, Group = "font", Generator = TokenGenerators.Derived, Description = "等宽字族栈" },
            new() { Path = "weight.regular", Tier = TokenTiers.Primitive, Type = TokenTypes.FontWeight, Value = "400", Group = "weight", Generator = TokenGenerators.Derived, SortOrder = 1 },
            new() { Path = "weight.medium", Tier = TokenTiers.Primitive, Type = TokenTypes.FontWeight, Value = "500", Group = "weight", Generator = TokenGenerators.Derived, SortOrder = 2 },
            new() { Path = "weight.semibold", Tier = TokenTiers.Primitive, Type = TokenTypes.FontWeight, Value = "600", Group = "weight", Generator = TokenGenerators.Derived, SortOrder = 3 },
            new() { Path = "weight.bold", Tier = TokenTiers.Primitive, Type = TokenTypes.FontWeight, Value = "700", Group = "weight", Generator = TokenGenerators.Derived, SortOrder = 4 },
            new() { Path = "scale.ratio", Tier = TokenTiers.Primitive, Type = TokenTypes.Number, Value = Num(o.Ratio), Group = "scale", Generator = TokenGenerators.Derived, Description = "模块化比例" },
        };

        for (var i = 0; i < Roles.Length; i++)
        {
            var (role, step, family) = Roles[i];
            var maxPx = Round(Rem(o.BasePx * Math.Pow(o.Ratio, step)));
            // 小屏把层级向基准收拢：step 乘以 (1-collapse)，display 不会在手机上撑满整屏
            var minPx = Round(Rem(o.BasePx * Math.Pow(o.Ratio, step * (1 - Oklch.Clamp01(o.Collapse)))));
            var (slopeVw, interceptRem) = Fluid(minPx, maxPx, o.MinVw, o.MaxVw, o.BasePx);
            var lineHeight = LineHeight(maxPx);
            var tracking = Tracking(maxPx, role);
            var weight = WeightFor(role);
            var sizePath = $"size.{role}";

            list.Add(new TokenPatch
            {
                Path = sizePath,
                Tier = TokenTiers.Semantic,
                Type = TokenTypes.Dimension,
                Value = $"{Num(maxPx)}px",
                Group = "type",
                Generator = TokenGenerators.Derived,
                SortOrder = i,
                Description = $"clamp 视口 {o.MinVw}~{o.MaxVw}px；小屏 {Num(minPx)}px",
                Extensions = JsonSerializer.Serialize(new { fluid = new { minPx = Num(minPx), maxPx = Num(maxPx), slope = Num(slopeVw), intercept = Num(interceptRem) } }),
            });

            list.Add(new TokenPatch
            {
                Path = $"leading.{role}", Tier = TokenTiers.Primitive, Type = TokenTypes.Number,
                Value = Num(lineHeight), Group = "leading", Generator = TokenGenerators.Derived, SortOrder = i,
            });
            list.Add(new TokenPatch
            {
                Path = $"tracking.{role}", Tier = TokenTiers.Primitive, Type = TokenTypes.Dimension,
                Value = $"{Num(tracking)}em", Group = "tracking", Generator = TokenGenerators.Derived, SortOrder = i,
            });

            // 复合 typography 令牌：DTCG 的 typography 类型，可直接被组件层引用
            list.Add(new TokenPatch
            {
                Path = $"type.{role}", Tier = TokenTiers.Semantic, Type = TokenTypes.Typography,
                ValueJson = JsonSerializer.Serialize(new
                {
                    fontFamily = family == "mono" ? "{font.mono}" : "{font.sans}",
                    fontSize = $"{{{sizePath}}}",
                    fontWeight = weight,
                    lineHeight = Num(lineHeight),
                    letterSpacing = $"{Num(tracking)}em",
                    textTransform = role == "overline" ? "uppercase" : "none",
                }),
                Group = "type",
                Generator = TokenGenerators.Derived,
                SortOrder = i,
                Description = $"fluid: clamp({Num(minPx / o.BasePx)}rem, {Num(slopeVw)}vw + {Num(interceptRem)}rem, {Num(maxPx / o.BasePx)}rem)",
            });
        }

        return list;
    }

    /// <summary>大字更紧、小字更松：可读性靠行高换</summary>
    static Double LineHeight(Double px) => px switch
    {
        >= 40 => 1.08,
        >= 28 => 1.15,
        >= 20 => 1.25,
        >= 16 => 1.5,
        >= 13 => 1.6,
        _ => 1.7,
    };

    static Double Tracking(Double px, String role) => role switch
    {
        "overline" or "caption" => 0.04,
        _ when px >= 28 => -0.02,
        _ when px >= 20 => -0.01,
        _ => 0,
    };

    static Int32 WeightFor(String role) => role switch
    {
        "display" or "h1" or "h2" => 700,
        "h3" or "h4" => 600,
        "overline" or "caption" => 500,
        _ => 400,
    };

    static (Double SlopeVw, Double InterceptRem) Fluid(Double minPx, Double maxPx, Int32 minVw, Int32 maxVw, Double basePx)
    {
        var span = Math.Max(1, maxVw - minVw);
        var slope = (maxPx - minPx) / span * 100;                    // 视口每 1vw 涨多少 px
        var interceptPx = minPx - (maxPx - minPx) / span * minVw;     // 与 0vw 的交点
        return (Math.Round(slope, 3), Math.Round(interceptPx / basePx, 4));
    }

    /// <summary>取整到 0.5px 网格：字号带小数会在 Windows 上出现半像素抖字</summary>
    static Double Round(Double px) => Math.Round(px * 2, MidpointRounding.AwayFromZero) / 2;

    static Double Rem(Double px) => px;

    static String Num(Double v) => Math.Round(v, 4).ToString("0.####", CultureInfo.InvariantCulture);
}

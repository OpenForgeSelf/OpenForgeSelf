namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>尺度类生成参数（间距/圆角/描边/阴影/动效/断点/层级）。</summary>
/// <param name="Density">密度：comfortable=基准 8px，default=4px，compact=3px（同时缩放圆角与阴影）</param>
/// <param name="RadiusBase">圆角中点基准 px</param>
/// <param name="ShadowStrength">阴影整体强度 0~2</param>
/// <param name="MotionScale">动效时长倍率 0.5~2</param>
/// <param name="ShadowStyle">M3 阴影风格轴取值（`soft` = M3 之前的唯一做法，默认必须仍是它）</param>
/// <param name="BorderStrength">M3 描边强度轴取值（`regular` = 现状 1px/2px）</param>
public sealed record ScaleOptions(
    String Density = "default",
    Double RadiusBase = 6,
    Double ShadowStrength = 1,
    Double MotionScale = 1,
    String ShadowStyle = StyleAxes.ShadowStyleDefault,
    String BorderStrength = StyleAxes.BorderStrengthDefault);

/// <summary>
/// 非色彩尺度生成。**必须随参数变化**：v1 的 spacing/radius/shadow/motion 在
/// generate.ts:23-61 与 presets.ts:68-93 两处逐字节相同，任何设计系统拿到的都是同一套数，
/// 本类的输出以 Density/RadiusBase/ShadowStrength/MotionScale 为输入，两项目不同参数即得不同值。
/// </summary>
public static class ScaleGenerators
{
    /// <summary>密度 → 基准单位 px</summary>
    public static Double BaseUnit(String? density) => density switch
    {
        "compact" => 3,
        "comfortable" => 8,
        _ => 4,
    };

    /// <summary>间距档名与倍率（**审计的单调性检查也读这张表**：档位序只在这里定义一份，不在别处重列）</summary>
    public static readonly (String Name, Double Multiplier)[] SpaceSteps =
    [
        ("1", 0.5), ("2", 1), ("3", 1.5), ("4", 2), ("5", 3), ("6", 4), ("7", 6), ("8", 8), ("9", 12), ("10", 16),
    ];

    public static IReadOnlyList<TokenPatch> Spacing(ScaleOptions o)
    {
        var unit = BaseUnit(o.Density);
        return SpaceSteps.Select(s => new TokenPatch
        {
            Path = $"space.{s.Name}",
            Tier = TokenTiers.Primitive,
            Type = TokenTypes.Dimension,
            Value = FmtPx(unit * s.Multiplier),
            Group = "space",
            Generator = TokenGenerators.Derived,
            Description = $"{s.Name} 号间距 = 基准 {FmtPx(unit)} × {s.Multiplier}",
            SortOrder = Array.FindIndex(SpaceSteps, x => x.Name == s.Name),
        }).ToList();
    }

    /// <summary>圆角档名与系数（同上：档序的唯一定义处；`pill`/`full` 是"非尺度档"，不参与单调性检查）</summary>
    /// <summary>动效时长档（同一份表既生成也审计：单调性检查需要"谁是第几档"）</summary>
    public static readonly (String Name, Double Ms)[] DurationSteps =
    [
        ("micro", 120), ("base", 200), ("macro", 320), ("emphasized", 480),
    ];

    public static readonly (String Name, Double Factor)[] RadiusSteps =
    [
        ("xs", 0.35), ("sm", 0.65), ("md", 1), ("lg", 1.65), ("xl", 2.3), ("2xl", 3.3),
    ];

    /// <summary>圆角里两个**绝对值**档（不随基准/密度缩放），所以不在倍率表里，但必须在档名序里</summary>
    public const String RadiusPill = "pill";
    public const String RadiusFull = "full";

    /// <summary>
    /// 尺度档名的**完整**顺序（唯一真源）：`/meta` 出给前端的词表、以及"界面显示序"都读它。
    /// 为什么不能只给倍率表：`Radius()` 还会追加 pill / full 两个绝对值档，
    /// 词表漏了它们，界面就会把它们插在 `2xl` 前面（`stepOf("2xl")` 还会把 `2xl` 误读成数值 2）。
    /// </summary>
    public static readonly String[] RadiusOrder = [.. RadiusSteps.Select(r => r.Name), RadiusPill, RadiusFull];

    /// <summary>间距档名序（数值档 1..10）</summary>
    public static readonly String[] SpaceOrder = [.. SpaceSteps.Select(s => s.Name)];

    /// <summary>时长档名序（`-reduced` 派生档不在表内，由比较器退回字母序，仍排在基准档后）</summary>
    public static readonly String[] DurationOrder = [.. DurationSteps.Select(d => d.Name)];

    public static IReadOnlyList<TokenPatch> Radius(ScaleOptions o)
    {
        var list = RadiusSteps.Select(r => new TokenPatch
        {
            Path = $"radius.{r.Name}",
            Tier = TokenTiers.Primitive,
            Type = TokenTypes.Dimension,
            Value = FmtPx(Math.Round(o.RadiusBase * r.Factor * DensityScale(o.Density), 1)),
            Group = "radius",
            Generator = TokenGenerators.Derived,
            SortOrder = Array.FindIndex(RadiusSteps, x => x.Name == r.Name),
        }).ToList();

        list.Add(new TokenPatch { Path = $"radius.{RadiusPill}", Tier = TokenTiers.Primitive, Type = TokenTypes.Dimension, Value = "999px", Group = "radius", Generator = TokenGenerators.Derived, SortOrder = 90 });
        list.Add(new TokenPatch { Path = $"radius.{RadiusFull}", Tier = TokenTiers.Primitive, Type = TokenTypes.Dimension, Value = "9999px", Group = "radius", Generator = TokenGenerators.Derived, SortOrder = 91 });
        return list;
    }

    public static IReadOnlyList<TokenPatch> Border(ScaleOptions o)
    {
        var (hairline, thick) = StyleAxes.BorderFor(o.BorderStrength);
        return
        [
            new() { Path = "border.hairline", Tier = TokenTiers.Primitive, Type = TokenTypes.Dimension, Value = hairline, Group = "border", Generator = TokenGenerators.Derived, Description = "分隔线/描边默认宽" },
            new() { Path = "border.thick", Tier = TokenTiers.Primitive, Type = TokenTypes.Dimension, Value = thick, Group = "border", Generator = TokenGenerators.Derived, Description = "焦点环/强调描边宽" },
        ];
    }

    /// <summary>
    /// 阴影：按层展开（一层一行），亮色用 drop shadow、暗色用 inset 高光替代——
    /// 参考物 Stardust 在暗色主题下正是这么做的（colors_and_type.css:155-158），照抄其"暗色不靠黑投影"的判断。
    ///
    /// M3 起阴影有四种风格（<see cref="StyleAxes.ShadowStyleValues"/>）。**默认 `soft` 的算式与取整一字未改**：
    /// 阴影是"看起来是不是同一套系统"最强的信号，动它等于动存量项目的脸。其余三种各是一个明确取向——
    /// `crisp` 锐利单层、`flat` 环线无投影、`layered` 多层加环境光；`shadowStrength` 对四者都是 alpha 乘数。
    /// </summary>
    public static IReadOnlyList<ShadowToken> Shadows(ScaleOptions o, String themeCode)
    {
        var dark = SemanticResolver.IsDark(themeCode);
        var d = DensityScale(o.Density);
        var levels = new[] { 1, 2, 3, 4, 5 };
        var list = new List<ShadowToken>();

        foreach (var level in levels)
        {
            var layers = o.ShadowStyle switch
            {
                "crisp" => CrispLayers(level, d, o.ShadowStrength, dark),
                "flat" => FlatLayers(level, o.ShadowStrength, dark),
                "layered" => LayeredLayers(level, d, o.ShadowStrength, dark),
                _ => SoftLayers(level, d, o.ShadowStrength, dark),
            };
            var note = o.ShadowStyle switch
            {
                "crisp" => "锐利单层",
                "flat" => "环形描边（无投影）",
                "layered" => "多层 + 环境光",
                _ => dark ? "inset 高光 + 外投影" : "多层外投影",
            };
            list.Add(new ShadowToken($"elevation-{level}", layers, note));
        }
        return list;
    }

    /// <summary>现状（默认）阴影层：多层外投影，`level>=3` 补一层近距投影，暗色首层换 1px inset 白高光</summary>
    static List<ShadowLayerInput> SoftLayers(Int32 level, Double d, Double S, Boolean dark)
    {
        var y = Math.Round(level * 1.0 * d, 1);
        var blur = Math.Round(Math.Pow(level, 1.6) * 3 * d, 1);
        var spread = Math.Round(-level * 0.5, 1);
        var alpha = Oklch.Clamp(S * (0.05 + level * 0.045), 0, 0.45);

        var layers = new List<ShadowLayerInput>
        {
            new() { OffsetY = y, Blur = blur, Spread = spread, ColorValue = dark ? "#000000" : "#0f172a", Alpha = alpha },
        };
        if (!dark && level >= 3)
            layers.Add(new() { OffsetY = Math.Round(y / 2, 1), Blur = Math.Round(blur / 3, 1), ColorValue = "#0f172a", Alpha = alpha * 0.6 });
        if (dark)
            // 暗色用 1px inset 高光勾边代替外投影，避免"黑块叠黑块"看不出层次
            layers.Insert(0, new() { IsInset = true, OffsetY = 1, Blur = 0, ColorValue = "#ffffff", Alpha = 0.06, Usage = "暗色顶边高光" });

        return layers;
    }

    /// <summary>锐利：只留一层，模糊随 `L^1.35` 收敛（比 soft 更硬但不成描边），暗色仍先勾一道 inset 高光</summary>
    static List<ShadowLayerInput> CrispLayers(Int32 level, Double d, Double S, Boolean dark)
    {
        var alpha = Oklch.Clamp(S * (0.07 + 0.05 * level), 0, 0.5);
        var layers = new List<ShadowLayerInput>
        {
            new() { OffsetY = Math.Round(level * 1.0 * d, 1), Blur = Math.Round(Math.Pow(level, 1.35) * 1.8 * d, 1), Spread = 0, ColorValue = dark ? "#000000" : "#0f172a", Alpha = alpha },
        };
        if (dark)
            layers.Insert(0, new() { IsInset = true, OffsetY = 1, Blur = 0, ColorValue = "#ffffff", Alpha = 0.06, Usage = "暗色顶边高光" });
        return layers;
    }

    /// <summary>环线：不投影，用 1~2px 的实描边环表现层级（`y=0 blur=0`）；暗色用白环，不叠 inset 高光</summary>
    static List<ShadowLayerInput> FlatLayers(Int32 level, Double S, Boolean dark)
    {
        var alpha = Oklch.Clamp(S * (0.08 + 0.02 * level), 0, 0.3);
        return
        [
            new() { OffsetY = 0, Blur = 0, Spread = level <= 3 ? 1 : 2, ColorValue = dark ? "#ffffff" : "#0f172a", Alpha = alpha },
        ];
    }

    /// <summary>多层 = 现状的软阴影 + `level>=2` 再叠一层大范围低透明环境光（大浮层的"空气感"）</summary>
    static List<ShadowLayerInput> LayeredLayers(Int32 level, Double d, Double S, Boolean dark)
    {
        var layers = SoftLayers(level, d, S, dark);
        if (level < 2) return layers;

        var color = dark ? "#000000" : "#0f172a";
        layers.Add(new()
        {
            OffsetY = Math.Round(0.4 * level * d, 1),
            Blur = Math.Round(Math.Pow(level, 1.6) * 1.5 * d, 1),
            Spread = 0,
            ColorValue = color,
            Alpha = Oklch.Clamp(S * (0.03 + 0.02 * level), 0, 0.25),
            Usage = "环境光",
        });
        return layers;
    }

    public static IReadOnlyList<TokenPatch> Motion(ScaleOptions o)
    {
        var tokens = new List<TokenPatch>();

        var easings = new (String Name, Double[] Curve)[]
        {
            ("standard", [0.2, 0.0, 0.0, 1.0]),
            ("decelerate", [0.05, 0.7, 0.1, 1.0]),
            ("accelerate", [0.3, 0.0, 0.8, 0.15]),
            ("emphasized", [0.3, 0.0, 0.2, 1.0]),
        };
        for (var i = 0; i < easings.Length; i++)
        {
            var (name, curve) = easings[i];
            tokens.Add(new TokenPatch
            {
                Path = $"ease.{name}", Tier = TokenTiers.Primitive, Type = TokenTypes.CubicBezier,
                Value = $"[{Fmt(curve[0])}, {Fmt(curve[1])}, {Fmt(curve[2])}, {Fmt(curve[3])}]",
                ValueJson = System.Text.Json.JsonSerializer.Serialize(new { x = curve }),
                Group = "ease", Generator = TokenGenerators.Derived, SortOrder = i,
            });
        }

        var durations = DurationSteps;
        for (var i = 0; i < durations.Length; i++)
        {
            var (name, ms) = durations[i];
            var scaled = Math.Round(ms * o.MotionScale, 0);
            tokens.Add(new TokenPatch
            {
                Path = $"duration.{name}", Tier = TokenTiers.Primitive, Type = TokenTypes.Duration,
                Value = $"{Fmt(scaled)}ms", Group = "duration", Generator = TokenGenerators.Derived, SortOrder = i,
                Description = $"基准 {ms}ms × 动效倍率 {Fmt(o.MotionScale)}",
            });
            // reduced 变体：可达性要求"可关闭动画"，这里把它生成为令牌而不是文档提醒
            tokens.Add(new TokenPatch
            {
                Path = $"duration.{name}-reduced", Tier = TokenTiers.Primitive, Type = TokenTypes.Duration,
                Value = "0.01ms", Group = "duration", Generator = TokenGenerators.Derived, SortOrder = i,
                Description = "prefers-reduced-motion 下的替代时长",
            });
        }

        tokens.Add(new TokenPatch
        {
            Path = "transition.base", Tier = TokenTiers.Semantic, Type = TokenTypes.Transition,
            ValueJson = System.Text.Json.JsonSerializer.Serialize(new { duration = "{duration.base}", timing = "{ease.standard}", delay = "{duration.micro}" }),
            Group = "transition", Generator = TokenGenerators.Derived,
        });
        return tokens;
    }

    /// <summary>断点与层级进 primitive 层（参考物未令牌化这两类，属我们的补齐项）</summary>
    public static IReadOnlyList<TokenPatch> Breakpoints(ScaleOptions o)
    {
        int[] pts = [640, 768, 1024, 1280, 1536];
        var list = new List<TokenPatch>();
        for (var i = 0; i < pts.Length; i++)
            list.Add(new TokenPatch { Path = $"breakpoint.{i + 1}", Tier = TokenTiers.Primitive, Type = TokenTypes.Dimension, Value = $"{pts[i]}px", Group = "breakpoint", Generator = TokenGenerators.Derived, SortOrder = i, Description = $"min-width {pts[i]}px" });

        int[] zs = [0, 10, 20, 30, 40, 1000];
        for (var i = 0; i < zs.Length; i++)
            list.Add(new TokenPatch { Path = $"z-index.{i + 1}", Tier = TokenTiers.Primitive, Type = TokenTypes.Number, Value = zs[i].ToString(), Group = "z-index", Generator = TokenGenerators.Derived, SortOrder = i });

        return list;
    }

    static Double DensityScale(String? density) => density switch
    {
        "compact" => 0.8,
        "comfortable" => 1.2,
        _ => 1,
    };

    static String FmtPx(Double v) => v == Math.Truncate(v) ? $"{(Int32)v}px" : $"{Fmt(v)}px";

    static String Fmt(Double v) => Math.Round(v, 3).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>shadow 复合令牌：ValueJson 为真源，Layers 为 DesignShadowLayer 展开（二者必须一致，G6）</summary>
    public sealed record ShadowToken(String Name, IReadOnlyList<ShadowLayerInput> Layers, String Note);
}

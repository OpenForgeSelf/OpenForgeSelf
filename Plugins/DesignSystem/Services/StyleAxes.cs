using NewLife;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>
/// 风格轴词表的**唯一真源**（M3 §A1）：轴名、请求字段、取值、默认值、数值轴的范围都只在这里定义一份。
///
/// 为什么要有这个类：M2 之前的生成器只有色相/彩度/圆角基准/密度/字号比例/动效倍率可调，
/// 字体栈、阴影模型、描边、圆角映射、中性色取向全是写死的——于是"换十件衣服，轮廓还是同一个人"。
/// 加轴最容易犯的两个错：① 界面/工具各抄一份取值表（改一处漏两处）；② 新轴只是装饰（取值传进去、产物没变）。
/// 所以词表在这里单点定义，`GET meta.styleAxes` 原样输出，界面只渲染不另抄；
/// 每个取值必须真改产物由 <c>StyleAxisTests</c> 逐轴逐值钉住。
///
/// 约定：**每个枚举轴的首项 = 默认值**，传 null 等价于传默认值（默认路径逐字节兼容，见黄金基线）。
/// </summary>
public static class StyleAxes
{
    // ---- 轴名（meta 的 `axis` 字段与偏差/校验文案都用它，不用请求字段名当标识） ----
    public const String ShadowStyle = "shadowStyle";
    public const String ShadowStrength = "shadowStrength";
    public const String BorderStrength = "borderStrength";
    public const String NeutralTemp = "neutralTemp";
    public const String FontPairing = "fontPairing";
    public const String RadiusStyle = "radiusStyle";
    public const String AccentStrategy = "accentStrategy";

    /// <summary>轴序（meta 输出序与种子后缀序都按它；种子后缀里数值轴排最后，见 <see cref="SeedSuffixOrder"/></summary>
    public static readonly String[] Order = [ShadowStyle, BorderStrength, NeutralTemp, FontPairing, RadiusStyle, AccentStrategy, ShadowStrength];

    /// <summary>轴 → 请求字段名（前端按 field 拼请求体，不在 TS 里镜像一份）</summary>
    public static String FieldOf(String axis) => axis switch
    {
        ShadowStyle => "shadowStyle",
        ShadowStrength => "shadowStrength",
        BorderStrength => "borderStrength",
        NeutralTemp => "neutralTemp",
        FontPairing => "fontPairing",
        RadiusStyle => "radiusStyle",
        AccentStrategy => "accentStrategy",
        _ => throw new ArgumentException($"未知风格轴 {axis}"),
    };

    // ---- 各轴取值（首项 = 默认；默认值同时是 const，供 `ScaleOptions`/`TypeOptions` 的参数默认值引用） ----
    public const String ShadowStyleDefault = "soft";
    public const String BorderStrengthDefault = "regular";
    public const String NeutralTempDefault = "brand";
    public const String FontPairingDefault = "modern";
    public const String RadiusStyleDefault = "soft";
    public const String AccentStrategyDefault = "complement";

    public static readonly String[] ShadowStyleValues = [ShadowStyleDefault, "crisp", "flat", "layered"];
    public static readonly String[] BorderStrengthValues = [BorderStrengthDefault, "bold"];
    public static readonly String[] NeutralTempValues = [NeutralTempDefault, "cool", "warm", "pure"];
    public static readonly String[] FontPairingValues = [FontPairingDefault, "system", "humanist", "editorial"];
    public static readonly String[] RadiusStyleValues = [RadiusStyleDefault, "sharp", "round", "pill"];

    /// <summary>强调色策略取值（**首项 = 默认**；显式列序，`complement` 必须排第一，不靠字典枚举序）</summary>
    public static readonly String[] AccentStrategyValues = [AccentStrategyDefault, "analogous", "split", "triadic", "mono"];

    /// <summary>强调色策略 → 色相偏移（`complement` 就是既有默认偏移 168°）</summary>
    public static readonly IReadOnlyDictionary<String, Double> AccentStrategyOffsets =
        new Dictionary<String, Double>(StringComparer.Ordinal)
        {
            ["complement"] = 168,
            ["analogous"] = 30,
            ["split"] = 150,
            ["triadic"] = 120,
            ["mono"] = 0,
        };

    /// <summary>数值轴范围（越界夹取并写 Notes，不拒绝请求：滑杆上多走一格不该报错）</summary>
    public const Double ShadowStrengthMin = 0;
    public const Double ShadowStrengthMax = 2;
    public const Double ShadowStrengthDefault = 1;

    /// <summary>取值名的中文说法（Notes 文案与界面 tooltip 同源，不在前端另抄）</summary>
    static readonly Dictionary<String, String> ValueLabels = new(StringComparer.Ordinal)
    {
        ["soft-shadow"] = "柔和", ["crisp"] = "锐利", ["flat"] = "环线", ["layered"] = "多层",
        ["regular"] = "常规", ["bold"] = "加粗",
        ["brand"] = "品牌色温", ["cool"] = "冷调", ["warm"] = "暖调", ["pure"] = "纯中性",
        ["modern"] = "现代无衬线", ["system"] = "系统字体", ["humanist"] = "人文无衬线", ["editorial"] = "衬线标题",
        ["soft-radius"] = "柔和", ["sharp"] = "锐利", ["round"] = "圆润", ["pill"] = "胶囊",
        ["complement"] = "互补", ["analogous"] = "邻近", ["split"] = "分裂互补", ["triadic"] = "三角", ["mono"] = "单色",
    };

    /// <summary>轴 → 默认取值（枚举轴 = 取值首项；数值轴 = 1）</summary>
    public static String DefaultValueOf(String axis) => axis switch
    {
        ShadowStyle => ShadowStyleValues[0],
        BorderStrength => BorderStrengthValues[0],
        NeutralTemp => NeutralTempValues[0],
        FontPairing => FontPairingValues[0],
        RadiusStyle => RadiusStyleValues[0],
        AccentStrategy => AccentStrategyValues[0],
        ShadowStrength => "1",
        _ => throw new ArgumentException($"未知风格轴 {axis}"),
    };

    /// <summary>轴 → 枚举取值（数值轴返回 null，靠 min/max 描述）</summary>
    public static IReadOnlyList<String>? ValuesOf(String axis) => axis switch
    {
        ShadowStyle => ShadowStyleValues,
        BorderStrength => BorderStrengthValues,
        NeutralTemp => NeutralTempValues,
        FontPairing => FontPairingValues,
        RadiusStyle => RadiusStyleValues,
        AccentStrategy => AccentStrategyValues,
        ShadowStrength => null,
        _ => throw new ArgumentException($"未知风格轴 {axis}"),
    };

    /// <summary>轴 → 中文名（校验与 Notes 文案用）</summary>
    public static String LabelOf(String axis) => axis switch
    {
        ShadowStyle => "阴影风格",
        ShadowStrength => "阴影强度",
        BorderStrength => "描边强度",
        NeutralTemp => "中性色温",
        FontPairing => "字体搭配",
        RadiusStyle => "圆角风格",
        AccentStrategy => "强调色策略",
        _ => throw new ArgumentException($"未知风格轴 {axis}"),
    };

    /// <summary>
    /// `GET meta.styleAxes` 的输出：每轴一条 <c>{axis,field,kind,values?,default,min?,max?}</c>。
    /// 键序固定按 <see cref="Order"/>，界面按它排控件——加第七条轴只改本类。
    /// </summary>
    public static IReadOnlyList<IReadOnlyDictionary<String, Object?>> Vocabulary() =>
        Order.Select(axis =>
        {
            var values = ValuesOf(axis);
            var entry = new Dictionary<String, Object?>(StringComparer.Ordinal)
            {
                ["axis"] = axis,
                ["field"] = FieldOf(axis),
                ["kind"] = values == null ? "number" : "enum",
                ["label"] = LabelOf(axis),
                ["default"] = values == null ? ShadowStrengthDefault : DefaultValueOf(axis),
            };
            if (values != null)
            {
                entry["values"] = values;
                // 取值的中文说法一起给：界面显示"锐利/环线/多层"而不是英文档名，
                // 而机器值仍是 crisp/flat/…（写回请求用它们）。词典只在这里有一份
                entry["valueLabels"] = values.ToDictionary(v => v, v => ValueLabel(axis, v), StringComparer.Ordinal);
            }
            else
            {
                entry["min"] = ShadowStrengthMin;
                entry["max"] = ShadowStrengthMax;
                entry["step"] = 0.05;
            }
            return (IReadOnlyDictionary<String, Object?>)entry;
        }).ToList();

    /// <summary>取值的中文说法；界面 tooltip 与 Notes 同源（`soft` 在阴影/圆角两轴各译各的，键按轴前缀取，不撞名）</summary>
    public static String ValueLabel(String axis, String value)
    {
        var key = axis switch
        {
            ShadowStyle => ShadowLabel(value),
            RadiusStyle => RadiusLabel(value),
            _ => value,
        };
        return ValueLabels.TryGetValue(key, out var label) ? label : value;
    }

    // `soft` 在阴影与圆角两轴里各含义（都译"柔和"），但字典键不能撞名，故按轴前缀取词
    static String ShadowLabel(String value) => value == "soft" ? "soft-shadow" : value;
    static String RadiusLabel(String value) => value == "soft" ? "soft-radius" : value;

    /// <summary>请求里读出的原始取值（枚举轴）；null = 未传</summary>
    static String? Raw(String? value) => value.IsNullOrWhiteSpace() ? null : value.Trim();

    /// <summary>取出生效值：未传或传空 = 默认值（默认值即"现状"，生成器据此走原路径）</summary>
    public static String ShadowStyleOf(GenerationRequest req) => Effective(ShadowStyle, req.ShadowStyle);
    public static String BorderStrengthOf(GenerationRequest req) => Effective(BorderStrength, req.BorderStrength);
    public static String NeutralTempOf(GenerationRequest req) => Effective(NeutralTemp, req.NeutralTemp);
    public static String FontPairingOf(GenerationRequest req) => Effective(FontPairing, req.FontPairing);
    public static String RadiusStyleOf(GenerationRequest req) => Effective(RadiusStyle, req.RadiusStyle);
    public static String AccentStrategyOf(GenerationRequest req) => Effective(AccentStrategy, req.AccentStrategy);
    public static Double ShadowStrengthOf(GenerationRequest req) => req.ShadowStrength ?? ShadowStrengthDefault;

    public static String Effective(String axis, String? value)
    {
        var v = Raw(value);
        if (v == null) return DefaultValueOf(axis);
        var values = ValuesOf(axis) ?? throw new ArgumentException($"轴 {axis} 不是枚举轴");
        var hit = values.FirstOrDefault(x => x.Equals(v, StringComparison.OrdinalIgnoreCase));
        if (hit == null) throw new ArgumentException($"风格轴 {axis} 的取值 {v} 不在 [{String.Join(", ", values)}] 内");
        return hit;
    }

    /// <summary>
    /// 校验 + 夹取（§A10）：非法枚举取值抛 <see cref="ArgumentException"/>（REST 经 Guard 回 400）；
    /// 数值轴越界则**夹取并回一行 Notes**（夹取必须可见，否则用户以为强度生效了）。
    /// 返回"非默认取值"的说明行，供生成器直接进 Notes。
    /// </summary>
    public static List<String> Normalize(GenerationRequest req)
    {
        var notes = new List<String>();

        // 逐轴取生效值（枚举轴在此判非法，非法即抛）
        foreach (var (axis, raw) in new[]
                 {
                     (ShadowStyle, req.ShadowStyle), (BorderStrength, req.BorderStrength), (NeutralTemp, req.NeutralTemp),
                     (FontPairing, req.FontPairing), (RadiusStyle, req.RadiusStyle), (AccentStrategy, req.AccentStrategy),
                 })
        {
            var value = Effective(axis, raw);
            if (!IsDefault(axis, value)) notes.Add(StyleNote(axis, value));
        }

        var strength = req.ShadowStrength;
        if (strength != null)
        {
            var clamped = Oklch.Clamp(strength.Value, ShadowStrengthMin, ShadowStrengthMax);
            if (Math.Abs(clamped - strength.Value) > Double.Epsilon)
                notes.Add($"阴影强度 {strength.Value} 越界，已夹取到 {Num(clamped)}（可用范围 {Num(ShadowStrengthMin)}~{Num(ShadowStrengthMax)}）");
            req.ShadowStrength = clamped;
        }

        return notes;
    }

    /// <summary>该轴该值是否等价默认（默认 = 走 M3 之前的原路径，产物不得有任何变化）</summary>
    public static Boolean IsDefault(String axis, String? value)
    {
        var v = Raw(value);
        return v == null || v.Equals(DefaultValueOf(axis), StringComparison.OrdinalIgnoreCase);
    }

    public static Boolean IsDefault(String axis, Double? value) => value == null || Math.Abs(value.Value - ShadowStrengthDefault) < Double.Epsilon;

    static String StyleNote(String axis, String value) => $"风格轴：{LabelOf(axis)}={ValueLabel(axis, value)}（{value}）";

    /// <summary>强调色策略 → 色相偏移；策略优先于 <c>AccentHueOffset</c>（§A1）</summary>
    public static Double AccentOffsetOf(String strategy) =>
        AccentStrategyOffsets.TryGetValue(strategy, out var offset)
            ? offset
            : throw new ArgumentException($"风格轴 {AccentStrategy} 的取值 {strategy} 不在 [{String.Join(", ", AccentStrategyValues)}] 内");

    /// <summary>
    /// 种子后缀（§A10）：**只有存在非默认取值时**才在原 seed 串末尾追加 `;key=value`，
    /// 全默认时必须返回空串——种子进了每行令牌，多一个字符就意味着存量项目重生成会改值。
    /// </summary>
    public static String SeedSuffix(GenerationRequest req)
    {
        var parts = new List<String>();
        foreach (var axis in new[] { ShadowStyle, BorderStrength, NeutralTemp, FontPairing, RadiusStyle, AccentStrategy })
        {
            var raw = axis switch
            {
                ShadowStyle => req.ShadowStyle,
                BorderStrength => req.BorderStrength,
                NeutralTemp => req.NeutralTemp,
                FontPairing => req.FontPairing,
                RadiusStyle => req.RadiusStyle,
                _ => req.AccentStrategy,
            };
            var value = Effective(axis, raw);
            if (!IsDefault(axis, value)) parts.Add($";{SeedKey(axis)}={value}");
        }

        if (!IsDefault(ShadowStrength, req.ShadowStrength))
            parts.Add($";{SeedKey(ShadowStrength)}={Num(req.ShadowStrength!.Value)}");

        return string.Concat(parts);
    }

    /// <summary>
    /// 种子里的短键名。
    /// 偏差（登记于 03-plan）：03 §A10 举例用轴名（`;shadow=crisp`），但七个轴同时非默认时后缀 + 16 位种子
    /// 会超出 `DesignToken.GeneratorSeed` 的 100 列宽，而**改既有列属 Forbidden**。
    /// 这里改为一轴两字符的短键，**取值名一律保持契约原词**（`crisp/layered/editorial/analogous…`），
    /// 轴序仍按 §A10；完整轴名与中文说法在 `Notes` 里可见。守卫用例钉"任意轴组合下 seed ≤ 100"。
    /// </summary>
    static String SeedKey(String axis) => axis switch
    {
        ShadowStyle => "sh",
        BorderStrength => "bo",
        NeutralTemp => "ne",
        FontPairing => "fo",
        RadiusStyle => "ra",
        AccentStrategy => "ac",
        ShadowStrength => "st",
        _ => throw new ArgumentException($"未知风格轴 {axis}"),
    };

    /// <summary>
    /// agent 工具 JSON Schema 的轴属性片段（**运行时拼接，不是另抄一份词表**）：
    /// 工具的 `enum` 必须与 `GET meta.styleAxes` 同源，否则 agent 按 schema 传的取值会被后端判非法。
    /// 返回的是带真实引号的 JSON 片段（供字符串内插），顺序同 <see cref="Order"/>。
    /// </summary>
    public static String SchemaProperties() => string.Join(",", Order.Select(axis =>
    {
        var field = FieldOf(axis);
        var values = ValuesOf(axis);
        const String Q = "\"";
        var body = values == null
            ? $"\"{field}\":{{\"type\":\"number\",\"minimum\":{Num(ShadowStrengthMin)},\"maximum\":{Num(ShadowStrengthMax)},\"default\":{Num(ShadowStrengthDefault)},\"description\":\"{LabelOf(axis)} 0~2，缺省 1\"}}"
            : $"\"{field}\":{{\"type\":\"string\",\"enum\":[{string.Join(",", values.Select(v => Q + v + Q))}],\"default\":\"{DefaultValueOf(axis)}\",\"description\":\"{LabelOf(axis)}，缺省 {DefaultValueOf(axis)}\"}}";
        return body;
    }));

    /// <summary>字体搭配 → 三族栈（只用系统/通用字体栈，不分发字体文件；`editorial` 才有 display）</summary>
    public static (String Sans, String Mono, String? Display) FontsFor(String pairing) => pairing switch
    {
        "modern" => (FontStacks.Sans, FontStacks.Mono, null),
        "system" => ("system-ui, -apple-system, \"Segoe UI\", \"PingFang SC\", \"Microsoft YaHei\", sans-serif",
                     "ui-monospace, \"SF Mono\", Menlo, Consolas, monospace", null),
        "humanist" => ("\"Source Sans 3\", \"Noto Sans SC\", \"PingFang SC\", \"Segoe UI\", sans-serif",
                       "\"Source Code Pro\", ui-monospace, Menlo, Consolas, monospace", null),
        "editorial" => (FontStacks.Sans, FontStacks.Mono,
                        "\"Noto Serif SC\", \"Songti SC\", \"Source Han Serif SC\", Georgia, serif"),
        _ => throw new ArgumentException($"风格轴 {FontPairing} 的取值 {pairing} 不在 [{String.Join(", ", FontPairingValues)}] 内"),
    };

    /// <summary>中性色温 → (色相, 微染彩度)：`brand` 沿用品牌色相与现状 tint，`pure` 彩度 0</summary>
    public static (Double? Hue, Double Tint) NeutralFor(String temp, Double brandHue) => temp switch
    {
        "brand" => (null, 0.012),
        "cool" => (250, 0.014),
        "warm" => (75, 0.012),
        "pure" => (null, 0),
        _ => throw new ArgumentException($"风格轴 {NeutralTemp} 的取值 {temp} 不在 [{String.Join(", ", NeutralTempValues)}] 内"),
    };

    /// <summary>描边强度 → (hairline, thick) px</summary>
    public static (String Hairline, String Thick) BorderFor(String strength) => strength switch
    {
        "regular" => ("1px", "2px"),
        "bold" => ("2px", "3px"),
        _ => throw new ArgumentException($"风格轴 {BorderStrength} 的取值 {strength} 不在 [{String.Join(", ", BorderStrengthValues)}] 内"),
    };

    static String Num(Double v) => Math.Round(v, 4).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
}

namespace ForgeSelf.Api.Plugins.DesignSystem;

/// <summary>设计系统插件的契约常量（版本三元组 + 封闭枚举 + 保留 ID）。</summary>
public static class DesignSystemConstants
{
    /// <summary>插件 Id（与 plugin.json 一致）</summary>
    public const String PluginId = "design-system";

    /// <summary>
    /// 版本三元组的口径说明：三者**按发布同步递增**（e2e 与后端测试都断言 plugin.json.Version == 这三值，
    /// 界面徽标也显示它）。各自的"必须递增"触发条件：
    /// Model 改 Model.xml 结构 / Generator 改派生算法或生成落库内容 / Projection 改任一投影格式。
    /// 因为对外只有一个交付版本号，任一发生变化都足以要求整包递增，故不再各走各的。
    /// </summary>
    public const String ModelVersion = "2.8.0";

    /// <summary>生成器版本：改色阶/派生算法、或生成时落库的目录内容（组件种子、品牌资产）必须递增</summary>
    public const String GeneratorVersion = "2.8.0";

    /// <summary>导出投影版本：改任一投影格式必须递增，消费方据此判兼容</summary>
    public const String ProjectionVersion = "2.8.0";

    /// <summary>内置图标库的项目 ID 哨兵值：该库只读，任何写入必须被拒绝</summary>
    public const Int64 BuiltinProjectId = 0;

    /// <summary>跨主题共享层（通常为 primitive 基线）的主题 ID 哨兵值</summary>
    public const Int64 SharedThemeId = 0;

    /// <summary>内置图标集合名（本项目自绘，无第三方许可证负担）</summary>
    public const String BuiltinIconCollection = "forge";

    /// <summary>插件自带界面路由（真源 = plugin.json frontend.route；此处是运行时唯一引用点，不手写第二份）</summary>
    public const String FrontendRoute = "/design-system";

    /// <summary>别名链解析深度上限：超过即判为异常设计，直接报错而非继续深挖</summary>
    public const Int32 MaxAliasDepth = 16;
}

/// <summary>令牌层级（行业 3 层约定；mode 是轴不是层级）</summary>
public static class TokenTiers
{
    public const String Primitive = "primitive";
    public const String Semantic = "semantic";
    public const String Component = "component";

    /// <summary>层级序：数值越大越上层，别名只可指向同层或更低层</summary>
    public static Int32 Rank(String tier) => tier switch
    {
        Primitive => 1,
        Semantic => 2,
        Component => 3,
        _ => 0,
    };

    public static readonly String[] All = [Primitive, Semantic, Component];

    public static Boolean IsValid(String? tier) => tier != null && Array.IndexOf(All, tier) >= 0;
}

/// <summary>DTCG $type 封闭枚举。新增取值属契约变更（影响全部投影），须先出 ADR。</summary>
public static class TokenTypes
{
    public const String Color = "color";
    public const String Dimension = "dimension";
    public const String FontStyle = "fontStyle";
    public const String FontWeight = "fontWeight";
    public const String FontFamily = "fontFamily";
    public const String Duration = "duration";
    public const String CubicBezier = "cubicBezier";
    public const String Number = "number";
    public const String Text = "string";

    // 复合类型
    public const String Shadow = "shadow";
    public const String Border = "border";
    public const String Gradient = "gradient";
    public const String Typography = "typography";
    public const String Transition = "transition";
    public const String StrokeStyle = "strokeStyle";

    public static readonly String[] All =
    [
        Color, Dimension, FontStyle, FontWeight, FontFamily, Duration, CubicBezier, Number, Text,
        Shadow, Border, Gradient, Typography, Transition, StrokeStyle
    ];

    /// <summary>是否为 DTCG 规范定义的复合类型（值必须放 ValueJson）</summary>
    public static Boolean IsComposite(String? type) => type is Shadow or Border or Gradient or Typography or Transition;

    public static Boolean IsValid(String? type) => type != null && Array.IndexOf(All, type) >= 0;
}

/// <summary>令牌治理生命周期</summary>
public static class TokenLifecycles
{
    public const String Proposed = "proposed";
    public const String Adopted = "adopted";
    public const String Deprecated = "deprecated";
    public const String Removed = "removed";

    public static readonly String[] All = [Proposed, Adopted, Deprecated, Removed];
}

/// <summary>令牌来源：决定是否允许被重新生成覆盖</summary>
public static class TokenGenerators
{
    public const String Manual = "manual";
    public const String Ramp = "oklch-ramp";
    public const String Derived = "derived";
    public const String Imported = "imported";

    /// <summary>人工/导入的行默认不参与重新生成覆盖</summary>
    public static Boolean IsProtected(String? generator) => generator is Manual or Imported;
}

/// <summary>
/// 导入（回流）的格式面：**未实现的格式一律不声明**（与 `ExportFormats` 同一纪律——
/// 声明了却拿不出真解析器，界面就会长出一个点了才知是空壳的按钮）。
/// </summary>
public static class ImportFormats
{
    /// <summary>W3C DTCG 2025.10 —— 也是我们导出的格式，所以 round-trip 判据天然闭合</summary>
    public const String Dtcg = "dtcg";

    public static readonly String[] All = [Dtcg];

    public static Boolean IsKnown(String? format) => format != null && All.Contains(format, StringComparer.OrdinalIgnoreCase);
}

/// <summary>单次导入的硬上限：只在这里定义一次，控制器与 `/meta` 都读它（前端据此提示，不另抄一份）</summary>
public static class ImportLimits
{
    public const Int32 MaxEntries = 5000;
    public const Int32 MaxBytes = 4 * 1024 * 1024;
}

/// <summary>审计类别（可达性作为数据与门禁，不作散文）</summary>
public static class AuditKinds
{
    public const String Contrast = "contrast";
    public const String Focus = "focus";
    public const String ReducedMotion = "reduced-motion";
    public const String TierViolation = "tier-violation";
    public const String Orphan = "orphan";
    public const String Unused = "unused";
    public const String Alias = "alias";

    /// <summary>交互构件最小可点区域（WCAG 2.2 2.5.8）</summary>
    public const String TargetSize = "target-size";

    /// <summary>尺度族按声明序必须单调（手改出一个逆序档 = 整套尺度不可用）</summary>
    public const String Ramp = "ramp-monotonic";

    /// <summary>路径形状（点分 kebab）：形状不对，CSS/DTCG 投影就静默失效</summary>
    public const String Naming = "naming";

    /// <summary>仍被引用的 deprecated / removed 令牌</summary>
    public const String Lifecycle = "lifecycle-ref";

    /// <summary>
    /// 审计类别的**展示序**（唯一真源）：`GET /meta.auditKinds` 把它出给界面，审计板按它排下拉，
    /// 前端不再另抄一份常量数组。次序本身是"先阻断级、后提示级"，不是字母序。
    /// </summary>
    public static readonly String[] All =
    [
        Contrast, Alias, TierViolation, Focus, ReducedMotion, TargetSize, Ramp, Naming, Lifecycle, Orphan, Unused
    ];
}

/// <summary>
/// primitive 色族与**声明序**（唯一真源）：<see cref="DesignGenerator"/> 按这张表逐族产阶，
/// 少产一族会被测试当场抓住；界面（色彩实验室 / 令牌总览的色阶条带）读 `GET /meta.colorFamilies` 排同一批族。
/// 之前是生成器里七行 `ramps["brand"] = …` 加前端一份手抄的 `['brand','accent',…]` —— 两份顺序一致纯属巧合。
/// </summary>
public static class ColorFamilies
{
    public const String Brand = "brand";
    public const String Accent = "accent";
    public const String Neutral = "neutral";
    public const String Success = "success";
    public const String Warning = "warning";
    public const String Danger = "danger";
    public const String Info = "info";

    /// <summary>品牌色在前、中性色居中、状态色在后：设计师按这条线检查色阶，不是任意序</summary>
    public static readonly String[] All = [Brand, Accent, Neutral, Success, Warning, Danger, Info];
}

/// <summary>主题模式轴</summary>
public static class ThemeModeKinds
{
    public const String Color = "color";
    public const String Density = "density";
    public const String Brand = "brand";

    /// <summary>mode 轴的合法取值：写错一个字母就会多出一条"看起来能用但没人处理"的轴</summary>
    public static readonly String[] All = [Color, Density, Brand];
}

/// <summary>项目状态</summary>
public static class ProjectStatus
{
    public const String Draft = "draft";
    public const String Published = "published";
    public const String Archived = "archived";
}

/// <summary>
/// 变体轴与交互态的**档位序**（唯一真源）：生成器按它认轴，投影按它排序。
/// 为什么要这张表：产物里曾输出 `size = lg / md / sm`、`状态 = active、default、disabled、hover` —— 那是字母序凑的，
/// 读起来像随机；而设计师检查的恰恰是"档位从最小往上、状态从默认往后"。顺序错了，规格就只是"有"而不是"能用"。
/// 表外的轴/值一律退回字母序：没证据的顺序不编造。
/// </summary>
public static class VariantAxes
{
    public const String Size = "size";
    public const String Role = "role";
    public const String State = "state";

    static readonly Dictionary<String, String[]> Orders = new(StringComparer.Ordinal)
    {
        [Size] = ["xs", "sm", "md", "lg", "xl"],
        [Role] = ["primary", "secondary", "danger", "ghost", "link"],
        // default 在前，其余与建矩阵用的后缀序**同源同序**（改这里等于改矩阵生成顺序，不是排版问题）
        [State] = ["default", "hover", "active", "focus-visible", "focus", "disabled", "pressed"],
    };

    /// <summary>该轴的档位序；表外的轴返回空数组 = 调用方退回字母序</summary>
    public static IReadOnlyList<String> OrderOf(String axis) => Orders.TryGetValue(axis, out var o) ? o : [];

    /// <summary>一条轴及其档位序（/meta 出给界面做"先选轴再选值"的下拉）</summary>
    public sealed record AxisVocabulary(String Axis, IReadOnlyList<String> Values);

    /// <summary>
    /// 全部轴 + 档位序，**顺序 = 这张表的声明序**（size→role→state），界面下拉照它排。
    /// 为什么出清单而不是出 `stateOrder`/`sizeOrder`/`roleOrder` 三个字段：那等于把同一张表在契约里抄三遍，
    /// 加第四条轴时必然漏改一处；一份 `variantAxes` 清单，加轴只改 `Orders` 一处。
    /// </summary>
    public static IReadOnlyList<AxisVocabulary> Vocabulary() =>
        [.. Orders.Select(kv => new AxisVocabulary(kv.Key, kv.Value))];

    /// <summary>某值是否属于该轴（生成器认轴与投影排序共用一张表，不各写一份字面量）</summary>
    public static Boolean Has(String axis, String value) => OrderOf(axis).Contains(value, StringComparer.Ordinal);

    /// <summary>某值在该轴里的档位序号；表外的值返回 <see cref="Int32.MaxValue"/>（排序时落到最后）</summary>
    public static Int32 Rank(String axis, String value)
    {
        if (!Orders.TryGetValue(axis, out var o)) return Int32.MaxValue;
        var i = Array.IndexOf(o, value);
        return i < 0 ? Int32.MaxValue : i;
    }

    /// <summary>按档位序排一批值：表内的按档位序，表外的排到最后按字母序（稳定、可复现）</summary>
    public static List<String> Sort(String axis, IEnumerable<String> values) =>
        values.Distinct(StringComparer.Ordinal)
            .OrderBy(v => Rank(axis, v))
            .ThenBy(v => v, StringComparer.Ordinal)
            .ToList();
}

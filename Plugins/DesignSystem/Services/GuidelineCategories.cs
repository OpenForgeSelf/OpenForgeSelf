using NewLife;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>
/// UX 规范的分类/规则级别/状态/来源词表（唯一真源）。
///
/// 与 <see cref="StyleAxes"/> 同一条纪律：分类清单只在这里定义一份，
/// 经 <c>GET meta.guidelineCategories</c> 供给界面，工具与 REST 都按它校验；
/// 任何一处再列一份 `layout|page|…` 就是第二份真相（后端加了分类，界面筛不到）。
/// </summary>
public static class GuidelineCategories
{
    /// <summary>全部分类（顺序 = 界面分组顺序 = `Default` 归属顺序）</summary>
    public static readonly String[] All =
    [
        "layout", "page", "navigation", "form", "feedback", "state", "data", "content", "a11y", "motion",
    ];

    /// <summary>规则级别（MUST 进 agent 审查清单与 brief；SHOULD/MAY 只进完整规范）</summary>
    public static readonly String[] Levels = ["MUST", "SHOULD", "MAY"];

    /// <summary>状态：adopted 采纳 / draft 草稿 / archived 归档（归档 = 软删，本插件不提供删除，铁律 10）</summary>
    public static readonly String[] Statuses = ["adopted", "draft", "archived"];

    /// <summary>来源：generated 生成器产出 / manual 用户手改（manual 受重新生成保护）</summary>
    public static readonly String[] Sources = ["generated", "manual"];

    /// <summary>分类的中文显示名（界面与 design-md 共用一份，不在前端另译）</summary>
    static readonly Dictionary<String, String> DisplayNames = new(StringComparer.Ordinal)
    {
        ["layout"] = "布局与栅格",
        ["page"] = "页面模式",
        ["navigation"] = "导航",
        ["form"] = "表单与输入",
        ["feedback"] = "反馈与提示",
        ["state"] = "状态（空/加载/错误）",
        ["data"] = "数据展示",
        ["content"] = "文案与术语",
        ["a11y"] = "可达性",
        ["motion"] = "动效",
    };

    public static Boolean Has(String? category) => !category.IsNullOrEmpty() && All.Contains(category, StringComparer.Ordinal);
    public static Boolean HasLevel(String? level) => !level.IsNullOrEmpty() && Levels.Contains(level, StringComparer.OrdinalIgnoreCase);
    public static Boolean HasStatus(String? status) => !status.IsNullOrEmpty() && Statuses.Contains(status, StringComparer.Ordinal);
    public static Boolean HasSource(String? source) => !source.IsNullOrEmpty() && Sources.Contains(source, StringComparer.Ordinal);

    /// <summary>分类显示名；未知分类原样返回（不编造没证据的中文名）</summary>
    public static String Display(String category) => DisplayNames.GetValueOrDefault(category, category);

    /// <summary>规范标识的形：小写字母数字开头的 kebab，1~60 位（与 `Code` 列 100 宽留余量）</summary>
    public const String CodePattern = "^[a-z0-9][a-z0-9-]{0,59}$";

    /// <summary>规模上限（超限一律 400 并写明上限，不静默截断）</summary>
    public const Int32 MaxPerProject = 200;
    public const Int32 MaxRulesPerGuideline = 30;
    public const Int32 MaxBodyLength = 4000;

    public static Boolean IsCode(String? code) =>
        !code.IsNullOrEmpty() && System.Text.RegularExpressions.Regex.IsMatch(code, CodePattern);

    /// <summary>
    /// <c>design_edit action=guideline</c> 的入参 schema 片段（与 <see cref="StyleAxes.SchemaProperties"/> 同一条规矩）：
    /// 分类/状态/级别枚举一律由本词表生成，不在工具索引里手抄第二份 —— 加一个分类只需要改这里，
    /// agent 侧看到的枚举与后端校验用的枚举永远一致（AC9/AC18 同源断言）。
    /// </summary>
    public static String SchemaProperties()
    {
        const String Q = "\"";
        static String Enum(String[] values) => "[" + string.Join(",", values.Select(v => Q + v + Q)) + "]";

        return string.Join(",",
            $"\"code\":{{\"type\":\"string\",\"pattern\":\"{CodePattern}\",\"description\":\"规范标识（项目内唯一，kebab）；action=guideline 必填\"}}",
            $"\"title\":{{\"type\":\"string\",\"maxLength\":100,\"description\":\"规范标题\"}}",
            $"\"summary\":{{\"type\":\"string\",\"maxLength\":500,\"description\":\"一句话摘要\"}}",
            $"\"body\":{{\"type\":\"string\",\"maxLength\":{MaxBodyLength},\"description\":\"规范正文：只写反引号令牌路径，不写数字\"}}",
            "\"rules\":{\"type\":\"array\",\"maxItems\":" + MaxRulesPerGuideline + ",\"items\":{\"type\":\"object\",\"properties\":{\"id\":{\"type\":\"string\",\"description\":\"规则 id，小写 kebab；缺省 <code>-<序号>\"},\"level\":{\"type\":\"string\",\"enum\":" + Enum(Levels) + "},\"text\":{\"type\":\"string\",\"description\":\"规则文本，同样只写令牌路径\"}},\"required\":[\"level\",\"text\"]}}",
            "\"tokens\":{\"type\":\"array\",\"items\":{\"type\":\"string\"},\"description\":\"引用的令牌路径；不存在的路径会被拒并逐条列出\"}",
            $"\"category\":{{\"type\":\"string\",\"enum\":{Enum(All)},\"description\":\"规范分类，缺省 {All[0]}\"}}",
            $"\"status\":{{\"type\":\"string\",\"enum\":{Enum(Statuses)},\"description\":\"状态；archived=软删（本插件不提供删除）\"}}");
    }
}

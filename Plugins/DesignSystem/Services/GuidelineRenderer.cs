using System.Text;
using System.Text.RegularExpressions;
using NewLife;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>渲染后的一条规则（<see cref="Value"/> 是括注后的文本，<see cref="Broken"/> 说明它引用的令牌没了）</summary>
public sealed record RenderedRule(String Id, String Level, String Text, Boolean Broken);

/// <summary>
/// 规范渲染器（§G4）：**brief / design-md / bundle / REST / 界面共用这一个取值与标注函数**。
///
/// 为什么必须只有一个：规范文本里写的是令牌路径（D5），"这条规范到底说多少"取决于当前令牌值。
/// 一旦有第二个地方自己查值，两边就会在不同时刻读到不同的数 —— 本仓反复出现的"第二份真相"就是这个形状。
///
/// 取值由调用方以 <c>valueOf</c> 注入（通常来自 <see cref="TokenGraph"/> 或 <c>tokens/effective</c>），
/// 因此"界面 chip 上显示的值"与"导出里括注的值"天然是同一个函数的两次调用。
/// </summary>
public static class GuidelineRenderer
{
    /// <summary>正文里的反引号路径。带 <c>*</c> 的是族引用（`space.*`），不做取值括注</summary>
    static readonly Regex TokenPath = new(@"`([a-z][a-z0-9.-]*)`", RegexOptions.Compiled);

    static readonly Regex FamilyPath = new(@"\*$", RegexOptions.Compiled);

    /// <summary>该路径是否是要括注的具体令牌（族引用不算）</summary>
    public static Boolean IsConcreteRef(String path) => !FamilyPath.IsMatch(path);

    /// <summary>
    /// 给文本里的每个具体令牌路径括注当前值：<c>`space.6`</c> → <c>`space.6`（24px）</c>；
    /// 取不到值 → <c>`space.6`（令牌已不存在）</c> 并把路径收进 <paramref name="broken"/>（不静默丢）。
    /// </summary>
    public static String Annotate(String? text, Func<String, String?> valueOf, ICollection<String>? broken = null)
    {
        if (text.IsNullOrEmpty()) return text ?? "";
        return TokenPath.Replace(text!, m =>
        {
            var path = m.Groups[1].Value;
            if (!IsConcreteRef(path)) return m.Value;
            var value = valueOf(path);
            if (value.IsNullOrWhiteSpace())
            {
                broken?.Add(path);
                return $"{m.Value}（令牌已不存在）";
            }
            return $"{m.Value}（{value}）";
        });
    }

    /// <summary>一条规范里引用了哪些具体令牌路径（与 TokenRefs 无关，纯按文本扫，用于 brokenRefs 兜底）</summary>
    public static List<String> ConcreteRefs(String? text)
    {
        if (text.IsNullOrEmpty()) return [];
        return TokenPath.Matches(text!).Select(m => m.Groups[1].Value)
            .Where(IsConcreteRef)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>渲染一条规则（完整形态：括注当前值）。Broken = 本条引用的令牌里有取不到值的</summary>
    public static RenderedRule Render(GuidelineRule rule, Func<String, String?> valueOf, ICollection<String>? broken = null)
    {
        var refs = ConcreteRefs(rule.Text);
        var missing = refs.Where(p => valueOf(p).IsNullOrWhiteSpace()).ToList();
        foreach (var m in missing) broken?.Add(m);
        return new RenderedRule(rule.Id, rule.Level, Annotate(rule.Text, valueOf), missing.Count > 0);
    }

    /// <summary>
    /// 紧凑形态（brief 用）：标题 + 摘要 + 全部 MUST。预算紧，SHOULD/MAY 交给 design-md 与 bundle。
    /// </summary>
    public static String RenderCompact(String code, String title, String? summary, IEnumerable<GuidelineRule> rules, Func<String, String?> valueOf)
    {
        var musts = rules.Where(r => r.Level.Equals("MUST", StringComparison.OrdinalIgnoreCase)).ToList();
        var sb = new StringBuilder();
        sb.Append($"### {title}（{code}）");
        if (!summary.IsNullOrWhiteSpace()) sb.Append($" —— {Annotate(summary, valueOf)}");
        sb.AppendLine();
        foreach (var r in musts)
            sb.AppendLine($"- [ ] {Annotate(r.Text, valueOf)}");
        return sb.ToString().TrimEnd();
    }

    /// <summary>完整形态（design-md / bundle 的 GUIDELINES.md 用）：标题 + 摘要 + 全部规则 + 引用令牌 + 正文</summary>
    public static String RenderFull(String code, String category, String title, String? summary, String? body,
        IEnumerable<GuidelineRule> rules, IEnumerable<String> tokenRefs, Func<String, String?> valueOf)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"### {title}（{code}）");
        sb.AppendLine($"分类：{GuidelineCategories.Display(category)}");
        if (!summary.IsNullOrWhiteSpace()) sb.AppendLine(Annotate(summary, valueOf));
        sb.AppendLine();
        foreach (var r in rules)
            sb.AppendLine($"- **{r.Level}** {Annotate(r.Text, valueOf)}");

        var refs = tokenRefs.Where(p => !p.IsNullOrEmpty()).Distinct(StringComparer.Ordinal).ToList();
        if (refs.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("引用令牌：" + string.Join("、", refs.Select(p => $"`{p}`＝{Show(valueOf(p))}")));
        }

        if (!body.IsNullOrWhiteSpace())
        {
            sb.AppendLine();
            sb.AppendLine(Annotate(body, valueOf));
        }

        return sb.ToString().TrimEnd();

        static String Show(String? v) => v.IsNullOrWhiteSpace() ? "令牌已不存在" : v!;
    }
}

using System.Text.Json;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>
/// 设计系统审计引擎：把可达性与分层纪律变成**落库的数据 + 发布门禁**，而不是 README 里的散文。
///
/// 审计类（DesignAudit.Kind）：
/// - contrast：语义/组件色对按 WCAG 2.2 实测（AA 4.5 / 大字与非文本 3.0），未达 = critical；
/// - alias：别名环、悬空、逆向 —— 未达 = critical；
/// - tier-violation：component 色令牌直连 primitive（跳过 semantic）= warning；
/// - focus：:focus-visible 令牌是否齐备（WCAG 2.4.7/2.4.11）= 缺失 critical；
/// - reduced-motion：是否有 duration.*-reduced 派生（WCAG 2.3.3 精神）= 缺失 warning；
/// - unused / orphan：未被任何上层引用的 semantic / primitive = info~warning。
///
/// 后面四类（v2.6.0）是为"**对用户手改之后仍然成立**"而加的：
/// 生成器的正确性由 xUnit 钉，但库里的值谁都能改 —— 门禁若只覆盖颜色与别名，
/// 把 `space.6` 改成比 `space.5` 小、把按钮 `min-height` 改成 18px、写出一条带空格的 path，
/// 审计都会安静地放行，那门禁就只是生成器的自检，不是设计系统的门禁。
/// - target-size：`*.min-height` 低于 24px（WCAG 2.2 2.5.8）= warning（该条本身有例外，不据此拦发布）；
/// - ramp-monotonic：space/radius/duration 按声明序必须递增 = warning；
/// - naming：path 必须是点分 kebab，否则 CSS 变量名与 DTCG 树静默产出用不了的键 = warning；
/// - lifecycle-ref：仍被别名引用的 deprecated/removed 令牌 = warning。
/// </summary>
public sealed class AuditEngine
{
    readonly TokenRepository _tokens;
    readonly DesignProjectService _projects;
    readonly AuditRepository _audits;

    public AuditEngine(TokenRepository tokens, DesignProjectService projects, AuditRepository audits)
    {
        _tokens = tokens;
        _projects = projects;
        _audits = audits;
    }

    /// <summary>要成对检的语义角色（对 surface-bg）</summary>
    static readonly (String Role, ContrastMath.Usage Usage)[] SemanticPairs =
    [
        ("text-1", ContrastMath.Usage.TextNormal),
        ("text-2", ContrastMath.Usage.TextNormal),
        ("text-3", ContrastMath.Usage.TextLarge),
        ("border-1", ContrastMath.Usage.NonTextUi),
        ("border-strong", ContrastMath.Usage.NonTextUi),
        ("brand-strong", ContrastMath.Usage.TextNormal),
        ("link", ContrastMath.Usage.TextNormal),
        ("success", ContrastMath.Usage.TextNormal),
        ("warning", ContrastMath.Usage.TextNormal),
        ("danger", ContrastMath.Usage.TextNormal),
        ("info", ContrastMath.Usage.TextNormal),
    ];

    /// <summary>
    /// 组件层里"命名不对称"的对：推不出来但确实该查的，用显式清单补。
    /// 不推出来的那部分（前景配的是 hover 底、占位符配的是输入框底）不是约定能表达的。
    /// </summary>
    static readonly (String Fg, String Bg, ContrastMath.Usage Usage)[] ExplicitComponentPairs =
    [
        ("component.input.placeholder", "component.input.background", ContrastMath.Usage.TextLarge),
        ("component.nav.item.foreground", "component.nav.item.background-hover", ContrastMath.Usage.TextNormal),
        ("component.nav.item.foreground-active", "component.nav.item.background-hover", ContrastMath.Usage.TextNormal),
        ("component.tabs.tab.foreground", "component.tabs.tab.background-hover", ContrastMath.Usage.TextNormal),
        ("component.tabs.tab.foreground-active", "component.tabs.tab.background-hover", ContrastMath.Usage.TextNormal),
    ];

    /// <summary>
    /// 组件层的对**按命名约定从库里推导**：每个 `component.&lt;ns&gt;.foreground[-状态]` 去找同命名空间的
    /// `.background[-状态]`，没有同名状态就退回 `.background`，再退回 `.tint`；
    /// 最后补上推不出来的显式对。
    ///
    /// 为什么不能写死一张表：写死的清单只覆盖当时想到的八个对 —— `dialog` / `tooltip` / `select` 这些
    /// 生成器真实产出的组件从来没被查过，用户自己新增的组件更是直接绕过门禁。
    /// 约定推导才是"新增组件自动被管"的那道闸。
    ///
    /// 第四个字段是 **WCAG 豁免标记**：1.4.3 原文豁免"非活动界面构件"，所以 `-disabled` 的对
    /// 只报读数、不升 critical —— 拿豁免项拦发布就是本仓反复在防的"假警报"。
    /// </summary>
    static IEnumerable<(String Fg, String Bg, ContrastMath.Usage Usage, Boolean Exempt)> ComponentPairsOf(TokenGraph graph)
    {
        var seen = new HashSet<String>(StringComparer.Ordinal);
        foreach (var n in graph.All().Where(n => n.Tier == TokenTiers.Component && n.Type == TokenTypes.Color
                                                 && n.Path.Contains(".foreground", StringComparison.Ordinal))
                     .OrderBy(n => n.Path, StringComparer.Ordinal))
        {
            var i = n.Path.LastIndexOf(".foreground", StringComparison.Ordinal);
            var ns = n.Path[..i];
            var suffix = n.Path[(i + ".foreground".Length)..];   // "" / "-active" / "-disabled"
            var bg = FirstExisting(graph, $"{ns}.background{suffix}", $"{ns}.background", $"{ns}.tint{suffix}", $"{ns}.tint");
            if (bg == null || bg == n.Path) continue;
            seen.Add(n.Path);
            yield return (n.Path, bg, ContrastMath.Usage.TextNormal, suffix.Contains("-disabled", StringComparison.Ordinal));
        }

        foreach (var (fg, bg, usage) in ExplicitComponentPairs)
        {
            if (seen.Contains(fg) || graph.Find(fg) == null || graph.Find(bg) == null) continue;
            seen.Add(fg);
            yield return (fg, bg, usage, fg.Contains("-disabled", StringComparison.Ordinal));
        }
    }

    static String? FirstExisting(TokenGraph graph, params String[] paths)
    {
        foreach (var p in paths)
            if (graph.Find(p) != null) return p;
        return null;
    }

    /// <summary>跑一次全量审计并落库（releaseId=0 草稿态）</summary>
    public AuditSummary Run(Int64 projectId, Int64 releaseId = 0)
    {
        var items = new List<AuditItem>();
        var themes = _projects.ListThemes(projectId).ToList();

        foreach (var theme in themes)
        {
            // 密度轴不含颜色语义，按颜色审计它等于凭空造 80 条假 critical
            if (theme.ModeKind == ThemeModeKinds.Density) continue;

            var themedCount = DesignToken.FindCount(DesignToken._.ProjectId == projectId & DesignToken._.ThemeId == theme.Id);
            if (themedCount == 0)
            {
                items.Add(new AuditItem(AuditKinds.Orphan, "theme-empty", "warning", "token", $"{theme.Code}:<semantic>", false,
                    Expected: "该主题应有语义层令牌", Actual: "0 行",
                    Message: $"主题 {theme.Code} 存在但没有任何语义层令牌：生成时未包含它，组件在该主题下解析不出颜色",
                    Suggestion: $"用 generate 请求把 themes 加上 {theme.Code}"));
                continue;
            }

            var graph = _tokens.LoadGraph(projectId, theme.Id == DesignSystemConstants.SharedThemeId ? null : theme.Id, theme.Code);
            CheckAlias(graph, theme, items);
            CheckContrast(graph, theme, items);
            CheckColorResolvable(graph, theme, items);
            CheckTierDiscipline(graph, theme, items);
            CheckLifecycleRefs(graph, theme.Code, items);
            // 只有密度轴主题会覆盖 space./radius./duration.，所以单调性只在"真的铺了尺度覆盖"的主题上复查
            if (theme.ModeKind == ThemeModeKinds.Density) CheckRamps(graph, theme.Code, items);
        }

        var sharedGraph = _tokens.LoadGraph(projectId, null, "shared");
        CheckFocusTokens(sharedGraph, items);
        CheckReducedMotion(sharedGraph, items);
        CheckNaming(sharedGraph, items);
        CheckRamps(sharedGraph, "shared", items);
        CheckTargetSize(sharedGraph, items);
        CheckUsage(projectId, sharedGraph, themes, items);

        _audits.Record(projectId, releaseId, items);
        return _audits.Summarize(projectId, releaseId);
    }

    static void CheckAlias(TokenGraph graph, DesignTheme theme, List<AuditItem> items)
    {
        foreach (var d in graph.Validate())
            items.Add(new AuditItem(AuditKinds.Alias, "dtcg-alias", "critical", "token", $"{theme.Code}:{d.Path}", false,
                Expected: "别名可解析且不跨层", Actual: d.Status.ToString(), Message: d.Message,
                Suggestion: d.Status == ResolveStatus.Cycle ? "打断环：把其中一环改为直接给值" : "补上缺失的被引用令牌或改指向"));
    }

    static void CheckContrast(TokenGraph graph, DesignTheme theme, List<AuditItem> items)
    {
        var bg = graph.ResolveColor($"semantic.{BgRole(theme.ModeKind)}") ?? graph.ResolveColor("semantic.surface-bg");
        if (bg == null) return;

        foreach (var (role, usage) in SemanticPairs)
        {
            var fg = graph.ResolveColor($"semantic.{role}");
            if (fg == null) continue;
            AddContrast(items, theme, $"semantic.{role}", $"semantic.{BgRole(theme.ModeKind)}", fg.Value, bg.Value, usage);
        }

        foreach (var (fgPath, bgPath, usage, exempt) in ComponentPairsOf(graph))
        {
            var fg = graph.ResolveColor(fgPath);
            var b = EffectiveComponentColor(graph, bgPath);
            if (fg == null || b == null) continue;
            AddContrast(items, theme, fgPath, bgPath, fg.Value, b.Value, usage, exempt);
        }
    }

    static void AddContrast(List<AuditItem> items, DesignTheme theme, String fgPath, String bgPath, Oklch.Color fg, Oklch.Color bg,
        ContrastMath.Usage usage, Boolean exempt = false)
    {
        var ratio = ContrastMath.Ratio(fg, bg);
        var required = ContrastMath.AaThreshold(usage);
        var pass = ContrastMath.MeetsAa(ratio, usage);
        var rule = $"wcag22-{(usage == ContrastMath.Usage.NonTextUi ? "1.4.11" : "1.4.3")}";
        if (exempt && !pass)
        {
            // WCAG 1.4.3 原文豁免"非活动界面构件"（disabled 控件、不可点的占位），所以禁用态不达 4.5 **不能拦发布**。
            // 但不等于放过：退回 1.4.11 的 3.0 兜底线，低于它仍给 warning —— 拦在豁免线以上是本仓反复在防的"假警报"。
            var identifiable = ratio >= 3.0;
            items.Add(new AuditItem(AuditKinds.Contrast, rule + "-exempt", identifiable ? "info" : "warning", "token", $"{theme.Code}:{fgPath}",
                identifiable, bgPath, ">= 3.00（1.4.3 豁免，仅按非文本可辨性提示）", ratio.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), ratio,
                Message: $"{fgPath} on {bgPath} = {ratio:F2}:1，未达 {required}:1，但 {usage} 属禁用态：WCAG 1.4.3 豁免非活动构件，不计入发布阻断"));
            return;
        }

        items.Add(new AuditItem(AuditKinds.Contrast, rule,
            pass ? "info" : "critical", "token", $"{theme.Code}:{fgPath}", pass, bgPath,
            $">= {required}", ratio.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), ratio,
            Suggestion: pass ? null : SuggestFg(theme, fgPath, bgPath),
            Message: pass ? $"{fgPath} on {bgPath} = {ratio:F2}:1 达标（{usage}）" : $"{fgPath} on {bgPath} = {ratio:F2}:1，需 ≥{required}:1（{usage}）"));
    }

    /// <summary>
    /// 声明为 color 却解析不出颜色的令牌：对比度无从判定，必须**自己报一条"无法判定"**。
    /// 不报的话它会从审计里静默消失 —— 界面上既不见红也不见黄，和"查过且没问题"长得一模一样。
    /// 别名本身就失败的（Missing/Cycle/…）由 <c>alias</c> 报 critical，这里不重复记账，只接"解析得出但不是颜色"这一类。
    /// </summary>
    static void CheckColorResolvable(TokenGraph graph, DesignTheme theme, List<AuditItem> items)
    {
        foreach (var n in graph.All().Where(n => n.Type == TokenTypes.Color).OrderBy(n => n.Path, StringComparer.Ordinal))
        {
            var r = graph.Resolve(n.Path);
            if (!r.IsOk || ColorRampGenerator.SeedColorOrNull(r.Value) != null) continue;
            items.Add(new AuditItem(AuditKinds.Contrast, "wcag22-1.4.3-unresolved", "warning", "token", $"{theme.Code}:{n.Path}", false,
                Expected: "解析为 hex / oklch 的颜色", Actual: r.Value,
                Message: $"{n.Path} 声明为 color 却解析不出颜色（值 {r.Value}）—— 对比度无法判定，不等于没有问题",
                Suggestion: $"把 {n.Path} 写成 hex/oklch 颜色，或让它的别名指向一个真颜色令牌"));
        }
    }

    /// <summary>不达标时给可执行建议：往对比度更大的方向换 tone（同族内），而不是泛泛"请提高对比度"</summary>
    static String? SuggestFg(DesignTheme theme, String fgPath, String bgPath)
    {
        var family = fgPath.Split('.')[1];
        return $"{theme.Code} 主题下把 {family} 族往对比度更大的方向换 1~2 阶（暗底换更亮的阶、亮底换更暗的阶），或把 {bgPath} 反向调整明度";
    }

    /// <summary>component 色令牌可能带 tint（color-mix），按 oklab 混合折算实际像素色</summary>
    static Oklch.Color? EffectiveComponentColor(TokenGraph graph, String path)
    {
        var node = graph.Find(path);
        if (node == null) return null;

        var resolved = graph.ResolveColor(path);
        if (resolved == null) return resolved;

        var tint = ReadTint(node);
        if (tint == null) return resolved;

        var baseColor = graph.ResolveColor("semantic.surface-bg");
        return baseColor == null ? resolved : Oklch.Mix(baseColor.Value, resolved.Value, tint.Value);
    }

    static Double? ReadTint(TokenNode node)
    {
        var ext = node.Extensions;
        if (ext == null) return null;
        try
        {
            using var doc = JsonDocument.Parse(ext);
            if (doc.RootElement.TryGetProperty("forgeself", out var f) &&
                f.TryGetProperty("tint", out var t) && t.TryGetDouble(out var v)) return v;
        }
        catch (JsonException) { return null; }
        return null;
    }

    static String BgRole(String modeKind) => "surface-bg";

    static void CheckTierDiscipline(TokenGraph graph, DesignTheme theme, List<AuditItem> items)
    {
        foreach (var node in graph.All().Where(n => n.Tier == TokenTiers.Component && n.Type == TokenTypes.Color && !string.IsNullOrEmpty(n.AliasPath)))
        {
            var target = graph.Find(node.AliasPath!);
            if (target == null || target.Tier != TokenTiers.Primitive) continue;
            items.Add(new AuditItem(AuditKinds.TierViolation, "tier-skip", "warning", "token", $"{theme.Code}:{node.Path}", false,
                target.Path, "alias 指向 semantic", target.Tier, -1,
                Suggestion: $"改指向对应的 semantic.* 角色（如 semantic.brand）",
                Message: $"component 令牌 {node.Path} 直连 primitive {target.Path}，绕过了语义层"));
        }
    }

    static void CheckFocusTokens(TokenGraph graph, List<AuditItem> items)
    {
        String[] need = ["component.focus.outline-width", "component.focus.outline-color", "component.focus.outline-offset"];
        foreach (var p in need)
        {
            // 兼容两种写法：component.focus.* 与 focus.*
            var ok = graph.Contains(p) || graph.Contains(p.Substring("component.".Length));
            if (ok) continue;
            items.Add(new AuditItem(AuditKinds.Focus, "wcag22-2.4.7", "critical", "token", p, false,
                Expected: "存在 focus-visible 描边令牌", Actual: "缺失",
                Message: $"{p} 缺失：没有焦点可见令牌，键盘用户看不到焦点位置（WCAG 2.2 1.4.11/2.4.7）"));
        }
    }

    static void CheckReducedMotion(TokenGraph graph, List<AuditItem> items)
    {
        var baseDurations = graph.All().Where(n => n.Type == TokenTypes.Duration && n.Path.StartsWith("duration.", StringComparison.Ordinal)).ToList();
        foreach (var d in baseDurations)
        {
            var name = d.Path["duration.".Length..];
            if (name.EndsWith("-reduced", StringComparison.Ordinal)) continue;
            var has = graph.Contains($"duration.{name}-reduced");
            items.Add(new AuditItem(AuditKinds.ReducedMotion, "prefers-reduced-motion", has ? "info" : "warning", "token", d.Path, has,
                Expected: $"duration.{name}-reduced 存在", Actual: has ? "存在" : "缺失",
                Message: has ? $"{d.Path} 有 reduced 变体" : $"{d.Path} 缺少 reduced 变体，动效不可关闭"));
        }
    }

    /// <summary>WCAG 2.2 2.5.8 指针目标下限（CSS px）</summary>
    const Double MinTargetPx = 24;

    /// <summary>
    /// 点分 kebab：每段以小写字母或数字开头，段内可带连字符（`z-index.1`、`duration.base-reduced`、`radius.2xl` 都合法）。
    /// 形状不对，`--ds-*` 变量名与 DTCG 树就会静默产出谁也用不上的键。
    /// </summary>
    static readonly System.Text.RegularExpressions.Regex PathShape =
        new(@"^[a-z0-9][a-z0-9-]*(\.[a-z0-9][a-z0-9-]*)*$", System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    static void CheckNaming(TokenGraph graph, List<AuditItem> items)
    {
        foreach (var n in graph.All().Where(n => !PathShape.IsMatch(n.Path)).OrderBy(n => n.Path, StringComparer.Ordinal))
            items.Add(new AuditItem(AuditKinds.Naming, "path-shape", "warning", "token", n.Path, false,
                Expected: "点分 kebab，如 space.4 / component.button.min-height", Actual: n.Path,
                Message: $"令牌路径「{n.Path}」不符合点分 kebab：投影会按点分路径生成 CSS 变量名与 DTCG 树，形状不对就是一条谁也用不上的令牌",
                Suggestion: "改成小写、段与段用点、段内只用字母数字与连字符"));
    }

    /// <summary>
    /// 尺度族按**声明序**必须递增。档序取自 `ScaleGenerators` 的同一张表 ——
    /// 在审计里另列一份档位清单，就是本仓一直在灭的"第二份真相"。
    /// </summary>
    static void CheckRamps(TokenGraph graph, String scope, List<AuditItem> items)
    {
        CheckRamp(graph, scope, items, "space.", ScaleGenerators.SpaceSteps.Select(s => s.Name));
        CheckRamp(graph, scope, items, "radius.", ScaleGenerators.RadiusSteps.Select(r => r.Name));
        CheckRamp(graph, scope, items, "duration.", ScaleGenerators.DurationSteps.Select(d => d.Name));
    }

    static void CheckRamp(TokenGraph graph, String scope, List<AuditItem> items, String prefix, IEnumerable<String> order)
    {
        var family = prefix[..^1];   // "space." → "space"：消息里要报完整路径，只报档名用户得自己拼
        var steps = new List<(String Name, Int32 Idx, Double Num, String Raw)>();
        var idx = 0;
        foreach (var name in order)
        {
            var v = ValueOf(graph, prefix + name);
            if (v != null) steps.Add((name, idx, v.Value.Num, v.Value.Raw));
            idx++;
        }

        for (var i = 1; i < steps.Count; i++)
        {
            var (prev, cur) = (steps[i - 1], steps[i]);
            if (cur.Num > prev.Num) continue;
            var (prevPath, curPath) = ($"{family}.{prev.Name}", $"{family}.{cur.Name}");
            items.Add(new AuditItem(AuditKinds.Ramp, "monotonic", "warning", "token",
                $"{scope}:{curPath}", false, prevPath,
                Expected: $"> {prev.Raw}", Actual: cur.Raw,
                Message: $"{family} 族在 {cur.Name} 处不再递增（{prevPath}={prev.Raw} → {curPath}={cur.Raw}）：档位号失去含义，按档取间距/圆角/时长会忽大忽小",
                Suggestion: $"把 {curPath} 调到大于 {prevPath}，或回「项目与生成」按参数重算 {family} 族"));
        }
    }

    /// <summary>交互构件的可点高度：低于 24px 报 warning（2.5.8 本身有例外条款，不据此拦发布）</summary>
    static void CheckTargetSize(TokenGraph graph, List<AuditItem> items)
    {
        foreach (var n in graph.All().Where(n => n.Path.EndsWith(".min-height", StringComparison.Ordinal))
                     .OrderBy(n => n.Path, StringComparer.Ordinal))
        {
            var px = PxOf(graph, n.Path);
            if (px == null) continue;
            var pass = px >= MinTargetPx;
            items.Add(new AuditItem(AuditKinds.TargetSize, "wcag22-2.5.8", pass ? "info" : "warning", "token", n.Path, pass,
                Expected: $">= {MinTargetPx}px", Actual: $"{px}px",
                Message: pass ? $"{n.Path} = {px}px，达到 2.5.8 目标尺寸下限"
                              : $"{n.Path} = {px}px，低于 {MinTargetPx}px：触屏与指针设备用户点不中这个控件",
                Suggestion: pass ? null : $"把该档提到 ≥{MinTargetPx}px，或用 padding 把可点区域撑到 {MinTargetPx}×{MinTargetPx}"));
        }
    }

    /// <summary>下架一个还在被引用的令牌，必须让下游看得见（否则换肤/清理时链条静默断掉）</summary>
    static void CheckLifecycleRefs(TokenGraph graph, String scope, List<AuditItem> items)
    {
        foreach (var n in graph.All().Where(n => !string.IsNullOrEmpty(n.AliasPath)))
        {
            var target = graph.Find(n.AliasPath!);
            if (target == null || !IsRetiring(target.Lifecycle)) continue;
            items.Add(new AuditItem(AuditKinds.Lifecycle, "retired-still-referenced", "warning", "token",
                $"{scope}:{n.Path}", false, target.Path, "被引用的令牌应为 adopted", target.Lifecycle,
                Message: $"{n.Path} 指向 {target.Path}，而后者已标为 {target.Lifecycle}：这条链在换肤或清理时会静默断掉",
                Suggestion: $"把 {n.Path} 改指向仍在用的令牌，或把 {target.Path} 恢复为 adopted"));
        }
    }

    static Boolean IsRetiring(String? lifecycle) =>
        lifecycle is TokenLifecycles.Deprecated or TokenLifecycles.Removed;

    /// <summary>取某条令牌解析后的 px 数值（只认 dimension + `px`）；解析不出来返回 null —— 不猜</summary>
    static Double? PxOf(TokenGraph graph, String path)
    {
        var node = graph.Find(path);
        if (node == null || node.Type != TokenTypes.Dimension) return null;
        var v = ValueOf(graph, path);
        return v != null && v.Value.Raw.EndsWith("px", StringComparison.OrdinalIgnoreCase) ? v.Value.Num : null;
    }

    /// <summary>
    /// 尺度比较用的"数值 + 原文"：dimension(px) / duration(ms) / number(裸数) 都算得出来。
    /// 只认 px 会让 `duration.` 那条判据**静默空跑** —— 界面上写着检三类、实际只检两类，就是名实不符。
    /// 原文一并带回，消息里显示 `duration.base=200ms → duration.macro=100ms`，单位跟着真值走。
    /// </summary>
    static (Double Num, String Raw)? ValueOf(TokenGraph graph, String path)
    {
        var node = graph.Find(path);
        if (node == null || node.Type is not (TokenTypes.Dimension or TokenTypes.Duration or TokenTypes.Number)) return null;
        var r = graph.Resolve(path);
        if (!r.IsOk) return null;

        var s = r.Value.Trim().ToLowerInvariant();
        foreach (var unit in new[] { "px", "ms", "rem", "em", "s", "%" })
            if (s.EndsWith(unit, StringComparison.Ordinal)) { s = s[..^unit.Length].Trim(); break; }
        return Double.TryParse(s, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? (d, r.Value.Trim()) : null;
    }

    void CheckUsage(Int64 projectId, TokenGraph shared, IReadOnlyCollection<DesignTheme> themes, List<AuditItem> items)
    {
        var referenced = new HashSet<String>(StringComparer.Ordinal);

        foreach (var theme in themes)
        {
            var g = _tokens.LoadGraph(projectId, theme.Id == DesignSystemConstants.SharedThemeId ? null : theme.Id, theme.Code);
            foreach (var n in g.All())
                if (!string.IsNullOrEmpty(n.AliasPath)) referenced.Add(n.AliasPath);
        }

        foreach (var n in shared.All().Where(n => n.Tier == TokenTiers.Primitive))
        {
            if (referenced.Contains(n.Path)) continue;
            items.Add(new AuditItem(AuditKinds.Orphan, "unused-primitive", "info", "token", $"shared:{n.Path}", false,
                Message: $"{n.Path} 未被任何语义/组件令牌引用"));
        }

        var componentAliases = shared.All()
            .Where(n => n.Tier == TokenTiers.Component && !string.IsNullOrEmpty(n.AliasPath))
            .Select(n => n.AliasPath!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var theme in themes.Where(t => t.ModeKind == ThemeModeKinds.Color || t.ModeKind == ThemeModeKinds.Brand))
        {
            foreach (var n in _tokens.FindThemed(projectId, theme.Id).Where(t => t.Tier == TokenTiers.Semantic))
            {
                if (componentAliases.Contains(n.Path)) continue;
                if (n.Path.StartsWith("semantic.surface", StringComparison.Ordinal) || n.Path.StartsWith("semantic.text", StringComparison.Ordinal)) continue;
                items.Add(new AuditItem(AuditKinds.Unused, "unused-semantic", "info", "token", $"{theme.Code}:{n.Path}", true,
                    Message: $"{n.Path} 未被组件层引用（可能是给人手用的，也可能是冗余）"));
            }
        }
    }
}

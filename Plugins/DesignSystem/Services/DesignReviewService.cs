using NewLife;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>checklist 条目（tokens[] 只含本项目真存在的路径，AC13）</summary>
public sealed record ChecklistItem(String Id, String Severity, String Check, String How, IReadOnlyList<String> Tokens);

/// <summary>
/// 审查服务层（FR8/FR9）：加载项目快照 → 令牌索引（含生命周期映射）→ 纯函数审查引擎。
/// 主题缺失回落默认主题并记 notes；checklist 与 `design_review mode=checklist` 同源，
/// 条目 tokens[] 逐项在 TokenIndex 校验存在，不存在的路径绝不列出。
/// </summary>
public sealed class DesignReviewService
{
    readonly ExportService _export;
    readonly TokenRepository _tokens;
    readonly DesignProjectService _projects;

    public DesignReviewService(ExportService export, TokenRepository tokens, DesignProjectService projects)
    {
        _export = export;
        _tokens = tokens;
        _projects = projects;
    }

    /// <summary>加载项目令牌索引（快照 + 生命周期映射）</summary>
    public TokenIndex LoadIndex(Int64 projectId, String? themeCode, out String usedTheme, out List<String> notes)
    {
        notes = new List<String>();
        var theme = themeCode;
        ExportService.Snapshot snap;
        try
        {
            snap = _export.Load(projectId, theme);
        }
        catch (KeyNotFoundException) when (!themeCode.IsNullOrEmpty())
        {
            // 主题缺失：回落默认色向主题并记 notes
            notes.Add($"主题 {themeCode} 不存在，已回落默认主题");
            theme = null;
            snap = _export.Load(projectId, null);
        }
        usedTheme = theme ?? "shared";

        var index = TokenIndex.FromSnapshot(_export, snap);
        var lifecycles = LoadLifecycles(projectId, theme);
        return index.WithLifecycles(lifecycles);
    }

    /// <summary>生命周期映射：TokenRepository.LoadGraph 节点 → path → lifecycle（缺失按 active）</summary>
    Dictionary<String, String> LoadLifecycles(Int64 projectId, String? themeCode)
    {
        var map = new Dictionary<String, String>(StringComparer.Ordinal);
        var themeId = themeCode.IsNullOrEmpty() ? DesignSystemConstants.SharedThemeId : _projects.ResolveThemeId(projectId, themeCode);
        try
        {
            var graph = _tokens.LoadGraph(projectId, themeId > 0 ? themeId : null, themeCode);
            foreach (var node in graph.All())
                if (!node.Lifecycle.IsNullOrEmpty())
                    map[node.Path] = node.Lifecycle;
        }
        catch (Exception)
        {
            // 图加载失败不影响审查主流程（生命周期缺失按 active 处理）
        }
        return map;
    }

    public DesignReviewer.Outcome Review(Int64 projectId, String? themeCode, IReadOnlyList<ReviewInput> inputs,
        Boolean strict, Int32 maxFindings)
    {
        var index = LoadIndex(projectId, themeCode, out var usedTheme, out var notes);
        var outcome = new DesignReviewer().Review(index, inputs, strict, maxFindings);
        if (notes.Count > 0)
        {
            var all = new List<String>(outcome.Notes);
            all.InsertRange(0, notes);
            return outcome with { Notes = all };
        }
        return outcome;
    }

    /// <summary>交付前清单（≥8 条基础条目，page 特有条目有对应令牌才加）。tokens[] 逐项校验存在。
    /// M3：再接上"由项目 UX 规范派生"的条目（<see cref="DerivedItems"/>），两条来源同出一个清单。</summary>
    public IReadOnlyList<ChecklistItem> Checklist(Int64 projectId, String? themeCode, String page)
    {
        var index = LoadIndex(projectId, themeCode, out _, out _);
        var items = BuildItems(index, page).ToList();
        items.AddRange(DerivedItems(projectId, themeCode));
        return items;
    }

    /// <summary>
    /// 由规范 <c>MUST/SHOULD/MAY</c> 规则派生的清单条目：级别映射 error/warning/info，
    /// id 固定为 <c>g:&lt;规范code&gt;:&lt;规则id&gt;</c>（下游可以稳定引用一条规则，不随文本改动漂移）。
    /// <c>tokens[]</c> 只列当前取值视图里真存在的路径 —— 但**允许为空**：
    /// "按钮文字用动词+宾语"这类规则没有令牌引用，套用静态条目的"无令牌即省略"会把规范的一半要求悄悄丢掉。
    /// </summary>
    IReadOnlyList<ChecklistItem> DerivedItems(Int64 projectId, String? themeCode)
    {
        var service = _export.Guidelines;
        if (service == null) return [];

        var view = _export.GuidelineView(_export.Load(projectId, themeCode));
        var valueOf = ExportService.ValueOf(view);
        var items = new List<ChecklistItem>();
        foreach (var g in service.List(projectId))
        {
            foreach (var r in GuidelineRepository.ReadRules(g.RulesJson))
            {
                var paths = GuidelineRenderer.ConcreteRefs(r.Text)
                    .Where(p => !valueOf(p).IsNullOrEmpty())
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                items.Add(new ChecklistItem(
                    $"g:{g.Code}:{r.Id}",
                    SeverityOf(r.Level),
                    $"{g.Title}：{r.Text}",
                    $"来源 UX 规范「{g.Title}」（{g.Code} · {GuidelineCategories.Display(g.Category)}）；要改这条请回插件改规范（design_edit action=guideline），不要在代码里就地绕开",
                    paths));
            }
        }
        return items;
    }

    /// <summary>级别→严重度。<c>level</c> 声明为非空：仓储读侧已把缺失级别规一成 SHOULD（`GuidelineRepository.ReadRules`），
    /// 判据用例 <c>AC18_脏规则行_级别为null_checklist不抛且回落SHOULD</c> 钉着这条不变量，所以这里不需要再兜 null。</summary>
    static String SeverityOf(String level) => level switch
    {
        _ when level.Equals("MUST", StringComparison.OrdinalIgnoreCase) => "error",
        _ when level.Equals("SHOULD", StringComparison.OrdinalIgnoreCase) => "warning",
        _ => "info",
    };

    /// <summary>清单条目构造（纯函数，无 DB；服务层加载索引后调用）</summary>
    public static IReadOnlyList<ChecklistItem> BuildItems(TokenIndex index, String page)
    {
        var items = new List<ChecklistItem>();

        void Add(String id, String sev, String check, String how, params String[] prefixes)
        {
            var tokens = prefixes
                .SelectMany(p => index.Tokens.Where(t => t.Path == p
                    || (p.EndsWith('.') && t.Path.StartsWith(p, StringComparison.Ordinal))))
                .Select(t => t.Path)
                .Distinct(StringComparer.Ordinal)
                .Take(3)
                .ToList();
            if (tokens.Count == 0) return;              // 令牌不存在 → 整条省略（AC13）
            items.Add(new ChecklistItem(id, sev, check, how, tokens));
        }

        Add("color-token", "error", "颜色只用设计系统令牌", "颜色一律写 var(--ds-…)，禁止字面色值；写完用 design_review 对照", "semantic.brand", "semantic.surface-bg");
        Add("bg-surface", "warning", "背景/表面用语义令牌", "页面背景与卡片表面使用 semantic.surface-* 而不是自己调色", "semantic.surface-");
        Add("space-scale", "warning", "间距用 space.* 档位", "padding/margin/gap 从 space.* 取值，不写随意像素", "space.");
        Add("radius-scale", "warning", "圆角用 radius.* 档位", "border-radius 从 radius.* 取值", "radius.");
        Add("type-size", "warning", "字号用 size.* 档位", "font-size 从 size.* 取值，不用 px 手写", "size.");
        Add("font-family", "warning", "字体用 font.* 令牌", "font-family 用 var(--ds-font-sans)/var(--ds-font-mono)", "font.sans", "font.mono");
        Add("font-weight", "info", "字重用 weight.* 令牌", "font-weight 从 weight.* 取值", "weight.");
        Add("shadow-scale", "warning", "阴影用 shadow.elevation-*", "box-shadow 用 elevation 令牌而不是自造投影", "shadow.elevation-");
        Add("duration-scale", "info", "时长用 duration.* 档位", "transition/animation 时长从 duration.* 取值", "duration.");
        Add("focus-visible", "error", "焦点必须可见", "不写 outline:none 而不给替代；用焦点令牌", "component.focus-", "component.ring-");

        // page 特有（有令牌才加）
        if (page is "mobile" or "any")
            Add("touch-target", "warning", "触控目标最小 44px", "移动端可点区域不小于 44px，用 size.* 保证", "size.");
        if (page is "form" or "any")
            Add("form-feedback", "warning", "表单错误/成功态用语义色", "错误/成功提示用 semantic 状态色而不是自定义色值", "semantic.danger", "semantic.success");
        if (page is "login" or "landing" or "any")
            Add("brand-lockup", "info", "品牌标识用品牌令牌", "Logo/品牌区用 semantic.brand 与品牌资产", "semantic.brand", "asset.");

        return items;
    }
}

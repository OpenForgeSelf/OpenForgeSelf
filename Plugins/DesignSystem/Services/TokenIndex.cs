using System.Text.RegularExpressions;
using NewLife;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>
/// 令牌索引（FR6）：从 <see cref="ExportService.Snapshot"/> 构建，供审查引擎与 `design_lookup nearest` 共用。
/// 只含有效值令牌；同时提供「已定义 CSS 变量集」（对 <see cref="ExportService.ToCss"/> 产物扫描，
/// 与前端 `definedVars` 同口径 —— 审查引擎的 `unknown-token-ref` 就按这份集合判定）。
/// 生命周期映射由调用方（DesignReviewService 从 LoadGraph 节点）注入，默认全 active。
/// </summary>
public sealed class TokenIndex
{
    static readonly Regex VarDeclRegex = new("--ds-[\\w-]+(?=\\s*:)", RegexOptions.Compiled);

    TokenIndex(IReadOnlyList<ExportService.Snap> tokens, IReadOnlySet<String> definedCssVars,
        IReadOnlyDictionary<String, String> cssVarByPath, IReadOnlyDictionary<String, String> pathByCssVar,
        IReadOnlyDictionary<String, String> lifecycleByPath)
    {
        Tokens = tokens;
        DefinedCssVars = definedCssVars;
        CssVarByPath = cssVarByPath;
        PathByCssVar = pathByCssVar;
        LifecycleByPath = lifecycleByPath;
    }

    /// <summary>有效值令牌（快照序：tier 序 → 路径序）</summary>
    public IReadOnlyList<ExportService.Snap> Tokens { get; }

    /// <summary>CSS 投影实际定义的变量集（`--ds-…`，不含别名行 —— 与前端 definedVars 同口径）</summary>
    public IReadOnlySet<String> DefinedCssVars { get; }

    /// <summary>路径 → CSS 变量名（ExportService.CssVarName 同源）</summary>
    public IReadOnlyDictionary<String, String> CssVarByPath { get; }

    /// <summary>CSS 变量名 → 路径（反查）</summary>
    public IReadOnlyDictionary<String, String> PathByCssVar { get; }

    /// <summary>路径 → 生命周期（active / deprecated / removed）；缺失按 active</summary>
    public IReadOnlyDictionary<String, String> LifecycleByPath { get; }

    public static TokenIndex FromSnapshot(ExportService export, ExportService.Snapshot snap)
    {
        var tokens = snap.Tokens.Where(t => !t.Value.IsNullOrEmpty() && t.Value != "null").ToList();

        var defined = new HashSet<String>(StringComparer.Ordinal);
        foreach (Match m in VarDeclRegex.Matches(export.ToCss(snap)))
            defined.Add(m.Value);

        var byPath = new Dictionary<String, String>(StringComparer.Ordinal);
        foreach (var t in tokens) byPath[t.Path] = ExportService.CssVarName(t.Path);
        var byVar = byPath.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

        return new TokenIndex(tokens, defined, byPath, byVar, new Dictionary<String, String>(StringComparer.Ordinal));
    }

    /// <summary>注入生命周期映射（DesignReviewService 从 TokenRepository.LoadGraph 节点取）</summary>
    public TokenIndex WithLifecycles(IReadOnlyDictionary<String, String> lifecycles) =>
        new(Tokens, DefinedCssVars, CssVarByPath, PathByCssVar, lifecycles);

    public String LifecycleOf(String path) => LifecycleByPath.TryGetValue(path, out var l) ? l : "active";

    /// <summary>颜色候选：semantic + component 层 type=color（不推荐 primitive 色阶），排除 removed</summary>
    public IEnumerable<ExportService.Snap> ColorCandidates() =>
        Tokens.Where(t => t.Type == TokenTypes.Color
            && (t.Tier == TokenTiers.Semantic || t.Tier == TokenTiers.Component)
            && LifecycleOf(t.Path) != "removed");

    /// <summary>长度候选（category：space / radius / border / size），排除 removed</summary>
    public IEnumerable<ExportService.Snap> LengthCandidates(String category) =>
        Tokens.Where(t => t.Tier == TokenTiers.Semantic || t.Tier == TokenTiers.Component)
            .Where(t => LifecycleOf(t.Path) != "removed")
            .Where(t => (category == "space" && t.Path.StartsWith("space.", StringComparison.Ordinal))
                || (category == "radius" && t.Path.StartsWith("radius.", StringComparison.Ordinal))
                || (category == "border" && t.Path.StartsWith("border.", StringComparison.Ordinal))
                || (category == "size" && t.Path.StartsWith("size.", StringComparison.Ordinal)));

    /// <summary>时长候选：`duration.*`（不含 `-reduced` 派生档），排除 removed</summary>
    public IEnumerable<ExportService.Snap> DurationCandidates() =>
        Tokens.Where(t => LifecycleOf(t.Path) != "removed"
            && t.Path.StartsWith("duration.", StringComparison.Ordinal)
            && !t.Path.EndsWith("-reduced", StringComparison.Ordinal));

    /// <summary>阴影候选：`shadow.elevation-*`，排除 removed</summary>
    public IEnumerable<ExportService.Snap> ShadowCandidates() =>
        Tokens.Where(t => LifecycleOf(t.Path) != "removed"
            && t.Path.StartsWith("shadow.elevation-", StringComparison.Ordinal));

    /// <summary>字体族候选：font.sans / font.mono</summary>
    public IEnumerable<ExportService.Snap> FontFamilyCandidates() =>
        Tokens.Where(t => t.Path == "font.sans" || t.Path == "font.mono");

    /// <summary>字重候选：weight.*，排除 removed</summary>
    public IEnumerable<ExportService.Snap> FontWeightCandidates() =>
        Tokens.Where(t => LifecycleOf(t.Path) != "removed" && t.Path.StartsWith("weight.", StringComparison.Ordinal));
}

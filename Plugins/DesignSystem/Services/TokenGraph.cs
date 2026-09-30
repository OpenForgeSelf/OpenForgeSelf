namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>别名图节点：从实体投影出的最小可解析视图（纯数据，便于无库单测）。</summary>
/// <param name="Path">点分路径</param>
/// <param name="Tier">层级</param>
/// <param name="Type">DTCG $type</param>
/// <param name="Value">字面值（别名为空时使用）</param>
/// <param name="AliasPath">别名指向的路径</param>
/// <param name="Extensions">DTCG $extensions / 厂商元数据袋（JSON），审计据此读 tint 等派生信息</param>
/// <param name="ValueJson">复合值（typography/shadow/transition 等）。DTCG 复合类型的 $value 就是对象，
/// 这类令牌没有字面 Value 也算"有值"，不能按空令牌拒绝</param>
/// <param name="Lifecycle">生命周期（removed 的令牌不进发布快照）</param>
public sealed record TokenNode(String Path, String Tier, String Type, String? Value, String? AliasPath,
    String? Extensions = null, String? ValueJson = null, String? Lifecycle = null, String? Description = null);

/// <summary>解析结果状态</summary>
public enum ResolveStatus
{
    /// <summary>解析成功</summary>
    Ok,
    /// <summary>既无字面值也无别名</summary>
    Empty,
    /// <summary>别名指向不存在的路径</summary>
    Missing,
    /// <summary>别名链成环</summary>
    Cycle,
    /// <summary>别名链超过深度上限</summary>
    TooDeep,
    /// <summary>别名逆向指向更上层（如 semantic→component）</summary>
    LayerViolation,
}

/// <summary>单个令牌的解析输出</summary>
/// <param name="Status">状态</param>
/// <param name="Value">解析后的有效值；失败为空串</param>
/// <param name="SourcePath">值实际来源的令牌路径（别名链末端）</param>
/// <param name="Chain">走过的路径链，用于把错误指到具体环节</param>
public readonly record struct ResolveResult(ResolveStatus Status, String Value, String SourcePath, IReadOnlyList<String> Chain)
{
    public Boolean IsOk => Status == ResolveStatus.Ok;
    public static ResolveResult Fail(ResolveStatus status, IReadOnlyList<String> chain) => new(status, "", chain[chain.Count - 1], chain);
}

/// <summary>校验错误明细（返回给前端逐行标红）。</summary>
/// <param name="Path">出错的令牌路径</param>
/// <param name="Status">错误类型</param>
/// <param name="Message">人类可读原因</param>
public readonly record struct TokenDiagnostic(String Path, ResolveStatus Status, String Message);

/// <summary>
/// 令牌别名图：路径索引、有效值解析、环检测与分层合规校验。
///
/// 这里是「共享层(ThemeId=0) + 主题覆盖层」两层合并的唯一负责点（design G4）——
/// 任何投影/审计都必须经本类取有效值，禁止各自再实现一遍合并逻辑，否则导出与界面会漂。
/// 悬空别名一律不猜测、不回退（design G17）：解析失败即失败，由审计记 critical。
/// </summary>
public sealed class TokenGraph
{
    readonly Dictionary<String, TokenNode> _shared = new(StringComparer.Ordinal);
    readonly Dictionary<String, TokenNode> _themed = new(StringComparer.Ordinal);

    /// <summary>当前生效的主题编码（null 表示只看共享层）</summary>
    public String? ThemeCode { get; }

    /// <summary>构造别名图</summary>
    /// <param name="shared">共享层令牌（ThemeId=0）</param>
    /// <param name="themed">当前主题的覆盖层令牌</param>
    /// <param name="themeCode">主题编码，仅用于诊断文案</param>
    public TokenGraph(IEnumerable<TokenNode> shared, IEnumerable<TokenNode>? themed = null, String? themeCode = null)
    {
        foreach (var n in shared) _shared[n.Path] = n;
        if (themed != null)
            foreach (var n in themed) _themed[n.Path] = n;
        ThemeCode = themeCode;
    }

    /// <summary>按路径取合并后的节点（主题覆盖优先于共享层）；不存在返回 null</summary>
    public TokenNode? Find(String path) =>
        _themed.TryGetValue(path, out var t) ? t : _shared.TryGetValue(path, out var s) ? s : null;

    /// <summary>合并后的全部路径（主题覆盖把共享层同路径项顶掉）</summary>
    public IEnumerable<TokenNode> All()
    {
        foreach (var kv in _shared)
            if (!_themed.ContainsKey(kv.Key)) yield return kv.Value;
        foreach (var kv in _themed)
            yield return kv.Value;
    }

    /// <summary>是否已登记该路径</summary>
    public Boolean Contains(String path) => _themed.ContainsKey(path) || _shared.ContainsKey(path);

    /// <summary>
    /// 解析某路径的有效值：沿别名链走到第一个带字面值的节点。
    /// 失败不猜测——Missing/Cycle/TooDeep/LayerViolation 原样返回给调用方。
    /// </summary>
    public ResolveResult Resolve(String path)
    {
        var chain = new List<String>();
        var current = path;
        var visited = new HashSet<String>(StringComparer.Ordinal);

        for (var depth = 0; depth <= DesignSystemConstants.MaxAliasDepth; depth++)
        {
            if (current == null) break;
            chain.Add(current);

            if (!visited.Add(current))
                return ResolveResult.Fail(ResolveStatus.Cycle, chain);

            var node = Find(current);
            if (node == null)
                return ResolveResult.Fail(ResolveStatus.Missing, chain);

            // 别名逆向指向上层：分层纪律被破坏
            if (!string.IsNullOrEmpty(node.AliasPath))
            {
                var target = Find(node.AliasPath);
                if (target != null && TokenTiers.Rank(target.Tier) > TokenTiers.Rank(node.Tier))
                    return ResolveResult.Fail(ResolveStatus.LayerViolation, chain);
            }

            if (!string.IsNullOrEmpty(node.Value))
                return new ResolveResult(ResolveStatus.Ok, node.Value, current, chain);

            // 复合令牌（typography/shadow/transition）的值就是 ValueJson 对象，等同"有值"
            if (!string.IsNullOrEmpty(node.ValueJson))
                return new ResolveResult(ResolveStatus.Ok, node.ValueJson!, current, chain);

            if (string.IsNullOrEmpty(node.AliasPath))
                return ResolveResult.Fail(ResolveStatus.Empty, chain);

            current = node.AliasPath;
        }

        return ResolveResult.Fail(ResolveStatus.TooDeep, chain);
    }

    /// <summary>解析有效颜色：仅接受可解析的颜色值，其余返回 null</summary>
    public Oklch.Color? ResolveColor(String path)
    {
        var r = Resolve(path);
        if (!r.IsOk) return null;
        return Oklch.ParseHex(r.Value) ?? Oklch.ParseOklch(r.Value);
    }

    /// <summary>
    /// 全图校验：返回所有解析失败与分层违规的令牌（按路径序）。
    /// 生成/保存路径据此决定「整批拒绝」，审计据此落 critical 行。
    /// </summary>
    public IReadOnlyList<TokenDiagnostic> Validate()
    {
        var list = new List<TokenDiagnostic>();
        foreach (var node in All().OrderBy(n => n.Path, StringComparer.Ordinal))
        {
            var r = Resolve(node.Path);
            if (r.IsOk) continue;
            list.Add(new TokenDiagnostic(node.Path, r.Status, Describe(node, r)));
        }
        return list;
    }

    /// <summary>是否存在环或悬空别名（生成前置检查的快速路径）</summary>
    public Boolean HasBlockingErrors(out IReadOnlyList<TokenDiagnostic> diagnostics)
    {
        diagnostics = Validate();
        return diagnostics.Count > 0;
    }

    String Describe(TokenNode node, ResolveResult r) => r.Status switch
    {
        ResolveStatus.Missing => $"别名 {node.AliasPath} 指向的令牌不存在（主题 {ThemeCode ?? "shared"}）",
        ResolveStatus.Cycle => $"别名链成环：{string.Join(" → ", r.Chain)}",
        ResolveStatus.TooDeep => $"别名链超过 {DesignSystemConstants.MaxAliasDepth} 层：{string.Join(" → ", r.Chain)}",
        ResolveStatus.LayerViolation => $"{node.Tier} 层令牌 {node.Path} 的别名逆向指向了更上层：{string.Join(" → ", r.Chain)}",
        _ => $"令牌 {node.Path} 既无值也无别名",
    };
}

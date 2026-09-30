namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>一个语义角色的取色要求。</summary>
/// <param name="Role">语义角色名（不含 semantic. 前缀）</param>
/// <param name="Family">取色族：brand|neutral|accent|success|warning|danger|info</param>
/// <param name="Usage">对比度用途（决定阈值）</param>
/// <param name="Against">与之配对的背景角色；null 表示与 surface-bg 配</param>
/// <param name="PreferL">审美锚点明度：满足阈值的前提下尽量靠近它（可空=取最贴合阈值的）</param>
/// <param name="MinRatio">最低要求（一般由 Usage 推导，显式给出则覆盖）</param>
public sealed record SemanticRole(
    String Role,
    String Family,
    ContrastMath.Usage Usage,
    String? Against = null,
    Double? PreferL = null,
    Double? MinRatio = null);

/// <summary>角色取色结果。</summary>
/// <param name="Role">语义角色</param>
/// <param name="TokenPath">选中的 primitive 令牌路径，如 color.neutral.900</param>
/// <param name="Hex">该 primitive 的 hex</param>
/// <param name="AgainstPath">配对背景路径</param>
/// <param name="AgainstHex">配对背景 hex</param>
/// <param name="Ratio">实测对比度</param>
/// <param name="Required">要求阈值</param>
/// <param name="Satisfied">是否达标（不达标不静默：交给审计记 critical）</param>
public sealed record RoleChoice(
    String Role,
    String TokenPath,
    String Hex,
    String? AgainstPath,
    String? AgainstHex,
    Double Ratio,
    Double Required,
    Boolean Satisfied);

/// <summary>
/// 语义角色解析：**按所需对比度反查 tone**（Leonardo 式定向选取），而不是先生成再事后审计。
///
/// 这是 v1 与 v2 的分水岭：v1 的 selfCheck 写着"对比度可达性 ≥4.5:1"却从未计算
/// （generate.ts:358 的散文），v2 由本类保证角色对默认达标，达不了的显式标未满足。
/// </summary>
public static class SemanticResolver
{
    /// <summary>明暗两套角色表。命名沿用参考物 Stardust 的 fg/surface/border/brand/link + 状态色习惯</summary>
    public static IReadOnlyList<SemanticRole> RolesFor(String themeCode) => IsDark(themeCode) ? DarkRoles : LightRoles;

    static readonly SemanticRole[] LightRoles =
    [
        new("surface-bg", "neutral", ContrastMath.Usage.Decorative, PreferL: 0.96),
        new("surface-1", "neutral", ContrastMath.Usage.Decorative, PreferL: 0.93),
        new("surface-2", "neutral", ContrastMath.Usage.Decorative, PreferL: 0.89),
        new("surface-3", "neutral", ContrastMath.Usage.Decorative, PreferL: 0.83),
        new("overlay", "neutral", ContrastMath.Usage.Decorative, PreferL: 0.75),
        new("text-1", "neutral", ContrastMath.Usage.TextNormal, PreferL: 0.25),
        new("text-2", "neutral", ContrastMath.Usage.TextNormal, PreferL: 0.45),
        new("text-3", "neutral", ContrastMath.Usage.TextLarge, PreferL: 0.60),
        new("border-1", "neutral", ContrastMath.Usage.NonTextUi, PreferL: 0.80),
        new("border-strong", "neutral", ContrastMath.Usage.NonTextUi, PreferL: 0.60),
        new("brand", "brand", ContrastMath.Usage.Decorative, PreferL: 0.62),
        new("brand-hover", "brand", ContrastMath.Usage.Decorative, PreferL: 0.55),
        new("brand-strong", "brand", ContrastMath.Usage.TextNormal, PreferL: 0.42),
        new("link", "accent", ContrastMath.Usage.TextNormal, PreferL: 0.55),
        new("success", "success", ContrastMath.Usage.TextNormal, PreferL: 0.52),
        new("warning", "warning", ContrastMath.Usage.TextNormal, PreferL: 0.58),
        new("danger", "danger", ContrastMath.Usage.TextNormal, PreferL: 0.52),
        new("info", "info", ContrastMath.Usage.TextNormal, PreferL: 0.55),
    ];

    static readonly SemanticRole[] DarkRoles =
    [
        new("surface-bg", "neutral", ContrastMath.Usage.Decorative, PreferL: 0.22),
        new("surface-1", "neutral", ContrastMath.Usage.Decorative, PreferL: 0.28),
        new("surface-2", "neutral", ContrastMath.Usage.Decorative, PreferL: 0.34),
        new("surface-3", "neutral", ContrastMath.Usage.Decorative, PreferL: 0.42),
        new("overlay", "neutral", ContrastMath.Usage.Decorative, PreferL: 0.50),
        new("text-1", "neutral", ContrastMath.Usage.TextNormal, PreferL: 0.93),
        new("text-2", "neutral", ContrastMath.Usage.TextNormal, PreferL: 0.78),
        new("text-3", "neutral", ContrastMath.Usage.TextLarge, PreferL: 0.62),
        new("border-1", "neutral", ContrastMath.Usage.NonTextUi, PreferL: 0.40),
        new("border-strong", "neutral", ContrastMath.Usage.NonTextUi, PreferL: 0.60),
        new("brand", "brand", ContrastMath.Usage.Decorative, PreferL: 0.66),
        new("brand-hover", "brand", ContrastMath.Usage.Decorative, PreferL: 0.74),
        new("brand-strong", "brand", ContrastMath.Usage.TextNormal, PreferL: 0.82),
        new("link", "accent", ContrastMath.Usage.TextNormal, PreferL: 0.72),
        new("success", "success", ContrastMath.Usage.TextNormal, PreferL: 0.74),
        new("warning", "warning", ContrastMath.Usage.TextNormal, PreferL: 0.80),
        new("danger", "danger", ContrastMath.Usage.TextNormal, PreferL: 0.66),
        new("info", "info", ContrastMath.Usage.TextNormal, PreferL: 0.74),
    ];

    public static Boolean IsDark(String? themeCode) =>
        themeCode is not ("light" or "compact" or null or "") &&
        (themeCode.Equals("dark", StringComparison.OrdinalIgnoreCase)
         || themeCode.Equals("high-contrast", StringComparison.OrdinalIgnoreCase)
         || themeCode.StartsWith("dark", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 填充色与压在它上面的文字必须成对达标：只按审美锚点选 brand 阶，
    /// 白字放上去常常只有 3.4:1（实测 light 主题 #7c3aed 就是这样），按钮正文就不合规。
    /// </summary>
    static readonly (String Fill, String Label, Double AnchorL, Double AnchorLDark)[] FillLabelPairs =
    [
        ("brand", "surface-1", 0.62, 0.66),
        ("danger", "surface-1", 0.52, 0.66),
    ];

    /// <summary>
    /// 为每个语义角色选一个 primitive 阶。
    /// </summary>
    /// <param name="ramps">族名 → 色阶（键如 brand/neutral/accent/success/warning/danger/info）</param>
    /// <param name="themeCode">主题编码，决定角色表与明暗方向</param>
    /// <param name="projectPrefix">令牌路径前缀（默认 color）</param>
    public static IReadOnlyList<RoleChoice> Resolve(
        IReadOnlyDictionary<String, IReadOnlyList<RampStep>> ramps,
        String? themeCode,
        String projectPrefix = "color")
    {
        var dark = IsDark(themeCode);
        var bg = PickClosest(GetRamp(ramps, "neutral"), dark ? 0.22 : 0.96);
        var bgHex = bg.Hex;
        var bgPath = $"{projectPrefix}.neutral.{bg.Step}";

        var list = new List<RoleChoice>();
        foreach (var role in RolesFor(themeCode ?? "light"))
        {
            var steps = GetRamp(ramps, role.Family);
            var againstPath = role.Against == null ? bgPath : null;
            var againstHex = role.Against == null ? bgHex : null;

            RampStep chosen;
            if (role.Usage == ContrastMath.Usage.Decorative)
            {
                // 背景/填充类不做对比度反查，只贴审美锚点（对比度由它上面的文字角色负责）
                chosen = PickClosest(steps, role.PreferL ?? 0.5);
            }
            else
            {
                var required = role.MinRatio ?? ContrastMath.AaThreshold(role.Usage);
                chosen = PickByContrast(steps, againstHex!, required, role.PreferL, dark);
            }

            var ratio = againstHex == null ? -1 : ContrastMath.Ratio(chosen.Hex, againstHex);
            list.Add(new RoleChoice(role.Role, $"{projectPrefix}.{role.Family}.{chosen.Step}", chosen.Hex,
                againstPath, againstHex, ratio,
                role.Usage == ContrastMath.Usage.Decorative ? -1 : (role.MinRatio ?? ContrastMath.AaThreshold(role.Usage)),
                role.Usage == ContrastMath.Usage.Decorative || ratio < 0 || ratio + 1e-6 >= (role.MinRatio ?? ContrastMath.AaThreshold(role.Usage))));
        }

        return AdjustFillForLabel(list, ramps, dark);
    }

    /// <summary>
    /// 第二轮：把"填充色"往能满足其标签文字 AA 的方向挪一档（仍在同族内，保持品牌识别）。
    /// 找不到任何达标阶时保留原选择并标 Satisfied=false —— 让审计报 critical，而不是静默通过。
    /// </summary>
    static List<RoleChoice> AdjustFillForLabel(List<RoleChoice> list, IReadOnlyDictionary<String, IReadOnlyList<RampStep>> ramps, Boolean dark)
    {
        for (var i = 0; i < list.Count; i++)
        {
            var pair = FillLabelPairs.FirstOrDefault(p => p.Fill == list[i].Role);
            if (pair.Fill == null) continue;

            var fill = list[i];
            var label = list.FirstOrDefault(x => x.Role == pair.Label);
            if (label == null || !TryParseHexes(label.Hex, fill.Hex, out var lab, out var fil)) continue;

            if (ContrastMath.Ratio(lab, fil) + 1e-6 >= ContrastMath.AaThreshold(ContrastMath.Usage.TextNormal)) continue;

            var steps = GetRamp(ramps, FamilyOf(fill.TokenPath));
            var anchor = dark ? pair.AnchorLDark : pair.AnchorL;
            var better = steps
                .Select(s => (Step: s, Ratio: ContrastMath.Ratio(label.Hex, s.Hex)))
                .Where(x => x.Ratio + 1e-6 >= ContrastMath.AaThreshold(ContrastMath.Usage.TextNormal))
                .OrderBy(x => Math.Abs(x.Step.Oklch.L - anchor))
                .ThenByDescending(x => x.Ratio)
                .FirstOrDefault().Step;

            if (better == null) continue;   // 该族无任何达标档：留给审计报 critical

            var newPath = ReplaceStep(fill.TokenPath, better);
            var againstBg = fill.AgainstHex == null ? -1 : ContrastMath.Ratio(better.Hex, fill.AgainstHex);
            list[i] = fill with
            {
                TokenPath = newPath,
                Hex = better.Hex,
                Ratio = againstBg,
                Satisfied = againstBg < 0 || againstBg + 1e-6 >= fill.Required,
            };
        }
        return list;
    }

    /// <summary>同族内换阶：只替换路径末段的阶名</summary>
    static String ReplaceStep(String tokenPath, RampStep step)
    {
        var i = tokenPath.LastIndexOf('.');
        return i < 0 ? step.Step : string.Concat(tokenPath.AsSpan(0, i), ".", step.Step);
    }

    static Boolean TryParseHexes(String a, String b, out Oklch.Color ca, out Oklch.Color cb)
    {
        var pa = Oklch.ParseHex(a);
        var pb = Oklch.ParseHex(b);
        ca = pa ?? default;
        cb = pb ?? default;
        return pa != null && pb != null;
    }

    static String FamilyOf(String tokenPath)
    {
        var parts = tokenPath.Split('.');
        return parts.Length >= 2 ? parts[1] : parts[0];
    }

    /// <summary>
    /// 对比度定向选 tone：先筛出所有满足阈值的阶，再在其中挑最贴近审美锚点的。
    /// 一个都不满足时退化为"最接近阈值的"，并把 Satisfied 置 false（不假装通过）。
    /// </summary>
    static RampStep PickByContrast(IReadOnlyList<RampStep> steps, String againstHex, Double minRatio, Double? preferL, Boolean dark)
    {
        var candidates = steps
            .Select(s => (Step: s, Ratio: ContrastMath.Ratio(s.Hex, againstHex)))
            .Where(x => x.Ratio >= minRatio - 1e-6)
            .ToList();

        if (candidates.Count == 0)
            return steps.OrderByDescending(x => ContrastMath.Ratio(x.Hex, againstHex)).First();

        // 满足阈值后，暗色主题优先更亮的一端、亮色主题优先更暗的一端，再按锚点收敛，
        // 避免出现"勉强达标但视觉上轻飘飘"的中间灰当正文色
        return candidates
            .OrderBy(x => Math.Abs(x.Step.Oklch.L - (preferL ?? (dark ? 0.9 : 0.2))))
            .ThenBy(x => dark ? -x.Step.Oklch.L : x.Step.Oklch.L)
            .First().Step;
    }

    static RampStep PickClosest(IReadOnlyList<RampStep> steps, Double targetL) =>
        steps.OrderBy(s => Math.Abs(s.Oklch.L - targetL)).First();

    static IReadOnlyList<RampStep> GetRamp(IReadOnlyDictionary<String, IReadOnlyList<RampStep>> ramps, String family) =>
        ramps.TryGetValue(family, out var steps) && steps.Count > 0
            ? steps
            : throw new ArgumentException($"色阶族 {family} 缺失，无法解析语义角色");
}

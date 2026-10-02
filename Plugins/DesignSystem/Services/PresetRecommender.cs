using NewLife;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>预设推荐命中（request 为深拷贝，调用方可安全改写）</summary>
public sealed record PresetMatch(String Id, String Name, String Tagline, Int32 Score,
    IReadOnlyList<String> Reasons, GenerationRequest Request);

/// <summary>
/// §D 预设推荐器：industry +40 / kind +30 / keyword +10（封顶 40）/ tone +10（封顶 30）/ density +15；
/// 同分按声明序；全 0 按声明序取前 N 且 reasons 说明。brandColor 覆盖 request.seedColor 并置空 hue。
/// </summary>
public static class PresetRecommender
{
    /// <summary>推荐预设（brief 推断行业可空——同 03-plan design_presets 出参 inferredIndustry）</summary>
    public static IReadOnlyList<PresetMatch> Recommend(String? brief, String? kind, String? industry,
        IReadOnlyList<String>? tones, String? density, String? brandColor, Int32 limit)
    {
        limit = Math.Clamp(limit, 1, 8);
        var inferred = DesignGenerator.InferIndustry(brief);
        var target = industry.IsNullOrEmpty() ? inferred : DesignGenerator.NormalizeIndustry(industry);

        var scored = new List<(StylePreset P, Int32 Score, List<String> Reasons)>();
        foreach (var p in StylePresets.All)
        {
            var score = 0;
            var reasons = new List<String>();

            if (!target.IsNullOrEmpty() && p.Request.Industry == target)
            {
                score += 40;
                reasons.Add($"行业匹配：{target}");
            }
            if (!kind.IsNullOrEmpty() && p.Kinds.Any(k => k.Equals(kind, StringComparison.OrdinalIgnoreCase)))
            {
                score += 30;
                reasons.Add($"类型匹配：{kind}");
            }
            if (!brief.IsNullOrEmpty())
            {
                var kw = 0;
                foreach (var k in p.Keywords)
                {
                    if (brief.Contains(k, StringComparison.OrdinalIgnoreCase))
                    {
                        kw += 10;
                        reasons.Add($"关键词命中：{k}");
                        if (kw >= 40) break;
                    }
                }
                score += kw;
            }
            if (tones != null)
            {
                var t = 0;
                foreach (var tone in tones)
                    if (p.Tones.Any(x => x.Equals(tone, StringComparison.OrdinalIgnoreCase)))
                    {
                        t += 10;
                        if (t >= 30) break;
                    }
                score += t;
            }
            if (!density.IsNullOrEmpty() && p.Request.Density == density)
            {
                score += 15;
                reasons.Add($"密度匹配：{density}");
            }
            scored.Add((p, score, reasons));
        }

        // 同分按声明序：Score 降序，声明序由 StylePresets.All 顺序天然保持
        var ordered = scored.OrderByDescending(x => x.Score).Take(limit).ToList();
        if (ordered.All(x => x.Score == 0) && scored.Count > 0)
        {
            var items = new List<PresetMatch>();
            for (var i = 0; i < limit && i < scored.Count; i++)
            {
                var (p, _, _) = scored[i];
                items.Add(new PresetMatch(p.Id, p.Name, p.Tagline, 0,
                    ["无匹配线索，按通用推荐排序"], WithSeedColor(p.Request, brandColor)));
            }
            return items;
        }

        return ordered.Select(x => new PresetMatch(x.P.Id, x.P.Name, x.P.Tagline, x.Score,
            x.Reasons, WithSeedColor(x.P.Request, brandColor))).ToList();
    }

    /// <summary>brandColor → request.seedColor 覆盖且 hue 置空（深拷贝，不影响目录）</summary>
    static GenerationRequest WithSeedColor(GenerationRequest src, String? brandColor)
    {
        var copy = Copy(src);
        if (!brandColor.IsNullOrEmpty())
        {
            copy.SeedColor = brandColor;
            copy.Hue = null;
        }
        return copy;
    }

    /// <summary>GenerationRequest 深拷贝（调用方改写不影响目录）</summary>
    public static GenerationRequest Copy(GenerationRequest src) => new()
    {
        Brief = src.Brief,
        SeedColor = src.SeedColor,
        Hue = src.Hue,
        AccentHueOffset = src.AccentHueOffset,
        Chroma = src.Chroma,
        Density = src.Density,
        TypeRatio = src.TypeRatio,
        TypeBasePx = src.TypeBasePx,
        RadiusBase = src.RadiusBase,
        MotionScale = src.MotionScale,
        BrandName = src.BrandName,
        Industry = src.Industry,
        Themes = [.. src.Themes],
    };
}

using System.Security.Cryptography;
using System.Text;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;
using NewLife;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>快速创建（B7）：按预设/参数建项目，apply=false 干跑不落库。</summary>
public sealed class QuickCreateService
{
    public const String CodePattern = "^[a-z0-9][a-z0-9-]{0,39}$";

    readonly DesignProjectService _projects;
    readonly GenerationService _generation;

    public QuickCreateService(DesignProjectService projects, GenerationService generation)
    {
        _projects = projects;
        _generation = generation;
    }

    /// <summary>干跑预览</summary>
    public sealed record Preview(String Code, Boolean CodeAvailable, String Name, String Seed,
        String Industry, Double Hue, Int32 Tokens, IReadOnlyList<String> Themes, IReadOnlyList<String> Notes);

    /// <summary>落库结果（project + generation + audit）</summary>
    public sealed record AppliedResult(DesignProject Project, GenerationOutcome Generation,
        AuditSummary Audit, IReadOnlyList<String> Warnings);

    /// <summary>
    /// 创建（apply=false 只干跑）。request 构造顺序：预设 → 显式参数覆盖预设 → 生成器默认兜底。
    /// code 校验：已存在 → 抛 <see cref="DesignProjectService.DesignConflictException"/>（工具层转中文冲突错误）。
    /// </summary>
    public Preview? Create(String name, String? code, String? kind, String? description, String? presetId,
        GenerationRequest overrides, Boolean apply,
        out String? uiRoute, out AppliedResult? applied, out String? error)
    {
        uiRoute = DesignSystemConstants.FrontendRoute;
        applied = null;
        error = null;

        if (name.IsNullOrWhiteSpace())
        {
            error = "name 必填（显示名，中文可）";
            return null;
        }
        var trimmed = name.Trim();

        GenerationRequest request;
        if (!presetId.IsNullOrEmpty())
        {
            var preset = StylePresets.Find(presetId);
            if (preset == null)
            {
                error = $"未知预设 {presetId}，可选：{string.Join(", ", StylePresets.All.Select(p => p.Id))}";
                return null;
            }
            request = PresetRecommender.Copy(preset.Request);
        }
        else
        {
            request = new GenerationRequest();
        }

        // 显式参数覆盖预设（03-plan §B7：显式参数覆盖预设，预设覆盖默认）
        ApplyOverrides(request, overrides);

        // §G：brief 缺省取 description ?? name（description 为空时用显示名兜底）
        if (request.Brief.IsNullOrEmpty())
            request.Brief = description.IsNullOrWhiteSpace() ? trimmed : description!;

        var finalCode = code.IsNullOrWhiteSpace() ? AllocateCode(trimmed) : code!.Trim().ToLowerInvariant();
        var codeRegex = new System.Text.RegularExpressions.Regex(CodePattern);
        if (!codeRegex.IsMatch(finalCode))
        {
            error = $"code 只能含小写字母/数字/中划线（^[a-z0-9][a-z0-9-]{{0,39}}$）：{finalCode}";
            return null;
        }
        var codeTaken = _projects.FindByCode(finalCode) != null;
        if (codeTaken)
        {
            error = $"项目 code「{finalCode}」已存在，请换一个（或留空自动分配）";
            return null;
        }

        // 干跑：确定性生成预览（不落库）
        var gen = DesignGenerator.Generate(request);
        var preview = new Preview(finalCode, !codeTaken, trimmed, gen.Seed, gen.Industry, gen.Hue,
            gen.Total, [.. request.Themes], [.. gen.Notes]);

        if (!apply)
        {
            return preview;
        }

        // 落库
        DesignProject project;
        try
        {
            project = _projects.Create(new ProjectInput
            {
                Code = finalCode,
                Name = trimmed,
                Kind = kind,
                Description = description,
            });
        }
        catch (Exception ex)
        {
            // 项目未建成（前序校验已排除 code/name 问题，此为防御），无项目可归档，直接报错
            error = $"生成失败：{ex.Message}";
            return null;
        }

        GenerationOutcome outcome;
        try
        {
            outcome = _generation.Run(project.Id, request, overwrite: false);
        }
        catch (Exception ex)
        {
            // §G：生成阶段抛异常 → 刚建的项目软归档（软删，数据保留），错误文案含 code
            _projects.Archive(project.Id);
            error = $"生成失败，已将刚建的项目 {finalCode} 归档（软删）：{ex.Message}";
            return null;
        }

        applied = new AppliedResult(project, outcome, outcome.Audit, BuildWarnings(outcome.Audit));
        return preview;
    }

    /// <summary>§G：审计阻断（critical &gt; 0）时给出发布前提示，否则空列表。</summary>
    public static IReadOnlyList<String> BuildWarnings(AuditSummary audit) =>
        audit.Blocking ? [$"审计存在 {audit.Critical} 条 critical，发布前需处理"] : [];

    /// <summary>显式参数覆盖预设（仅覆盖非空项；Apply 标志单独透传）</summary>
    void ApplyOverrides(GenerationRequest target, GenerationRequest o)
    {
        target.Brief = o.Brief ?? target.Brief;
        target.SeedColor = o.SeedColor ?? target.SeedColor;
        target.Hue = o.Hue ?? target.Hue;
        target.Chroma = o.Chroma ?? target.Chroma;
        target.Density = o.Density ?? target.Density;
        target.TypeRatio = o.TypeRatio ?? target.TypeRatio;
        target.TypeBasePx = o.TypeBasePx ?? target.TypeBasePx;
        target.RadiusBase = o.RadiusBase ?? target.RadiusBase;
        target.MotionScale = o.MotionScale ?? target.MotionScale;
        target.BrandName = o.BrandName ?? target.BrandName;
        target.Industry = o.Industry ?? target.Industry;
        // M3 风格轴：这里曾是"手写逐字段"的漏网之鱼——界面把轴发过来了，快速创建把它丢掉，
        // 于是"选了 flat 却拿到 soft 阴影"（e2e S2 实测抓到）。新增可空字段必须同步到这里，
        // 由 QuickCreateServiceTests 的反射守卫逐字段核对。
        target.ShadowStyle = o.ShadowStyle ?? target.ShadowStyle;
        target.ShadowStrength = o.ShadowStrength ?? target.ShadowStrength;
        target.BorderStrength = o.BorderStrength ?? target.BorderStrength;
        target.NeutralTemp = o.NeutralTemp ?? target.NeutralTemp;
        target.FontPairing = o.FontPairing ?? target.FontPairing;
        target.RadiusStyle = o.RadiusStyle ?? target.RadiusStyle;
        target.AccentStrategy = o.AccentStrategy ?? target.AccentStrategy;
        if (o.Themes is { Count: > 0 }) target.Themes = [.. o.Themes];
    }

    /// <summary>
    /// 自动分配 code（§G，确定性）：lower → 非 [a-z0-9] 换 - → 连续 - 合并 → 去首尾 -；
    /// 空/无字母数字 → `ds-` + SHA-256(name) 前 3 字节 6 位 hex；截 40；判重冲突试 -2…-99。
    /// </summary>
    public String AllocateCode(String name)
    {
        var slug = new StringBuilder();
        var hasAlnum = false;
        foreach (var c in name.Trim().ToLowerInvariant())
        {
            if (Char.IsAsciiLetterOrDigit(c)) { slug.Append(c); hasAlnum = true; }
            else slug.Append('-');
        }
        var s = slug.ToString();
        // 连续 - 合并
        var merged = new StringBuilder();
        Char prev = '\0';
        foreach (var c in s)
        {
            if (c == '-' && prev == '-') continue;
            merged.Append(c);
            prev = c;
        }
        var baseCode = merged.ToString().Trim('-').Trim();
        if (!hasAlnum || baseCode.Length == 0)
        {
            baseCode = "ds-" + StableHashHex(name);
        }
        if (baseCode.Length > 40) baseCode = baseCode[..40];
        if (baseCode.Length == 0 || !char.IsAsciiLetterOrDigit(baseCode[0])) baseCode = "ds-" + baseCode.TrimStart('-');

        // 判重冲突试 -2…-99
        if (_projects.FindByCode(baseCode) == null) return baseCode;
        for (var i = 2; i <= 99; i++)
        {
            var suffix = "-" + i;
            var candidate = baseCode[..Math.Min(baseCode.Length, 40 - suffix.Length)] + suffix;
            if (_projects.FindByCode(candidate) == null) return candidate;
        }
        return baseCode + "-" + StableHashHex(name);
    }

    static String StableHashHex(String text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes, 0, 3).ToLowerInvariant();
    }
}

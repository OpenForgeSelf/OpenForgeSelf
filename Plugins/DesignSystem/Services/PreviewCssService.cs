using ForgeSelf.Api.Plugins.DesignSystem.Entities;
using NewLife;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>
/// `preview-css` 入参：<see cref="GenerationRequest"/> 的平铺镜像 + 单个 <c>theme</c>。
/// 刻意**不收 <c>Themes</c> 列表**：预览只关心"这一套主题长什么样"，
/// 让调用方传整套色向既没必要，也会诱导它去猜生成器怎么处理多主题。
/// </summary>
public sealed class PreviewCssInput
{
    public String? Brief { get; set; }
    public String? SeedColor { get; set; }
    public Double? Hue { get; set; }
    public Double? AccentHueOffset { get; set; }
    public Double? Chroma { get; set; }
    public String? Density { get; set; }
    public Double? TypeRatio { get; set; }
    public Double? TypeBasePx { get; set; }
    public Double? RadiusBase { get; set; }
    public Double? MotionScale { get; set; }
    public String? BrandName { get; set; }
    public String? Industry { get; set; }

    // ---- M3 风格轴（与 GenerationRequest 同名同义；不镜像就等于展厅调了轴但预览不变，见 03-plan 偏差） ----
    public String? ShadowStyle { get; set; }
    public Double? ShadowStrength { get; set; }
    public String? BorderStrength { get; set; }
    public String? NeutralTemp { get; set; }
    public String? FontPairing { get; set; }
    public String? RadiusStyle { get; set; }
    public String? AccentStrategy { get; set; }

    /// <summary>要预览的主题（缺省 light）；只影响这一份内存 CSS，不落库</summary>
    public String? Theme { get; set; }

    /// <summary>平铺入参 → 生成请求（AccentHueOffset 缺省沿用生成器的 168，不在这里另写一份默认值）</summary>
    public GenerationRequest ToRequest() => new()
    {
        Brief = Brief,
        SeedColor = SeedColor,
        Hue = Hue,
        AccentHueOffset = AccentHueOffset ?? new GenerationRequest().AccentHueOffset,
        Chroma = Chroma,
        Density = Density,
        TypeRatio = TypeRatio,
        TypeBasePx = TypeBasePx,
        RadiusBase = RadiusBase,
        MotionScale = MotionScale,
        BrandName = BrandName,
        Industry = Industry,
        ShadowStyle = ShadowStyle,
        ShadowStrength = ShadowStrength,
        BorderStrength = BorderStrength,
        NeutralTemp = NeutralTemp,
        FontPairing = FontPairing,
        RadiusStyle = RadiusStyle,
        AccentStrategy = AccentStrategy,
    };
}

/// <summary>`preview-css` 出参：展厅"滑参数立刻看到的那份换肤 CSS"。</summary>
/// <param name="Theme">实际预览的主题（原样回显；缺省时回落 light）</param>
/// <param name="Css">与落库导出同源的 CSS 文本</param>
/// <param name="Seed">生成种子（可复现标识）</param>
/// <param name="Industry">解析后的行业倾向（未知值已回落 general）</param>
/// <param name="Hue">解析后的种子色相</param>
/// <param name="Notes">生成器提示（如 brief 未给种子色时按哈希定色相）</param>
public sealed record PreviewCssResult(String Theme, String Css, String Seed, String Industry, Double Hue, IReadOnlyList<String> Notes);

/// <summary>
/// 内存预览 CSS：把生成请求按**同一套生成器**在内存里跑一遍，构图后交给 <see cref="ExportService.ToCss"/>。
///
/// 三条纪律：
/// ① **零写库**：不建项目、不落令牌 —— 展厅滑动参数不该往库里灌一次性垃圾项目；
/// ② **同源**：构造走 <see cref="ExportService.BuildSnaps"/>、投影走 <see cref="ExportService.ToCss"/>，
///    与落库导出共用同一份逻辑，故 AC2 的"内存预览 == 落库导出"逐字成立；
/// ③ **不改写请求**：先克隆再收窄 <c>Themes</c>（共享的预设目录是单例，直接改会污染其他调用方）。
/// </summary>
public sealed class PreviewCssService
{
    readonly ExportService _export;

    public PreviewCssService(ExportService export) => _export = export;

    /// <summary>按请求与主题产出内存预览 CSS（不触碰数据库）。</summary>
    public PreviewCssResult Preview(GenerationRequest? request, String? theme)
    {
        var req = PresetRecommender.Copy(request ?? new GenerationRequest());
        GuardInput(req);   // 非法数值参数在入口显式拒绝（AC3：400，见 GuardInput 说明）
        var code = theme.IsNullOrEmpty() ? "light" : theme!;
        req.Themes = [code];   // 只看这一套主题：换肤预览不该顺带生成别的色向

        var result = DesignGenerator.Generate(req);
        var themed = result.Themed.TryGetValue(code, out var t) ? t : [];
        var graph = new TokenGraph(result.Shared.Select(ToNode), themed.Select(ToNode), code);
        // code/version 只进注释头（同源判据会去掉注释），这里给可辨识的占位值即可
        var project = new DesignProject { Code = "preview", Name = "内存预览", Version = "0.0.0" };
        var css = _export.ToCss(_export.SnapshotFromGraph(graph, project, code));
        return new PreviewCssResult(code, css, result.Seed, result.Industry, result.Hue, result.Notes);
    }

    /// <summary>
    /// 数值参数域校验（AC3「非法参数 400」）。
    ///
    /// 生成器本身对任意数值输入都不抛（未知 industry 回落 general、未知 theme 走浅色、色阶族恒全），
    /// 若不在入口显式拒绝，`preview-css` 就永远没有 400 —— 违反 spec。这里抛
    /// <see cref="ArgumentException"/>，由控制器 <c>Guard</c> 统一映射为 400。
    /// 刻意**只属于 preview-css**：既有 generate/preview 保持原语义不动（范围纪律，D1 裁定）。
    /// 判据：值域外显式拒绝；边界值放行（Hue=0 / RadiusBase=0 合法，见 AC3_合法边界值_不误伤）。
    /// </summary>
    static void GuardInput(GenerationRequest req)
    {
        if (req.Hue is < 0 or >= 360)
            throw new ArgumentException($"Hue 必须在 [0,360) 内，收到 {req.Hue}");
        if (req.Chroma is <= 0)
            throw new ArgumentException($"Chroma 必须 > 0，收到 {req.Chroma}");
        if (req.TypeRatio is <= 0)
            throw new ArgumentException($"TypeRatio 必须 > 0，收到 {req.TypeRatio}");
        if (req.TypeBasePx is <= 0)
            throw new ArgumentException($"TypeBasePx 必须 > 0，收到 {req.TypeBasePx}");
        if (req.RadiusBase is < 0)
            throw new ArgumentException($"RadiusBase 必须 >= 0，收到 {req.RadiusBase}");
        if (req.MotionScale is <= 0)
            throw new ArgumentException($"MotionScale 必须 > 0，收到 {req.MotionScale}");
    }

    /// <summary>生成补丁 → 图节点（字段序与 <see cref="TokenRepository"/> 的实体投影对齐，只取投影要用的列）。</summary>
    static TokenNode ToNode(TokenPatch p) => new(p.Path, p.Tier ?? "", p.Type ?? "", p.Value,
        p.AliasPath.IsNullOrEmpty() ? null : p.AliasPath, p.Extensions, p.ValueJson, p.Lifecycle, p.Description);
}
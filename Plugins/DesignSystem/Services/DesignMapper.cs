using ForgeSelf.Api.Plugins.DesignSystem.Entities;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>对外响应契约（不把 XCode 实体直接序列化出去：实体带脏标记/索引器等内部成员，形状不稳）。</summary>
public sealed record ProjectDto(Int64 Id, String Code, String Name, String? Description, String Kind, String Version, String Status,
    Int64 ParentProjectId, Int64 DefaultThemeId, String? SeedText, String? SeedJson, String? Generator, String? GeneratorVersion,
    String? SchemaVersion, String? ProjectionVersion, Int32 TokenCount, Int32 ComponentCount, DateTime? PublishedAt,
    DateTime CreatedAt, DateTime UpdatedAt);

/// <summary>主题</summary>
public sealed record ThemeDto(Int64 Id, Int64 ProjectId, String Code, String? Name, String ModeKind, Boolean IsDefault,
    Int64 BaseThemeId, Int32 SortOrder, DateTime UpdatedAt);

/// <summary>令牌（含解析后的有效值与诊断）</summary>
public sealed record TokenDto(Int64 Id, Int64 ProjectId, Int64 ThemeId, String Tier, String Path, String? Name, String Type,
    String? Value, String? ValueJson, String? AliasPath, String? Group, String? Description,
    String? ColorHex, Double OklchL, Double OklchC, Double OklchH, Double Alpha, String? ColorSpace, Boolean IsPrimary,
    String? ContrastOn, Double ContrastRatio, String? WcagLevel,
    String? Generator, String? GeneratorSeed, String? GeneratorVersion, String? Lifecycle, String? ReplacedBy,
    Boolean Deprecated, String? Tags, Int32 SortOrder, DateTime UpdatedAt);

/// <summary>有效令牌视图：解析别名后的结果，附来源路径与失败原因（复合值/描述/扩展一并带出，界面不必再打一次原行接口）</summary>
public sealed record EffectiveToken(String Path, String Tier, String Type, String Value, String SourcePath, String? AliasPath,
    Boolean Resolved, String? Error, String? ColorHex, Double ContrastRatio, String? WcagLevel,
    String? Description = null, String? ValueJson = null, String? Extensions = null);

/// <summary>组件</summary>
public sealed record ComponentDto(Int64 Id, Int64 ProjectId, String Code, String? Name, String? Category, Boolean Interactive,
    String? Description, String? DocJson, String? GuidanceJson, String? A11yNotes, String? TokenRefsJson, String? Status,
    String? Version, Int32 SortOrder, DateTime UpdatedAt);

/// <summary>组件变体</summary>
public sealed record VariantDto(Int64 Id, Int64 ComponentId, String ComponentCode, String Code, String? Name, String VariantJson,
    String State, Int64 ThemeId, String? TokenRefsJson, String? CssSnippet, String? ContrastSummary, Int32 SortOrder);

/// <summary>图标</summary>
public sealed record IconDto(Int64 Id, Int64 ProjectId, String Code, String? Name, String Collection, String? SvgBody,
    Double StrokeWidth, Int32 GridPx, String? ViewBox, String? Sizes, String? Tags, String? Usage, String? License);

/// <summary>资产（logo / 母题 / 插画等）</summary>
public sealed record AssetDto(Int64 Id, Int64 ProjectId, String Code, String? Name, String Kind, String? SvgBody, String? FileRef,
    String? TokenRefsJson, String? Description, String? License, Int32 SortOrder);

/// <summary>页面清单</summary>
public sealed record ScreenDto(Int64 Id, Int64 ProjectId, String Code, String? Title, String? IconCode, String? Route,
    Int64 ThemeId, String? ComponentIdsJson, String? Description, String? Notes, Int32 SortOrder);

/// <summary>字体资产</summary>
public sealed record FontDto(Int64 Id, Int64 ProjectId, String Family, Int32 Weight, String Style, String? FileName,
    String? FileRef, String? Display, String? Role, String? SourceUrl, String? License);

/// <summary>审计结论</summary>
public sealed record AuditDto(Int64 Id, String Kind, String? Rule, String Severity, String TargetType, String TargetPath,
    String? PairedPath, String? Expected, String? Actual, Double Ratio, Boolean Passed, String? Suggestion, String? Message,
    DateTime CheckedAt);

/// <summary>发布快照索引（不含快照文件物理路径：对外只给"可不可读"，路径属宿主文件系统）</summary>
public sealed record ReleaseDto(Int64 Id, Int64 ProjectId, String Version, String Status, String TokensHash,
    Int32 TokenCount, String? AuditSummary, Boolean AuditPassed, Int64 SourceReleaseId, String? ReleaseNotes,
    Boolean SnapshotAvailable, DateTime CreatedAt);

/// <summary>实体 → DTO 的集中映射（唯一一处，避免形状在多个端点间漂移）</summary>
public static class DesignMapper
{
    public static ProjectDto ToDto(DesignProject e) => ToDto(e, e.TokenCount, e.ComponentCount);

    /// <summary>带实时计数重载：列表与详情用它，避免把没人维护的缓存列当事实（曾显示"有效令牌 250 / 令牌数 0"）</summary>
    public static ProjectDto ToDto(DesignProject e, Int32 tokenCount, Int32 componentCount) => new(e.Id, e.Code, e.Name, e.Description, e.Kind, e.Version, e.Status,
        e.ParentProjectId, e.DefaultThemeId, e.SeedText, e.SeedJson, e.Generator, e.GeneratorVersion, e.SchemaVersion,
        e.ProjectionVersion, tokenCount, componentCount, e.PublishedAt, e.CreatedAt, e.UpdatedAt);

    public static ThemeDto ToDto(DesignTheme e) => new(e.Id, e.ProjectId, e.Code, e.Name, e.ModeKind, e.IsDefault,
        e.BaseThemeId, e.SortOrder, e.UpdatedAt);

    public static TokenDto ToDto(DesignToken e) => new(e.Id, e.ProjectId, e.ThemeId, e.Tier, e.Path, e.Name, e.Type,
        e.Value, e.ValueJson, e.AliasPath, e.Group, e.Description, e.ColorHex, e.OklchL, e.OklchC, e.OklchH, e.Alpha,
        e.ColorSpace, e.IsPrimary, e.ContrastOn, e.ContrastRatio, e.WcagLevel, e.Generator, e.GeneratorSeed,
        e.GeneratorVersion, e.Lifecycle, e.ReplacedBy, e.Deprecated, e.Tags, e.SortOrder, e.UpdatedAt);

    public static ComponentDto ToDto(DesignComponent e) => new(e.Id, e.ProjectId, e.Code, e.Name, e.Category, e.Interactive,
        e.Description, e.DocJson, e.GuidanceJson, e.A11yNotes, e.TokenRefsJson, e.Status, e.Version, e.SortOrder, e.UpdatedAt);

    public static VariantDto ToDto(DesignComponentVariant e) => new(e.Id, e.ComponentId, e.ComponentCode, e.Code, e.Name,
        e.VariantJson, e.State, e.ThemeId, e.TokenRefsJson, e.CssSnippet, e.ContrastSummary, e.SortOrder);

    public static IconDto ToDto(DesignIcon e) => new(e.Id, e.ProjectId, e.Code, e.Name, e.Collection, e.SvgBody,
        e.StrokeWidth, e.GridPx, e.ViewBox, e.Sizes, e.Tags, e.Usage, e.License);

    public static AssetDto ToDto(DesignAsset e) => new(e.Id, e.ProjectId, e.Code, e.Name, e.Kind, e.SvgBody, e.FileRef,
        e.TokenRefsJson, e.Description, e.License, e.SortOrder);

    public static ScreenDto ToDto(DesignScreen e) => new(e.Id, e.ProjectId, e.Code, e.Title, e.IconCode, e.Route,
        e.ThemeId, e.ComponentIdsJson, e.Description, e.Notes, e.SortOrder);

    public static FontDto ToDto(DesignFontFace e) => new(e.Id, e.ProjectId, e.Family, e.Weight, e.Style, e.FileName,
        e.FileRef, e.Display, e.Role, e.SourceUrl, e.License);

    public static AuditDto ToDto(DesignAudit e) => new(e.Id, e.Kind, e.Rule, e.Severity, e.TargetType, e.TargetPath,
        e.PairedPath, e.Expected, e.Actual, e.Ratio, e.Passed, e.Suggestion, e.Message, e.CheckedAt);

    public static ReleaseDto ToDto(DesignRelease e) => new(e.Id, e.ProjectId, e.Version, e.Status, e.TokensHash,
        e.TokenCount, e.AuditSummary, e.AuditPassed, e.SourceReleaseId, e.ReleaseNotes,
        !string.IsNullOrWhiteSpace(e.SnapshotFile) && File.Exists(e.SnapshotFile), e.CreatedAt);
}

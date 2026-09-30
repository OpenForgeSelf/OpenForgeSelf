using NewLife;
using NewLife.Log;
using XCode;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>项目创建输入</summary>
public sealed class ProjectInput
{
    public String Code { get; set; } = "";
    public String Name { get; set; } = "";
    public String? Description { get; set; }
    public String? Kind { get; set; }
    public String? SeedText { get; set; }
    public String? SeedJson { get; set; }
    public Int64 ParentProjectId { get; set; }
}

/// <summary>项目更新输入（部分更新：只改传了值的字段）</summary>
public sealed class ProjectPatch
{
    public String? Name { get; set; }
    public String? Description { get; set; }
    public String? Kind { get; set; }
    public String? Status { get; set; }
    public String? Version { get; set; }
    public String? SeedText { get; set; }
    public String? SeedJson { get; set; }
    public Int64? DefaultThemeId { get; set; }
    public String? Extensions { get; set; }
}

/// <summary>主题创建输入</summary>
public sealed class ThemeInput
{
    public String Code { get; set; } = "";
    public String? Name { get; set; }
    public String ModeKind { get; set; } = ThemeModeKinds.Color;
    public Boolean IsDefault { get; set; }
    public Int64 BaseThemeId { get; set; } = DesignSystemConstants.SharedThemeId;
    public Int32 SortOrder { get; set; }
}

/// <summary>业务校验失败（映射到 409/400，带可执行原因）</summary>
public sealed class DesignConflictException(String message) : Exception(message);

/// <summary>
/// 设计系统项目与主题的读写门面。
/// 唯一性判断一律直查库（plugin-development 铁律 11）；归档是软删（铁律 10，绝不物理删数据）。
/// </summary>
public sealed class DesignProjectService
{
    /// <summary>新项目的默认主题集：明暗两色向 + 紧凑密度（mode 是轴不是层级）</summary>
    static readonly (String Code, String Name, String Kind, Boolean Def, Int32 Sort)[] DefaultThemes =
    [
        ("light", "浅色", ThemeModeKinds.Color, true, 1),
        ("dark", "深色", ThemeModeKinds.Color, false, 2),
        ("high-contrast", "高对比", ThemeModeKinds.Color, false, 3),
        ("compact", "紧凑", ThemeModeKinds.Density, false, 4),
    ];

    public DesignProject? Find(Int64 projectId) =>
        DesignProject.FindAll(DesignProject._.Id == projectId).FirstOrDefault();

    public DesignProject? FindByCode(String code) =>
        DesignProject.FindAll(DesignProject._.Code == code).FirstOrDefault();

    public IList<DesignProject> List(String? status, String? keyword)
    {
        var exp = DesignProject._.Id > 0;
        if (!status.IsNullOrEmpty()) exp &= DesignProject._.Status == status;
        if (!keyword.IsNullOrEmpty())
            exp &= DesignProject._.Name.Contains(keyword!) | DesignProject._.Code.Contains(keyword!);
        // 项目量级小，直接内存排序，避免依赖 FindAll 的排序重载
        return DesignProject.FindAll(exp).OrderByDescending(p => p.UpdatedAt).ToList();
    }

    /// <summary>建项目并自动配齐默认主题；Code 冲突抛 <see cref="DesignConflictException"/></summary>
    public DesignProject Create(ProjectInput input)
    {
        var code = (input.Code ?? "").Trim().ToLowerInvariant();
        if (code.IsNullOrEmpty()) throw new ArgumentException("项目 Code 不能为空", nameof(input));
        if (input.Name.IsNullOrEmpty()) throw new ArgumentException("项目名称不能为空", nameof(input));

        // 直查库判重：缓存路径可能读出别的库的幽灵行，误报「已被占用」
        if (DesignProject.FindCount(DesignProject._.Code == code) > 0)
            throw new DesignConflictException($"项目标识 {code} 已存在");

        var now = DateTime.Now;
        var project = new DesignProject
        {
            Code = code,
            Name = input.Name!,
            Description = input.Description ?? "",
            Kind = input.Kind.IsNullOrEmpty() ? "product" : input.Kind!,
            Status = ProjectStatus.Draft,
            Version = "0.1.0",
            ParentProjectId = input.ParentProjectId,
            SeedText = input.SeedText ?? "",
            SeedJson = input.SeedJson ?? "",
            Generator = "oklch-ramp",
            GeneratorVersion = DesignSystemConstants.GeneratorVersion,
            SchemaVersion = DesignSystemConstants.ModelVersion,
            ProjectionVersion = DesignSystemConstants.ProjectionVersion,
            CreatedAt = now,
            UpdatedAt = now,
        };
        project.Insert();

        foreach (var (tc, tn, mk, def, sort) in DefaultThemes)
        {
            var theme = new DesignTheme
            {
                ProjectId = project.Id,
                Code = tc,
                Name = tn,
                ModeKind = mk,
                IsDefault = def,
                BaseThemeId = DesignSystemConstants.SharedThemeId,
                SortOrder = sort,
                CreatedAt = now,
                UpdatedAt = now,
            };
            theme.Insert();
            if (def)
            {
                project.DefaultThemeId = theme.Id;
                project.Save();
            }
        }

        XTrace.Log.Info("[DesignSystem] 项目已创建 {0}（默认主题已配齐）", code);
        return project;
    }

    /// <summary>部分更新项目字段</summary>
    public DesignProject Update(Int64 projectId, ProjectPatch patch)
    {
        var p = Find(projectId) ?? throw new KeyNotFoundException($"项目 {projectId} 不存在");
        if (!patch.Name.IsNullOrEmpty()) p.Name = patch.Name!;
        if (patch.Description != null) p.Description = patch.Description;
        if (!patch.Kind.IsNullOrEmpty()) p.Kind = patch.Kind!;
        if (!patch.Status.IsNullOrEmpty())
        {
            if (patch.Status != ProjectStatus.Draft && patch.Status != ProjectStatus.Published && patch.Status != ProjectStatus.Archived)
                throw new ArgumentException($"非法状态 {patch.Status}", nameof(patch));
            p.Status = patch.Status!;
        }
        if (!patch.Version.IsNullOrEmpty()) p.Version = patch.Version!;
        if (patch.SeedText != null) p.SeedText = patch.SeedText;
        if (patch.SeedJson != null) p.SeedJson = patch.SeedJson;
        if (patch.DefaultThemeId != null) p.DefaultThemeId = patch.DefaultThemeId.Value;
        if (patch.Extensions != null) p.Extensions = patch.Extensions;
        p.UpdatedAt = DateTime.Now;
        p.Save();
        return p;
    }

    /// <summary>归档（软删）。数据行与库文件一律保留。</summary>
    public DesignProject Archive(Int64 projectId) => Update(projectId, new ProjectPatch { Status = ProjectStatus.Archived });

    public DesignTheme? FindTheme(Int64 themeId) =>
        DesignTheme.FindAll(DesignTheme._.Id == themeId).FirstOrDefault();

    public DesignTheme? FindTheme(Int64 projectId, String code) =>
        DesignTheme.FindAll(DesignTheme._.ProjectId == projectId & DesignTheme._.Code == code.Trim().ToLowerInvariant()).FirstOrDefault();

    public IList<DesignTheme> ListThemes(Int64 projectId) =>
        DesignTheme.FindAll(DesignTheme._.ProjectId == projectId).OrderBy(t => t.SortOrder).ThenBy(t => t.Id).ToList();

    /// <summary>
    /// 令牌条数（读取时真算）。
    /// 表里的 TokenCount/ComponentCount 是历史缓存列，没人负责在每次令牌写入后更新它 ——
    /// 直接把缓存列当事实会显示"有 250 条有效令牌的项目：令牌数 0"，故一律现算。
    /// </summary>
    public Int32 CountTokens(Int64 projectId) => (Int32)DesignToken.FindCount(DesignToken._.ProjectId == projectId);

    /// <summary>组件条数（同上，现算）</summary>
    public Int32 CountComponents(Int64 projectId) => (Int32)DesignComponent.FindCount(DesignComponent._.ProjectId == projectId);

    /// <summary>新增主题（如自定义品牌色向）；Code 冲突抛 <see cref="DesignConflictException"/></summary>
    public DesignTheme AddTheme(Int64 projectId, ThemeInput input)
    {
        var code = input.Code.Trim().ToLowerInvariant();
        if (code.IsNullOrEmpty()) throw new ArgumentException("主题 Code 不能为空", nameof(input));
        // mode 轴取值必须白名单校验：界面下拉只是第一道防线，直接打接口的请求得在这里挡住
        if (!ThemeModeKinds.All.Contains(input.ModeKind))
            throw new ArgumentException($"ModeKind 只能是 {string.Join("/", ThemeModeKinds.All)}，收到 {input.ModeKind}", nameof(input));
        if (DesignTheme.FindCount(DesignTheme._.ProjectId == projectId & DesignTheme._.Code == code) > 0)
            throw new DesignConflictException($"主题 {code} 已存在于项目 {projectId}");

        var now = DateTime.Now;
        var theme = new DesignTheme
        {
            ProjectId = projectId,
            Code = code,
            Name = input.Name.IsNullOrEmpty() ? code : input.Name!,
            ModeKind = input.ModeKind,
            IsDefault = input.IsDefault,
            BaseThemeId = input.BaseThemeId,
            SortOrder = input.SortOrder,
            CreatedAt = now,
            UpdatedAt = now,
        };
        theme.Insert();

        if (input.IsDefault)
        {
            foreach (var other in ListThemes(projectId).Where(t => t.IsDefault && t.Id != theme.Id))
            {
                other.IsDefault = false;
                other.Save();
            }
            var p = Find(projectId);
            if (p != null) { p.DefaultThemeId = theme.Id; p.Save(); }
        }
        return theme;
    }

    /// <summary>把主题码解析成 ThemeId（shared/null → 0）</summary>
    public Int64 ResolveThemeId(Int64 projectId, String? themeCode)
    {
        if (themeCode.IsNullOrEmpty() || themeCode == "shared") return DesignSystemConstants.SharedThemeId;
        var t = FindTheme(projectId, themeCode!) ?? throw new KeyNotFoundException($"主题 {themeCode} 不存在");
        return t.Id;
    }
}

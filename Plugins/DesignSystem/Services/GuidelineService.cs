using ForgeSelf.Api.Plugins.DesignSystem.Entities;
using NewLife;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>生成结果：四类计数都必须回给调用方，"生成了但一条没建"不能看着像成功（自查表 #10）</summary>
/// <param name="Created">新建条数</param>
/// <param name="Skipped">已存在、未动的条数</param>
/// <param name="SkippedProtected">因用户手改（Source=manual）而跳过的条数</param>
/// <param name="Overwritten">overwrite=true 时被覆盖的 code</param>
public sealed record GuidelineGenerateResult(IReadOnlyList<String> Created, IReadOnlyList<String> Skipped,
    IReadOnlyList<String> SkippedProtected, IReadOnlyList<String> Overwritten)
{
    public Int32 Total => Created.Count + Skipped.Count + SkippedProtected.Count + Overwritten.Count;
}

/// <summary>
/// UX 规范服务：生成（只补空）、保存（upsert + 校验）、归档（软删）。
///
/// 与 M1 <see cref="DesignGenerator.SeedBrandCatalog"/> 同一套纪律：
/// - **只补空不覆盖**，用户手改（<c>Source=manual</c>）的行默认保护，要覆盖必须显式 <c>overwrite=true</c> 且在响应里列出被覆盖的 code；
/// - 引用不存在的路径 → 400 并**逐条列出**，零行写入（半条规范比没有规范更坏：它会误导审查）；
/// - 归档是改状态，本服务没有任何删除方法（铁律 10）。
///
/// 不 sealed：生成链路只通过 <see cref="SeedGuidelines"/> 调进来，测试需要一个能让它抛异常的替身来验
/// 「播种失败不判死生成、但必须在 Notes 里可见」这条（GuidelineServiceTests）。
/// </summary>
public class GuidelineService
{
    readonly DesignProjectService _projects;
    readonly TokenRepository _tokens;
    readonly GuidelineRepository _guidelines;

    public GuidelineService(DesignProjectService projects, TokenRepository tokens, GuidelineRepository guidelines)
    {
        _projects = projects;
        _tokens = tokens;
        _guidelines = guidelines;
    }

    /// <summary>读清单（默认排除 archived）。控制器与工具都经本服务，不各自持有仓储</summary>
    public IList<DesignGuideline> List(Int64 projectId, String? status = null, String? category = null) =>
        _guidelines.List(projectId, status, category);

    public DesignGuideline? Find(Int64 projectId, String code) => _guidelines.Find(projectId, code);

    /// <summary>项目里可被引用的令牌路径（共享层 + 各主题覆盖层，去重）</summary>
    public List<String> TokenPaths(Int64 projectId)
    {
        var paths = _tokens.FindShared(projectId).Select(t => t.Path).ToList();
        foreach (var theme in _projects.ListThemes(projectId))
            if (theme.Id != DesignSystemConstants.SharedThemeId)
                paths.AddRange(_tokens.FindThemed(projectId, theme.Id).Select(t => t.Path));
        return paths.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// 生成默认规范。<paramref name="overwrite"/>=false 时只补空并保护手改行；=true 时覆盖 generated 行，
    /// 手改行仍被覆盖但会在 <see cref="GuidelineGenerateResult.Overwritten"/> 里点名。
    /// </summary>
    public GuidelineGenerateResult Generate(Int64 projectId, Boolean overwrite = false)
    {
        var project = _projects.Find(projectId) ?? throw new KeyNotFoundException($"项目 {projectId} 不存在");
        var ctx = Context(project);
        var paths = TokenPaths(projectId);
        var drafts = GuidelineGenerator.Generate(ctx.Kind, ctx.Industry, ctx.Density, paths);

        var created = new List<String>();
        var skipped = new List<String>();
        var protectedSkipped = new List<String>();
        var overwritten = new List<String>();

        // 判重必须连归档行一起看：只看未归档会让"归档后重新生成"又建一条同 code 的新行，撞唯一索引
        var existing = _guidelines.List(projectId, "all").ToDictionary(g => g.Code, StringComparer.Ordinal);

        var sort = existing.Count > 0 ? existing.Values.Max(g => g.SortOrder) + 1 : 0;
        foreach (var d in drafts)
        {
            existing.TryGetValue(d.Code, out var row);
            if (row != null)
            {
                if (row.IsManualProtected && !overwrite)
                {
                    protectedSkipped.Add(d.Code);
                    continue;
                }
                if (!overwrite)
                {
                    skipped.Add(d.Code);
                    continue;
                }
                ApplyDraft(row, d, ctx.Seed);
                overwritten.Add(d.Code);
                continue;
            }

            var e = new DesignGuideline
            {
                ProjectId = projectId,
                Code = d.Code,
                Category = d.Category,
                Title = d.Title,
                Summary = d.Summary,
                Body = d.Body,
                RulesJson = GuidelineRepository.WriteRules(d.Rules),
                TokenRefsJson = GuidelineRepository.WritePaths(d.TokenRefs),
                AppliesToJson = GuidelineRepository.WritePaths(d.AppliesTo),
                Source = "generated",
                Status = "adopted",
                GeneratorVersion = GuidelineGenerator.Version,
                GeneratorSeed = ctx.Seed,
                SortOrder = sort++,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
            };
            e.Save();
            created.Add(d.Code);
        }

        return new GuidelineGenerateResult(created, skipped, protectedSkipped, overwritten);
    }

    /// <summary>
    /// 生成链路的挂载点（<see cref="GenerationService"/> 在组件/品牌种子之后调用）：**只补空**。
    /// 返回的是"库里现在有几条能用"（未归档），不是"这次新增了几条"——第二次生成必然零新增，
    /// 拿新增数当响应会让界面上的规范凭空变成 0（自查表 #10）。
    /// </summary>
    public virtual Int32 SeedGuidelines(Int64 projectId)
    {
        Generate(projectId, false);
        return _guidelines.List(projectId).Count;
    }

    /// <summary>保存（upsert）。校验通过后写入，并把 Source 置为 manual（用户改过的东西不该被下次生成悄悄覆盖）</summary>
    public DesignGuideline Save(Int64 projectId, String code, GuidelinePatch patch)
    {
        var project = _projects.Find(projectId) ?? throw new KeyNotFoundException($"项目 {projectId} 不存在");
        code = (code ?? "").Trim();
        if (!GuidelineCategories.IsCode(code))
            throw new ArgumentException($"规范标识 {code} 不合形（须匹配 {GuidelineCategories.CodePattern}）");

        if (patch.Category != null && !GuidelineCategories.Has(patch.Category))
            throw new ArgumentException($"分类 {patch.Category} 非法，可用：{String.Join("|", GuidelineCategories.All)}");
        if (patch.Status != null && !GuidelineCategories.HasStatus(patch.Status))
            throw new ArgumentException($"状态 {patch.Status} 非法，可用：{String.Join("|", GuidelineCategories.Statuses)}（归档=软删，不提供删除）");
        if (patch.Body != null && patch.Body.Length > GuidelineCategories.MaxBodyLength)
            throw new ArgumentException($"正文长度 {patch.Body.Length} 超过上限 {GuidelineCategories.MaxBodyLength}");
        if (patch.Rules != null)
        {
            if (patch.Rules.Count > GuidelineCategories.MaxRulesPerGuideline)
                throw new ArgumentException($"规则条数 {patch.Rules.Count} 超过上限 {GuidelineCategories.MaxRulesPerGuideline}");
            foreach (var r in patch.Rules)
                if (!r.Level.IsNullOrEmpty() && !GuidelineCategories.HasLevel(r.Level))
                    throw new ArgumentException($"规则级别 {r.Level} 非法，可用：{String.Join("|", GuidelineCategories.Levels)}");
        }

        var existing = _guidelines.Find(projectId, code);
        if (existing == null && _guidelines.Count(projectId) >= GuidelineCategories.MaxPerProject)
            throw new ArgumentException($"规范条数已达上限 {GuidelineCategories.MaxPerProject}，请先归档不再适用的条目（归档是软删，数据保留）");

        if (patch.TokenRefs is { Count: > 0 })
        {
            var paths = TokenPaths(projectId).ToHashSet(StringComparer.Ordinal);
            var missing = patch.TokenRefs.Select(p => p.Trim()).Where(p => p.Length > 0 && !paths.Contains(p)).Distinct(StringComparer.Ordinal).ToList();
            if (missing.Count > 0)
                throw new ArgumentException($"引用了项目里不存在的令牌，本条未写入：{String.Join("、", missing)}");
        }

        // ExpectUpdatedAt 的比对交给仓储（它已经读到了行，避免多读一次）
        patch.Title ??= existing == null ? code : null;
        var saved = _guidelines.Upsert(projectId, code, patch);
        if (!saved.Source.Equals("manual", StringComparison.OrdinalIgnoreCase))
        {
            saved.Source = "manual";
            saved.UpdatedAt = DateTime.Now;
            saved.Save();
        }
        return saved;
    }

    /// <summary>归档（软删）。行仍在库里、仍可恢复，本方法不删任何数据。</summary>
    public DesignGuideline? Archive(Int64 projectId, String code) => _guidelines.Archive(projectId, code);

    void ApplyDraft(DesignGuideline row, GuidelineDraft d, String seed)
    {
        row.Category = d.Category;
        row.Title = d.Title;
        row.Summary = d.Summary;
        row.Body = d.Body;
        row.RulesJson = GuidelineRepository.WriteRules(d.Rules);
        row.TokenRefsJson = GuidelineRepository.WritePaths(d.TokenRefs);
        row.AppliesToJson = GuidelineRepository.WritePaths(d.AppliesTo);
        row.GeneratorVersion = GuidelineGenerator.Version;
        row.GeneratorSeed = seed;
        row.UpdatedAt = DateTime.Now;
        row.Save();
    }

    /// <summary>
    /// 生成上下文：kind 取项目用途；industry 由项目描述/名称推断；
    /// **density 从产物反推**（基准间距的实际值只能是某个密度档算出来的，比再存一份参数更接近真源）。
    /// 取 `space.2`（倍率 1，就是基准本身）而不是 `space.4`：倍率 2 会让 default 项目的 8px 撞上 comfortable 的基准 8px。
    /// </summary>
    (String Kind, String Industry, String Density, String Seed) Context(DesignProject project)
    {
        var kind = project.Kind.IsNullOrEmpty() ? "product" : project.Kind!;
        var industry = DesignGenerator.InferIndustry(project.Description) ?? DesignGenerator.InferIndustry(project.Name) ?? "general";
        var density = DensityFromSpace(_tokens.FindShared(project.Id).FirstOrDefault(t => t.Path == "space.2")?.Value);
        return (kind, industry, density, GuidelineGenerator.SeedFor(kind, industry, density));
    }

    /// <summary>反查密度档：把间距基准值代回 <see cref="ScaleGenerators.BaseUnit"/>，不在这里重列像素数字（避免第二份真相）</summary>
    static String DensityFromSpace(String? raw)
    {
        if (raw.IsNullOrEmpty()) return "default";
        var num = new String(raw.Where(c => char.IsAsciiDigit(c) || c is '.' or '-').ToArray());
        if (!Double.TryParse(num, System.Globalization.CultureInfo.InvariantCulture, out var px)) return "default";

        foreach (var density in new[] { "default", "compact", "comfortable" })
            if (Math.Abs(ScaleGenerators.BaseUnit(density) - px) < 0.001) return density;
        return "default";   // 手改过基准间距的项目：回落 default 只会让规范引用哪一档不同，不会写错数字
    }
}

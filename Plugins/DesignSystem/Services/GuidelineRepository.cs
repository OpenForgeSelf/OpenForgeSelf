using System.Text.Json;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;
using NewLife;
using XCode;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>规则入参（id 可空，服务层补 kebab 默认值）</summary>
public sealed record GuidelineRuleInput(String? Id, String? Level, String? Text);

/// <summary>
/// 规范写入入参：字段为 null 表示"不改这一项"（点即保存），空数组表示"清成空"。
/// <c>Status</c> 可用来把 archived 行恢复为 adopted。
/// </summary>
public sealed class GuidelinePatch
{
    public String? Title { get; set; }
    public String? Summary { get; set; }
    public String? Body { get; set; }
    public List<GuidelineRuleInput>? Rules { get; set; }
    public List<String>? TokenRefs { get; set; }
    public List<String>? AppliesTo { get; set; }
    public String? Category { get; set; }
    public String? Status { get; set; }
    /// <summary>乐观并发：传了就必须与库中 UpdatedAt 一致，否则 409（同 TokenPatch 的口径）</summary>
    public DateTime? ExpectUpdatedAt { get; set; }
}

/// <summary>
/// <see cref="DesignGuideline"/> 的读写门面。
///
/// 三条既有纪律照抄（同 <see cref="CatalogRepository"/> / <see cref="TokenRepository"/>）：
/// - 唯一性**直查库**判重，不碰 <c>Meta.Cache</c>（缓存是执行上下文级，切库/多实例会读出幽灵行，铁律 11）；
/// - 整批写入走一个事务，失败零行落库，不留半成品；
/// - 只写不删：归档 = <c>Status=archived</c>（铁律 10）。本类没有任何 Delete 出口。
/// </summary>
public sealed class GuidelineRepository
{
    static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>
    /// 某项目的规范清单。
    /// <paramref name="status"/>：null = 排除 archived（读路径的默认口径）；<c>"all"</c> = 全都要（快照与导出用，
    /// 归档行也必须进版本，否则"归档"这个动作在两个版本之间不可见）；其它 = 只要这一状态。
    /// </summary>
    public IList<DesignGuideline> List(Int64 projectId, String? status = null, String? category = null)
    {
        var exp = DesignGuideline._.ProjectId == projectId;
        if (status.IsNullOrEmpty()) exp &= DesignGuideline._.Status != "archived";
        else if (!status.Equals("all", StringComparison.OrdinalIgnoreCase)) exp &= DesignGuideline._.Status == status;
        if (!category.IsNullOrEmpty()) exp &= DesignGuideline._.Category == category;

        return DesignGuideline.QueryAll(exp)
            .OrderBy(g => g.SortOrder)
            .ThenBy(g => g.Code, StringComparer.Ordinal)
            .ToList();
    }

    public DesignGuideline? Find(Int64 projectId, String code) =>
        DesignGuideline.QueryAll(DesignGuideline._.ProjectId == projectId & DesignGuideline._.Code == code.Trim()).FirstOrDefault();

    public Int64 Count(Int64 projectId) => DesignGuideline.QueryCount(DesignGuideline._.ProjectId == projectId);

    /// <summary>读 JSON 袋；解析失败回空集合（宁可显示成"没有"，也不让一个坏行把整页打死）</summary>
    public static List<String> ReadPaths(String? json) => ReadArray(json).ToList();

    public static List<GuidelineRule> ReadRules(String? json)
    {
        var list = new List<GuidelineRule>();
        if (json.IsNullOrEmpty()) return list;
        try
        {
            using var doc = JsonDocument.Parse(json!);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return list;
            var i = 0;
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var id = el.TryGetProperty("id", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                var level = el.TryGetProperty("level", out var l) ? l.GetString() : null;
                var text = el.TryGetProperty("text", out var t) ? t.GetString() : null;
                list.Add(new GuidelineRule(
                    id.IsNullOrEmpty() ? $"rule-{i + 1}" : id!,
                    level.IsNullOrEmpty() ? "SHOULD" : level!.Trim().ToUpperInvariant(),
                    text ?? ""));
                i++;
            }
        }
        catch (JsonException) { /* 半截 JSON 当空处理：写入侧已被 Biz 挡住，这里只兜历史脏数据 */ }

        return list;
    }

    static IEnumerable<String> ReadArray(String? json)
    {
        if (json.IsNullOrEmpty()) yield break;
        JsonElement root;
        try
        {
            root = JsonDocument.Parse(json!).RootElement;
        }
        catch (JsonException) { yield break; }

        if (root.ValueKind != JsonValueKind.Array) yield break;
        foreach (var el in root.EnumerateArray())
            if (el.ValueKind == JsonValueKind.String && el.GetString() is { Length: > 0 } s)
                yield return s;
    }

    public static String WritePaths(IEnumerable<String>? paths) =>
        JsonSerializer.Serialize((paths ?? []).Select(p => p.Trim()).Where(p => p.Length > 0).Distinct(StringComparer.Ordinal), JsonOpts);

    public static String WriteRules(IEnumerable<GuidelineRule>? rules) =>
        JsonSerializer.Serialize((rules ?? []).Select(r => new { id = r.Id, level = r.Level, text = r.Text }), JsonOpts);

    /// <summary>
    /// 插入或更新一行（按 <c>(ProjectId, Code)</c> 判重，直查库）。
    /// 返回的实体带最新的 <c>UpdatedAt</c>，供调用方回读与乐观并发比对。
    /// </summary>
    public DesignGuideline Upsert(Int64 projectId, String code, GuidelinePatch patch, String? generatorSeed = null)
    {
        code = code.Trim();
        var e = Find(projectId, code);
        var isNew = e == null;
        if (e == null)
        {
            e = new DesignGuideline { ProjectId = projectId, Code = code, CreatedAt = DateTime.Now };
        }
        else if (patch.ExpectUpdatedAt != null && e.UpdatedAt != patch.ExpectUpdatedAt.Value)
        {
            throw new DesignConflictException($"规范 {code} 已被他人改动（库里 UpdatedAt={e.UpdatedAt:O}，你读到的是 {patch.ExpectUpdatedAt.Value:O}），本次未写入");
        }

        if (patch.Title != null) e.Title = patch.Title.Trim();
        if (patch.Summary != null) e.Summary = patch.Summary.Trim();
        if (patch.Body != null) e.Body = patch.Body;
        if (patch.Category != null) e.Category = patch.Category;
        if (patch.Status != null) e.Status = patch.Status;
        if (patch.Rules != null) e.RulesJson = WriteRules(patch.Rules.Select((r, i) => NormalizeRule(r, i, code)));
        if (patch.TokenRefs != null) e.TokenRefsJson = WritePaths(patch.TokenRefs);
        if (patch.AppliesTo != null) e.AppliesToJson = WritePaths(patch.AppliesTo);
        if (generatorSeed != null) e.GeneratorSeed = generatorSeed;

        // 首次落库补齐默认值：Status/Source 有列默认值，但新建实体在内存里是空串，不补会被 Biz 的词表校验拒掉
        if (isNew)
        {
            if (e.Status.IsNullOrEmpty()) e.Status = "adopted";
            if (e.Source.IsNullOrEmpty()) e.Source = "manual";      // 走这条路径的都是用户/工具写入 → 受重新生成保护
            if (e.Category.IsNullOrEmpty()) e.Category = "layout";
            if (string.IsNullOrEmpty(e.Title)) e.Title = code;
        }

        e.UpdatedAt = DateTime.Now;
        e.Save();
        return e;
    }

    static GuidelineRule NormalizeRule(GuidelineRuleInput r, Int32 index, String code)
    {
        var text = (r.Text ?? "").Trim();
        if (text.Length == 0) throw new ArgumentException($"规范 {code} 第 {index + 1} 条规则文本为空");
        var level = (r.Level ?? "").Trim().ToUpperInvariant();
        if (!GuidelineCategories.HasLevel(level))
            throw new ArgumentException($"规范 {code} 第 {index + 1} 条规则的级别 {r.Level} 非法，可用：{String.Join("|", GuidelineCategories.Levels)}");
        var id = (r.Id ?? "").Trim();
        if (id.Length == 0) id = $"{code}-{index + 1}".ToLowerInvariant();
        if (!GuidelineCategories.IsCode(id))
            throw new ArgumentException($"规范 {code} 第 {index + 1} 条规则 id {id} 不合形（须是小写 kebab）");
        return new GuidelineRule(id, level, text);
    }

    /// <summary>归档（软删）。恢复走 Upsert(status=adopted)，本类不提供任何删除出口。</summary>
    public DesignGuideline? Archive(Int64 projectId, String code)
    {
        var e = Find(projectId, code);
        if (e == null) return null;
        e.Status = "archived";
        e.UpdatedAt = DateTime.Now;
        e.Save();
        return e;
    }

    /// <summary>整批写入（生成器播种用）：一次事务，任一行失败整批回滚</summary>
    public Int32 SaveBatch(Int64 projectId, IEnumerable<DesignGuideline> entities)
    {
        var n = 0;
        using var et = new EntityTransaction<DesignGuideline>();
        foreach (var e in entities) { e.Save(); n++; }
        et.Commit();
        return n;
    }
}

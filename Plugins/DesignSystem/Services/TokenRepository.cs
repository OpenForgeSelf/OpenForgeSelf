using System.Linq;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using XCode;
using XCode.DataAccessLayer;
using ForgeSelf.Api.Plugins.DesignSystem.Data;
using ForgeSelf.Api.Plugins.DesignSystem.Entities;

namespace ForgeSelf.Api.Plugins.DesignSystem.Services;

/// <summary>令牌写入载荷：全部字段可空，只更新传了的字段（点即保存 + 部分更新）。</summary>
public sealed class TokenPatch
{
    public String Path { get; set; } = "";
    public Int64 ThemeId { get; set; } = DesignSystemConstants.SharedThemeId;
    public String? Tier { get; set; }
    public String? Name { get; set; }
    public String? Type { get; set; }
    public String? Value { get; set; }
    public String? ValueJson { get; set; }
    public String? AliasPath { get; set; }
    public String? Group { get; set; }
    public String? Description { get; set; }
    public String? ColorSpace { get; set; }
    public Double? Alpha { get; set; }
    public Boolean? IsPrimary { get; set; }
    public String? Tags { get; set; }
    public String? Lifecycle { get; set; }
    public String? ReplacedBy { get; set; }
    public Boolean? Deprecated { get; set; }
    public Int32? SortOrder { get; set; }
    public String? Extensions { get; set; }
    public String? Generator { get; set; }
    public String? GeneratorSeed { get; set; }

    /// <summary>乐观并发：传了就必须与库中 UpdatedAt 一致，否则 409</summary>
    public DateTime? ExpectUpdatedAt { get; set; }
}

/// <summary>批量写入结果</summary>
public sealed class UpsertResult
{
    public Int32 Created { get; set; }
    public Int32 Updated { get; set; }

    /// <summary>因 Generator=manual/imported 而被保护、未被覆盖的行数（AC18）</summary>
    public Int32 SkippedProtected { get; set; }

    /// <summary>与受保护行冲突的载荷路径（前端提示"这些是你手改过的，需要显式覆盖"）</summary>
    public List<String> Conflicts { get; } = new();

    /// <summary>校验失败明细；非空表示整批已回滚，零行落库（AC4）</summary>
    public List<TokenDiagnostic> Diagnostics { get; } = new();

    public Boolean Succeeded => Diagnostics.Count == 0;
}

/// <summary>分页令牌查询结果</summary>
public sealed record TokenPage(IList<DesignToken> Items, Int32 Total, Int32 Page, Int32 PageSize);

/// <summary>
/// 令牌库读写门面。控制器与服务只经此访问 DesignToken / DesignShadowLayer 两张表。
///
/// 并发与正确性纪律：
/// - 唯一性/存在性判断一律 <c>FindCount/FindAll(exp)</c> 直查库，不碰 <c>Meta.Cache</c>
///   （缓存是 AsyncLocal 每执行上下文一份，切库/多实例时会读出幽灵行，见 plugin-development 铁律 11）；
/// - 批量写入必须在事务内，校验失败整批回滚，不留半成品（design G11）；
/// - 别名/分层合规校验以 <see cref="TokenGraph"/> 为唯一裁决点（design G4）。
/// </summary>
public sealed class TokenRepository
{
    static readonly String[] ProtectedGenerators = [TokenGenerators.Manual, TokenGenerators.Imported];

    #region 读

    /// <summary>取单条令牌（直查库）</summary>
    public DesignToken? Find(Int64 projectId, Int64 themeId, String path) =>
        DesignToken.FindAll(DesignToken._.ProjectId == projectId & DesignToken._.ThemeId == themeId & DesignToken._.Path == path).FirstOrDefault();

    /// <summary>共享层全部令牌</summary>
    public IList<DesignToken> FindShared(Int64 projectId) =>
        DesignToken.FindAll(DesignToken._.ProjectId == projectId & DesignToken._.ThemeId == DesignSystemConstants.SharedThemeId);

    /// <summary>某主题的覆盖层令牌</summary>
    public IList<DesignToken> FindThemed(Int64 projectId, Int64 themeId) =>
        DesignToken.FindAll(DesignToken._.ProjectId == projectId & DesignToken._.ThemeId == themeId);

    /// <summary>
    /// 载入别名图：共享层 + 指定主题的覆盖层。
    /// themeId 为 null 或 0 时只有共享层。
    /// </summary>
    public TokenGraph LoadGraph(Int64 projectId, Int64? themeId, String? themeCode = null)
    {
        var shared = FindShared(projectId).Select(ToNode).ToList();
        IEnumerable<TokenNode>? themed = null;
        if (themeId is > 0) themed = FindThemed(projectId, themeId.Value).Select(ToNode);
        return new TokenGraph(shared, themed, themeCode);
    }

    /// <summary>按 Id 取主题（直查库）</summary>
    public DesignTheme? FindTheme(Int64 themeId) =>
        DesignTheme.FindAll(DesignTheme._.Id == themeId).FirstOrDefault();

    /// <summary>按编码取主题</summary>
    public DesignTheme? FindThemeByCode(Int64 projectId, String code) =>
        DesignTheme.FindAll(DesignTheme._.ProjectId == projectId & DesignTheme._.Code == code).FirstOrDefault();

    /// <summary>项目全部主题</summary>
    public IList<DesignTheme> ListThemes(Int64 projectId) =>
        DesignTheme.FindAll(DesignTheme._.ProjectId == projectId).OrderBy(t => t.SortOrder).ThenBy(t => t.Id).ToList();

    /// <summary>分页查询令牌（tier/group/关键字/色相区间过滤）</summary>
    public TokenPage List(Int64 projectId, Int64? themeId, String? tier, String? group, String? keyword, Int32 page, Int32 pageSize)
    {
        var exp = DesignToken._.ProjectId == projectId;
        if (themeId != null) exp &= DesignToken._.ThemeId == themeId.Value;
        if (!tier.IsNullOrEmpty()) exp &= DesignToken._.Tier == tier;
        if (!group.IsNullOrEmpty()) exp &= DesignToken._.Group == group;
        if (!keyword.IsNullOrEmpty())
            exp &= DesignToken._.Path.Contains(keyword!) | DesignToken._.Name.Contains(keyword!) | DesignToken._.Tags.Contains(keyword!);

        var pp = new PageParameter { PageIndex = Math.Max(1, page), PageSize = Math.Clamp(pageSize, 1, 2000), Sort = DesignToken.__.SortOrder, RetrieveTotalCount = true };
        var items = DesignToken.FindAll(exp, pp);
        return new TokenPage(items, (Int32)pp.TotalCount, pp.PageIndex, pp.PageSize);
    }

    #endregion

    #region 写

    /// <summary>
    /// 批量写入令牌：先全部整形，再统一校验别名图与分层，通过后事务内落库。
    /// 任何校验失败 → 零行落库 + 返回明细（AC4）。
    /// </summary>
    /// <param name="projectId">项目</param>
    /// <param name="patches">载荷；同一批内 Path 不得重复</param>
    /// <param name="overwrite">true 时允许覆盖人工/导入行</param>
    public UpsertResult UpsertBatch(Int64 projectId, IReadOnlyList<TokenPatch> patches, Boolean overwrite = false)
    {
        var result = new UpsertResult();
        if (patches.Count == 0) return result;

        // 载荷内自重复必须早判：DB 唯一索引只会给一条晦涩的约束错误
        var dupKey = patches
            .GroupBy(p => (p.ThemeId, NormalizePath(p.Path)))
            .FirstOrDefault(g => g.Count() > 1)?.Key;
        if (dupKey != null)
        {
            result.Diagnostics.Add(new TokenDiagnostic(dupKey.Value.Item2, ResolveStatus.Missing,
                $"同一批次内路径重复：{dupKey.Value.Item2}（主题 {dupKey.Value.Item1}）"));
            return result;
        }

        // 事务区域：离开 using 且未 Commit 即自动回滚（EntityTransaction.Rollback 是受保护成员）
        using var et = new EntityTransaction<DesignToken>();
        try
        {
            var touched = new List<DesignToken>();
            foreach (var patch in patches)
            {
                var existing = Find(projectId, patch.ThemeId, patch.Path);

                // 客户端声明了版本就必须先验版本：否则「手改保护」会静默吞掉并发冲突，
                // 用户看到的是"保存成功但值没变"，比报错更难查
                if (patch.ExpectUpdatedAt != null && existing != null && existing.UpdatedAt != patch.ExpectUpdatedAt.Value)
                {
                    result.Diagnostics.Add(new TokenDiagnostic(patch.Path, ResolveStatus.Missing,
                        $"令牌 {patch.Path} 已被他人修改（期望 UpdatedAt={patch.ExpectUpdatedAt:O}，实际={existing.UpdatedAt:O}），请重载后再改"));
                    continue;
                }

                if (existing != null && !overwrite && ProtectedGenerators.Contains(existing.Generator))
                {
                    result.SkippedProtected++;
                    result.Conflicts.Add(patch.Path);
                    continue;
                }

                var entity = existing ?? new DesignToken { ProjectId = projectId, ThemeId = patch.ThemeId, Path = patch.Path, CreatedAt = DateTime.Now };

                Apply(entity, patch, isNew: existing == null);
                NormalizeColor(entity);
                touched.Add(entity);

                if (existing == null) result.Created++;
                else result.Updated++;
            }

            // 全图校验（含未改动行）：本批只保证「不因本批而破」，历史破损也一并拒绝写入
            var graph = BuildGraph(projectId, touched);
            if (graph.HasBlockingErrors(out var diags))
            {
                foreach (var d in diags) result.Diagnostics.Add(d);
                result.Created = result.Updated = 0;
                return result;   // 未 Commit → 离开 using 自动回滚
            }

            foreach (var t in touched) t.Save();
            et.Commit();
            return result;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DesignSystem] 令牌批量写入失败，已回滚: {0}", ex.Message);
            throw;
        }
    }

    /// <summary>单条写入（点即保存的落点）。返回与批量一致的结果对象。</summary>
    public UpsertResult UpsertOne(Int64 projectId, TokenPatch patch, Boolean overwrite = false) =>
        UpsertBatch(projectId, [patch], overwrite);

    /// <summary>
    /// 导入用的一次性查表：
    /// ① <c>path → 该写入的主题</c>（同一路径在目标主题与共享层都有时，目标主题优先；只有共享层有就写共享层 ——
    ///    这就是"导入不搬家"，否则 round-trip 会把 primitive 复制进主题层）；
    /// ② <c>path → 库里已有的层级</c>（DTCG 文件里**没有** tier：已有行必须保留库里的 tier，
    ///    按路径前缀猜会把语义层降级成 primitive，它的别名就"逆向指向上层"，整批被图校验拒掉）；
    /// ③ 受手改/导入保护的键集合（<see cref="DtcgImporter.Key"/>）。
    /// 一次查询拿全，不逐条 Find：导入动辄上千条，逐条查会把锁窗口拉成马拉松（v2.6.8 刚为同类 N+1 收过口）。
    /// </summary>
    public (Dictionary<String, ImportRowInfo> ByPath, HashSet<String> ProtectedKeys) ImportLookups(Int64 projectId, Int64 targetThemeId)
    {
        var byPath = new Dictionary<String, ImportRowInfo>(StringComparer.Ordinal);
        var bestRank = new Dictionary<String, Int32>(StringComparer.Ordinal);
        var keys = new HashSet<String>(StringComparer.Ordinal);

        foreach (var row in DesignToken.FindAll(DesignToken._.ProjectId == projectId))
        {
            var path = NormalizePath(row.Path);
            var rank = row.ThemeId == targetThemeId ? 0 : row.ThemeId == DesignSystemConstants.SharedThemeId ? 1 : 2;
            if (!bestRank.TryGetValue(path, out var seen) || rank < seen)
            {
                bestRank[path] = rank;
                byPath[path] = new ImportRowInfo(row.ThemeId, row.Tier);
            }
            if (TokenGenerators.IsProtected(row.Generator)) keys.Add(DtcgImporter.Key(row.ThemeId, path));
        }
        return (byPath, keys);
    }

    /// <summary>退役令牌（软删：Lifecycle=removed + Deprecated），不物理删行，保留可追溯与 diff 能力</summary>
    public Boolean Retire(Int64 projectId, Int64 themeId, String path, String? replacedBy)
    {
        var entity = Find(projectId, themeId, path);
        if (entity == null) return false;
        entity.Lifecycle = TokenLifecycles.Removed;
        entity.Deprecated = true;
        if (!replacedBy.IsNullOrEmpty()) entity.ReplacedBy = replacedBy;
        entity.UpdatedAt = DateTime.Now;
        entity.Save();
        return true;
    }

    /// <summary>写阴影层展开行（复合 shadow 令牌的真源仍是 ValueJson，本表为查询投影；二者由本方法成对维护，design G6）</summary>
    public void ReplaceShadowLayers(DesignToken token, IEnumerable<ShadowLayerInput> layers)
    {
        var existing = DesignShadowLayer.FindAll(DesignShadowLayer._.TokenId == token.Id);
        var keep = new HashSet<Int64>();
        var idx = 0;
        foreach (var input in layers)
        {
            var row = existing.FirstOrDefault(e => e.Layer == idx);
            if (row == null) row = new DesignShadowLayer { ProjectId = token.ProjectId, TokenId = token.Id, Layer = idx, CreatedAt = DateTime.Now };
            row.TokenPath = token.Path;
            row.Name = input.Name ?? $"layer-{idx}";
            row.IsInset = input.IsInset;
            row.OffsetX = input.OffsetX;
            row.OffsetY = input.OffsetY;
            row.Blur = input.Blur;
            row.Spread = input.Spread;
            row.ColorValue = input.ColorValue ?? "";
            row.ColorAliasPath = input.ColorAliasPath ?? "";
            row.Alpha = input.Alpha;
            row.Usage = input.Usage ?? "";
            row.UpdatedAt = DateTime.Now;
            row.Save();
            keep.Add(row.Id);
            idx++;
        }

        // 只删除本次收缩掉的层，绝不动未涉及的行
        foreach (var stale in existing.Where(e => !keep.Contains(e.Id))) stale.Delete();
    }

    /// <summary>读某 shadow 令牌的展开层（按层序）</summary>
    public IList<DesignShadowLayer> FindShadowLayers(Int64 tokenId) =>
        DesignShadowLayer.FindAll(DesignShadowLayer._.TokenId == tokenId).OrderBy(l => l.Layer).ToList();

    #endregion

    #region 内部

    /// <summary>
    /// 合成校验图：库中现状 + 本批待写实体一起参与环/分层/存在性判定。
    ///
    /// 主题层要**全主题并集**而不是只取本批主题：component 令牌住在共享层、别名指向
    /// 各主题各自的 semantic.*，若校验时只看共享层，component 批会因"semantic 不存在"被整批拒绝
    /// （实测踩过）。存在性按并集判，真正的逐主题解析仍走 <see cref="LoadGraph"/>（G4）。
    /// </summary>
    TokenGraph BuildGraph(Int64 projectId, IEnumerable<DesignToken> pending)
    {
        var pendingList = pending.ToList();

        var shared = FindShared(projectId).ToList();
        foreach (var p in pendingList.Where(p => p.ThemeId == DesignSystemConstants.SharedThemeId))
        {
            var i = shared.FindIndex(x => String.Equals(x.Path, p.Path, StringComparison.Ordinal));
            if (i >= 0) shared[i] = p;
            else shared.Add(p);
        }

        var themedByPath = new Dictionary<String, TokenNode>(StringComparer.Ordinal);
        foreach (var theme in DesignTheme.FindAll(DesignTheme._.ProjectId == projectId))
            foreach (var n in FindThemed(projectId, theme.Id).Select(ToNode))
                if (!themedByPath.ContainsKey(n.Path)) themedByPath[n.Path] = n;

        foreach (var p in pendingList.Where(p => p.ThemeId > 0))
            themedByPath[p.Path] = ToNode(p);

        return new TokenGraph(shared.Select(ToNode).ToList(), themedByPath.Values.ToList());
    }

    static String NormalizePath(String path) => path.Trim().ToLowerInvariant();

    static TokenNode ToNode(DesignToken e) => new(e.Path, e.Tier, e.Type, e.Value,
        e.AliasPath.IsNullOrEmpty() ? null : e.AliasPath,
        e.Extensions.IsNullOrEmpty() ? null : e.Extensions,
        e.ValueJson.IsNullOrEmpty() ? null : e.ValueJson,
        e.Lifecycle.IsNullOrEmpty() ? null : e.Lifecycle,
        e.Description.IsNullOrEmpty() ? null : e.Description);

    /// <summary>把补丁应用到实体；只覆盖传了值的字段（部分更新语义）</summary>
    static void Apply(DesignToken e, TokenPatch p, Boolean isNew)
    {
        if (isNew)
        {
            e.Tier = (p.Tier ?? TokenTiers.Primitive);
            e.Type = p.Type ?? TokenTypes.Color;
            e.Name = p.Name ?? p.Path;
            e.Generator = p.Generator ?? TokenGenerators.Manual;
            e.Lifecycle = p.Lifecycle ?? TokenLifecycles.Adopted;
            e.Alpha = p.Alpha ?? 1;
            e.ContrastRatio = -1;
            e.GeneratorVersion = DesignSystemConstants.GeneratorVersion;
        }
        else
        {
            // 人工改动一经写入即受重新生成保护：除非调用方显式声明来源，否则归为 manual
            if (p.Generator.IsNullOrEmpty() && !ProtectedGenerators.Contains(e.Generator))
                e.Generator = TokenGenerators.Manual;
            else if (!p.Generator.IsNullOrEmpty()) e.Generator = p.Generator!;
        }

        if (!p.Tier.IsNullOrEmpty()) e.Tier = p.Tier!;
        if (!p.Name.IsNullOrEmpty()) e.Name = p.Name!;
        if (!p.Type.IsNullOrEmpty()) e.Type = p.Type!;
        if (p.Value != null) e.Value = p.Value;
        if (p.ValueJson != null) e.ValueJson = p.ValueJson;
        if (p.AliasPath != null) e.AliasPath = p.AliasPath;
        if (p.Group != null) e.Group = p.Group;
        if (p.Description != null) e.Description = p.Description;
        if (p.ColorSpace != null) e.ColorSpace = p.ColorSpace;
        if (p.Alpha != null) e.Alpha = p.Alpha.Value;
        if (p.IsPrimary != null) e.IsPrimary = p.IsPrimary.Value;
        if (p.Tags != null) e.Tags = p.Tags;
        if (p.Lifecycle != null) e.Lifecycle = p.Lifecycle;
        if (p.ReplacedBy != null) e.ReplacedBy = p.ReplacedBy;
        if (p.Deprecated != null) e.Deprecated = p.Deprecated.Value;
        if (p.SortOrder != null) e.SortOrder = p.SortOrder.Value;
        if (p.Extensions != null) e.Extensions = p.Extensions;
        if (p.GeneratorSeed != null) e.GeneratorSeed = p.GeneratorSeed;
        e.UpdatedAt = DateTime.Now;
    }

    /// <summary>颜色令牌派生列（hex + oklch 三元组）随值刷新；非颜色令牌不参与</summary>
    static void NormalizeColor(DesignToken e)
    {
        if (e.Type != TokenTypes.Color) return;

        var parsed = Oklch.ParseHex(e.Value) ?? Oklch.ParseOklch(e.Value);
        if (parsed == null) return;

        var o = parsed.Value;
        var rgb = Oklch.ToRgb8(o);
        e.ColorHex = rgb.ToHex();
        e.OklchL = Math.Round(o.L, 5);
        e.OklchC = Math.Round(o.C, 5);
        e.OklchH = Math.Round(Oklch.NormalizeHue(o.H), 2);
        e.ColorSpace = "oklch";
    }

    #endregion
}

/// <summary>阴影层写入输入（与 DesignShadowLayer 对应的轻量载荷）</summary>
public sealed class ShadowLayerInput
{
    public String? Name { get; set; }
    public Boolean IsInset { get; set; }
    public Double OffsetX { get; set; }
    public Double OffsetY { get; set; }
    public Double Blur { get; set; }
    public Double Spread { get; set; }
    public String? ColorValue { get; set; }
    public String? ColorAliasPath { get; set; }
    public Double Alpha { get; set; } = 1;
    public String? Usage { get; set; }
}

using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Plugins.CostScope.Services;

/// <summary>模型→供应商解析的命中级别（数值即优先级，越小越优先）。</summary>
/// <remarks>
/// 4 级回退顺序 = 02-spec FR-3.8 / BC-3 / BC-5 与 032 §3.2 定义的优先级：
/// ChatModelId → UpstreamModelId/Alias → 大小写不敏感 → 未归属。
/// <b>绝不按前缀硬切</b>：前三级一律是「相等」比较（区分 / 不区分大小写），
/// 不存在 <c>StartsWith</c> / <c>Contains</c> / 正则一类模糊匹配——
/// 前缀相似但未被目录收录的模型必须落到 <see cref="Unattributed"/>，显式上报而非猜测。
/// </remarks>
public enum ModelMatchLevel
{
    /// <summary>级别 1：ChatModelId 精确命中（区分大小写）。</summary>
    ChatModelId = 1,

    /// <summary>级别 2：UpstreamModelId 或 Alias 精确命中（区分大小写）。</summary>
    UpstreamOrAlias = 2,

    /// <summary>级别 3：上述三字段任一「大小写不敏感」命中。</summary>
    CaseInsensitive = 3,

    /// <summary>级别 4：未归属（解析不中）。显式上报，不猜测、不降级为某个默认供应商。</summary>
    Unattributed = 4,
}

/// <summary>单次模型解析结果。</summary>
/// <param name="ModelName">被解析的模型名（原样回带，便于聚合侧列出未归属模型）。</param>
/// <param name="ProviderName">解析到的供应商名；未归属时为 <see langword="null"/>（不猜、不填占位符）。</param>
/// <param name="Level">命中级别。</param>
/// <param name="IsAttributed">是否已归属；false 表示未归属，消费方须计入未归属并做命中率统计。</param>
public readonly record struct ModelResolution(
    string ModelName,
    string? ProviderName,
    ModelMatchLevel Level,
    bool IsAttributed);

/// <summary>解析命中率统计快照（FR-3.8「命中率统计」）。</summary>
/// <param name="ChatModelIdHits">级别 1 命中次数。</param>
/// <param name="UpstreamOrAliasHits">级别 2 命中次数。</param>
/// <param name="CaseInsensitiveHits">级别 3 命中次数。</param>
/// <param name="UnattributedHits">未归属次数（解析不中）。</param>
public readonly record struct ModelResolutionStats(
    long ChatModelIdHits,
    long UpstreamOrAliasHits,
    long CaseInsensitiveHits,
    long UnattributedHits)
{
    /// <summary>总解析次数。</summary>
    public long Total => ChatModelIdHits + UpstreamOrAliasHits + CaseInsensitiveHits + UnattributedHits;

    /// <summary>已归属次数（前三级之和）。</summary>
    public long Attributed => ChatModelIdHits + UpstreamOrAliasHits + CaseInsensitiveHits;

    /// <summary>命中率（0~1）。无样本时为 0——不伪造、不返回 NaN。</summary>
    public double HitRate => Total == 0 ? 0d : (double)Attributed / Total;
}

/// <summary>
/// 模型→供应商解析器（FR-3.8 / BC-3 / BC-5）。
/// </summary>
/// <remarks>
/// 核心 <see cref="Match"/> 是<b>纯函数</b>（零 IO、零隐藏状态），4 级回退与「不按前缀硬切」由单测完全锁死；
/// <see cref="ResolveAsync"/> 只负责经 <see cref="ITurnTelemetryQuery"/> 取目录后转调它并累计命中率。
/// 目录数据源是宿主 <c>AIModel</c> / <c>AIProvider</c>（经契约投影取得），
/// 插件<b>不直连宿主库</b>（FR-4.6 / BR-5）。
/// </remarks>
public sealed class ModelPriceResolver
{
    private readonly ITurnTelemetryQuery _query;

    private long _chatModelIdHits;
    private long _upstreamOrAliasHits;
    private long _caseInsensitiveHits;
    private long _unattributedHits;

    public ModelPriceResolver(ITurnTelemetryQuery query)
    {
        _query = query ?? throw new ArgumentNullException(nameof(query));
    }

    /// <summary>
    /// 纯函数核心：给定模型名与目录，按 4 级回退解析归属（零 IO，可完全单测）。
    /// </summary>
    /// <param name="modelName">被解析的模型名（来自 ChatTurn.Model）。</param>
    /// <param name="catalog">模型身份目录；null 或空一律判未归属。</param>
    /// <returns>解析结果；未命中时 <see cref="ModelResolution.IsAttributed"/> 为 false 且供应商为 null。</returns>
    public static ModelResolution Match(string? modelName, IReadOnlyList<ModelIdentityDto>? catalog)
    {
        var name = modelName ?? string.Empty;

        // 空模型名 / 空目录 ⇒ 未归属（不猜、不抛，聚合侧据此计入 unattributed）
        if (string.IsNullOrWhiteSpace(name) || catalog is null || catalog.Count == 0)
            return new ModelResolution(name, null, ModelMatchLevel.Unattributed, false);

        // 级别 1：ChatModelId 精确（区分大小写）
        foreach (var m in catalog)
        {
            if (string.Equals(m.ChatModelId, name, StringComparison.Ordinal))
                return new ModelResolution(name, m.ProviderName, ModelMatchLevel.ChatModelId, true);
        }

        // 级别 2：UpstreamModelId / Alias 精确（区分大小写）
        foreach (var m in catalog)
        {
            if (string.Equals(m.UpstreamModelId, name, StringComparison.Ordinal)
                || string.Equals(m.Alias, name, StringComparison.Ordinal))
                return new ModelResolution(name, m.ProviderName, ModelMatchLevel.UpstreamOrAlias, true);
        }

        // 级别 3：三字段任一「大小写不敏感」命中
        foreach (var m in catalog)
        {
            if (string.Equals(m.ChatModelId, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(m.UpstreamModelId, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(m.Alias, name, StringComparison.OrdinalIgnoreCase))
                return new ModelResolution(name, m.ProviderName, ModelMatchLevel.CaseInsensitive, true);
        }

        // 级别 4：未归属
        return new ModelResolution(name, null, ModelMatchLevel.Unattributed, false);
    }

    /// <summary>取目录后按 4 级回退解析，并累计命中率统计。</summary>
    public async Task<ModelResolution> ResolveAsync(string? modelName, CancellationToken ct = default)
    {
        var catalog = await _query.GetModelCatalogAsync(ct).ConfigureAwait(false);
        var result = Match(modelName, catalog);

        switch (result.Level)
        {
            case ModelMatchLevel.ChatModelId:
                Interlocked.Increment(ref _chatModelIdHits);
                break;
            case ModelMatchLevel.UpstreamOrAlias:
                Interlocked.Increment(ref _upstreamOrAliasHits);
                break;
            case ModelMatchLevel.CaseInsensitive:
                Interlocked.Increment(ref _caseInsensitiveHits);
                break;
            default:
                Interlocked.Increment(ref _unattributedHits);
                break;
        }

        return result;
    }

    /// <summary>取命中率统计快照（并发安全）。</summary>
    public ModelResolutionStats SnapshotStats() => new(
        Interlocked.Read(ref _chatModelIdHits),
        Interlocked.Read(ref _upstreamOrAliasHits),
        Interlocked.Read(ref _caseInsensitiveHits),
        Interlocked.Read(ref _unattributedHits));

    /// <summary>清零统计（供测试与「换目录后重新统计」使用）。</summary>
    public void ResetStats()
    {
        Interlocked.Exchange(ref _chatModelIdHits, 0);
        Interlocked.Exchange(ref _upstreamOrAliasHits, 0);
        Interlocked.Exchange(ref _caseInsensitiveHits, 0);
        Interlocked.Exchange(ref _unattributedHits, 0);
    }
}

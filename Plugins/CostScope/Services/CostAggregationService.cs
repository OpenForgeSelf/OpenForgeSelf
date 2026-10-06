using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.CostScope.Entities;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.CostScope.Services;

/// <summary>聚合维度（FR-4.2：模型 / 供应商 / 日 / 风格）。</summary>
public enum AggregationDimension
{
    /// <summary>按模型（= ChatTurn.Model）。</summary>
    Model,

    /// <summary>按供应商（经 A4 的 4 级解析；未归属单独归键）。</summary>
    Provider,

    /// <summary>按 API 风格（= ChatTurn.Style）。</summary>
    Style,

    /// <summary>按日（= ChatTurn.CreatedTime 的日期部分）。</summary>
    Day,
}

/// <summary>维度聚合条目。</summary>
/// <param name="Key">维度键；供应商维度下「未归属」用固定键 <see cref="CostAggregationService.UnattributedKey"/>。</param>
/// <param name="Turns">轮次数。</param>
/// <param name="PromptTokens">提示词 token 累计。</param>
/// <param name="CompletionTokens">补全 token 累计。</param>
/// <param name="Cost">金额合计；<paramref name="CostIsLowerBound"/> 为 true 时该值是<b>下界</b>（未配单价部分按 0 计入）。</param>
/// <param name="FailCount">失败轮次（BR-4：不计成本）。</param>
/// <param name="CostIsLowerBound">是否含未配单价的未知段。</param>
public readonly record struct DimensionItem(
    string Key,
    long Turns,
    long PromptTokens,
    long CompletionTokens,
    decimal Cost,
    long FailCount,
    bool CostIsLowerBound);

/// <summary>覆盖度（FR-1.2 / AC1）。</summary>
/// <param name="CoveredCalls">本次聚合实际纳入的轮次数（已配单价且可核算的调用数按口径说明）。</param>
/// <param name="TotalRecords">传入的遥测记录总数。</param>
/// <param name="UnpricedCalls">其中「未配单价」的调用数。</param>
/// <param name="MainChatObservable">
/// 主聊天链路（legacy <c>ChatController</c> 纯文本接缝）是否可观测。
/// U-2 裁决 = (c) 下<b>恒为 false</b>——主聊天不写入 <c>ChatTurn</c>，其用量<b>不可计算</b>。
/// </param>
/// <param name="Note">给用户看的显式说明（不得让部分覆盖被误读为全量）。</param>
public readonly record struct CoverageInfo(
    long CoveredCalls,
    long TotalRecords,
    long UnpricedCalls,
    bool MainChatObservable,
    string Note);

/// <summary>成本总览（02-spec Output 表第 1 行）。</summary>
public readonly record struct CostOverview(
    decimal TodayCost,
    decimal MonthCost,
    decimal TotalCost,
    bool CostIsLowerBound,
    long PromptTokens,
    long CompletionTokens,
    long TotalTokens,
    long Turns,
    long FailCount,
    IReadOnlyList<string> UnpricedModels,
    IReadOnlyList<string> UnattributedModels,
    IReadOnlyList<string> TopModels,
    CoverageInfo Coverage);

/// <summary>分位统计（样本数随分位一并返回，BC-7）。</summary>
public readonly record struct LatencyPercentiles(long P50, long P95, long P99, long Samples);

/// <summary>延迟统计：首字延迟与总耗时<b>分别出</b>分位（FR-4.3 / AC5）。</summary>
public readonly record struct LatencyStats(LatencyPercentiles FirstToken, LatencyPercentiles Duration);

/// <summary>错误分布桶（按 <c>ErrorMessage</c> 原文归类，不臆造分类体系）。</summary>
public readonly record struct ErrorBucket(string Category, long Count);

/// <summary>错误率（FR-4.4）。</summary>
public readonly record struct ErrorStats(long Total, long Failed, long UnknownStatus, double Rate, IReadOnlyList<ErrorBucket> Buckets);

/// <summary>日趋势点（02-spec Output 表第 2 行）。</summary>
public readonly record struct DailyPoint(
    DateTime Day,
    decimal Cost,
    bool CostIsLowerBound,
    long PromptTokens,
    long CompletionTokens,
    long Turns,
    long FailCount);

/// <summary>「日 × 模型」汇总明细（FR-3.7 惰性物化的输入粒度）。</summary>
/// <param name="Day">汇总日（= CreatedTime 日期部分）。</param>
/// <param name="Model">模型名。</param>
/// <param name="Provider">供应商（未归属时为 <see langword="null"/>，不猜）。</param>
/// <param name="PromptTokens">提示词 token 累计。</param>
/// <param name="CompletionTokens">补全 token 累计。</param>
/// <param name="Turns">轮次数。</param>
/// <param name="FailCount">失败轮次（BR-4，不计成本）。</param>
/// <param name="Cost">金额合计。</param>
/// <param name="CostIsLowerBound">含未配单价未知段 ⇒ 该值仅为下界（BR-2）。</param>
public readonly record struct DailyModelPoint(
    DateTime Day,
    string Model,
    string? Provider,
    long PromptTokens,
    long CompletionTokens,
    long Turns,
    long FailCount,
    decimal Cost,
    bool CostIsLowerBound);

/// <summary>
/// LLM 用量/成本<b>只读</b>聚合器（FR-4.2~4.4、FR-3.4、BR-2、BR-4）。
/// </summary>
/// <remarks>
/// <para><b>纯聚合、只读</b>：入参是 <see cref="TurnTelemetryRecord"/> 只读 DTO（由 A3b 宿主接缝从
/// <c>ChatTurn</c> 投影而来），本类<b>不碰宿主库</b>（FR-4.6 / BR-5），也<b>不写任何库</b>；
/// 惰性日汇总物化在 <c>TelemetryProjectionService</c>（FR-3.7）。</para>
/// <para><b>成本口径唯一</b>：一律委托 A3 的 <see cref="CostCalculationService"/> 纯函数计算，本类不重复实现计价公式。</para>
/// <para><b>BR-2 绝不静默计 0</b>：未配单价的模型成本计入「下界」并进 <c>UnpricedModels</c>，
/// <b>不</b>把它当作已核算成本。</para>
/// <para><b>BR-4</b>：<c>ResponseStatus != 200</c> 的轮次计入失败数且<b>不计成本</b>。
/// 另单列 <c>UnknownStatus</c>（<c>ResponseStatus == 0</c>，上游未回填）——按 BR-4 字面它计入失败，
/// 但同时单列出来避免「未回填」被当成「真失败」而不自知。</para>
/// </remarks>
public sealed class CostAggregationService
{
    /// <summary>供应商维度下「未归属」的固定键（BC-3：解析不中显式归入，不猜测）。</summary>
    public const string UnattributedKey = "(未归属)";

    /// <summary>未配单价模型在 <c>UnpricedModels</c> 里显示的占位说明（非模型名本身）。</summary>
    public const string NoPriceBucketLabel = "(未配单价)";

    private const int MaxErrorBuckets = 10;
    private const int MaxTopModels = 5;

    private readonly PriceCatalogService _prices;

    /// <summary>模型 → 供应商 的解析函数（由 A4 <c>ModelPriceResolver</c> 提供；为 null 时全部记未归属）。</summary>
    private readonly Func<string, string?>? _providerResolver;

    public CostAggregationService(PriceCatalogService prices, Func<string, string?>? providerResolver = null)
    {
        _prices = prices ?? throw new ArgumentNullException(nameof(prices));
        _providerResolver = providerResolver;
    }

    /// <summary>BR-4 失败判定：<c>ResponseStatus != 200</c>（含未回填的 0，按规格字面口径）。</summary>
    public static bool IsFailed(TurnTelemetryRecord r) => r.ResponseStatus != 200;

    /// <summary>响应状态未回填（<c>== 0</c>）——单列出来，不静默混进「成功」或「失败」。</summary>
    public static bool IsStatusUnknown(TurnTelemetryRecord r) => r.ResponseStatus == 0;

    /// <summary>成本总览（Output 表第 1 行）。</summary>
    public CostOverview Overview(IReadOnlyList<TurnTelemetryRecord> records, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(records);

        var (todayStart, _) = BudgetService.ResolvePeriod("day", now);
        var (monthStart, _) = BudgetService.ResolvePeriod("month", now);

        decimal today = 0m, month = 0m, total = 0m;
        var lowerBound = false;
        long prompt = 0, completion = 0, turns = 0, fail = 0, covered = 0, unpricedCalls = 0;
        var unpricedModels = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var unattributed = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var byModel = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in records)
        {
            turns++;
            prompt += r.PromptTokens;
            completion += r.CompletionTokens;

            if (IsFailed(r))
            {
                fail++;
                // BR-4：失败轮次不计成本
                continue;
            }

            var (cost, isLowerBound) = CostOf(r);
            total += cost;
            if (isLowerBound) { lowerBound = true; unpricedCalls++; unpricedModels.Add(r.Model); }
            else covered++;

            if (r.CreatedTime >= todayStart) today += cost;
            if (r.CreatedTime >= monthStart) month += cost;

            byModel[r.Model] = byModel.GetValueOrDefault(r.Model) + cost;

            // 「未归属」只表示**有解析器但解析不出供应商**。
            // ① 没配单价归 unpricedModels 自己的清单，混进来会双重计数、也看不出到底缺什么；
            // ② resolver 未接线时**不产出**该列表——那不是「未归属」，而是「没配置归属解析」，
            //    把全部模型列成未归属只会制造噪音、掩盖真正需要关注的项。
            if (_providerResolver is not null && string.IsNullOrWhiteSpace(_providerResolver(r.Model)))
                unattributed.Add(r.Model);
        }

        var topModels = byModel
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Take(MaxTopModels)
            .Select(kv => kv.Key)
            .ToList();

        return new CostOverview(
            today, month, total, lowerBound,
            prompt, completion, prompt + completion,
            turns, fail,
            unpricedModels.ToList(), unattributed.ToList(), topModels,
            new CoverageInfo(
                covered, records.Count, unpricedCalls,
                MainChatObservable: false,
                Note: "仅统计统一 AI 网关写入的轮次；主聊天（legacy 纯文本接缝）用量当前不可见（U-2 裁决 = 不迁接缝），其数量不可计算故不填报。"));
    }

    /// <summary>维度排行（Output 表第 3 行）。</summary>
    /// <param name="records">只读遥测记录。</param>
    /// <param name="by">聚合维度。</param>
    /// <param name="top">取前 N 条；<paramref name="top"/> ≤ 0 表示不限。</param>
    public IReadOnlyList<DimensionItem> Breakdown(IReadOnlyList<TurnTelemetryRecord> records, AggregationDimension by, int top = 0)
    {
        ArgumentNullException.ThrowIfNull(records);

        var buckets = new Dictionary<string, DimensionItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in records)
        {
            var key = KeyOf(r, by);
            var failed = IsFailed(r);
            var (cost, lower) = failed ? (0m, false) : CostOf(r);

            buckets.TryGetValue(key, out var acc);
            buckets[key] = new DimensionItem(
                string.IsNullOrEmpty(acc.Key) ? key : acc.Key,
                acc.Turns + 1,
                acc.PromptTokens + r.PromptTokens,
                acc.CompletionTokens + r.CompletionTokens,
                acc.Cost + cost,
                acc.FailCount + (failed ? 1 : 0),
                acc.CostIsLowerBound || lower);
        }

        var items = buckets.Values
            .OrderByDescending(i => i.Cost)
            .ThenByDescending(i => i.Turns)
            .ThenBy(i => i.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return top > 0 ? items.Take(top).ToList() : items;
    }

    /// <summary>延迟统计：<c>FirstTokenMs</c> 与 <c>DurationMs</c> 分别出 P50/P95/P99（FR-4.3 / AC5）。</summary>
    public LatencyStats Latency(IReadOnlyList<TurnTelemetryRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        // 0 = 上游未回填，剔除（BC-7：样本数随分位返回，由调用方判断可信度）
        var firstTokens = records.Where(r => r.FirstTokenMs > 0).Select(r => r.FirstTokenMs).ToList();
        var durations = records.Where(r => r.DurationMs > 0).Select(r => r.DurationMs).ToList();

        return new LatencyStats(Percentiles(firstTokens), Percentiles(durations));
    }

    /// <summary>错误率与错误分布（FR-4.4）。</summary>
    public ErrorStats Errors(IReadOnlyList<TurnTelemetryRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var failed = records.Where(IsFailed).ToList();
        var groups = failed
            .GroupBy(r => string.IsNullOrWhiteSpace(r.ErrorMessage) ? "(未提供错误信息)" : r.ErrorMessage.Trim())
            .Select(g => new ErrorBucket(g.Key, g.LongCount()))
            .OrderByDescending(b => b.Count)
            .ThenBy(b => b.Category, StringComparer.OrdinalIgnoreCase)
            .Take(MaxErrorBuckets)
            .ToList();

        var total = records.Count;
        var rate = total == 0 ? 0d : (double)failed.Count / total;

        return new ErrorStats(total, failed.Count, records.Count(IsStatusUnknown), rate, groups);
    }

    /// <summary>日趋势（Output 表第 2 行）。</summary>
    public IReadOnlyList<DailyPoint> Daily(IReadOnlyList<TurnTelemetryRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        return records
            .GroupBy(r => r.CreatedTime.Date)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                long prompt = 0, completion = 0, turns = 0, fail = 0;
                decimal cost = 0m;
                var lower = false;

                foreach (var r in g)
                {
                    turns++;
                    prompt += r.PromptTokens;
                    completion += r.CompletionTokens;
                    if (IsFailed(r)) { fail++; continue; }
                    var (c, l) = CostOf(r);
                    cost += c;
                    lower |= l;
                }

                return new DailyPoint(g.Key, cost, lower, prompt, completion, turns, fail);
            })
            .ToList();
    }

    /// <summary>「日 × 模型」汇总明细（FR-3.7 物化的输入粒度）。</summary>
    public IReadOnlyList<DailyModelPoint> DailyByModel(IReadOnlyList<TurnTelemetryRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var acc = new Dictionary<(DateTime Day, string Model), DailyModelPoint>();

        foreach (var r in records)
        {
            var day = r.CreatedTime.Date;
            var k = (day, r.Model ?? string.Empty);
            var failed = IsFailed(r);
            var (cost, lower) = failed ? (0m, false) : CostOf(r);

            acc.TryGetValue(k, out var cur);
            acc[k] = new DailyModelPoint(
                day,
                k.Item2,
                cur.Provider ?? ResolveProviderOrNull(r.Model),
                cur.PromptTokens + r.PromptTokens,
                cur.CompletionTokens + r.CompletionTokens,
                cur.Turns + 1,
                cur.FailCount + (failed ? 1 : 0),
                cur.Cost + cost,
                cur.CostIsLowerBound || lower);
        }

        return acc.Values
            .OrderBy(p => p.Day)
            .ThenBy(p => p.Model, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>最近秩法分位（nearest-rank）：<c>p = ceil(q/100 × n)</c>，下标钳到 [0, n-1]。</summary>
    /// <remarks>空样本返回全 0 且 <c>Samples = 0</c>（不伪造分位、不返回 NaN）。</remarks>
    public static LatencyPercentiles Percentiles(IReadOnlyList<long> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count == 0) return new LatencyPercentiles(0, 0, 0, 0);

        var sorted = samples.OrderBy(x => x).ToList();
        return new LatencyPercentiles(
            NearestRank(sorted, 50d),
            NearestRank(sorted, 95d),
            NearestRank(sorted, 99d),
            sorted.Count);
    }

    private static long NearestRank(List<long> sorted, double q)
    {
        var rank = (int)Math.Ceiling(q / 100d * sorted.Count);
        var index = Math.Clamp(rank - 1, 0, sorted.Count - 1);
        return sorted[index];
    }

    private string KeyOf(TurnTelemetryRecord r, AggregationDimension by) => by switch
    {
        AggregationDimension.Model => string.IsNullOrWhiteSpace(r.Model) ? "(未记录模型)" : r.Model,
        AggregationDimension.Style => string.IsNullOrWhiteSpace(r.Style) ? "(未记录风格)" : r.Style,
        AggregationDimension.Day => r.CreatedTime.Date.ToString("yyyy-MM-dd"),
        AggregationDimension.Provider => ResolveProvider(r.Model),
        _ => throw new ArgumentOutOfRangeException(nameof(by), by, "未知聚合维度（须返回 400，不静默空表）"),
    };

    private string ResolveProvider(string model)
    {
        if (_providerResolver is null) return UnattributedKey;
        var provider = _providerResolver(model);
        return string.IsNullOrWhiteSpace(provider) ? UnattributedKey : provider;
    }

    /// <summary>供应商解析（原始值，未归属为 null）——供明细点使用，避免把「未归属」写成真供应商名。</summary>
    private string? ResolveProviderOrNull(string model)
    {
        var provider = _providerResolver?.Invoke(model);
        return string.IsNullOrWhiteSpace(provider) ? null : provider;
    }


    /// <summary>单轮成本 + 是否为下界；计价一律委托 A3 纯函数，本类不复制公式。</summary>
    private (decimal Cost, bool IsLowerBound) CostOf(TurnTelemetryRecord r)
    {
        var price = _prices.Find(r.Model);
        if (price is null)
        {
            XTrace.Log.Debug("[CostScope] 模型 {0} 未配单价 ⇒ 成本按未知处理（BR-2，不静默计 0）", r.Model);
            return (0m, true);
        }

        var amount = CostCalculationService.Calculate(new CostInput(
            r.PromptTokens, r.CompletionTokens,
            price.InputPricePer1M, price.OutputPricePer1M));

        return (amount.Total, amount.HasUnknownPart);
    }
}

using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.CostScope.Entities;
using NewLife.Log;
using XCode;

namespace ForgeSelf.Api.Plugins.CostScope.Services;

/// <summary>
/// 成本按日<b>惰性物化</b>（FR-3.7），落插件自有库 <c>CostTurnDaySummary</c>。
/// </summary>
/// <remarks>
/// <para><b>为什么是「惰性」而不是定时物化</b>（032 D3）：插件热重载不会重启宿主，
/// 任何 <c>IHostedService</c>/定时器都可能不触发 ⇒ 物化改为<b>查询时按需执行</b>，无后台任务、无生命周期依赖。
/// 本类<b>不注册、不引用任何 <c>IHostedService</c></b>（有守卫测试静态扫描全插件源码钉死这一点）。</para>
/// <para><b>缓存策略</b>：<b>当日</b>未结束 ⇒ <b>每次查询都重算</b>并回写（当天数据还在变，缓存会骗人）；
/// <b>已结束</b>的日 ⇒ <b>读缓存</b>（跨日不再变化，重算只是浪费）。</para>
/// <para><b>数据落点</b>：只写插件自有库（铁律 12），<b>绝不写宿主库</b>（F1 / BR-5）。
/// 成本<b>不入明细层</b>：这里存的是<b>物化缓存</b>，改单价后由 <see cref="InvalidateAll"/> 清缓存即恢复重算（FR-3.4）。</para>
/// </remarks>
public sealed class TelemetryProjectionService
{
    /// <summary>跨会话汇总时写入 <c>SessionKey</c> 列的固定值（该列此时只是来源标注，不代表真实会话）。</summary>
    public const string RollupSessionKey = "*";

    private readonly CostAggregationService _aggregation;

    public TelemetryProjectionService(CostAggregationService aggregation)
        => _aggregation = aggregation ?? throw new ArgumentNullException(nameof(aggregation));

    /// <summary>
    /// 查询日趋势：<b>已结束日读缓存、当日每次重算</b>（FR-3.7）。
    /// </summary>
    /// <remarks>
    /// 返回按日汇总的序列（对外形状与 <see cref="CostAggregationService.Daily"/> 一致）；
    /// 内部物化粒度是 Day×Model（表结构如此），返回时按日再合一次。
    /// </remarks>
    public IReadOnlyList<DailyPoint> QueryWithCache(IReadOnlyList<TurnTelemetryRecord> records, DateTime now)
    {
        var live = _aggregation.DailyByModel(records);
        var today = now.Date;

        // ① 物化：当日逐次回写；已结束日仅在「还没有缓存」时补一次
        foreach (var p in live)
        {
            if (p.Day >= today || !CacheExists(p.Day, p.Model))
                Upsert(p, now);
        }

        // ② 读取：已结束日优先用缓存，当日用实时值
        var days = live.Select(p => p.Day).Distinct().OrderBy(d => d).ToList();
        var result = new List<DailyPoint>(days.Count);

        foreach (var day in days)
        {
            var dayLive = live.Where(p => p.Day == day).ToList();
            var source = day < today ? CacheOf(day, dayLive, now) : dayLive;
            result.Add(Rollup(day, source));
        }

        return result;
    }

    /// <summary>清空物化缓存（改单价 / 删单价后调用，使下次查询重算 —— FR-3.4）。</summary>
    public int InvalidateAll()
    {
        var rows = CostTurnDaySummary.FindAll();
        var n = rows.Count;
        foreach (var row in rows) row.Delete();
        if (n > 0) XTrace.Log.Info("[CostScope] 已清空日汇总物化缓存 {0} 行（下次查询将重算）", n);
        return n;
    }

    /// <summary>直接物化指定明细（不读缓存），供运维/测试核对。</summary>
    public int Materialize(IReadOnlyList<DailyModelPoint> points, DateTime now)
    {
        var n = 0;
        foreach (var p in points)
        {
            Upsert(p, now);
            n++;
        }
        return n;
    }

    private static DailyPoint Rollup(DateTime day, List<DailyModelPoint> points)
    {
        long prompt = 0, completion = 0, turns = 0, fail = 0;
        decimal cost = 0m;
        var lower = false;

        foreach (var p in points)
        {
            prompt += p.PromptTokens;
            completion += p.CompletionTokens;
            turns += p.Turns;
            fail += p.FailCount;
            cost += p.Cost;
            lower |= p.CostIsLowerBound;
        }

        return new DailyPoint(day, cost, lower, prompt, completion, turns, fail);
    }

    /// <summary>取某日缓存；缺任一模型缓存时回落用实时值（宁可实时也不返回半截缓存）。</summary>
    private static List<DailyModelPoint> CacheOf(DateTime day, List<DailyModelPoint> fallback, DateTime now)
    {
        var cached = CostTurnDaySummary.FindAll(CostTurnDaySummary._.Day == day)
            .Select(ToPoint)
            .ToList();

        if (cached.Count == 0)
        {
            // 首次访问该历史日：补写缓存后返回实时值
            foreach (var p in fallback) Upsert(p, now);
            return fallback;
        }

        return cached;
    }

    private static bool CacheExists(DateTime day, string model) =>
        CostTurnDaySummary.FindAll(
            CostTurnDaySummary._.Day == day & CostTurnDaySummary._.Model == model).Count > 0;

    private static void Upsert(DailyModelPoint p, DateTime now)
    {
        var row = CostTurnDaySummary.FindAll(
            CostTurnDaySummary._.Day == p.Day & CostTurnDaySummary._.Model == p.Model).FirstOrDefault();

        if (row is null)
        {
            row = new CostTurnDaySummary
            {
                Day = p.Day,
                Model = p.Model,
                CreatedTime = now,
            };
        }

        row.Provider = p.Provider ?? string.Empty;
        row.PromptTokens = p.PromptTokens;
        row.CompletionTokens = p.CompletionTokens;
        row.TotalTokens = p.PromptTokens + p.CompletionTokens;
        row.CostAmount = p.Cost;
        row.CostIsLowerBound = p.CostIsLowerBound;
        row.Currency = PriceCatalogService.DefaultCurrency;
        row.TurnCount = (int)p.Turns;
        // 失败轮次虽不计成本，但**必须留痕**：漏存会让历史日的失败数在缓存路径上恒为 0（静默错数）
        row.FailCount = (int)p.FailCount;
        row.SessionKey = RollupSessionKey;
        row.UpdatedTime = now;
        row.Save();
    }

    private static DailyModelPoint ToPoint(CostTurnDaySummary row) => new(
        row.Day,
        row.Model ?? string.Empty,
        string.IsNullOrWhiteSpace(row.Provider) ? null : row.Provider,
        row.PromptTokens,
        row.CompletionTokens,
        row.TurnCount,
        row.FailCount,
        row.CostAmount,
        row.CostIsLowerBound);
}

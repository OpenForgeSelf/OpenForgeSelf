using ForgeSelf.Api.Plugins.CostScope.Entities;
using NewLife.Log;
using XCode;

namespace ForgeSelf.Api.Plugins.CostScope.Services;

/// <summary>预算达成状态（超支时由页面显式横幅呈现，<b>不发通知</b>——FR-3.6）。</summary>
public enum BudgetStatus
{
    /// <summary>未达预警线。</summary>
    Ok = 0,

    /// <summary>已达预警线（ratio ≥ AlertThreshold）。</summary>
    Warning = 1,

    /// <summary>已超支（ratio ≥ 1）。</summary>
    Exceeded = 2,
}

/// <summary>预算实时达成率快照（纯计算结果，不含 IO）。</summary>
/// <param name="LimitAmount">限额。</param>
/// <param name="UsedAmount">当期已用金额（由聚合侧提供）。</param>
/// <param name="Remaining">剩余额度，下限截断为 0（不出现负数）。</param>
/// <param name="UsageRatio">使用比例 = 已用 / 限额。</param>
/// <param name="Level"><see cref="BudgetStatus"/>。</param>
/// <param name="ShouldAlert">是否应触发横幅（ratio ≥ AlertThreshold）。</param>
public readonly record struct BudgetAchievement(
    decimal LimitAmount,
    decimal UsedAmount,
    decimal Remaining,
    double UsageRatio,
    BudgetStatus Level,
    bool ShouldAlert);

/// <summary>
/// 成本预算规则 CRUD + 实时达成率（FR-3.6）。
/// </summary>
/// <remarks>
/// <para><b>数据落点</b>：插件自有库 <c>CostScope.db</c> 的 <c>CostBudget</c> 表（铁律 12），不写宿主库（F1）。
/// <b>端点不在本任务</b>——并入 A11 的单一 <c>CostController</c>（避免多控制器双真相）。</para>
/// <para><b>已用金额从哪来</b>：本服务<b>不自己聚合</b>，由调用方（A7 <c>CostAggregationService</c>）传入
/// <see cref="Evaluate"/> 的 <c>usedAmount</c>——这样 A6 可与 A7 并行开发，且达成率口径纯函数化、可完全单测。</para>
/// <para><b>时区</b>（U-8）：周期边界按<b>设备本地时区</b>计算（直接用传入的本地 <see cref="DateTime"/>）。</para>
/// </remarks>
public sealed class BudgetService
{
    /// <summary>限额上界（防止把预算当成天文数字误填）。</summary>
    public const decimal MaxLimitAmount = 1_000_000_000m;

    /// <summary>预警阈值上界（比例，1 = 100%；达此值即必为超支，故上界取 1）。</summary>
    public const double MaxAlertThreshold = 1.0d;

    /// <summary>合法作用域（FR-3.6：Model / Provider / Global）。</summary>
    public static readonly IReadOnlyList<string> ValidScopes = ["global", "model", "provider"];

    /// <summary>合法周期（FR-3.6：day / month / quarter / year / all）。</summary>
    public static readonly IReadOnlyList<string> ValidPeriods = ["day", "month", "quarter", "year", "all"];

    /// <summary>列出全部预算规则（按名称排序）。</summary>
    public IReadOnlyList<CostBudget> List() =>
        CostBudget.FindAll().OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>按规则名取一条；不存在返回 <see langword="null"/>。</summary>
    public CostBudget? Find(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        var trimmed = name.Trim();
        return CostBudget.FindAll(CostBudget._.Name == trimmed).FirstOrDefault();
    }

    /// <summary>新增或更新一条预算规则（按 <see cref="CostBudget.Name"/> 唯一）。</summary>
    /// <remarks>
    /// 校验失败一律抛 <see cref="ArgumentException"/> / <see cref="ArgumentOutOfRangeException"/>，
    /// 让端点层（A11）能直接映射成 400 + 明确错误（02-spec Error Handling「禁止静默返回空表」）。
    /// 同名重复提交且内容不同 ⇒ <b>显式拒绝</b>（不静默覆盖）。
    /// </remarks>
    public CostBudget Save(CostBudget draft)
    {
        var v = Validate(draft);
        var now = DateTime.Now;
        var existing = Find(v.Name);

        if (existing is null)
        {
            var created = new CostBudget
            {
                Name = v.Name,
                Scope = v.Scope,
                Target = v.Target,
                LimitAmount = v.Limit,
                Currency = v.Currency,
                Period = v.Period,
                AlertThreshold = v.Alert,
                CreatedTime = now,
                UpdatedTime = now,
            };
            created.Save();
            XTrace.Log.Info("[CostScope] 新增预算 {0} {1}/{2} 限额={3} {4}", created.Name, created.Scope, created.Target, created.LimitAmount, created.Period);
            return created;
        }

        // 同名重复提交：内容相同 ⇒ 幂等放行；内容不同 ⇒ 显式拒绝（只比内容，不比名称）
        var sameContent = existing.Scope == v.Scope
            && existing.Target == v.Target
            && existing.LimitAmount == v.Limit
            && existing.Period == v.Period
            && Math.Abs(existing.AlertThreshold - v.Alert) < 1e-9;

        if (!sameContent)
            throw new InvalidOperationException($"预算规则 {v.Name} 已存在且内容不同；若要改预算请调用 Update（新增通道不静默覆盖）");

        ApplyTo(existing, v, now);
        return existing;
    }

    /// <summary>显式<b>改预算</b>通道（与 <see cref="Save"/> 的分工同 <c>PriceCatalogService</c>）。</summary>
    /// <remarks>
    /// <b>为什么必须有</b>：<see cref="Save"/> 是「新增」通道，同名且内容不同 ⇒ 拒绝（防误覆盖）。
    /// 若没有本方法，「改预算」就<b>无路可走</b>——因为改预算按定义就是「同名 + 内容不同」，
    /// 端点 PUT 会被 <see cref="InvalidOperationException"/> 打成 500。
    /// 与价格目录同构：新增防误覆盖与修改必须是<b>两个通道</b>。
    /// </remarks>
    public CostBudget Update(CostBudget draft)
    {
        // ⚠ 必须与 Save 共用同一份校验。早先这里直接调 ApplyTo，**绕过了全部校验**，
        //    导致非法作用域/周期/限额/阈值能经 PUT 写入库（由 Update_非法内容_抛入参异常 抓到）。
        var v = Validate(draft);

        var existing = Find(v.Name)
            ?? throw new InvalidOperationException($"预算规则 {v.Name} 不存在，请先用新增（Save）创建");

        ApplyTo(existing, v, DateTime.Now);
        return existing;
    }

    /// <summary>校验并归一化草稿（<b>Save / Update 共用</b>，保证两条通道口径一致、都不漏校验）。</summary>
    private static Normalized Validate(CostBudget draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var name = (draft.Name ?? string.Empty).Trim();
        if (name.Length == 0) throw new ArgumentException("规则名称不可为空", nameof(draft));

        var scope = (draft.Scope ?? string.Empty).Trim().ToLowerInvariant();
        if (!IsValidScope(scope)) throw new ArgumentException($"作用域非法，合法值：{string.Join("/", ValidScopes)}", nameof(draft));

        var period = (draft.Period ?? string.Empty).Trim().ToLowerInvariant();
        if (!IsValidPeriod(period)) throw new ArgumentException($"周期非法，合法值：{string.Join("/", ValidPeriods)}", nameof(draft));

        var target = (draft.Target ?? string.Empty).Trim();

        // global 不针对具体目标；model / provider 必须指定目标，否则规则无从生效
        if (scope == "global" && target.Length > 0)
            throw new ArgumentException("作用域为 global 时不应指定目标（Target）", nameof(draft));
        if (scope != "global" && target.Length == 0)
            throw new ArgumentException($"作用域为 {scope} 时必须指定目标（Target）", nameof(draft));

        if (draft.LimitAmount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(draft), "限额必须大于 0（否则达成率无意义）");
        if (draft.LimitAmount > MaxLimitAmount)
            throw new ArgumentOutOfRangeException(nameof(draft), $"限额不得超过 {MaxLimitAmount}");

        if (draft.AlertThreshold <= 0d || draft.AlertThreshold > MaxAlertThreshold)
            throw new ArgumentOutOfRangeException(nameof(draft), $"预警阈值须在 (0, {MaxAlertThreshold}] 区间（比例）");

        var currency = string.IsNullOrWhiteSpace(draft.Currency) ? PriceCatalogService.DefaultCurrency : draft.Currency.Trim();
        return new Normalized(name, scope, target, draft.LimitAmount, period, draft.AlertThreshold, currency);
    }

    /// <summary>把<b>已校验</b>的草稿落到既有实体上（新增后的重放 / 改预算共用）。</summary>
    private static void ApplyTo(CostBudget existing, Normalized v, DateTime now)
    {
        existing.Scope = v.Scope;
        existing.Target = v.Target;
        existing.LimitAmount = v.Limit;
        existing.Period = v.Period;
        existing.AlertThreshold = v.Alert;
        existing.Currency = v.Currency;
        existing.UpdatedTime = now;
        existing.Save();

        XTrace.Log.Info("[CostScope] 更新预算 {0} 限额={1} {2}", existing.Name, existing.LimitAmount, existing.Period);
    }

    /// <summary>校验归一化后的预算草稿（<see cref="Validate"/> 的返回值）。</summary>
    private readonly record struct Normalized(
        string Name, string Scope, string Target, decimal Limit, string Period, double Alert, string Currency);

    /// <summary>删除一条预算规则；返回 true 表示确实删掉了。</summary>
    public bool Remove(string? name)
    {
        var existing = Find(name);
        if (existing is null) return false;

        var n = existing.Name;
        existing.Delete();
        XTrace.Log.Info("[CostScope] 删除预算 {0}", n);
        return true;
    }

    /// <summary>作用域是否合法（global / model / provider，不区分大小写）。</summary>
    public static bool IsValidScope(string? scope) =>
        scope is not null && ValidScopes.Contains(scope.Trim().ToLowerInvariant());

    /// <summary>周期是否合法（day / month / quarter / year / all，不区分大小写）。</summary>
    public static bool IsValidPeriod(string? period) =>
        period is not null && ValidPeriods.Contains(period.Trim().ToLowerInvariant());

    /// <summary>
    /// 计算某周期在「本地时区」的起止边界（左闭右开）。
    /// </summary>
    /// <param name="period">周期关键字（不区分大小写）。</param>
    /// <param name="now">基准时刻（本地时区，U-8）。</param>
    /// <returns>(起始, 结束)；<c>all</c> 返回 <see cref="DateTime.MinValue"/>~<see cref="DateTime.MaxValue"/>。</returns>
    public static (DateTime Start, DateTime End) ResolvePeriod(string period, DateTime now)
    {
        var p = (period ?? string.Empty).Trim().ToLowerInvariant();
        return p switch
        {
            "day" => (now.Date, now.Date.AddDays(1)),
            "month" => (new DateTime(now.Year, now.Month, 1), new DateTime(now.Year, now.Month, 1).AddMonths(1)),
            "quarter" => QuarterOf(now),
            "year" => (new DateTime(now.Year, 1, 1), new DateTime(now.Year, 1, 1).AddYears(1)),
            "all" => (DateTime.MinValue, DateTime.MaxValue),
            _ => throw new ArgumentException($"周期非法，合法值：{string.Join("/", ValidPeriods)}", nameof(period)),
        };
    }

    /// <summary>判断某时刻是否落在给定周期内（左闭右开）。</summary>
    public static bool IsInPeriod(string period, DateTime moment)
    {
        var (start, end) = ResolvePeriod(period, moment);
        return moment >= start && moment < end;
    }

    /// <summary>
    /// 纯函数：按已用金额计算达成率与状态（OK / 预警 / 超支）。
    /// </summary>
    /// <remarks>
    /// 「已用金额」由聚合侧传入，本方法<b>不查库</b> ⇒ 口径完全可单测（A6 与 A7 可并行）。
    /// 限额 ≤ 0（脏数据）时不做除法：已用 &gt; 0 判超支，否则判正常。
    /// </remarks>
    public static BudgetAchievement Evaluate(CostBudget budget, decimal usedAmount)
    {
        ArgumentNullException.ThrowIfNull(budget);
        if (usedAmount < 0m) throw new ArgumentOutOfRangeException(nameof(usedAmount), "已用金额不可为负");

        var limit = budget.LimitAmount;
        var ratio = limit > 0m ? (double)usedAmount / (double)limit : 0d;
        var remaining = limit - usedAmount;
        if (remaining < 0m) remaining = 0m;

        var level = limit <= 0m
            ? (usedAmount > 0m ? BudgetStatus.Exceeded : BudgetStatus.Ok)
            : ratio >= 1d ? BudgetStatus.Exceeded
            : ratio >= budget.AlertThreshold ? BudgetStatus.Warning
            : BudgetStatus.Ok;

        return new BudgetAchievement(limit, usedAmount, remaining, ratio, level, ratio >= budget.AlertThreshold);
    }

    private static (DateTime Start, DateTime End) QuarterOf(DateTime now)
    {
        var firstMonth = ((now.Month - 1) / 3) * 3 + 1;
        var start = new DateTime(now.Year, firstMonth, 1);
        return (start, start.AddMonths(3));
    }
}

using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.CostScope.Entities;
using ForgeSelf.Api.Plugins.CostScope.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.CostScope.Controllers;

/// <summary>单价 upsert 请求体。</summary>
public sealed record PriceUpsertRequest(
    string? Model, string? Provider,
    decimal InputPricePer1M, decimal OutputPricePer1M,
    string? Currency, bool? IsEnabled);

/// <summary>预算 upsert 请求体。</summary>
public sealed record BudgetUpsertRequest(
    string? Name, string? Scope, string? Target,
    decimal LimitAmount, string? Currency,
    string? Period, double AlertThreshold);

/// <summary>
/// 成本观测端点面（FR-4 / FR-3.5 / FR-3.6 / A11）。
/// </summary>
/// <remarks>
/// <para><b>鉴权</b>：类级 <c>[Authorize("ApiKeyPolicy")]</c>——插件控制器<b>不会自动被保护</b>，
/// 必须显式声明（铁律 17 / FR-4.7）。未带宿主令牌一律 401。</para>
/// <para><b>数据来源</b>：宿主轮次经 <see cref="ITurnTelemetryQuery"/> 只读取数（插件不直连宿主库，FR-4.6 / BR-5）；
/// 单价/预算/日汇总落插件自有库。</para>
/// <para><b>聚合器按请求新建</b>：模型目录（供应商归属）在每次请求内取一次快照注入聚合器，
/// 避免单例聚合器持有<b>陈旧目录</b>导致归属错乱。</para>
/// <para><b>错误纪律</b>：非法维度 / 非法价格 / 非法预算 一律 <b>400 + 明确错误</b>，
/// <b>禁止静默返回空表</b>（02-spec Error Handling）；非法 <c>by=</c> 必须回 400 并附可用值。</para>
/// <para><b>不泄露</b>：端点只返回聚合与元数据，<b>不含请求/响应体</b>（NFR-5）；明细回看走既有 <c>ChatRecordsController</c>。</para>
/// </remarks>
[ApiController]
[Authorize("ApiKeyPolicy")]
[Route("api/cost-scope")]
public class CostController : ControllerBase
{
    /// <summary>默认时间窗：近 30 天（032 P0 口径）。</summary>
    public const int DefaultWindowDays = 30;

    /// <summary>分页默认页码。</summary>
    public const int DefaultPage = 1;

    /// <summary>分页默认每页条数。</summary>
    public const int DefaultSize = 100;

    /// <summary>分页每页上限（防止大时间窗一次性全表扫，NFR-4）。</summary>
    public const int MaxSize = 500;

    private readonly ITurnTelemetryQuery _telemetry;
    private readonly PriceCatalogService _prices;
    private readonly BudgetService _budgets;
    private readonly TraceProjectionService _trace;

    public CostController(
        ITurnTelemetryQuery telemetry,
        PriceCatalogService prices,
        BudgetService budgets,
        TraceProjectionService trace)
    {
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        _prices = prices ?? throw new ArgumentNullException(nameof(prices));
        _budgets = budgets ?? throw new ArgumentNullException(nameof(budgets));
        _trace = trace ?? throw new ArgumentNullException(nameof(trace));
    }

    // ---------- 读取类 ----------

    /// <summary>成本总览（含覆盖度与未配单价清单）。</summary>
    [HttpGet("overview")]
    public async Task<IActionResult> Overview(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? model, [FromQuery] string? style,
        CancellationToken ct)
    {
        var (turns, agg) = await LoadAsync(from, to, model, style, ct);
        return Ok(agg.Overview(turns, Now()));
    }

    /// <summary>精简总览（今日/本月/总额 + 轮次与失败数），供首屏卡片使用。</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var (turns, agg) = await LoadAsync(from, to, null, null, ct);
        var ov = agg.Overview(turns, Now());

        return Ok(new
        {
            ov.TodayCost,
            ov.MonthCost,
            ov.TotalCost,
            ov.CostIsLowerBound,
            ov.Turns,
            ov.FailCount,
            ov.TotalTokens,
            unpricedModelCount = ov.UnpricedModels.Count,
            ov.Coverage,
        });
    }

    /// <summary>日趋势（当日走实时重算，历史日读物化缓存）。</summary>
    [HttpGet("daily")]
    public async Task<IActionResult> Daily(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? model, [FromQuery] string? style,
        CancellationToken ct)
    {
        var (turns, agg) = await LoadAsync(from, to, model, style, ct);
        var projection = new TelemetryProjectionService(agg);

        return Ok(projection.QueryWithCache(turns, Now()));
    }

    /// <summary>维度排行。<paramref name="by"/> 非法 ⇒ <b>400 + 可用值</b>，不静默空表。</summary>
    [HttpGet("breakdown")]
    public async Task<IActionResult> Breakdown(
        [FromQuery] string? by,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? model, [FromQuery] string? style,
        [FromQuery] int? top,
        CancellationToken ct)
    {
        if (!TryParseDimension(by, out var dim))
            return BadRequest($"未知维度 by={by}；合法值：{string.Join("/", AllowedDimensions())}");

        var (turns, agg) = await LoadAsync(from, to, model, style, ct);
        return Ok(agg.Breakdown(turns, dim, top ?? 0));
    }

    /// <summary>延迟分位（首字与总耗时分别出）。</summary>
    [HttpGet("latency")]
    public async Task<IActionResult> Latency(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? model, [FromQuery] string? style,
        CancellationToken ct)
    {
        var (turns, agg) = await LoadAsync(from, to, model, style, ct);
        return Ok(agg.Latency(turns));
    }

    /// <summary>错误率与错误分布。</summary>
    [HttpGet("errors")]
    public async Task<IActionResult> Errors(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? model, [FromQuery] string? style,
        CancellationToken ct)
    {
        var (turns, agg) = await LoadAsync(from, to, model, style, ct);
        return Ok(agg.Errors(turns));
    }

    /// <summary>轮次明细分页（<b>只含元数据，不含请求/响应体</b>，NFR-5）。</summary>
    [HttpGet("turns")]
    public async Task<IActionResult> Turns(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? model, [FromQuery] string? style,
        [FromQuery] int? page, [FromQuery] int? size,
        CancellationToken ct)
    {
        var (turns, _) = await LoadAsync(from, to, model, style, ct);

        var p = page is null or < 1 ? DefaultPage : page.Value;
        var s = size is null or < 1 ? DefaultSize : Math.Min(size.Value, MaxSize);

        return Ok(new
        {
            page = p,
            size = s,
            total = turns.Count,
            items = turns.Skip((p - 1) * s).Take(s).Select(x => new
            {
                x.Id,
                x.SessionKey,
                x.TurnIndex,
                x.Model,
                x.Style,
                x.PromptTokens,
                x.CompletionTokens,
                x.FirstTokenMs,
                x.DurationMs,
                x.ResponseStatus,
                x.ErrorMessage,
                x.CreatedTime,
                x.AgentRunId,
            }),
        });
    }

    /// <summary>trace 瀑布（关联为近似，见 <see cref="TraceProjectionService.ApproximateNote"/>）。</summary>
    [HttpGet("trace")]
    public async Task<IActionResult> Trace(
        [FromQuery] string? agentRunId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var (turns, _) = await LoadAsync(from, to, null, null, ct);
        var runs = await LoadRunsAsync(turns, ct);

        return Ok(_trace.Build(agentRunId, turns, runs));
    }

    /// <summary>口径说明与默认值（供前端自解释，避免把部分覆盖误读为全量）。</summary>
    [HttpGet("settings")]
    public IActionResult Settings() => Ok(new
    {
        defaultWindowDays = DefaultWindowDays,
        defaultPage = DefaultPage,
        defaultSize = DefaultSize,
        maxSize = MaxSize,
        dimensions = AllowedDimensions(),
        currency = PriceCatalogService.DefaultCurrency,
        coverageNote = TraceProjectionService.UnlinkedNote,
        correlationNote = TraceProjectionService.ApproximateNote,
        unpricedPolicy = "未配单价的模型不计入总额（记为下界），并在 unpricedModels 中显式列出，绝不静默计 0",
    });

    // ---------- 单价目录 CRUD（端点随 A11 统一开，FR-3.5） ----------

    [HttpGet("prices")]
    public IActionResult ListPrices([FromQuery] bool? allEnabled)
        => Ok(_prices.List(allEnabled ?? true).Select(ToPriceDto));

    [HttpPost("prices")]
    public IActionResult CreatePrice([FromBody] PriceUpsertRequest req)
    {
        if (req is null) return BadRequest("请求体不可为空");
        if (string.IsNullOrWhiteSpace(req.Model)) return BadRequest("model 不可为空");
        if (_prices.Find(req.Model) is not null) return BadRequest($"模型 {req.Model} 已有单价条目，请改用 PUT 更新");

        return Guard(() => SafeSave(req), created: true, nameof(ListPrices));
    }

    [HttpPut("prices")]
    public IActionResult UpdatePrice([FromBody] PriceUpsertRequest req)
    {
        if (req is null) return BadRequest("请求体不可为空");
        if (string.IsNullOrWhiteSpace(req.Model)) return BadRequest("model 不可为空");
        if (_prices.Find(req.Model) is null) return BadRequest($"模型 {req.Model} 尚无单价条目，请用 POST 新增");

        return Guard(() => SafeUpdate(req));
    }

    [HttpDelete("prices/{model}")]
    public IActionResult DeletePrice(string model)
        => _prices.Remove(model) ? NoContent() : BadRequest($"模型 {model} 尚无单价条目");

    // ---------- 预算 CRUD + 达成率（FR-3.6） ----------

    [HttpGet("budgets")]
    public IActionResult ListBudgets() => Ok(_budgets.List().Select(ToBudgetDto));

    [HttpPost("budgets")]
    public IActionResult CreateBudget([FromBody] BudgetUpsertRequest req)
    {
        if (req is null) return BadRequest("请求体不可为空");
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("name 不可为空");
        if (_budgets.Find(req.Name) is not null) return BadRequest($"预算 {req.Name} 已存在，请改用 PUT 更新");

        return Guard(() => SafeSaveBudget(req), created: true, nameof(ListBudgets));
    }

    [HttpPut("budgets")]
    public IActionResult UpdateBudget([FromBody] BudgetUpsertRequest req)
    {
        if (req is null) return BadRequest("请求体不可为空");
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("name 不可为空");
        if (_budgets.Find(req.Name) is null) return BadRequest($"预算 {req.Name} 不存在，请用 POST 新增");

        return Guard(() => SafeUpdateBudget(req));
    }

    [HttpDelete("budgets/{name}")]
    public IActionResult DeleteBudget(string name)
        => _budgets.Remove(name) ? NoContent() : BadRequest($"预算 {name} 不存在");

    /// <summary>预算实时达成率（<c>usedAmount</c> 由调用方给，或由本服务按时间窗实算）。</summary>
    [HttpGet("budgets/achievement")]
    public async Task<IActionResult> BudgetAchievement(
        [FromQuery] string? name,
        [FromQuery] decimal? usedAmount,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var budgets = name is null ? _budgets.List() : new[] { _budgets.Find(name) }.Where(x => x is not null).Select(x => x!).ToList();
        if (budgets.Count == 0) return BadRequest(name is null ? "尚无预算规则" : $"预算 {name} 不存在");

        // 未显式给 usedAmount 时按时间窗实算。
        // ⚠ 必须**按作用域选对应维度**：先前只用 Model 维度建字典，provider 作用域的 Target 是
        // 「供应商名」，拿它去查模型字典必然查不到 ⇒ provider 预算恒显示已用 0（静默错数）。
        Dictionary<string, decimal>? byModel = null;
        Dictionary<string, decimal>? byProvider = null;
        if (usedAmount is null)
        {
            var (turns, agg) = await LoadAsync(from, to, null, null, ct);
            byModel = Sum(agg.Breakdown(turns, AggregationDimension.Model));
            byProvider = Sum(agg.Breakdown(turns, AggregationDimension.Provider));
        }

        var items = budgets.Select(b =>
        {
            var used = usedAmount ?? b.Scope switch
            {
                "global" => byModel!.Values.Sum(),
                "provider" => Lookup(byProvider!, b.Target),
                _ => Lookup(byModel!, b.Target),
            };
            return new { b.Name, b.Scope, b.Target, b.Period, b.LimitAmount, Achievement = BudgetService.Evaluate(b, used) };
        });

        return Ok(items);
    }

    private static Dictionary<string, decimal> Sum(IReadOnlyList<DimensionItem> items) =>
        items.GroupBy(i => i.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Cost), StringComparer.OrdinalIgnoreCase);

    private static decimal Lookup(Dictionary<string, decimal> map, string? key) =>
        !string.IsNullOrWhiteSpace(key) && map.TryGetValue(key, out var v) ? v : 0m;

    /// <summary>清空日汇总物化缓存并按当前窗口重算（FR-3.4 改价后恢复重算）。</summary>
    [HttpPost("summary/rebuild")]
    public IActionResult RebuildSummary()
    {
        var cleared = NewProjectionForCacheOnly().InvalidateAll();
        XTrace.Log.Info("[CostController] 日汇总缓存已清空 {0} 行，下次查询将重算", cleared);
        return Ok(new { clearedRows = cleared });
    }

    // ---------- 内部 ----------

    /// <summary>
    /// 把服务层的<b>入参校验异常统一映射为 400</b>（非法价格 / 非法阈值 / 作用域周期非法 / 重复条目）。
    /// </summary>
    /// <remarks>
    /// 刻意<b>不</b>捕获其它异常：数据访问失败等应如实冒泡为 500，不能被伪装成「参数错」。
    /// </remarks>
    private IActionResult Guard(Func<object> action, bool created = false, string? createdAction = null)
    {
        try
        {
            var result = action();
            return created
                ? CreatedAtAction(createdAction, ToDto(result))
                : Ok(ToDto(result));
        }
        catch (ArgumentException ex)
        {
            // ArgumentOutOfRangeException 继承自 ArgumentException，一并覆盖
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // 状态冲突（如「已存在且内容不同」「不存在」）是**客户端可纠正**的 ⇒ 409，
            // 不能冒泡成 500（那是服务端故障的语义，会把用户的操作错误误诊成系统问题）。
            return Conflict(new { message = ex.Message });
        }
    }

    private static object ToDto(object entity) => entity switch
    {
        CostModelPrice p => ToPriceDto(p),
        CostBudget b => ToBudgetDto(b),
        var other => other,
    };

    /// <summary>
    /// 仅用于「清缓存」的投影实例：<c>InvalidateAll()</c> 不触碰聚合逻辑，
    /// 因此无需本次请求的模型目录快照（避免为一个清缓存动作去查库）。
    /// </summary>
    private TelemetryProjectionService NewProjectionForCacheOnly() => new(new CostAggregationService(_prices));

    /// <summary>
    /// 载入窗口数据 + 按请求构造聚合器。
    /// </summary>
    /// <remarks>
    /// 聚合器<b>不进 DI</b>：它依赖「本次请求的模型目录快照」，
    /// 单例持有会在目录变更后把模型归错供应商。
    /// </remarks>
    private async Task<(IReadOnlyList<TurnTelemetryRecord> Turns, CostAggregationService Aggregation)> LoadAsync(
        DateTime? from, DateTime? to, string? model, string? style, CancellationToken ct)
    {
        var now = Now();
        var filter = new TurnTelemetryQueryFilter
        {
            From = from ?? now.AddDays(-DefaultWindowDays),
            To = to ?? now,
            Model = string.IsNullOrWhiteSpace(model) ? null : model,
            Style = string.IsNullOrWhiteSpace(style) ? null : style,
        };

        var turns = await _telemetry.QueryTurnsAsync(filter, ct);

        var catalog = await _telemetry.GetModelCatalogAsync(ct);

        // 目录快照在本次请求内固定：同一份目录喂给所有模型，避免请求中途目录变化导致归属不一致。
        // 解析结果按模型**记忆化**：聚合器对每条记录都会问一次「这个模型归谁」，
        // 不缓存就是 O(记录数 × 目录行数) 次重复字符串匹配。
        var providerCache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var agg = new CostAggregationService(_prices, m =>
        {
            if (providerCache.TryGetValue(m, out var cached)) return cached;

            var resolution = ModelPriceResolver.Match(m, catalog);
            var provider = IsAttributed(resolution) ? resolution.ProviderName : null;
            providerCache[m] = provider;
            return provider;
        });

        return (turns, agg);
    }

    private async Task<IReadOnlyList<AgentRunTelemetry>> LoadRunsAsync(
        IReadOnlyList<TurnTelemetryRecord> turns, CancellationToken ct)
    {
        // AgentRun 数据不在宿主（属 AIAgent 插件自有库）；宿主接缝未注册提供者时返回空集，
        // 端点据此呈现「未关联」而不是报错或 404（BC-8/BC-4）。
        var runIds = turns.Where(t => !string.IsNullOrWhiteSpace(t.AgentRunId))
            .Select(t => t.AgentRunId!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var runs = new List<AgentRunTelemetry>(runIds.Count);
        foreach (var id in runIds)
        {
            var run = await _telemetry.GetAgentRunTelemetryAsync(id, ct);
            if (run is not null) runs.Add(run);
        }

        return runs;
    }

    private static bool IsAttributed(ModelResolution r) => r.IsAttributed && !string.IsNullOrWhiteSpace(r.ProviderName);

    private static DateTime Now() => DateTime.Now;

    private static IEnumerable<string> AllowedDimensions()
        => Enum.GetNames<AggregationDimension>().Select(x => x.ToLowerInvariant());

    /// <summary>
    /// 解析聚合维度。
    /// </summary>
    /// <remarks>
    /// 口径：<b>未传</b>（null）⇒ 默认 model（友好）；<b>传了但为空/非法</b> ⇒ 判错（400）。
    /// 显式传空串是客户端错误，静默按默认维度返回会掩盖问题（02-spec「禁止静默返回空表」）。
    /// 数字串（如 <c>99</c>）虽能被 <c>Enum.TryParse</c> 接受，但 <c>Enum.IsDefined</c> 会挡掉。
    /// </remarks>
    private static bool TryParseDimension(string? by, out AggregationDimension dim)
    {
        dim = AggregationDimension.Model;
        if (by is null) return true;
        if (string.IsNullOrWhiteSpace(by)) return false;

        return Enum.TryParse(by.Trim(), ignoreCase: true, out dim) && Enum.IsDefined(dim);
    }

    private CostModelPrice SafeSave(PriceUpsertRequest req) => _prices.Save(new CostModelPrice
    {
        Model = req.Model!.Trim(),
        Provider = req.Provider,
        InputPricePer1M = req.InputPricePer1M,
        OutputPricePer1M = req.OutputPricePer1M,
        Currency = req.Currency,
        IsEnabled = req.IsEnabled ?? true,
    });

    private CostModelPrice SafeUpdate(PriceUpsertRequest req) => _prices.Update(new CostModelPrice
    {
        Model = req.Model!.Trim(),
        Provider = req.Provider,
        InputPricePer1M = req.InputPricePer1M,
        OutputPricePer1M = req.OutputPricePer1M,
        Currency = req.Currency,
        IsEnabled = req.IsEnabled ?? true,
    });

    private CostBudget SafeSaveBudget(BudgetUpsertRequest req) => _budgets.Save(new CostBudget
    {
        Name = req.Name!.Trim(),
        Scope = req.Scope,
        Target = req.Target,
        LimitAmount = req.LimitAmount,
        Currency = req.Currency,
        Period = req.Period,
        AlertThreshold = req.AlertThreshold,
    });

    private CostBudget SafeUpdateBudget(BudgetUpsertRequest req) => _budgets.Update(new CostBudget
    {
        Name = req.Name!.Trim(),
        Scope = req.Scope,
        Target = req.Target,
        LimitAmount = req.LimitAmount,
        Currency = req.Currency,
        Period = req.Period,
        AlertThreshold = req.AlertThreshold,
    });

    private static object ToPriceDto(CostModelPrice p) => new
    {
        p.Model,
        p.Provider,
        p.InputPricePer1M,
        p.OutputPricePer1M,
        p.Currency,
        p.IsEnabled,
        p.EffectiveFrom,
        p.EffectiveTo,
    };

    private static object ToBudgetDto(CostBudget b) => new
    {
        b.Name,
        b.Scope,
        b.Target,
        b.LimitAmount,
        b.Currency,
        b.Period,
        b.AlertThreshold,
    };
}

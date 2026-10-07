using System.IO;
using System.Reflection;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.CostScope;
using ForgeSelf.Api.Plugins.CostScope.Entities;
using ForgeSelf.Api.Plugins.CostScope.Services;
using XCode;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Tests.Plugins.CostScopeTests;

/// <summary>
/// A7 原子任务：只读成本/用量聚合单测（FR-4.2~4.4 / FR-3.4 / BR-2 / BR-4 / BC-7）。
/// </summary>
/// <remarks>
/// <b>输入口径说明（避免被误当硬编码金值）</b>：单价是<b>用户本地配置</b>，仓内无真实配置可驱动，
/// 故由用例显式写入并<b>跟着写入值断言</b>。为了让金额<b>可手算复核</b>，统一取
/// 「输入 1000 tokens × 1000 元/1M = 1 元；输出 500 tokens × 2000 元/1M = 1 元 ⇒ 单轮 2 元」——
/// 这组数字是<b>推导出来的算例</b>（见各用例注释），不是抄来的金值。
/// 延迟分位用 10 个等距样本，p50/p95/p99 可手算复核。
/// </remarks>
public class CostAggregationServiceTests
{
    private const decimal InPrice = 1000m;   // 元 / 1M tokens
    private const decimal OutPrice = 2000m;  // 元 / 1M tokens

    private static int _dbReady;
    private static readonly object DbLock = new();

    private readonly PriceCatalogService _prices;
    private readonly CostAggregationService _sut;

    public CostAggregationServiceTests()
    {
        EnsurePluginDb();
        ClearAll();
        _prices = new PriceCatalogService();
        _sut = new CostAggregationService(_prices);
    }

    // ---------- 夹具助手 ----------

    private static string NewModel(string tag) => $"{tag}-{Guid.NewGuid():N}";

    private void Price(string model) => _prices.Save(new CostModelPrice
    {
        Model = model,
        Provider = "prov-agg",
        InputPricePer1M = InPrice,
        OutputPricePer1M = OutPrice,
        IsEnabled = true,
    });

    private static TurnTelemetryRecord Rec(
        string model,
        int prompt = 1000,
        int completion = 500,
        int status = 200,
        long firstTokenMs = 0,
        long durationMs = 1000,
        string? error = null,
        string style = "openai",
        DateTime? created = null) => new()
        {
            Id = Math.Abs(HashCode.Combine(model, created ?? DateTime.UnixEpoch, status, firstTokenMs, durationMs)),
            ChatSessionId = 1,
            TurnIndex = 1,
            SessionKey = "sk-agg",
            Style = style,
            Model = model,
            PromptTokens = prompt,
            CompletionTokens = completion,
            FirstTokenMs = firstTokenMs,
            DurationMs = durationMs,
            ResponseStatus = status,
            ErrorMessage = error,
            CreatedTime = created ?? new DateTime(2026, 10, 6, 12, 0, 0),
            AgentRunId = null,
        };

    private static DateTime Day(int d) => new(2026, 10, d, 12, 0, 0);

    // ---------- AC7-1 四维聚合 + BR-2 ----------

    [Fact]
    public void Breakdown_按模型聚合_金额与轮次正确()
    {
        var m1 = NewModel("agg-m1");
        var m2 = NewModel("agg-m2");
        Price(m1); Price(m2);

        // m1: 两轮 ⇒ 2×2 = 4 元；m2: 一轮 ⇒ 2 元
        var items = _sut.Breakdown(
            [Rec(m1), Rec(m1), Rec(m2)],
            AggregationDimension.Model);

        items.Sum(i => i.Cost).Should().Be(6m, "3 轮 × 2 元/轮");
        var i1 = items.Single(i => i.Key == m1);
        i1.Turns.Should().Be(2);
        i1.Cost.Should().Be(4m);
        var i2 = items.Single(i => i.Key == m2);
        i2.Turns.Should().Be(1);
        i2.Cost.Should().Be(2m);
    }

    [Fact]
    public void Breakdown_按风格聚合()
    {
        var m = NewModel("agg-style");
        Price(m);

        var items = _sut.Breakdown(
            [Rec(m, style: "openai"), Rec(m, style: "anthropic"), Rec(m, style: "openai")],
            AggregationDimension.Style);

        items.Count.Should().Be(2);
        items.Single(i => i.Key == "openai").Turns.Should().Be(2);
        items.Single(i => i.Key == "anthropic").Turns.Should().Be(1);
    }

    [Fact]
    public void Breakdown_按日聚合_按成本降序_时序排序由Daily负责()
    {
        var m = NewModel("agg-day");
        Price(m);

        var items = _sut.Breakdown(
            [Rec(m, created: Day(6)), Rec(m, created: Day(4)), Rec(m, created: Day(6))],
            AggregationDimension.Day);

        // Breakdown 是「维度排行榜」（02-spec Output 第 3 行）⇒ 按成本降序；
        // 时序升序是 Daily() 的职责（见 Daily_按日分组… 用例），两者分工不同。
        items.Select(i => i.Key).Should().Equal(["2026-10-06", "2026-10-04"]);
        items[0].Turns.Should().Be(2);
        items[0].Cost.Should().Be(4m);
    }

    [Fact]
    public void Breakdown_按供应商聚合_无解析器时全部归未归属()
    {
        var m = NewModel("agg-prov");
        Price(m);

        var items = _sut.Breakdown([Rec(m)], AggregationDimension.Provider);

        items.Single().Key.Should().Be(CostAggregationService.UnattributedKey);
    }

    [Fact]
    public void Breakdown_按供应商聚合_用注入的解析器归到对应供应商()
    {
        var m = NewModel("agg-prov2");
        Price(m);
        var withResolver = new CostAggregationService(_prices, model => "openai");

        var items = withResolver.Breakdown([Rec(m)], AggregationDimension.Provider);

        items.Single().Key.Should().Be("openai");
    }

    [Fact]
    public void Breakdown_空集合_返回空且不抛()
    {
        _sut.Breakdown([], AggregationDimension.Model).Should().BeEmpty();
    }

    [Fact]
    public void Overview_未配单价模型_计入未配清单且总额标为下界()
    {
        var priced = NewModel("agg-priced");
        var unpriced = NewModel("agg-unpriced");
        Price(priced);

        var ov = _sut.Overview([Rec(priced), Rec(unpriced)], Day(6));

        ov.CostIsLowerBound.Should().BeTrue("含未配单价模型 ⇒ 总额只是下界");
        ov.UnpricedModels.Should().Contain(unpriced);
        ov.UnpricedModels.Should().NotContain(priced);
        ov.TotalCost.Should().Be(2m, "只有已配单价的 1 轮计入 = 2 元；未配单价那轮按 0 计入下界");
        ov.Coverage.UnpricedCalls.Should().Be(1);
        ov.Coverage.CoveredCalls.Should().Be(1);
    }

    [Fact]
    public void Overview_已配单价_总额为确定值且非下界()
    {
        var m = NewModel("agg-ok");
        Price(m);

        var ov = _sut.Overview([Rec(m), Rec(m)], Day(6));

        ov.TotalCost.Should().Be(4m);
        ov.CostIsLowerBound.Should().BeFalse();
        ov.Turns.Should().Be(2);
        ov.TotalTokens.Should().Be(3000);
        ov.Coverage.CoveredCalls.Should().Be(2);
        ov.Coverage.UnpricedCalls.Should().Be(0);
    }

    [Fact]
    public void Overview_未接线供应商解析器_未归属列表为空_不制造噪音()
    {
        var priced = NewModel("agg-nores");
        Price(priced);

        var ov = _sut.Overview([Rec(priced)], Day(6));

        ov.UnattributedModels.Should().BeEmpty("没有解析器不等于「未归属」，那是「没配置归属解析」");
        ov.UnpricedModels.Should().BeEmpty("已配单价不应出现在未配清单里（两件事不同，混了看不出到底缺什么）");
    }

    [Fact]
    public void Overview_有解析器但解析不出_计入未归属()
    {
        var orphan = NewModel("agg-orphan");
        Price(orphan);
        var withResolver = new CostAggregationService(_prices, _ => null);

        var ov = withResolver.Overview([Rec(orphan)], Day(6));

        ov.UnattributedModels.Should().Contain(orphan);
    }

    [Fact]
    public void Overview_覆盖度_显式声明主聊天不可观测且不伪造计数()
    {
        var m = NewModel("agg-cov");
        Price(m);

        var ov = _sut.Overview([Rec(m)], Day(6));

        ov.Coverage.MainChatObservable.Should().BeFalse("U-2=(c)：主聊天不写 ChatTurn，其数量不可计算");
        ov.Coverage.Note.Should().Contain("主聊天");
        ov.Coverage.TotalRecords.Should().Be(1);
    }

    [Fact]
    public void Overview_今日与本月成本按窗口切分()
    {
        var m = NewModel("agg-win");
        Price(m);
        var now = Day(6);

        var ov = _sut.Overview(
            [Rec(m, created: Day(6)), Rec(m, created: Day(5)), Rec(m, created: new DateTime(2026, 9, 1))],
            now);

        ov.TodayCost.Should().Be(2m, "仅当天那轮");
        ov.MonthCost.Should().Be(4m, "当天 + 昨天，同月");
        ov.TotalCost.Should().Be(6m);
    }

    // ---------- AC7-3 错误率 + BR-4 ----------

    [Fact]
    public void BR4_失败轮次计入失败数且不计成本()
    {
        var m = NewModel("agg-fail");
        Price(m);

        var ov = _sut.Overview([Rec(m), Rec(m, status: 500, error: "boom")], Day(6));

        ov.FailCount.Should().Be(1);
        ov.TotalCost.Should().Be(2m, "失败轮次不计成本（BR-4）");
    }

    [Fact]
    public void Errors_按错误信息归类_不臆造分类体系()
    {
        var m = NewModel("agg-err");
        Price(m);

        var st = _sut.Errors(
        [
            Rec(m),
            Rec(m, status: 500, error: "upstream 500"),
            Rec(m, status: 500, error: "upstream 500"),
            Rec(m, status: 429, error: "rate limited"),
            Rec(m, status: 502, error: "   "),
        ]);

        st.Total.Should().Be(5);
        st.Failed.Should().Be(4);
        st.Rate.Should().BeApproximately(0.8d, 1e-9);
        st.Buckets.Single(b => b.Category == "upstream 500").Count.Should().Be(2);
        st.Buckets.Should().Contain(b => b.Category == "rate limited" && b.Count == 1);
        st.Buckets.Should().Contain(b => b.Category == "(未提供错误信息)" && b.Count == 1);
    }

    [Fact]
    public void Errors_状态未回填_单列而不混进成功()
    {
        var m = NewModel("agg-unknown");
        Price(m);

        var st = _sut.Errors([Rec(m), Rec(m, status: 0)]);

        st.UnknownStatus.Should().Be(1);
        st.Failed.Should().Be(1, "按 BR-4 字面口径，0 != 200 计入失败");
        st.Rate.Should().BeApproximately(0.5d, 1e-9);
    }

    [Fact]
    public void Overview_失败轮次不计入维度成本()
    {
        var m = NewModel("agg-faildim");
        Price(m);

        var items = _sut.Breakdown([Rec(m), Rec(m, status: 500, error: "x")], AggregationDimension.Model);

        var item = items.Single();
        item.Cost.Should().Be(2m, "失败那轮不计成本");
        item.FailCount.Should().Be(1);
        item.Turns.Should().Be(2);
    }

    // ---------- AC7-2 延迟分位 ----------

    [Fact]
    public void Latency_首字与总耗时分别出分位_可手算复核()
    {
        // 样本 10..100 步长 10 ⇒ n=10；最近秩 p50=ceil(5)=第5个=50；p95=ceil(9.5)=10⇒100；p99=ceil(9.9)=10⇒100
        var ft = Enumerable.Range(1, 10).Select(i => (long)i * 10).ToList();
        var du = Enumerable.Range(1, 10).Select(i => (long)i * 100).ToList();

        var st = _sut.Latency(ft.Zip(du, (a, b) => Rec("m", firstTokenMs: a, durationMs: b)).ToList());

        st.FirstToken.Samples.Should().Be(10);
        st.FirstToken.P50.Should().Be(50);
        st.FirstToken.P95.Should().Be(100);
        st.FirstToken.P99.Should().Be(100);

        st.Duration.Samples.Should().Be(10);
        st.Duration.P50.Should().Be(500);
        st.Duration.P95.Should().Be(1000);
    }

    [Fact]
    public void Latency_剔除未回填样本且返回样本数()
    {
        var st = _sut.Latency(
        [
            Rec("m", firstTokenMs: 0, durationMs: 100),
            Rec("m", firstTokenMs: 30, durationMs: 200),
            Rec("m", firstTokenMs: 10, durationMs: 0),
        ]);

        st.FirstToken.Samples.Should().Be(2, "0 表示未回填必须剔除；三条里 firstToken 为 0/30/10 ⇒ 剩 2 个样本");
        st.FirstToken.P50.Should().Be(10, "样本 [10,30] ⇒ p50 = 第 ceil(0.5×2)=1 个 = 10");
        st.Duration.Samples.Should().Be(2, "duration 为 100/200/0 ⇒ 剩 2 个样本");
        st.Duration.P50.Should().Be(100);
    }

    [Fact]
    public void Percentiles_空样本_全0且样本数0_不伪造不返回NaN()
    {
        var p = CostAggregationService.Percentiles([]);

        p.Samples.Should().Be(0);
        p.P50.Should().Be(0);
        p.P95.Should().Be(0);
        p.P99.Should().Be(0);
        p.P50.Should().Be(0, "空样本不得出现 NaN 或随机值");
    }

    [Fact]
    public void Percentiles_单样本_三个分位同为该值()
    {
        var p = CostAggregationService.Percentiles([42L]);

        p.P50.Should().Be(42);
        p.P95.Should().Be(42);
        p.P99.Should().Be(42);
        p.Samples.Should().Be(1);
    }

    // ---------- AC7-4 日趋势 ----------

    [Fact]
    public void Daily_按日分组且成本与失败数正确()
    {
        var m = NewModel("agg-daily");
        Price(m);

        var points = _sut.Daily(
        [
            Rec(m, created: Day(4)),
            Rec(m, created: Day(4)),
            Rec(m, created: Day(5), status: 500, error: "e"),
        ]);

        points.Count.Should().Be(2);
        points[0].Day.Should().Be(Day(4).Date);
        points[0].Turns.Should().Be(2);
        points[0].Cost.Should().Be(4m);
        points[1].FailCount.Should().Be(1);
        points[1].Cost.Should().Be(0m, "整日全失败 ⇒ 成本 0");
    }

    // ---------- AC7-6 FR-3.4 改价后历史重算 ----------

    [Fact]
    public void FR34_改单价后总额随之变化_成本不入明细层()
    {
        var m = NewModel("agg-reprice");
        Price(m);

        var before = _sut.Overview([Rec(m)], Day(6)).TotalCost;

        // 改价走显式 Update 通道（Save 是新增通道，重复即拒绝）
        _prices.Update(new CostModelPrice
        {
            Model = m, Provider = "prov-agg",
            InputPricePer1M = InPrice * 2, OutputPricePer1M = OutPrice,
            IsEnabled = true,
        });

        var after = _sut.Overview([Rec(m)], Day(6)).TotalCost;

        after.Should().NotBe(before, "FR-3.4：改价后历史成本必须自动重算");
        after.Should().Be(3m);
    }

    [Fact]
    public void 删除单价后_该模型转未配单价且成本转下界()
    {
        var m = NewModel("agg-delprice");
        Price(m);

        _sut.Overview([Rec(m)], Day(6)).TotalCost.Should().Be(2m);

        _prices.Remove(m).Should().BeTrue();

        var ov = _sut.Overview([Rec(m)], Day(6));
        ov.UnpricedModels.Should().Contain(m, "删除单价 ⇒ 历史成本转 Unknown（BR-2）");
        ov.CostIsLowerBound.Should().BeTrue();
        ov.TotalCost.Should().Be(0m, "未配单价绝不静默计作已核算成本");
    }

    // ---------- 守卫 ----------

    [Fact]
    public void 构造_单价服务为空_抛()
    {
        var act = () => new CostAggregationService(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void 各聚合方法_传null_抛ArgumentNullException()
    {
        var actOverview = () => _sut.Overview(null!, Day(6));
        var actBreakdown = () => _sut.Breakdown(null!, AggregationDimension.Model);
        var actLatency = () => _sut.Latency(null!);
        var actErrors = () => _sut.Errors(null!);
        var actDaily = () => _sut.Daily(null!);

        actOverview.Should().Throw<ArgumentNullException>();
        actBreakdown.Should().Throw<ArgumentNullException>();
        actLatency.Should().Throw<ArgumentNullException>();
        actErrors.Should().Throw<ArgumentNullException>();
        actDaily.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void 非法维度_枚举越界抛_不静默返回空表()
    {
        var act = () => _sut.Breakdown([Rec("m")], (AggregationDimension)999);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ---------- 夹具 ----------

    private static void EnsurePluginDb()
    {
        if (Interlocked.Exchange(ref _dbReady, 1) == 1) return;

        lock (DbLock)
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "costscope-test-db");
            Directory.CreateDirectory(dir);
            DAL.AddConnStr(
                CostScopeDbNaming.ConnName,
                $"Data Source={Path.Combine(dir, CostScopeDbNaming.ConnName + ".db")};Busy Timeout=5000",
                null,
                "SQLite");

            CreateTable<CostModelPrice>();
        }
    }

    private static void CreateTable<TEntity>() where TEntity : Entity<TEntity>, new()
    {
        var meta = typeof(TEntity).GetProperty("Meta", BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
        meta?.GetType().GetMethod("CreateTable", Type.EmptyTypes)?.Invoke(meta, null);
    }

    private static void ClearAll()
    {
        foreach (var row in CostModelPrice.FindAll()) row.Delete();
    }
}

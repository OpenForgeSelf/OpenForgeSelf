using System.IO;
using System.Reflection;
using ForgeSelf.Api.Plugins.CostScope;
using ForgeSelf.Api.Plugins.CostScope.Entities;
using ForgeSelf.Api.Plugins.CostScope.Services;
using XCode;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Tests.Plugins.CostScopeTests;

/// <summary>
/// A6 原子任务：成本预算规则 CRUD + 实时达成率单测（FR-3.6 / U-8）。
/// </summary>
/// <remarks>
/// <b>输入口径说明（避免被误当硬编码金值）</b>：预算是<b>用户本地配置</b>，仓内无真实配置可驱动，
/// 故规则名/目标/限额均由模板串 + 变量构造，断言<b>跟着写入值走</b>；
/// 涉及比例的断言用<b>相对关系</b>（如 used = limit × 0.5）而非写死金额，
/// 并用 <c>阈值常量_与判据字面量一致_防漂移</c> 钉住常量。
/// 周期边界用<b>固定构造时刻</b>（非 <c>DateTime.Now</c>）以保证可重复。
/// </remarks>
public class BudgetServiceTests
{
    private static int _dbReady;
    private static readonly object DbLock = new();

    private readonly BudgetService _sut;

    public BudgetServiceTests()
    {
        EnsurePluginDb();
        ClearAll();
        _sut = new BudgetService();
    }

    private static string NewName(string tag) => $"{tag}-{Guid.NewGuid():N}";

    private static CostBudget Draft(string name, string scope, string target, decimal limit, string period, double alert) => new()
    {
        Name = name,
        Scope = scope,
        Target = target,
        LimitAmount = limit,
        Period = period,
        AlertThreshold = alert,
    };

    // ---------- CRUD ----------

    [Fact]
    public void Save_新增后可按名查回_字段与写入值一致()
    {
        var name = NewName("budget-a");
        var target = $"model-{Guid.NewGuid():N}";
        var limit = 120.5m;
        var alert = 0.8d;

        var saved = _sut.Save(Draft(name, "model", target, limit, "month", alert));

        saved.Id.Should().BePositive();

        var found = _sut.Find(name);
        found.Should().NotBeNull();
        found!.Scope.Should().Be("model");
        found.Target.Should().Be(target);
        found.LimitAmount.Should().Be(limit);
        found.Period.Should().Be("month");
        found.AlertThreshold.Should().Be(alert);
        found.Currency.Should().Be(PriceCatalogService.DefaultCurrency);
    }

    [Fact]
    public void Save_大小写与空白_归一化后可查回()
    {
        var name = NewName("budget-norm");

        _sut.Save(Draft($"  {name}  ", "  MODEL ", " m1 ", 10m, " MONTH ", 0.5d));

        var found = _sut.Find(name);
        found.Should().NotBeNull("名称去空白、作用域/周期转小写后应可查回");
        found!.Scope.Should().Be("model");
        found.Period.Should().Be("month");
    }

    [Fact]
    public void Save_同规则不同内容_显式拒绝_不静默覆盖()
    {
        var name = NewName("budget-dup");
        _sut.Save(Draft(name, "global", "", 100m, "month", 0.8d));

        var act = () => _sut.Save(Draft(name, "global", "", 999m, "month", 0.8d));

        act.Should().Throw<InvalidOperationException>();
        _sut.Find(name)!.LimitAmount.Should().Be(100m, "原限额必须保持不变");
    }

    [Fact]
    public void Save_同规则同内容重放_幂等不抛()
    {
        var name = NewName("budget-idem");
        _sut.Save(Draft(name, "global", "", 50m, "day", 0.9d));

        var act = () => _sut.Save(Draft(name, "global", "", 50m, "day", 0.9d));

        act.Should().NotThrow();
        _sut.List().Count(b => b.Name == name).Should().Be(1);
    }

    [Fact]
    public void Remove_删除后查不到()
    {
        var name = NewName("budget-del");
        _sut.Save(Draft(name, "global", "", 10m, "day", 0.5d));

        _sut.Remove(name).Should().BeTrue();

        _sut.Find(name).Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("never-existed")]
    public void Remove_不存在或空_返回false(string? name)
    {
        _sut.Remove(name).Should().BeFalse();
    }

    [Fact]
    public void Find_空名_返回null()
    {
        _sut.Find("   ").Should().BeNull();
    }

    // ---------- 显式改预算通道（与 Save 分工） ----------

    [Fact]
    public void Update_改预算成功_字段被覆盖()
    {
        var name = NewName("budget-upd");
        _sut.Save(Draft(name, "global", "", 100m, "month", 0.8d));

        _sut.Update(Draft(name, "global", "", 250m, "day", 0.5d));

        var found = _sut.Find(name);
        found!.LimitAmount.Should().Be(250m);
        found.Period.Should().Be("day");
        found.AlertThreshold.Should().Be(0.5d);
    }

    [Fact]
    public void Update_规则不存在_抛_不做upsert()
    {
        // upsert 会掩盖「改了一个根本不存在的规则」这种误操作
        var act = () => _sut.Update(Draft(NewName("budget-upd-missing"), "global", "", 10m, "month", 0.5d));

        act.Should().Throw<InvalidOperationException>().WithMessage("*不存在*");
    }

    [Fact]
    public void Update_非法内容_抛入参异常()
    {
        var name = NewName("budget-upd-bad");
        _sut.Save(Draft(name, "global", "", 100m, "month", 0.8d));

        var act = () => _sut.Update(Draft(name, "nope", "", 100m, "month", 0.8d));

        act.Should().Throw<ArgumentException>();
    }

    // ---------- 校验 ----------

    [Fact]
    public void Save_空规则名_抛()
    {
        var act = () => _sut.Save(Draft("  ", "global", "", 10m, "day", 0.5d));

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("Model2")]
    public void Save_作用域非法_抛(string scope)
    {
        var act = () => _sut.Save(Draft(NewName("budget-scope"), scope, "x", 10m, "day", 0.5d));

        act.Should().Throw<ArgumentException>().WithMessage("*作用域*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("week")]
    [InlineData("Yearly")]
    public void Save_周期非法_抛(string period)
    {
        var act = () => _sut.Save(Draft(NewName("budget-period"), "global", "", 10m, period, 0.5d));

        act.Should().Throw<ArgumentException>().WithMessage("*周期*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Save_限额非正_抛(decimal limit)
    {
        var act = () => _sut.Save(Draft(NewName("budget-limit"), "global", "", limit, "day", 0.5d));

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*限额*");
    }

    [Fact]
    public void Save_限额超上界_抛()
    {
        var act = () => _sut.Save(Draft(NewName("budget-big"), "global", "", BudgetService.MaxLimitAmount + 1, "day", 0.5d));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Save_预警阈值越界_抛(double alert)
    {
        var act = () => _sut.Save(Draft(NewName("budget-alert"), "global", "", 10m, "day", alert));

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*预警阈值*");
    }

    [Fact]
    public void 阈值常量_与判据字面量一致_防漂移()
    {
        BudgetService.MaxAlertThreshold.Should().Be(1.0d);
    }

    [Fact]
    public void Save_作用域model但无目标_抛()
    {
        var act = () => _sut.Save(Draft(NewName("budget-notarget"), "model", "   ", 10m, "day", 0.5d));

        act.Should().Throw<ArgumentException>().WithMessage("*必须指定目标*");
    }

    [Fact]
    public void Save_作用域global却带目标_抛()
    {
        var act = () => _sut.Save(Draft(NewName("budget-globaltarget"), "global", "some-model", 10m, "day", 0.5d));

        act.Should().Throw<ArgumentException>().WithMessage("*global*");
    }

    [Theory]
    [InlineData("global", true)]
    [InlineData("model", true)]
    [InlineData("PROVIDER", true)]
    [InlineData("Model", true)]
    [InlineData("vendor", false)]
    [InlineData(null, false)]
    public void IsValidScope_合法值判定(string? scope, bool expected)
    {
        BudgetService.IsValidScope(scope).Should().Be(expected);
    }

    [Theory]
    [InlineData("day", true)]
    [InlineData("month", true)]
    [InlineData("quarter", true)]
    [InlineData("year", true)]
    [InlineData("all", true)]
    [InlineData("ALL", true)]
    [InlineData("week", false)]
    [InlineData(null, false)]
    public void IsValidPeriod_合法值判定(string? period, bool expected)
    {
        BudgetService.IsValidPeriod(period).Should().Be(expected);
    }

    // ---------- 周期边界（U-8 本地时区，固定构造时刻保证可重复） ----------

    [Fact]
    public void ResolvePeriod_各周期边界正确()
    {
        var now = new DateTime(2026, 5, 17, 13, 42, 0); // 2026 年 Q2

        var day = BudgetService.ResolvePeriod("day", now);
        day.Start.Should().Be(new DateTime(2026, 5, 17));
        day.End.Should().Be(new DateTime(2026, 5, 18));

        var month = BudgetService.ResolvePeriod("month", now);
        month.Start.Should().Be(new DateTime(2026, 5, 1));
        month.End.Should().Be(new DateTime(2026, 6, 1));

        var quarter = BudgetService.ResolvePeriod("quarter", now);
        quarter.Start.Should().Be(new DateTime(2026, 4, 1));
        quarter.End.Should().Be(new DateTime(2026, 7, 1));

        var year = BudgetService.ResolvePeriod("year", now);
        year.Start.Should().Be(new DateTime(2026, 1, 1));
        year.End.Should().Be(new DateTime(2027, 1, 1));

        var all = BudgetService.ResolvePeriod("all", now);
        all.Start.Should().Be(DateTime.MinValue);
        all.End.Should().Be(DateTime.MaxValue);
    }

    [Fact]
    public void ResolvePeriod_季度边界覆盖四个季度起点()
    {
        // (所属月份, 该季度起始月份)：1→1 / 4→4 / 7→7 / 10→10
        var expected = new[]
        {
            (1, 1), (4, 4), (7, 7), (10, 10),
        };

        foreach (var (month, startMonth) in expected)
        {
            var (start, _) = BudgetService.ResolvePeriod("quarter", new DateTime(2026, month, 10));
            start.Should().Be(new DateTime(2026, startMonth, 1), $"{month} 月应落在以 {startMonth} 月为起点的季度");
        }
    }

    [Fact]
    public void ResolvePeriod_非法周期_抛()
    {
        var act = () => BudgetService.ResolvePeriod("week", DateTime.Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsInPeriod_相邻周期首尾不重叠_左闭右开()
    {
        var may = new DateTime(2026, 5, 17, 13, 42, 0);
        var (mayStart, mayEnd) = BudgetService.ResolvePeriod("month", may);

        // 左闭：月首当天属于该月
        BudgetService.IsInPeriod("month", mayStart).Should().BeTrue();
        // 右开的正确语义：下月首日**属于下一月**（而不是本月），且本月窗口终点恰为下月起点
        BudgetService.IsInPeriod("month", mayEnd).Should().BeTrue("6/1 属于 6 月，不属于 5 月");
        mayEnd.Should().Be(mayStart.AddMonths(1), "本月终点必须等于下月起点（窗口不重叠、无缝隙）");
        // 本月最后一刻仍属本月
        BudgetService.IsInPeriod("month", mayEnd.AddSeconds(-1)).Should().BeTrue();
    }

    // ---------- 达成率（纯函数） ----------

    [Fact]
    public void Evaluate_未达预警线_判正常()
    {
        var b = Draft(NewName("budget-ok"), "global", "", 100m, "month", 0.8d);

        var r = BudgetService.Evaluate(b, 40m);

        r.Level.Should().Be(BudgetStatus.Ok);
        r.ShouldAlert.Should().BeFalse();
        r.UsageRatio.Should().BeApproximately(0.4d, 1e-9);
        r.Remaining.Should().Be(60m);
        r.UsedAmount.Should().Be(40m);
        r.LimitAmount.Should().Be(100m);
    }

    [Fact]
    public void Evaluate_达预警线_判预警并应横幅()
    {
        var b = Draft(NewName("budget-warn"), "global", "", 100m, "month", 0.8d);

        var r = BudgetService.Evaluate(b, 80m);

        r.Level.Should().Be(BudgetStatus.Warning);
        r.ShouldAlert.Should().BeTrue();
        r.Remaining.Should().Be(20m);
    }

    [Fact]
    public void Evaluate_超支_判超支且剩余截断为0()
    {
        var b = Draft(NewName("budget-over"), "global", "", 100m, "month", 0.8d);

        var r = BudgetService.Evaluate(b, 150m);

        r.Level.Should().Be(BudgetStatus.Exceeded);
        r.ShouldAlert.Should().BeTrue();
        r.Remaining.Should().Be(0m, "剩余额度不得为负");
        r.UsageRatio.Should().BeApproximately(1.5d, 1e-9);
    }

    [Fact]
    public void Evaluate_恰好等于限额_判超支()
    {
        var b = Draft(NewName("budget-edge"), "global", "", 100m, "month", 0.8d);

        var r = BudgetService.Evaluate(b, 100m);

        r.Level.Should().Be(BudgetStatus.Exceeded, "用满额度即视为超支边界");
        r.UsageRatio.Should().BeApproximately(1.0d, 1e-9);
        r.Remaining.Should().Be(0m);
    }

    [Fact]
    public void Evaluate_零已用_判正常且比例0()
    {
        var b = Draft(NewName("budget-zero"), "global", "", 100m, "month", 0.5d);

        var r = BudgetService.Evaluate(b, 0m);

        r.Level.Should().Be(BudgetStatus.Ok);
        r.UsageRatio.Should().Be(0d);
        r.Remaining.Should().Be(100m);
    }

    [Fact]
    public void Evaluate_已用为负_抛()
    {
        var b = Draft(NewName("budget-neg"), "global", "", 100m, "month", 0.5d);

        var act = () => BudgetService.Evaluate(b, -1m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Evaluate_脏数据限额为0_不做除法且按已用判超支()
    {
        var b = Draft(NewName("budget-dirty"), "global", "", 0m, "month", 0.5d);

        var withSpend = BudgetService.Evaluate(b, 1m);
        withSpend.Level.Should().Be(BudgetStatus.Exceeded);
        withSpend.UsageRatio.Should().Be(0d, "限额为 0 时不得做除法");
        withSpend.Remaining.Should().Be(0m);

        var noSpend = BudgetService.Evaluate(b, 0m);
        noSpend.Level.Should().Be(BudgetStatus.Ok);
    }

    [Fact]
    public void Evaluate_规则为null_抛()
    {
        var act = () => BudgetService.Evaluate(null!, 1m);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Evaluate_阈值越大越晚进入预警()
    {
        var used = 70m;
        var strict = BudgetService.Evaluate(Draft(NewName("budget-s1"), "global", "", 100m, "month", 0.9d), used);
        var loose = BudgetService.Evaluate(Draft(NewName("budget-s2"), "global", "", 100m, "month", 0.5d), used);

        strict.Level.Should().Be(BudgetStatus.Ok);
        loose.Level.Should().Be(BudgetStatus.Warning);
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

            CreateTable<CostBudget>();
        }
    }

    private static void CreateTable<TEntity>() where TEntity : Entity<TEntity>, new()
    {
        var meta = typeof(TEntity).GetProperty("Meta", BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
        meta?.GetType().GetMethod("CreateTable", Type.EmptyTypes)?.Invoke(meta, null);
    }

    private static void ClearAll()
    {
        foreach (var row in CostBudget.FindAll()) row.Delete();
    }
}

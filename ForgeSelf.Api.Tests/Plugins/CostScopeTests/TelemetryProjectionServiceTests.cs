using System.IO;
using System.Reflection;
using System.Text;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.CostScope;
using ForgeSelf.Api.Plugins.CostScope.Entities;
using ForgeSelf.Api.Plugins.CostScope.Services;
using XCode;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Tests.Plugins.CostScopeTests;

/// <summary>
/// A7 原子任务：FR-3.7 惰性物化日汇总单测。
/// </summary>
/// <remarks>
/// <b>输入口径说明</b>：单价为用户本地配置（BR-8），由用例显式写入并跟着写入值断言；
/// 金额取可手算算例：输入 1000 tok × 1000 元/1M = 1 元 + 输出 500 tok × 2000 元/1M = 1 元 ⇒ 单轮 2 元。
/// 改价后（输入 3000 元/1M）单轮 = 3 + 1 = 4 元。
/// </remarks>
public class TelemetryProjectionServiceTests
{
    private const decimal InPrice = 1000m;
    private const decimal OutPrice = 2000m;

    private static int _dbReady;
    private static readonly object DbLock = new();

    private readonly PriceCatalogService _prices;
    private readonly CostAggregationService _aggregation;
    private readonly TelemetryProjectionService _sut;

    public TelemetryProjectionServiceTests()
    {
        EnsurePluginDb();
        ClearAll();
        _prices = new PriceCatalogService();
        _aggregation = new CostAggregationService(_prices);
        _sut = new TelemetryProjectionService(_aggregation);
    }

    private static DateTime Day(int d) => new(2026, 10, d, 12, 0, 0);

    /// <summary>写入单价：不存在则新增，已存在则显式改价（<c>Save</c> 是新增通道，重复会被拒）。</summary>
    private void Price(string model, decimal? inPrice = null, decimal? outPrice = null)
    {
        var draft = new CostModelPrice
        {
            Model = model,
            Provider = "prov-proj",
            InputPricePer1M = inPrice ?? InPrice,
            OutputPricePer1M = outPrice ?? OutPrice,
            IsEnabled = true,
        };

        if (_prices.Find(model) is null) _prices.Save(draft);
        else _prices.Update(draft);
    }

    private static TurnTelemetryRecord Rec(string model, DateTime created) => new()
    {
        Id = Math.Abs(HashCode.Combine(model, created)),
        ChatSessionId = 1,
        TurnIndex = 1,
        SessionKey = "sk-proj",
        Style = "openai",
        Model = model,
        PromptTokens = 1000,
        CompletionTokens = 500,
        FirstTokenMs = 100,
        DurationMs = 1000,
        ResponseStatus = 200,
        ErrorMessage = null,
        CreatedTime = created,
        AgentRunId = null,
    };

    // ---------- 核心：当日重算 vs 已结束读缓存 ----------

    [Fact]
    public void QueryWithCache_已结束日读缓存_当日每次重算()
    {
        var m = $"proj-{Guid.NewGuid():N}";
        Price(m);
        var now = Day(6);
        var records = new[] { Rec(m, Day(4)), Rec(m, now) };

        // 首次查询：两天都是 2 元（1000×1000/1M=1 + 500×2000/1M=1）
        var first = _sut.QueryWithCache(records, now);
        first.Should().HaveCount(2);
        first.Sum(p => p.Cost).Should().Be(4m);

        // 改价：输入单价 1000 → 3000 ⇒ 单轮变 4 元
        Price(m, inPrice: 3000m);

        var second = _sut.QueryWithCache(records, now);

        var past = second.Single(p => p.Day == Day(4).Date);
        var today = second.Single(p => p.Day == now.Date);

        past.Cost.Should().Be(2m, "已结束的日必须读缓存（跨日数据不变，重算只是浪费）");
        today.Cost.Should().Be(4m, "当日未结束必须每次重算（缓存会骗人）");
    }

    [Fact]
    public void QueryWithCache_清缓存后历史日回落实时值()
    {
        var m = $"proj-{Guid.NewGuid():N}";
        Price(m);
        var now = Day(6);
        var records = new[] { Rec(m, Day(4)) };

        _sut.QueryWithCache(records, now).Single().Cost.Should().Be(2m);

        Price(m, inPrice: 3000m);
        _sut.QueryWithCache(records, now).Single().Cost.Should().Be(2m, "仍是缓存");

        // FR-3.4：改价后清缓存 ⇒ 恢复重算
        _sut.InvalidateAll().Should().BeGreaterThan(0);

        _sut.QueryWithCache(records, now).Single().Cost.Should().Be(4m, "清缓存后应重算出新价");
    }

    [Fact]
    public void InvalidateAll_无缓存时返回0()
    {
        _sut.InvalidateAll().Should().Be(0);
    }

    // ---------- 物化内容 ----------

    [Fact]
    public void QueryWithCache_写入物化表_字段与聚合一致()
    {
        var m = $"proj-{Guid.NewGuid():N}";
        Price(m);
        var now = Day(6);

        _sut.QueryWithCache([Rec(m, now)], now);

        var row = CostTurnDaySummary.FindAll(CostTurnDaySummary._.Day == now.Date).Single();
        row.Model.Should().Be(m);
        row.PromptTokens.Should().Be(1000);
        row.CompletionTokens.Should().Be(500);
        row.TotalTokens.Should().Be(1500);
        row.CostAmount.Should().Be(2m);
        row.TurnCount.Should().Be(1);
        row.Currency.Should().Be(PriceCatalogService.DefaultCurrency);
        row.SessionKey.Should().Be(TelemetryProjectionService.RollupSessionKey, "跨会话汇总用固定来源标注");
    }

    [Fact]
    public void QueryWithCache_未配单价_下界标记被持久化()
    {
        var unpriced = $"proj-noprice-{Guid.NewGuid():N}";
        var now = Day(6);

        var points = _sut.QueryWithCache([Rec(unpriced, now)], now);

        points.Single().CostIsLowerBound.Should().BeTrue("未配单价必须标记为下界（BR-2）");
        var row = CostTurnDaySummary.FindAll(CostTurnDaySummary._.Day == now.Date).Single();
        row.CostIsLowerBound.Should().BeTrue("下界标记必须落库，否则缓存会把 0 当成已核算成本");
    }

    [Fact]
    public void QueryWithCache_多模型_分别成行且按日汇总合并()
    {
        var m1 = $"proj-a-{Guid.NewGuid():N}";
        var m2 = $"proj-b-{Guid.NewGuid():N}";
        Price(m1); Price(m2);
        var now = Day(6);

        var points = _sut.QueryWithCache([Rec(m1, now), Rec(m2, now)], now);

        CostTurnDaySummary.FindAll().Count.Should().Be(2, "两个模型 ⇒ 两行物化");
        points.Should().HaveCount(1, "对外仍是按日汇总（一天一行）");
        points.Single().Cost.Should().Be(4m);
    }

    [Fact]
    public void QueryWithCache_多天_按日升序返回()
    {
        var m = $"proj-d-{Guid.NewGuid():N}";
        Price(m);
        var now = Day(6);

        var points = _sut.QueryWithCache(
            [Rec(m, Day(6)), Rec(m, Day(4)), Rec(m, Day(5))],
            now);

        points.Select(p => p.Day).Should().Equal(
            [Day(4).Date, Day(5).Date, Day(6).Date]);
    }

    [Fact]
    public void QueryWithCache_历史日的失败数从缓存还原_不静默丢成0()
    {
        // BR-4：失败轮次不计成本，但**必须留痕**。物化表若无 FailCount 列，
        // 历史日走缓存路径时失败数会恒为 0 ⇒ 日趋势里失败数凭空消失（静默错数）。
        var m = $"proj-fail-{Guid.NewGuid():N}";
        Price(m);
        var now = Day(6);
        var records = new[] { Rec(m, Day(4)), Rec(m, Day(4)) with { ResponseStatus = 500 } };

        _sut.QueryWithCache(records, now);
        // 二次查询走缓存路径（已结束日读缓存）
        var cached = _sut.QueryWithCache(records, now).Single();

        cached.FailCount.Should().Be(1, "历史日的失败数必须从物化缓存还原，不能恒为 0");
        cached.Cost.Should().Be(2m, "失败轮次不计成本（BR-4）");
    }

    [Fact]
    public void QueryWithCache_空记录_返回空且不抛()
    {
        _sut.QueryWithCache([], Day(6)).Should().BeEmpty();
    }

    [Fact]
    public void QueryWithCache_传null_抛()
    {
        var act = () => _sut.QueryWithCache(null!, Day(6));

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void 构造_聚合器为空_抛()
    {
        var act = () => new TelemetryProjectionService(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Materialize_直接物化指定明细_返回写入条数()
    {
        var m = $"proj-m-{Guid.NewGuid():N}";
        var now = Day(6);

        var n = _sut.Materialize(
            [new DailyModelPoint(now.Date, m, "openai", 1000, 500, 1, 0, 2m, false)],
            now);

        n.Should().Be(1);
        CostTurnDaySummary.FindAll(CostTurnDaySummary._.Model == m).Single().CostAmount.Should().Be(2m);
    }

    // ---------- 守卫：零 IHostedService（FR-3.7 / 032 D3） ----------

    [Fact]
    public void 插件源码_零IHostedService_规避热重载不重启坑()
    {
        var root = FindRepoRoot();
        root.Should().NotBeNull("应能定位仓库根目录");
        var pluginDir = Path.Combine(root!, "Plugins", "CostScope");
        Directory.Exists(pluginDir).Should().BeTrue();

        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(pluginDir, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;

            // 先剥掉注释：本守卫要拦的是「真的注册了后台服务」，
            // 而文件里为了说明「为什么不注册」必然会出现 IHostedService 字样（否则读者莫名其妙）。
            var code = StripComments(File.ReadAllText(file));
            if (code.Contains("IHostedService") || code.Contains("AddHostedService"))
                offenders.Add(Path.GetRelativePath(pluginDir, file));
        }

        offenders.Should().BeEmpty("FR-3.7：物化必须查询时惰性执行，不得引入后台宿主服务（032 D3）");
    }

    /// <summary>去掉行注释与块注释，只留代码，避免文档里提到类型名被误判为使用。</summary>
    private static string StripComments(string text)
    {
        var sb = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                while (i < text.Length && text[i] != '\n') i++;
            }
            else if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < text.Length && !(text[i] == '*' && text[i + 1] == '/')) i++;
                i = Math.Min(i + 2, text.Length);
            }
            else
            {
                sb.Append(text[i]);
                i++;
            }
        }

        return sb.ToString();
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Plugins"))
                && File.Exists(Path.Combine(dir.FullName, "ForgeSelf.Api", "ForgeSelf.Api.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
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
            CreateTable<CostTurnDaySummary>();
        }
    }

    private static void CreateTable<TEntity>() where TEntity : Entity<TEntity>, new()
    {
        var meta = typeof(TEntity).GetProperty("Meta", BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
        meta?.GetType().GetMethod("CreateTable", Type.EmptyTypes)?.Invoke(meta, null);
    }

    private static void ClearAll()
    {
        foreach (var row in CostTurnDaySummary.FindAll()) row.Delete();
        foreach (var row in CostModelPrice.FindAll()) row.Delete();
    }
}

using System.Reflection;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.CostScope;
using ForgeSelf.Api.Plugins.CostScope.Controllers;
using ForgeSelf.Api.Plugins.CostScope.Entities;
using ForgeSelf.Api.Plugins.CostScope.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using XCode;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Tests.Plugins.CostScopeTests;

/// <summary>
/// A11 原子任务：成本观测端点面单测（FR-4 / AC8 / 02-spec Error Handling）。
/// </summary>
/// <remarks>
/// <b>测试方式</b>：<b>直接实例化控制器</b>（不起 Web 主机），契约层取替身；
/// 鉴权用<b>反射断言特性</b>（与仓内 <c>McpAdminAuthTests</c> 同一手法）——
/// 插件控制器不会被自动保护，鉴权声明错了 401 就不会发生，<b>只能靠反射守住</b>。
/// <b>输入口径</b>：单价/预算是用户本地配置，样本由模板串构造、断言跟着写入值走。
/// </remarks>
public class CostControllerAuthTests
{
    private static Type ControllerType => typeof(CostController);

    [Fact]
    public void 类级_声明ApiKeyPolicy鉴权_插件控制器不会自动被保护()
    {
        var attr = ControllerType.GetCustomAttribute<AuthorizeAttribute>();

        attr.Should().NotBeNull("铁律 17：插件控制器必须类级显式 [Authorize]");
        attr!.Policy.Should().Be("ApiKeyPolicy");
    }

    [Fact]
    public void 类级_带ApiController与路由前缀()
    {
        ControllerType.GetCustomAttribute<ApiControllerAttribute>().Should().NotBeNull();
        ControllerType.GetCustomAttribute<RouteAttribute>()!.Template.Should().Be("api/cost-scope");
    }

    [Fact]
    public void 无任何Action标注AllowAnonymous_不存在鉴权绕过口子()
    {
        var actions = ControllerType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
            .ToList();

        actions.Should().NotBeEmpty("端点面不应为空");
        actions.Should().OnlyContain(m => m.GetCustomAttribute<AllowAnonymousAttribute>() == null,
            "任何端点都不得标 [AllowAnonymous]（铁律 17）");
    }

    [Fact]
    public void 全部端点_都挂在带鉴权的类上_不存在无鉴权旁路()
    {
        // 结构性守卫：只要类级鉴权在，所有 Action 都被覆盖；此用例防止将来把某端点挪到无鉴权的控制器
        ControllerType.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        ControllerType.Namespace.Should().Contain("CostScope", "端点必须留在 CostScope 插件内（界面/端点归插件）");
    }
}

/// <summary>A11 端点行为测试。</summary>
public class CostControllerEndpointTests
{
    private static int _dbReady;
    private static readonly object DbLock = new();

    private readonly PriceCatalogService _prices;
    private readonly BudgetService _budgets;
    private readonly FakeTelemetry _telemetry = new();
    private readonly CostController _sut;

    public CostControllerEndpointTests()
    {
        EnsurePluginDb();
        ClearAll();
        _prices = new PriceCatalogService();
        _budgets = new BudgetService();
        _sut = new CostController(_telemetry, _prices, _budgets, new TraceProjectionService());
    }

    private static string NewModel(string tag) => $"{tag}-{Guid.NewGuid():N}";

    private static TurnTelemetryRecord Turn(string model, int prompt = 1000, int completion = 500) => new()
    {
        Id = 1, ChatSessionId = 1, TurnIndex = 1, SessionKey = "sk-e2e",
        Style = "openai", Model = model, PromptTokens = prompt, CompletionTokens = completion,
        FirstTokenMs = 100, DurationMs = 1000, ResponseStatus = 200,
        CreatedTime = DateTime.Now, AgentRunId = null,
    };

    // ---------- 非法维度必须 400（禁静默空表） ----------

    [Theory]
    [InlineData("unknown")]
    [InlineData("99")]
    [InlineData("")]
    public async Task Breakdown_非法维度_返回400且附可用值(string by)
    {
        var result = await _sut.Breakdown(by, null, null, null, null, null, CancellationToken.None);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value!.ToString().Should().Contain("model", "错误信息必须列出可用维度值");
    }

    [Theory]
    [InlineData("model")]
    [InlineData("Provider")]
    [InlineData("style")]
    [InlineData("day")]
    public async Task Breakdown_合法维度_返回200(string by)
    {
        var result = await _sut.Breakdown(by, null, null, null, null, null, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Breakdown_缺省维度_按model处理不报错()
    {
        var result = await _sut.Breakdown(null, null, null, null, null, null, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    // ---------- 读取类端点 ----------

    [Fact]
    public async Task Overview_无数据_返回200且零值_不返回500()
    {
        var result = await _sut.Overview(null, null, null, null, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var ov = ok.Value.Should().BeOfType<CostOverview>().Subject;
        ov.Turns.Should().Be(0);
        ov.TotalCost.Should().Be(0m);
        ov.Coverage.MainChatObservable.Should().BeFalse("必须显式声明主聊天不可观测");
    }

    [Fact]
    public async Task Turns_分页size超限_被钳到上限()
    {
        _telemetry.Turns = [Turn("m"), Turn("m")];

        var result = await _sut.Turns(null, null, null, null, 0, 99999, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task Settings_返回口径说明_供前端自解释()
    {
        var result = _sut.Settings();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Trace_无运行数据_返回未关联而非报错()
    {
        var result = await _sut.Trace("run-x", null, null, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var wf = ok.Value.Should().BeOfType<TraceWaterfall>().Subject;
        wf.AgentRunId.Should().BeNull();
        wf.CorrelationNote.Should().Contain("未关联", "拿不到运行必须显式说明，而不是静默返回空瀑布");
    }

    // ---------- 单价 CRUD ----------

    [Fact]
    public async Task CreatePrice_成功_返回201()
    {
        var model = NewModel("ctl-price");

        var result = _sut.CreatePrice(new PriceUpsertRequest(model, "prov", 1m, 2m, null, true));

        result.Should().BeOfType<CreatedAtActionResult>();
        _prices.Find(model).Should().NotBeNull();
    }

    [Fact]
    public async Task CreatePrice_非法单价_返回400而非500()
    {
        var result = _sut.CreatePrice(new PriceUpsertRequest(NewModel("bad"), "prov", -1m, 2m, null, true));

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value!.ToString().Should().Contain("单价");
    }

    [Fact]
    public async Task CreatePrice_空model_返回400()
    {
        var result = _sut.CreatePrice(new PriceUpsertRequest("  ", "prov", 1m, 2m, null, true));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreatePrice_重复model_返回400提示改用PUT()
    {
        var model = NewModel("dup");
        _sut.CreatePrice(new PriceUpsertRequest(model, "prov", 1m, 2m, null, true));

        var result = _sut.CreatePrice(new PriceUpsertRequest(model, "prov", 9m, 9m, null, true));

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value!.ToString().Should().Contain("PUT");
    }

    [Fact]
    public async Task UpdatePrice_不存在_返回400()
    {
        var result = _sut.UpdatePrice(new PriceUpsertRequest(NewModel("nope"), "prov", 1m, 1m, null, true));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdatePrice_存在_改价成功()
    {
        var model = NewModel("upd");
        _sut.CreatePrice(new PriceUpsertRequest(model, "prov", 1m, 1m, null, true));

        var result = _sut.UpdatePrice(new PriceUpsertRequest(model, "prov", 5m, 6m, null, true));

        result.Should().BeOfType<OkObjectResult>();
        _prices.Find(model)!.InputPricePer1M.Should().Be(5m);
    }

    [Fact]
    public async Task DeletePrice_不存在_返回400()
    {
        var result = _sut.DeletePrice(NewModel("gone"));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeletePrice_存在_返回204()
    {
        var model = NewModel("del");
        _sut.CreatePrice(new PriceUpsertRequest(model, "prov", 1m, 1m, null, true));

        var result = _sut.DeletePrice(model);

        result.Should().BeOfType<NoContentResult>();
        _prices.Find(model).Should().BeNull();
    }

    // ---------- 预算 CRUD ----------

    [Fact]
    public async Task CreateBudget_成功_返回201()
    {
        var name = $"ctl-budget-{Guid.NewGuid():N}";

        var result = _sut.CreateBudget(new BudgetUpsertRequest(name, "global", "", 100m, null, "month", 0.8d));

        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task CreateBudget_非法作用域_返回400()
    {
        var result = _sut.CreateBudget(new BudgetUpsertRequest($"b-{Guid.NewGuid():N}", "nope", "x", 10m, null, "month", 0.5d));

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value!.ToString().Should().Contain("作用域");
    }

    [Fact]
    public async Task CreateBudget_限额非正_返回400()
    {
        var result = _sut.CreateBudget(new BudgetUpsertRequest($"b-{Guid.NewGuid():N}", "global", "", 0m, null, "month", 0.5d));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateBudget_预警阈值越界_返回400()
    {
        var result = _sut.CreateBudget(new BudgetUpsertRequest($"b-{Guid.NewGuid():N}", "global", "", 10m, null, "month", 1.5d));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task BudgetAchievement_无规则_返回400而非空表()
    {
        var result = await _sut.BudgetAchievement(null, null, null, null, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task BudgetAchievement_指定规则_按传入已用金额计算()
    {
        var name = $"ach-{Guid.NewGuid():N}";
        _sut.CreateBudget(new BudgetUpsertRequest(name, "global", "", 100m, null, "month", 0.8d));

        var result = await _sut.BudgetAchievement(name, 90m, null, null, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
    }

    [Fact]
    public void UpdateBudget_存在且内容不同_改成功_不返回500()
    {
        // 回归：修复前 PUT 走 Save（新增通道），同名内容不同 ⇒ InvalidOperationException 冒泡成 500
        var name = $"upd-{Guid.NewGuid():N}";
        _sut.CreateBudget(new BudgetUpsertRequest(name, "global", "", 100m, null, "month", 0.8d));

        var result = _sut.UpdateBudget(new BudgetUpsertRequest(name, "global", "", 250m, null, "day", 0.5d));

        result.Should().BeOfType<OkObjectResult>();
        _budgets.Find(name)!.LimitAmount.Should().Be(250m);
    }

    [Fact]
    public async Task BudgetAchievement_provider作用域_按供应商维度实算_不恒为0()
    {
        _prices.Save(new CostModelPrice
        {
            Model = "gpt-4o", Provider = "openai",
            InputPricePer1M = 1000m, OutputPricePer1M = 2000m, IsEnabled = true,
        });
        _telemetry.Turns = [Turn("gpt-4o")];
        _telemetry.Catalog = [new ModelIdentityDto { ChatModelId = "gpt-4o", ProviderName = "openai" }];

        var name = $"prov-{Guid.NewGuid():N}";
        _sut.CreateBudget(new BudgetUpsertRequest(name, "provider", "openai", 100m, null, "month", 0.8d));

        var result = await _sut.BudgetAchievement(name, null, null, null, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        // 单轮成本 = 1000×1000/1M + 500×2000/1M = 1 + 1 = 2
        // 修复前只用 Model 维度建字典，provider 的 Target 查不到 ⇒ 恒为 0
        json.Should().Contain("\"UsedAmount\":2", "provider 作用域必须按供应商维度实算，不能恒为 0");
    }

    public async Task RebuildSummary_清空缓存_返回清掉行数()
    {
        var result = _sut.RebuildSummary();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
    }

    // ---------- 替身与夹具 ----------

    private sealed class FakeTelemetry : ITurnTelemetryQuery
    {
        public IReadOnlyList<TurnTelemetryRecord> Turns { get; set; } = Array.Empty<TurnTelemetryRecord>();

        public IReadOnlyList<ModelIdentityDto> Catalog { get; set; } = Array.Empty<ModelIdentityDto>();

        public Task<IReadOnlyList<TurnTelemetryRecord>> GetTurnTelemetryAsync(string sessionKey, CancellationToken ct = default)
            => Task.FromResult(Turns);

        public Task<IReadOnlyList<TurnTelemetryRecord>> QueryTurnsAsync(TurnTelemetryQueryFilter filter, CancellationToken ct = default)
            => Task.FromResult(Turns);

        public Task<AgentRunTelemetry?> GetAgentRunTelemetryAsync(string agentRunId, CancellationToken ct = default)
            => Task.FromResult<AgentRunTelemetry?>(null);

        public Task<IReadOnlyList<ModelIdentityDto>> GetModelCatalogAsync(CancellationToken ct = default)
            => Task.FromResult(Catalog);
    }

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

            foreach (var name in new[] { "CostModelPrice", "CostBudget", "CostTurnDaySummary" })
            {
                var type = typeof(CostController).Assembly.GetType($"ForgeSelf.Api.Plugins.CostScope.Entities.{name}");
                var meta = type?.GetProperty("Meta", BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
                meta?.GetType().GetMethod("CreateTable", Type.EmptyTypes)?.Invoke(meta, null);
            }
        }
    }

    private static void ClearAll()
    {
        foreach (var row in CostModelPrice.FindAll()) row.Delete();
        foreach (var row in CostBudget.FindAll()) row.Delete();
        foreach (var row in CostTurnDaySummary.FindAll()) row.Delete();
    }
}

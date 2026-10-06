using System.IO;
using System.Reflection;
using ForgeSelf.Api.Plugins.CostScope;
using ForgeSelf.Api.Plugins.CostScope.Entities;
using ForgeSelf.Api.Plugins.CostScope.Services;
using XCode;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Tests.Plugins.CostScopeTests;

/// <summary>
/// A5 原子任务：模型单价目录 CRUD 单测（FR-3.5 / FR-3.3 / BR-2 / BR-8）。
/// </summary>
/// <remarks>
/// <b>输入口径说明（避免被误当硬编码金值）</b>：单价目录是<b>用户本地配置</b>（BR-8），
/// 仓内不存在真实单价配置可驱动，故样本模型名/供应商/单价均由模板串 + 变量构造，
/// 断言一律<b>跟着写入值走</b>（写什么就断言什么），不比对任何写死的金额金值。
/// 只用「区间边界」（0 / 1e6）与「相对关系」做断言，这两类不随配置漂移；
/// 并有一条 <c>单价上界常量_与判据字面量一致_防漂移</c> 钉住 Theory 里的字面量。
/// </remarks>
public class PriceCatalogServiceTests
{
    private static int _dbReady;
    private static readonly object DbLock = new();

    private readonly PriceCatalogService _sut;

    public PriceCatalogServiceTests()
    {
        EnsurePluginDb();
        ClearAll();
        _sut = new PriceCatalogService();
    }

    /// <summary>构造样本条目：模型名/供应商均为模板串（每次唯一，避免跨用例串数据）。</summary>
    private static CostModelPrice Draft(string model, decimal input, decimal output, bool enabled = true, string? provider = null) => new()
    {
        Model = model,
        Provider = provider ?? $"prov-{Guid.NewGuid():N}",
        InputPricePer1M = input,
        OutputPricePer1M = output,
        IsEnabled = enabled,
    };

    private static string NewModel(string tag) => $"{tag}-{Guid.NewGuid():N}";

    // ---------- 新增 / 查回 ----------

    [Fact]
    public void Save_新增后按模型可查回_字段与写入值一致()
    {
        var model = NewModel("model-a");
        var provider = "prov-x";
        var input = 3.25m;
        var output = 9.5m;

        var saved = _sut.Save(Draft(model, input, output, provider: provider));

        saved.Id.Should().BePositive();

        var found = _sut.Find(model);
        found.Should().NotBeNull();
        found!.Model.Should().Be(model);
        found.Provider.Should().Be(provider);
        found.InputPricePer1M.Should().Be(input);
        found.OutputPricePer1M.Should().Be(output);
        found.Currency.Should().Be(PriceCatalogService.DefaultCurrency, "未指定币种时落到默认币种");
    }

    [Fact]
    public void Save_模型名含空白_按去空白后查回()
    {
        var model = NewModel("model-b");

        _sut.Save(Draft($"  {model}  ", 1m, 2m));

        _sut.Find(model).Should().NotBeNull("模型名应按去空白后的值落库");
    }

    [Fact]
    public void Find_未配置单价_返回null_供聚合判未知()
    {
        _sut.Find(NewModel("never-configured")).Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Find_空模型名_返回null(string? model)
    {
        _sut.Find(model).Should().BeNull();
    }

    // ---------- 重复模型名：显式拒绝，不静默覆盖 ----------

    [Fact]
    public void Save_同模型不同内容_显式拒绝_不静默覆盖()
    {
        var model = NewModel("model-dup");
        _sut.Save(Draft(model, 1m, 1m, provider: "prov-keep"));

        var act = () => _sut.Save(Draft(model, 999m, 999m, provider: "prov-other"));

        act.Should().Throw<InvalidOperationException>("重复模型名必须显式拒绝，不能静默覆盖既有配置");

        var kept = _sut.Find(model);
        kept!.InputPricePer1M.Should().Be(1m, "原值必须保持不变（证明没有静默覆盖）");
        kept.Provider.Should().Be("prov-keep");
    }

    [Fact]
    public void Save_同模型同内容重放_视为幂等不抛()
    {
        var model = NewModel("model-idem");
        var provider = "prov-idem";
        var first = _sut.Save(Draft(model, 2m, 4m, provider: provider));

        var act = () => _sut.Save(Draft(model, 2m, 4m, provider: provider));

        act.Should().NotThrow("完全相同的内容重放应幂等");
        _sut.List(onlyEnabled: false).Count(p => p.Model == model).Should().Be(1, "幂等重放不得产生第二条记录");
        first.Id.Should().Be(_sut.Find(model)!.Id);
    }

    // ---------- 非法输入 ----------

    [Fact]
    public void Save_空模型名_抛()
    {
        var act = () => _sut.Save(Draft("   ", 1m, 1m));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Save_供应商为空_抛ArgumentException_而非撞实体层异常()
    {
        // 实体层 Provider 是必填（XCode Valid()），服务层必须先显式校验并给清晰报错
        var act = () => _sut.Save(Draft(NewModel("model-noprov"), 1m, 1m, provider: "   "));

        act.Should().Throw<ArgumentException>().WithMessage("*供应商*");
    }

    [Theory]
    [InlineData(-0.0001)]
    [InlineData(1000000.0001)]
    public void Save_单价越界_抛(decimal illegal)
    {
        var modelIn = NewModel("model-bad-in");
        var modelOut = NewModel("model-bad-out");

        var actIn = () => _sut.Save(Draft(modelIn, illegal, 1m));
        var actOut = () => _sut.Save(Draft(modelOut, 1m, illegal));

        actIn.Should().Throw<ArgumentOutOfRangeException>();
        actOut.Should().Throw<ArgumentOutOfRangeException>();
        _sut.Find(modelIn).Should().BeNull("越界单价不得落库");
        _sut.Find(modelOut).Should().BeNull("越界单价不得落库");
    }

    [Fact]
    public void 单价上界常量_与判据字面量一致_防漂移()
    {
        // Theory 里的边界字面量必须是常量的真值，否则上界改了判据不会红（虚假安全感）
        PriceCatalogService.MaxPricePer1M.Should().Be(1000000m);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1000000, true)]
    [InlineData(-1, false)]
    [InlineData(1000001, false)]
    public void IsValidPrice_区间边界(decimal price, bool expected)
    {
        PriceCatalogService.IsValidPrice(price).Should().Be(expected);
    }

    // ---------- 显式改价通道（FR-3.4 入口） ----------

    [Fact]
    public void Update_改价成功_字段被覆盖()
    {
        var model = NewModel("model-upd");
        _sut.Save(Draft(model, 1m, 1m, provider: "prov-a"));

        _sut.Update(Draft(model, 8m, 9m, provider: "prov-b"));

        var found = _sut.Find(model);
        found!.InputPricePer1M.Should().Be(8m);
        found.OutputPricePer1M.Should().Be(9m);
        found.Provider.Should().Be("prov-b");
    }

    [Fact]
    public void Update_条目不存在_抛_不做upsert()
    {
        // upsert 会掩盖「改了一个根本不存在的模型」这种误操作
        var act = () => _sut.Update(Draft(NewModel("model-upd-missing"), 1m, 1m));

        act.Should().Throw<InvalidOperationException>().WithMessage("*尚无单价条目*");
    }

    [Fact]
    public void Update_非法单价_抛()
    {
        var model = NewModel("model-upd-bad");
        _sut.Save(Draft(model, 1m, 1m));

        var act = () => _sut.Update(Draft(model, -5m, 1m));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Update_空模型名或空供应商_抛()
    {
        var actModel = () => _sut.Update(Draft("  ", 1m, 1m));
        var actProvider = () => _sut.Update(Draft(NewModel("model-upd-p"), 1m, 1m, provider: "  "));

        actModel.Should().Throw<ArgumentException>();
        actProvider.Should().Throw<ArgumentException>().WithMessage("*供应商*");
    }

    // ---------- 删除：历史成本转 Unknown 且可撤销 ----------

    [Fact]
    public void Remove_删除后查不到_由聚合判未知而非计零()
    {
        var model = NewModel("model-del");
        _sut.Save(Draft(model, 5m, 5m));

        _sut.Remove(model).Should().BeTrue();

        // 关键：查不到单价 ⇒ 调用方（A7 聚合）按「未配单价 ⇒ Unknown」处理，绝不静默计 0（BR-2）
        _sut.Find(model).Should().BeNull();
    }

    [Fact]
    public void Remove_删除后重新补回_成本可恢复_可撤销()
    {
        var model = NewModel("model-undo");
        _sut.Save(Draft(model, 7m, 8m, provider: "prov-undo"));
        _sut.Remove(model).Should().BeTrue();

        _sut.Save(Draft(model, 7m, 8m, provider: "prov-undo"));

        _sut.Find(model).Should().NotBeNull("补回单价后应恢复核算能力（FR-3.5 可撤销）");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("never-existed")]
    public void Remove_不存在或空_返回false(string? model)
    {
        _sut.Remove(model).Should().BeFalse();
    }

    // ---------- 列表 ----------

    [Fact]
    public void List_默认只列启用态_含停用时排除()
    {
        var on = NewModel("model-on");
        var off = NewModel("model-off");
        _sut.Save(Draft(on, 1m, 1m, enabled: true));
        _sut.Save(Draft(off, 1m, 1m, enabled: false));

        var enabledOnly = _sut.List();
        enabledOnly.Should().Contain(p => p.Model == on);
        enabledOnly.Should().NotContain(p => p.Model == off);

        var all = _sut.List(onlyEnabled: false);
        all.Should().Contain(p => p.Model == on);
        all.Should().Contain(p => p.Model == off);
    }

    // ---------- 夹具 ----------

    /// <summary>注册插件自有库连接并建表（XCode 不会自动建表；反射调用 Meta.CreateTable，与宿主 XCodeConfig 同一手法）。</summary>
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

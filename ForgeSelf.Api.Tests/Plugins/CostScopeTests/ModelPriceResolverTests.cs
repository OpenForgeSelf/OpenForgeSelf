using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.CostScope.Services;

namespace ForgeSelf.Api.Tests.Plugins.CostScopeTests;

/// <summary>
/// A4 原子任务：模型→供应商 4 级解析单测（FR-3.8 / BC-3 / BC-5）。
/// </summary>
/// <remarks>
/// <b>输入口径说明（避免被误当硬编码金值）</b>：本用例的「模型目录」是<b>合成输入</b>——
/// 解析器的契约数据源是宿主 <c>AIModel</c>/<c>AIProvider</c>，其宿主实现（A3b）尚未落地，
/// 仓内亦无可驱动的真实目录配置，故目录在此显式构造。
/// 断言一律<b>按 4 级规则校验「命中级别 + 是否归属 + 供应商」</b>，而非比对某个名字金值，
/// 使目录结构变化时仍能守住两条核心口径：① 回退顺序（级别 1&gt;2&gt;3）② 绝不按前缀硬切。
/// </remarks>
public class ModelPriceResolverTests
{
    /// <summary>合成目录：三个供应商各一条，覆盖 ChatModelId / UpstreamModelId / Alias 三字段。</summary>
    private static readonly ModelIdentityDto[] Catalog =
    [
        new() { ChatModelId = "gpt-4o", UpstreamModelId = "gpt-4o-2024-05-13", Alias = "omni", ProviderName = "openai" },
        new() { ChatModelId = "claude-3-5-sonnet", UpstreamModelId = "claude-3-5-sonnet-20240620", Alias = "sonnet35", ProviderName = "anthropic" },
        new() { ChatModelId = "qwen-max", UpstreamModelId = "qwen-max-latest", Alias = "通义千问max", ProviderName = "qwen" },
    ];

    // ---------- 级别 1：ChatModelId 精确 ----------

    [Fact]
    public void Match_ChatModelId精确命中_归级别1且供应商正确()
    {
        var r = ModelPriceResolver.Match("claude-3-5-sonnet", Catalog);

        r.Level.Should().Be(ModelMatchLevel.ChatModelId);
        r.IsAttributed.Should().BeTrue();
        r.ProviderName.Should().Be("anthropic");
        r.ModelName.Should().Be("claude-3-5-sonnet");
    }

    // ---------- 级别 2：UpstreamModelId / Alias 精确 ----------

    [Fact]
    public void Match_UpstreamModelId命中_归级别2()
    {
        var r = ModelPriceResolver.Match("gpt-4o-2024-05-13", Catalog);

        r.Level.Should().Be(ModelMatchLevel.UpstreamOrAlias);
        r.IsAttributed.Should().BeTrue();
        r.ProviderName.Should().Be("openai");
    }

    [Fact]
    public void Match_Alias命中_归级别2()
    {
        var r = ModelPriceResolver.Match("sonnet35", Catalog);

        r.Level.Should().Be(ModelMatchLevel.UpstreamOrAlias);
        r.IsAttributed.Should().BeTrue();
        r.ProviderName.Should().Be("anthropic");
    }

    // ---------- 级别 3：大小写不敏感 ----------

    [Fact]
    public void Match_大小写不同_归级别3()
    {
        var r = ModelPriceResolver.Match("GPT-4O", Catalog);

        r.Level.Should().Be(ModelMatchLevel.CaseInsensitive);
        r.IsAttributed.Should().BeTrue();
        r.ProviderName.Should().Be("openai");
    }

    // ---------- 守门：绝不按前缀硬切（BC-3） ----------

    [Fact]
    public void Match_前缀相似但未收录_归未归属_绝不按前缀硬切()
    {
        // 目录里有 "gpt-4o"；"gpt-4" 是它的前缀、"gpt-4o-mini" 是另一个未收录模型。
        // 两者都必须判未归属——按前缀归到 openai 会产出**错误的供应商归因**（BC-3 明禁）。
        var prefix = ModelPriceResolver.Match("gpt-4", Catalog);
        var sibling = ModelPriceResolver.Match("gpt-4o-mini", Catalog);

        prefix.Level.Should().Be(ModelMatchLevel.Unattributed);
        prefix.IsAttributed.Should().BeFalse();
        prefix.ProviderName.Should().BeNull();

        sibling.Level.Should().Be(ModelMatchLevel.Unattributed);
        sibling.IsAttributed.Should().BeFalse();
        sibling.ProviderName.Should().BeNull();
    }

    // ---------- 空输入 ----------

    [Fact]
    public void Match_空目录_归未归属()
    {
        var r = ModelPriceResolver.Match("gpt-4o", Array.Empty<ModelIdentityDto>());

        r.Level.Should().Be(ModelMatchLevel.Unattributed);
        r.IsAttributed.Should().BeFalse();
        r.ProviderName.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Match_空或空白模型名_归未归属(string? modelName)
    {
        var r = ModelPriceResolver.Match(modelName, Catalog);

        r.Level.Should().Be(ModelMatchLevel.Unattributed);
        r.IsAttributed.Should().BeFalse();
        r.ProviderName.Should().BeNull();
    }

    // ---------- 优先级：级别 1 > 2 > 3 ----------

    [Fact]
    public void Match_同名既是ChatModelId又是UpstreamModelId_取级别1()
    {
        // 同名 "x" 同时是 A 的 ChatModelId 与 B 的 UpstreamModelId ⇒ 级别 1 优先（032 §3.2 优先级）
        ModelIdentityDto[] catalog =
        [
            new() { ChatModelId = "x", ProviderName = "byChatModelId" },
            new() { ChatModelId = "other", UpstreamModelId = "x", ProviderName = "byUpstream" },
        ];

        var r = ModelPriceResolver.Match("x", catalog);

        r.Level.Should().Be(ModelMatchLevel.ChatModelId);
        r.ProviderName.Should().Be("byChatModelId");
    }

    [Fact]
    public void Match_级别2精确_优先于级别3大小写()
    {
        // "modela" 精确命中 B 的 UpstreamModelId，同时大小写不敏感也能命中 A 的 ChatModelId
        // ⇒ 必须取级别 2（精确），不能被级别 3「抢先」
        ModelIdentityDto[] catalog =
        [
            new() { ChatModelId = "MODELA", ProviderName = "caseInsensitiveOnly" },
            new() { ChatModelId = "other", UpstreamModelId = "modela", ProviderName = "exactUpstream" },
        ];

        var r = ModelPriceResolver.Match("modela", catalog);

        r.Level.Should().Be(ModelMatchLevel.UpstreamOrAlias);
        r.ProviderName.Should().Be("exactUpstream");
    }

    // ---------- BC-5：一个模型名出现在多个 provider ----------

    [Fact]
    public void Match_同名出现在多个provider_仍归已归属_按目录顺序取第一条()
    {
        ModelIdentityDto[] catalog =
        [
            new() { ChatModelId = "shared-model", ProviderName = "provider-1" },
            new() { ChatModelId = "shared-model", ProviderName = "provider-2" },
        ];

        var r = ModelPriceResolver.Match("shared-model", catalog);

        // 多 provider 不是「解析失败」：不得降级为未归属，按目录顺序确定性取第一条
        r.IsAttributed.Should().BeTrue();
        r.Level.Should().Be(ModelMatchLevel.ChatModelId);
        r.ProviderName.Should().Be("provider-1");
    }

    // ---------- 命中率统计 ----------

    [Fact]
    public async Task ResolveAsync_按级别分别累计_命中率正确()
    {
        var resolver = new ModelPriceResolver(new FakeCatalogQuery(Catalog));

        await resolver.ResolveAsync("gpt-4o");       // 级别 1
        await resolver.ResolveAsync("sonnet35");     // 级别 2
        await resolver.ResolveAsync("GPT-4O");       // 级别 3
        await resolver.ResolveAsync("gpt-4o-mini");  // 未归属

        var s = resolver.SnapshotStats();

        s.Total.Should().Be(4);
        s.Attributed.Should().Be(3);
        s.ChatModelIdHits.Should().Be(1);
        s.UpstreamOrAliasHits.Should().Be(1);
        s.CaseInsensitiveHits.Should().Be(1);
        s.UnattributedHits.Should().Be(1);
        s.HitRate.Should().BeApproximately(3.0 / 4.0, 1e-9);
    }

    [Fact]
    public async Task ResolveAsync_全部未归属_命中率为0且不伪造()
    {
        var resolver = new ModelPriceResolver(new FakeCatalogQuery(Catalog));

        await resolver.ResolveAsync("unknown-a");
        await resolver.ResolveAsync("unknown-b");

        var s = resolver.SnapshotStats();

        s.Total.Should().Be(2);
        s.Attributed.Should().Be(0);
        s.UnattributedHits.Should().Be(2);
        s.HitRate.Should().Be(0d);
    }

    [Fact]
    public void ResetStats_清零统计()
    {
        var resolver = new ModelPriceResolver(new FakeCatalogQuery(Catalog));

        resolver.ResolveAsync("gpt-4o").GetAwaiter().GetResult();
        resolver.ResetStats();

        resolver.SnapshotStats().Total.Should().Be(0);
    }

    // ---------- 构造守卫 ----------

    [Fact]
    public void 构造_契约为空_抛ArgumentNullException()
    {
        var act = () => new ModelPriceResolver(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>契约替身：只提供目录方法，其余三个方法按契约返回空/空值（本任务不消费）。</summary>
    private sealed class FakeCatalogQuery(IReadOnlyList<ModelIdentityDto> catalog) : ITurnTelemetryQuery
    {
        public Task<IReadOnlyList<ModelIdentityDto>> GetModelCatalogAsync(CancellationToken ct = default)
            => Task.FromResult(catalog);

        public Task<IReadOnlyList<TurnTelemetryRecord>> GetTurnTelemetryAsync(
            string sessionKey, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TurnTelemetryRecord>>(Array.Empty<TurnTelemetryRecord>());

        public Task<IReadOnlyList<TurnTelemetryRecord>> QueryTurnsAsync(
            TurnTelemetryQueryFilter filter, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TurnTelemetryRecord>>(Array.Empty<TurnTelemetryRecord>());

        public Task<AgentRunTelemetry?> GetAgentRunTelemetryAsync(
            string agentRunId, CancellationToken ct = default)
            => Task.FromResult<AgentRunTelemetry?>(null);
    }
}

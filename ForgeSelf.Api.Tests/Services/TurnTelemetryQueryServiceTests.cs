using System.IO;
using System.Linq;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Services.CostScope;

namespace ForgeSelf.Api.Tests.Services;

/// <summary>
/// A3b 原子任务：宿主 <see cref="ITurnTelemetryQuery"/> 只读实现单测。
/// </summary>
/// <remarks>
/// <b>输入口径说明</b>：本类只覆盖<b>不依赖数据库</b>的行为（空输入守卫、无条件守卫、未注册提供者返回 null、
/// 只读与接线守卫）；涉及真实 <c>ChatTurn</c>/<c>AIModel</c> 取数的路径需 XCode 库夹具，
/// 由 A5/A7 的聚合用例与插件层 e2e 覆盖，此处不伪造库行为。
/// </remarks>
public class TurnTelemetryQueryServiceTests
{
    // ---------- 契约与构造 ----------

    [Fact]
    public void 服务_实现契约且可构造()
    {
        ITurnTelemetryQuery sut = new TurnTelemetryQueryService();

        sut.Should().NotBeNull();
        sut.Should().BeAssignableTo<ITurnTelemetryQuery>();
    }

    // ---------- 空输入守卫（不查库） ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetTurnTelemetryAsync_空白会话键_返回空集合(string? sessionKey)
    {
        var sut = new TurnTelemetryQueryService();

        var rows = await sut.GetTurnTelemetryAsync(sessionKey!);

        rows.Should().BeEmpty();
    }

    [Fact]
    public async Task QueryTurnsAsync_过滤器为null_返回空集合()
    {
        var sut = new TurnTelemetryQueryService();

        var rows = await sut.QueryTurnsAsync(null!);

        rows.Should().BeEmpty();
    }

    [Fact]
    public async Task QueryTurnsAsync_四项条件全空_返回空集合_避免无界全表扫()
    {
        // NFR-4：无条件 = 全表扫，必须显式拒绝而不是静默扫全表
        var sut = new TurnTelemetryQueryService();

        var rows = await sut.QueryTurnsAsync(new TurnTelemetryQueryFilter());

        rows.Should().BeEmpty();
    }

    // ---------- AgentRun trace：未注册提供者 ⇒ null（不伪造） ----------

    [Fact]
    public async Task GetAgentRunTelemetryAsync_无提供者_返回null()
    {
        var sut = new TurnTelemetryQueryService();

        var run = await sut.GetAgentRunTelemetryAsync("run-1");

        // 契约原文即「无对应运行返回 null」——未注册提供者时同样返回 null，不伪造空对象
        run.Should().BeNull();
    }

    [Fact]
    public async Task GetAgentRunTelemetryAsync_有提供者_转调提供者()
    {
        var provider = new StubAgentRunProvider();
        var sut = new TurnTelemetryQueryService(provider);

        var run = await sut.GetAgentRunTelemetryAsync("run-9");

        run.Should().NotBeNull();
        run!.AgentRunId.Should().Be("run-9");
        provider.LastRequestedId.Should().Be("run-9");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAgentRunTelemetryAsync_空白运行Id_返回null(string? agentRunId)
    {
        var sut = new TurnTelemetryQueryService(new StubAgentRunProvider());

        var run = await sut.GetAgentRunTelemetryAsync(agentRunId!);

        run.Should().BeNull();
    }

    // ---------- 守卫：实现只读（BR-5 / F1） ----------

    [Fact]
    public void 实现源码_无写库调用_只读()
    {
        var source = ReadSource("ForgeSelf.Api", "Services", "CostScope", "TurnTelemetryQueryService.cs");
        source.Should().NotBeNullOrWhiteSpace("应能定位到宿主取数实现源码");

        // 只读实现：不得出现任何写路径（带左括号，避免误命中注释里的词）
        foreach (var forbidden in new[] { ".Insert(", ".Update(", ".Delete(", ".Save(", "ExecuteNonQuery(" })
        {
            source.Should().NotContain(forbidden, $"取数实现必须只读，不得出现 {forbidden}");
        }
    }

    // ---------- 守卫：DI 接线（宿主只注册这一条接缝） ----------

    [Fact]
    public void AppBuilder_注册了ITurnTelemetryQuery接缝()
    {
        var source = ReadSource("ForgeSelf.Api", "AppBuilder.cs");
        source.Should().NotBeNullOrWhiteSpace("应能定位到 AppBuilder 源码");

        source.Should().Contain("AddScoped<ITurnTelemetryQuery, TurnTelemetryQueryService>()");
    }

    private static string ReadSource(params string[] relativeParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            dir = dir.Parent;
        }
        return string.Empty;
    }

    private sealed class StubAgentRunProvider : IAgentRunTelemetryProvider
    {
        public string? LastRequestedId { get; private set; }

        public Task<AgentRunTelemetry?> GetAgentRunTelemetryAsync(string agentRunId, CancellationToken ct = default)
        {
            LastRequestedId = agentRunId;
            return Task.FromResult<AgentRunTelemetry?>(new AgentRunTelemetry { AgentRunId = agentRunId });
        }
    }
}

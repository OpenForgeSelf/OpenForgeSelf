using System.Reflection;
using ForgeSelf.Abstractions;
using Xunit;

namespace ForgeSelf.Abstractions.Tests;

/// <summary>
/// A1 原子任务契约测试：验证遥测只读契约（ITurnTelemetryQuery + DTO）的形状与架构边界。
/// 对应 04-task A1 卡片 AC3 / AC4。
/// </summary>
public class TurnTelemetryContractTests
{
    // SSOT：契约对外承诺的字段集（与 02-spec FR-3.9 列清单一致）。
    // 数据驱动：断言对象从此清单加载，而非逐字段硬编码，避免「假绿」。
    private static readonly string[] ExpectedTurnFields =
    [
        nameof(TurnTelemetryRecord.Id),
        nameof(TurnTelemetryRecord.ChatSessionId),
        nameof(TurnTelemetryRecord.TurnIndex),
        nameof(TurnTelemetryRecord.SessionKey),
        nameof(TurnTelemetryRecord.Style),
        nameof(TurnTelemetryRecord.Model),
        nameof(TurnTelemetryRecord.PromptTokens),
        nameof(TurnTelemetryRecord.CompletionTokens),
        nameof(TurnTelemetryRecord.DurationMs),
        // A7 补入（2026-10-06）：FR-4.3 延迟分位 / FR-4.4 错误率 / BR-4 失败判定所依赖的三个字段
        nameof(TurnTelemetryRecord.FirstTokenMs),
        nameof(TurnTelemetryRecord.ResponseStatus),
        nameof(TurnTelemetryRecord.ErrorMessage),
        nameof(TurnTelemetryRecord.CreatedTime),
        nameof(TurnTelemetryRecord.AgentRunId),
    ];

    [Fact]
    public void TurnTelemetryRecord_ExposesContractedFields()
    {
        var props = typeof(TurnTelemetryRecord)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToArray();

        Assert.Equal(ExpectedTurnFields.OrderBy(x => x), props.OrderBy(x => x));
    }

    [Fact]
    public void TurnTelemetryRecord_PreservesAllValues_Immutable()
    {
        var record = new TurnTelemetryRecord
        {
            Id = 7,
            ChatSessionId = 3,
            TurnIndex = 2,
            SessionKey = "sk-abc",
            Style = "openai",
            Model = "gpt-4o",
            PromptTokens = 120,
            CompletionTokens = 30,
            DurationMs = 1500,
            CreatedTime = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc),
            AgentRunId = "run-9",
        };

        // 不可变：with 产生新实例，原值不变
        var copy = record with { CompletionTokens = 31 };
        Assert.Equal(30, record.CompletionTokens);
        Assert.Equal(31, copy.CompletionTokens);

        // 全字段保真
        Assert.Equal(7, copy.Id);
        Assert.Equal("sk-abc", copy.SessionKey);
        Assert.Equal("gpt-4o", copy.Model);
        Assert.Equal(120, copy.PromptTokens);
        Assert.Equal(1500, copy.DurationMs);
    }

    [Fact]
    public void TurnTelemetryRecord_AgentRunId_CanBeNull_NotForged()
    {
        // 主聊天 legacy 接缝未回填 AgentRunId 时，必须是 null，不得伪造 0/空串
        var legacy = new TurnTelemetryRecord
        {
            Id = 1,
            SessionKey = "sk-legacy",
            Model = "claude-3",
        };

        Assert.Null(legacy.AgentRunId);
        Assert.True(string.IsNullOrEmpty(legacy.SessionKey) is false);
    }

    [Fact]
    public void AgentRunTelemetry_CarriesSteps()
    {
        var run = new AgentRunTelemetry
        {
            AgentRunId = "run-1",
            SessionKey = "sk-1",
            StepCount = 2,
            TotalTokens = 450,
            Steps =
            [
                new AgentStepTelemetry { StepIndex = 0, Kind = "think", DurationMs = 200, Tokens = 100 },
                new AgentStepTelemetry { StepIndex = 1, Kind = "tool", DurationMs = 800, Tokens = 350 },
            ],
        };

        Assert.Equal(2, run.Steps.Count);
        Assert.Equal("tool", run.Steps[1].Kind);
        Assert.Equal(450, run.TotalTokens);
    }

    [Fact]
    public void ITurnTelemetryQuery_IsImplementable_ContractUsable()
    {
        // 契约可被下游（A2 宿主/CostScope 插件）实现，且不强制任何行为
        ITurnTelemetryQuery fake = new FakeTelemetryQuery();
        Assert.NotNull(fake);
    }

    [Fact]
    public void Abstractions_DoesNotReference_ApiOrPlugins()
    {
        // AC4 架构边界守卫：Abstractions 不得反向依赖 ForgeSelf.Api 或任何插件程序集
        var refs = typeof(ITurnTelemetryQuery).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToArray();

        Assert.DoesNotContain("ForgeSelf.Api", refs);
        Assert.False(refs.Any(n => n != null && n.StartsWith("Plugins", StringComparison.OrdinalIgnoreCase)),
            $"Abstractions 不应引用插件程序集，实际引用：{string.Join(", ", refs.Where(n => n != null && n.StartsWith("Plugins", StringComparison.OrdinalIgnoreCase)))}");
    }

    private sealed class FakeTelemetryQuery : ITurnTelemetryQuery
    {
        public Task<IReadOnlyList<TurnTelemetryRecord>> GetTurnTelemetryAsync(
            string sessionKey, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TurnTelemetryRecord>>(Array.Empty<TurnTelemetryRecord>());

        public Task<IReadOnlyList<TurnTelemetryRecord>> QueryTurnsAsync(
            TurnTelemetryQueryFilter filter, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TurnTelemetryRecord>>(Array.Empty<TurnTelemetryRecord>());

        public Task<AgentRunTelemetry?> GetAgentRunTelemetryAsync(
            string agentRunId, CancellationToken ct = default)
            => Task.FromResult<AgentRunTelemetry?>(null);

        // A4 前置补入的目录方法：契约扩展必须同步到本 fake，否则契约测试工程编译断
        public Task<IReadOnlyList<ModelIdentityDto>> GetModelCatalogAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ModelIdentityDto>>(Array.Empty<ModelIdentityDto>());
    }
}

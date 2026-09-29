using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Api.Services;
using ForgeSelf.Core;
using XCode;
using XCode.DataAccessLayer;
using AIChatMessageEntity = ForgeSelf.Api.Plugins.AIAgent.Entities.AIChatMessage;

namespace ForgeSelf.Api.Tests.Plugins;

/// <summary>
/// B5（041）QA 独立重写版工具轨迹判据。
/// </summary>
/// <remarks>
/// 纪律依据：team-lead 裁决「QA 探针文件禁止被实现方重写」。原
/// <c>AIAgentProjectionAdversarialTests</c> 三条（OrphanResult / OrphanCall / OutOfOrder）已被实现方改名重写，
/// 不再具备独立性，故本文件由 QA 以新眼睛<b>重新观察公开行为</b>并独立立判据，不沿用改写版本。
/// 裁决口径：孤立 / 乱序 CallId 不得静默丢弃（显式告警或落补偿事件），未闭合 call 标 Pending/Failed。
/// </remarks>
[Collection("XCode")]
public class AIAgentProjectionQaIndependentTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 28, 18, 0, 0, TimeSpan.Zero);

    private readonly InMemorySessionStore _store = new();
    private readonly AIAgentProjectionService _projection;

    public AIAgentProjectionQaIndependentTests()
    {
        // 与生产同款接线：IContext 共享表 → 懒解析（构造注入会 500）
        var ctx = new Context();
        ctx.Register<ISessionStore>(_store);
        _projection = new AIAgentProjectionService(ctx);

        var dir = Path.Combine(Path.GetTempPath(), $"ForgeSelfQAI_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        DAL.AddConnStr("AIAgent", $"Data Source={Path.Combine(dir, "AIAgent.db")}", null, "SQLite");
        EntityFactory.InitConnection("AIAgent");
        AIChatMessageEntity.Meta.Cache.Clear("qa reset");
        AIChatMessageEntity.Meta.Cache.Expire = 0;
    }

    private string NewSession() => "qa2-" + Guid.NewGuid().ToString("N")[..10];

    private List<ChatToolCallTrace> Traces(string sessionId)
        => System.Text.Json.JsonSerializer.Deserialize<List<ChatToolCallTrace>>(
               _projection.BuildLastToolCallsJson(sessionId)
               ?? throw new InvalidOperationException("本轮助手行无轨迹"),
               new System.Text.Json.JsonSerializerOptions
               {
                   PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
               })!;

    private static IList<AIChatMessageEntity> Rows(string sessionId)
        => AIChatMessageEntity.FindAll(AIChatMessageEntity._.SessionId == sessionId)
            .OrderBy(m => m.Id).ToList();

    private void AppendAssistant(string sessionId, string content = "答")
        => _store.Append(sessionId,
            new AssistantMessageEvent(0, sessionId, T0, content, null, null, "stop"));

    // ---- 判据 1：孤立 tool/result（无配对 call）不得静默丢弃，须留下可观测痕迹 ----
    [Fact]
    public async Task Qa_OrphanResult_LeavesObservableTrace()
    {
        var s = NewSession();
        _store.Append(s, new UserMessageEvent(0, s, T0, "问", MessageSource.Api));
        _store.Append(s, new ToolResultEvent(0, s, T0, "orphan-call", "tool-x", "{\"v\":9}", ToolOutcome.Ok, 42));
        AppendAssistant(s);

        // 裁决要求「不得静默丢弃」→ 轨迹必须存在且保留结果/成败/耗时
        var traces = Traces(s);
        Assert.Single(traces);
        Assert.Equal("tool-x", traces[0].Name);
        Assert.Contains("9", traces[0].Result ?? string.Empty);
        Assert.True(traces[0].Success);       // Outcome=Ok
        Assert.Equal(42, traces[0].DurationMs);

        // 落盘同样保留（不只是内存内存 WebSocket）
        await _projection.SyncAsync(s);
        Assert.Contains("tool-x", Rows(s)[1].ToolCallsJson);
    }

    // ---- 判据 2：未闭合 tool/call（无 result）必须标 Failed，不能显示成功 ----
    [Fact]
    public async Task Qa_UnclosedCall_MarkedFailed_NotSuccess()
    {
        var s = NewSession();
        _store.Append(s, new UserMessageEvent(0, s, T0, "问", MessageSource.Api));
        _store.Append(s, new ToolCallEvent(0, s, T0, "c-dangling", "search", "{\"q\":1}"));
        AppendAssistant(s);

        var traces = Traces(s);
        Assert.Single(traces);
        Assert.Equal("search", traces[0].Name);
        Assert.False(traces[0].Success);                       // 未闭合 ≠ 成功
        Assert.Contains("未闭合", traces[0].Result ?? string.Empty); // 必须有可观测说明，不能是 null

        // 投影表同样显示 failed（工程师称 MarkUnclosedFailed 落库和序列化各调一次）
        // 注意：落盘 JSON 默认转义非 ASCII（"未闭合" → \u672A…），故必须反序列化后比对，不能搜原始子串。
        await _projection.SyncAsync(s);
        var persisted = Rows(s)[1].ToolCallsJson;
        Assert.Contains("search", persisted);
        var persistedTraces = System.Text.Json.JsonSerializer.Deserialize<List<ChatToolCallTrace>>(
            persisted, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            })!;
        Assert.False(persistedTraces[0].Success);
        Assert.Contains("未闭合", persistedTraces[0].Result ?? string.Empty);
    }

    // ---- 判据 3：result 先于 call 到达，仍须配成一条完整轨迹（不得一条调用两条轨迹） ----
    [Fact]
    public async Task Qa_OutOfOrder_PairedIntoSingleTrace()
    {
        var s = NewSession();
        _store.Append(s, new UserMessageEvent(0, s, T0, "问", MessageSource.Api));
        _store.Append(s, new ToolResultEvent(0, s, T0, "c-late", "calc", "{\"r\":7}", ToolOutcome.Ok, 13));
        _store.Append(s, new ToolCallEvent(0, s, T0, "c-late", "calc", "{\"expr\":\"1+1\"}"));
        AppendAssistant(s);

        var traces = Traces(s);
        Assert.Single(traces);                              // 关键：不得出现「一条调用两条轨迹」
        Assert.Equal("calc", traces[0].Name);
        Assert.Equal("{\"expr\":\"1+1\"}", traces[0].Args); // 迟到 result 需回填 call 的参数
        Assert.Equal("{\"r\":7}", traces[0].Result);
        Assert.True(traces[0].Success);
        Assert.Equal(13, traces[0].DurationMs);
    }

    // ---- 判据 4：一条 call + 一条 result（正常情形）严格一对一一一一 ----
    [Fact]
    public async Task Qa_NormalPairing_OneTraceOnly()
    {
        var s = NewSession();
        _store.Append(s, new UserMessageEvent(0, s, T0, "问", MessageSource.Api));
        _store.Append(s, new ToolCallEvent(0, s, T0, "c1", "weather", "{\"city\":\"hz\"}"));
        _store.Append(s, new ToolResultEvent(0, s, T0, "c1", "weather", "{\"t\":25}", ToolOutcome.Error, 8));
        AppendAssistant(s);

        var traces = Traces(s);
        Assert.Single(traces);
        Assert.Equal("weather", traces[0].Name);
        Assert.Equal("{\"t\":25}", traces[0].Result);
        Assert.False(traces[0].Success); // Outcome=Error
        Assert.Equal(8, traces[0].DurationMs);
    }

    // ---- 判据 5：多工具 + 修正后投影幂等 ----
    [Fact]
    public async Task Qa_SyncAsync_Idempotent_AfterTracesFix()
    {
        var s = NewSession();
        _store.Append(s, new UserMessageEvent(0, s, T0, "问", MessageSource.Api));
        _store.Append(s, new ToolCallEvent(0, s, T0, "c1", "search", "{}"));
        _store.Append(s, new ToolResultEvent(0, s, T0, "c1", "search", "{}", ToolOutcome.Ok, 5));
        _store.Append(s, new ToolCallEvent(0, s, T0, "c2", "calc", "{}"));
        AppendAssistant(s);

        await _projection.SyncAsync(s);
        var first = Rows(s).Select(m => $"{m.Role}|{m.Content}|{m.ToolCallsJson}").ToArray();
        await _projection.SyncAsync(s);
        await _projection.SyncAsync(s);
        var third = Rows(s).Select(m => $"{m.Role}|{m.Content}|{m.ToolCallsJson}").ToArray();

        Assert.Equal(first, third);
        Assert.Equal(2, third.Length);
        Assert.Equal(2, Traces(s).Count); // c1 成功 + c2 未闭合失败，共两条
    }

    // ---- 判据 6：负路径不放松 ----
    [Fact]
    public async Task Qa_EmptySessionId_StillThrows()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _projection.SyncAsync(""));
        Assert.Throws<ArgumentException>(() => _projection.BuildLastToolCallsJson(""));
    }
}

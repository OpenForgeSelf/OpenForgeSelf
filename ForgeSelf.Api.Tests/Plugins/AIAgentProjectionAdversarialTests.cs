using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Entities;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using AIChatMessageEntity = ForgeSelf.Api.Plugins.AIAgent.Entities.AIChatMessage;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Api.Services;
using ForgeSelf.Core;
using XCode;
using XCode.DataAccessLayer;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins;

/// <summary>
/// B4（040）QA 独立对抗测试：AIAgent 插件投影的工具轨迹重建边界。
/// 工程师自曝已知边界——CallId 乱序/孤立当前<b>静默丢弃</b>；本组用例把实际行为固化成断言，
/// 供 B5 决策「是否升级为显式告警/补偿事件」。全部经公开面 SyncAsync / BuildLastToolCallsJson 观察。
/// </summary>
[Collection("XCode")]
public class AIAgentProjectionAdversarialTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemorySessionStore _store = new();
    private readonly AIAgentProjectionService _projection;

    public AIAgentProjectionAdversarialTests()
    {
        // 插件投影走 IContext 懒解析（与生产接线同款：宿主 seed 进共享表后插件 ctx.Get 可见）
        var ctx = new Context();
        ctx.Register<ISessionStore>(_store);
        _projection = new AIAgentProjectionService(ctx);

        // SyncAsync 会写 AIChatMessage 表（AIAgent 连接），按套件惯例隔离
        var dir = Path.Combine(Path.GetTempPath(), $"ForgeSelfAIProj_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        DAL.AddConnStr("AIAgent", $"Data Source={Path.Combine(dir, "AIAgent.db")}", null, "SQLite");
        EntityFactory.InitConnection("AIAgent");
        AIChatMessageEntity.Meta.Cache.Clear("qa reset");
        AIChatMessageEntity.Meta.Cache.Expire = 0;
    }

    private string NewSession() => "qa-tool-" + Guid.NewGuid().ToString("N")[..10];

    /// <summary>解出最后一条助手行的轨迹 JSON（无助手行时为 null）。</summary>
    private string? LastAssistantTrace(string sessionId)
        => _projection.BuildLastToolCallsJson(sessionId);

    // ---- 1. 正常配对：call → result → assistant，轨迹含成败与耗时 ----
    [Fact]
    public async Task NormalPairing_TraceHasResultAndDuration()
    {
        var s = NewSession();
        _store.Append(s, new UserMessageEvent(0, s, T0, "查天气", MessageSource.Api));
        _store.Append(s, new ToolCallEvent(0, s, T0, "c1", "weather", "{\"city\":\"杭州\"}"));
        _store.Append(s, new ToolResultEvent(0, s, T0, "c1", "weather", "{\"t\":25}", ToolOutcome.Ok, 321));
        _store.Append(s, new AssistantMessageEvent(0, s, T0, "今天 25 度", null, null, "stop"));

        var traces = System.Text.Json.JsonSerializer.Deserialize<List<ChatToolCallTrace>>(
            LastAssistantTrace(s)!, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        var trace = Assert.Single(traces!);
        Assert.Equal("weather", trace.Name);
        Assert.Equal("{\"t\":25}", trace.Result);
        Assert.True(trace.Success);
        Assert.Equal(321, trace.DurationMs);

        // SyncAsync 后落库行与响应一致
        await _projection.SyncAsync(s);
        var rows = AIChatMessageEntity.FindAll(AIChatMessageEntity._.SessionId == s).OrderBy(m => m.Id).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal("assistant", rows[1].Role);
        Assert.Contains("weather", rows[1].ToolCallsJson);
    }

    // ---- 2. 孤立 result（无前置 call）→ 【B5 收口后】显式保留成轨迹并告警，不再静默丢弃 ----
    // 旧用例名 OrphanResult_SilentlyDropped（固化「静默丢弃」现状）已按主理人裁决的行为变更改写。
    [Fact]
    public async Task OrphanResult_ExplicitlyRetained()
    {
        var s = NewSession();
        _store.Append(s, new UserMessageEvent(0, s, T0, "问", MessageSource.Api));
        _store.Append(s, new ToolResultEvent(0, s, T0, "ghost-call", "tool-x", "{}", ToolOutcome.Ok, 5));
        _store.Append(s, new AssistantMessageEvent(0, s, T0, "答", null, null, "stop"));

        // 收口后行为：孤立 result 保留为一条可见轨迹（另有 Warn 日志），不再凭空消失
        var json = LastAssistantTrace(s);
        Assert.NotNull(json);
        var traces = System.Text.Json.JsonSerializer.Deserialize<List<ChatToolCallTrace>>(
            json!, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        var trace = Assert.Single(traces!);
        Assert.Equal("tool-x", trace.Name);
        Assert.Equal("{}", trace.Result);
        Assert.True(trace.Success);

        await _projection.SyncAsync(s);
        var rows = AIChatMessageEntity.FindAll(AIChatMessageEntity._.SessionId == s).OrderBy(m => m.Id).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Contains("tool-x", rows[1].ToolCallsJson);
    }

    // ---- 3. 孤立 call（无 result）→ 【B5 收口后】标失败（不再默认 Success=true 假装成功） ----
    // 旧用例名 OrphanCall_StayPending_DefaultSuccessValue 已按主理人裁决的行为变更改写。
    [Fact]
    public async Task OrphanCall_MarkedFailed()
    {
        var s = NewSession();
        _store.Append(s, new UserMessageEvent(0, s, T0, "问", MessageSource.Api));
        _store.Append(s, new ToolCallEvent(0, s, T0, "c-noresult", "search", "{}"));
        _store.Append(s, new AssistantMessageEvent(0, s, T0, "答", null, null, "stop"));

        // 收口后行为：未闭合 call 仍进轨迹，但 Result 写明未闭合、Success=false（另有 Warn 日志）
        var json = LastAssistantTrace(s);
        Assert.NotNull(json);
        var traces = System.Text.Json.JsonSerializer.Deserialize<List<ChatToolCallTrace>>(
            json!, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        var trace = Assert.Single(traces!);
        Assert.Equal("search", trace.Name);
        Assert.NotNull(trace.Result);
        Assert.Contains("未闭合", trace.Result!);
        Assert.False(trace.Success);
    }

    // ---- 4. 乱序：result 先于 call 到达 → 【B5 收口后】仍按 CallId 合成一条完整轨迹 ----
    // 旧用例名 OutOfOrder_ResultBeforeCall_ResultDropped 已按主理人裁决的行为变更改写。
    [Fact]
    public async Task OutOfOrder_ResultBeforeCall_StillPaired()
    {
        var s = NewSession();
        _store.Append(s, new UserMessageEvent(0, s, T0, "问", MessageSource.Api));
        _store.Append(s, new ToolResultEvent(0, s, T0, "c-late", "calc", "{\"r\":1}", ToolOutcome.Ok, 7));
        _store.Append(s, new ToolCallEvent(0, s, T0, "c-late", "calc", "{}"));
        _store.Append(s, new AssistantMessageEvent(0, s, T0, "答", null, null, "stop"));

        // 收口后行为：先到的 result 先建孤儿轨迹（并告警），后到的 call 回填 Args → 一条完整轨迹
        var json = LastAssistantTrace(s);
        Assert.NotNull(json);
        var traces = System.Text.Json.JsonSerializer.Deserialize<List<ChatToolCallTrace>>(
            json!, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        var trace = Assert.Single(traces!);
        Assert.Equal("calc", trace.Name);
        Assert.Equal("{}", trace.Args);
        Assert.Equal("{\"r\":1}", trace.Result);
        Assert.True(trace.Success);
        Assert.Equal(7, trace.DurationMs);
    }

    // ---- 5. 跨助手行归属：第一条助手行消费此前轨迹，第二条不带旧轨迹 ----
    [Fact]
    public async Task TraceAttribution_ConsumedByNextAssistantOnly()
    {
        var s = NewSession();
        _store.Append(s, new UserMessageEvent(0, s, T0, "问", MessageSource.Api));
        _store.Append(s, new AssistantMessageEvent(0, s, T0, "第一答", null, null, "stop"));
        _store.Append(s, new ToolCallEvent(0, s, T0, "c2", "search", "{}"));
        _store.Append(s, new ToolResultEvent(0, s, T0, "c2", "search", "{}", ToolOutcome.Error, 9));
        _store.Append(s, new AssistantMessageEvent(0, s, T0, "第二答", null, null, "stop"));

        // 最后一条助手行带轨迹（Error → success=false）
        Assert.Contains("\"success\":false", LastAssistantTrace(s));

        await _projection.SyncAsync(s);
        var rows = AIChatMessageEntity.FindAll(AIChatMessageEntity._.SessionId == s).OrderBy(m => m.Id).ToList();
        Assert.Equal(3, rows.Count);
        Assert.Equal(string.Empty, rows[1].ToolCallsJson); // 第一条助手行无轨迹
        Assert.Contains("search", rows[2].ToolCallsJson);  // 轨迹归属第二条
    }

    // ---- 6. 投影幂等（含 ToolCallsJson 维度）：重复 SyncAsync 行集合不变 ----
    [Fact]
    public async Task SyncAsync_Idempotent_WithToolTraces()
    {
        var s = NewSession();
        _store.Append(s, new UserMessageEvent(0, s, T0, "问", MessageSource.Api));
        _store.Append(s, new ToolCallEvent(0, s, T0, "c1", "search", "{}"));
        _store.Append(s, new ToolResultEvent(0, s, T0, "c1", "search", "{}", ToolOutcome.Ok, 10));
        _store.Append(s, new AssistantMessageEvent(0, s, T0, "答", null, null, "stop"));

        await _projection.SyncAsync(s);
        var first = AIChatMessageEntity.FindAll(AIChatMessageEntity._.SessionId == s).OrderBy(m => m.Id)
            .Select(m => $"{m.Role}|{m.Content}|{m.ToolCallsJson}").ToArray();

        await _projection.SyncAsync(s);
        await _projection.SyncAsync(s);
        var third = AIChatMessageEntity.FindAll(AIChatMessageEntity._.SessionId == s).OrderBy(m => m.Id)
            .Select(m => $"{m.Role}|{m.Content}|{m.ToolCallsJson}").ToArray();

        Assert.Equal(first, third);
        Assert.Equal(2, third.Length);
    }

    // ---- 7. 孤儿行自愈：表里有、日志没有的助手行被以日志为准重写 ----
    [Fact]
    public async Task SyncAsync_HealsOrphanAssistantRow()
    {
        var s = NewSession();
        new AIChatMessageEntity
        {
            SessionId = s, Role = "assistant", Content = "孤儿回复", ToolCallsJson = "",
            CreateTime = DateTime.Now, UpdateTime = DateTime.Now,
        }.Insert();

        _store.Append(s, new UserMessageEvent(0, s, T0, "真实问", MessageSource.Api));
        _store.Append(s, new AssistantMessageEvent(0, s, T0, "真实答", null, null, "stop"));

        await _projection.SyncAsync(s);

        var rows = AIChatMessageEntity.FindAll(AIChatMessageEntity._.SessionId == s).OrderBy(m => m.Id).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { "真实问", "真实答" }, rows.Select(m => m.Content).ToArray());
    }

    // ---- 8. 空 sessionId：明确抛出，不静默 ----
    [Fact]
    public async Task EmptySessionId_Throws()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _projection.SyncAsync(""));
        Assert.Throws<ArgumentException>(() => _projection.BuildLastToolCallsJson(""));
    }
}

using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Entities;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using AIChatMessageEntity = ForgeSelf.Api.Plugins.AIAgent.Entities.AIChatMessage;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Api.Services;
using ForgeSelf.Core;
using XCode;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins;

/// <summary>
/// B5 收口后「lateResults（乱序/孤立 CallId）」新行为的归属测试。
/// </summary>
/// <remarks>
/// <para>
/// 本组用例是 <see cref="AIAgentProjectionAdversarialTests"/> 中三条<b>改写后</b>的新期望断言的<b>正式归属</b>：
/// QA 原用例固化的是「孤立/乱序 CallId 静默丢弃、未闭合 call 默认 Success=true」的旧现状；
/// B5 按主理人裁决把该行为升级为「显式保留 / 落补偿事件 / 未闭合 call 标 Failed」，
/// 因此新期望不再属于「QA 对抗观察」文件，单独落到本文件，避免语义混淆。
/// </para>
/// <para>
/// 说明：<c>AIAgentProjectionAdversarialTests.cs</c> 保持当前状态交 QA 处置（他会备份原观察并重写），
/// 本文件只做「复制 + 归属」，不删除、不回退对抗文件里的任何断言。
/// </para>
/// <para>
/// 隔离方式：<b>刻意</b>复用官方 <see cref="XCodeTestFixture"/>（每类一份独立临时库、全部连接名统一注册），
/// 而不是照抄对抗测试里「只改 AIAgent 一个连接串」的手搓写法——后者会额外改写进程级全局连接串，
/// 实测会把全量失败数从 10 抬到 24（AIProviderRegistry / MultimodalProcessor 报
/// <c>unable to open database file</c>、AgentHubRegistry 报厂商标识被占用）。
/// </para>
/// <para>
/// 第 4 条为告警/补偿路径断言：NewLife <c>XTrace</c> 的日志事件成员在所用版本中没有文档化公开入口
/// （元数据与 XML 文档均未确认 <c>OnWriteLog</c> 事件），直接挂钩有可能编译不过；
/// 故改为断言其<b>同一条代码路径产生的可观测结果</b>——补偿标记必须在助手行序列化<b>之前</b>生效，
/// 因此落库的 <c>ToolCallsJson</c> 里也能看到 <c>success:false</c> 与「未闭合」。
/// </para>
/// </remarks>
[Collection("XCode")]
public class AIAgentProjectionLateResultsTests : IClassFixture<XCodeTestFixture>
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemorySessionStore _store = new();
    private readonly AIAgentProjectionService _projection;

    public AIAgentProjectionLateResultsTests(XCodeTestFixture fixture)
    {
        _ = fixture; // 复用套件官方夹具：每类一份独立临时库目录（全部连接名统一注册），不在本类手搓 DAL.AddConnStr

        // 插件投影走 IContext 懒解析（与生产接线同款：宿主 seed 进共享表后插件 ctx.Get 可见）
        var ctx = new Context();
        ctx.Register<ISessionStore>(_store);
        _projection = new AIAgentProjectionService(ctx);

        // 本类断言读 AIChatMessage 行，关闭实体缓存避免读到别的用例残留
        AIChatMessageEntity.Meta.Cache.Clear("late results reset");
        AIChatMessageEntity.Meta.Cache.Expire = 0;
    }

    private string NewSession() => "late-" + Guid.NewGuid().ToString("N")[..10];

    /// <summary>解出最后一条助手行的轨迹 JSON（无助手行时为 null）。</summary>
    private string? LastAssistantTrace(string sessionId)
        => _projection.BuildLastToolCallsJson(sessionId);

    private static List<ChatToolCallTrace> ParseTraces(string json)
        => System.Text.Json.JsonSerializer.Deserialize<List<ChatToolCallTrace>>(
            json,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            })!;

    // ---- 1. 孤立 result（无前置 call）→ 显式保留成轨迹并告警，不再静默丢弃 ----
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
        var trace = Assert.Single(ParseTraces(json!));
        Assert.Equal("tool-x", trace.Name);
        Assert.Equal("{}", trace.Result);
        Assert.True(trace.Success);

        await _projection.SyncAsync(s);
        var rows = AIChatMessageEntity.FindAll(AIChatMessageEntity._.SessionId == s).OrderBy(m => m.Id).ToList();
        Assert.Equal(2, rows.Count);
        Assert.Contains("tool-x", rows[1].ToolCallsJson);
    }

    // ---- 2. 孤立 call（无 result）→ 标失败（不再默认 Success=true 假装成功） ----
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
        var trace = Assert.Single(ParseTraces(json!));
        Assert.Equal("search", trace.Name);
        Assert.NotNull(trace.Result);
        Assert.Contains("未闭合", trace.Result!);
        Assert.False(trace.Success);
    }

    // ---- 3. 乱序：result 先于 call 到达 → 仍按 CallId 合成一条完整轨迹 ----
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
        var trace = Assert.Single(ParseTraces(json!));
        Assert.Equal("calc", trace.Name);
        Assert.Equal("{}", trace.Args);
        Assert.Equal("{\"r\":1}", trace.Result);
        Assert.True(trace.Success);
        Assert.Equal(7, trace.DurationMs);
    }

    // ---- 4. 告警/补偿路径：未闭合 call 的补偿标记在助手行序列化「之前」就已生效 ----
    [Fact]
    public async Task UnclosedCall_Compensated_BeforeRowSerialization()
    {
        var s = NewSession();
        _store.Append(s, new UserMessageEvent(0, s, T0, "问", MessageSource.Api));
        _store.Append(s, new ToolCallEvent(0, s, T0, "c-open", "search", "{\"q\":1}"));
        _store.Append(s, new AssistantMessageEvent(0, s, T0, "答", null, null, "stop"));

        await _projection.SyncAsync(s);

        var rows = AIChatMessageEntity.FindAll(AIChatMessageEntity._.SessionId == s).OrderBy(m => m.Id).ToList();
        Assert.Equal(2, rows.Count);

        // 落库行里就能看到失败标记（不只是内存轨迹）——证明补偿在行序列化前已写入
        Assert.Contains("\"success\":false", rows[1].ToolCallsJson);

        // 反序列化后再校验一次文案，避免 JSON 转义导致的字符串比对脆弱性
        var trace = Assert.Single(ParseTraces(rows[1].ToolCallsJson));
        Assert.Equal("search", trace.Name);
        Assert.False(trace.Success);
        Assert.Contains("未闭合", trace.Result!);
    }
}

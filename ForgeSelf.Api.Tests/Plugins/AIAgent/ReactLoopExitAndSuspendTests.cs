using System.Runtime.CompilerServices;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Api.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AIAgent;

/// <summary>
/// B7 切片 1（两套循环统一）：ReactLoopAgent 状态机新增的「出口工具 + 请求级挂起」能力归属测试。
/// </summary>
/// <remarks>
/// <para>
/// 029 计划驱动的步骤语义（complete_step/request_help 出口、45s 超时卡住）自此进入统一状态机，
/// <c>StepRunLoopService</c> 的 while(true)+超时迁入本类。三组判据：
/// </para>
/// <list type="number">
/// <item><description><b>出口工具即声明</b>：命中 <see cref="AgentOptions.ExitToolNames"/> 的调用不执行
/// <see cref="IToolRegistry"/>（声明不是执行），落 <c>tool/call + 合成 tool/result(Ok) + step/end(ExitTool)</c>，
/// 出口信息经 <see cref="ExitToolInvoked"/> 帧交给驱动方；本步成功提交 → 确认消费收件箱输入（R2）。</description></item>
/// <item><description><b>请求级无进展超时 → 挂起而非失败</b>：<see cref="AgentOptions.StepTimeout"/>
/// 作为相邻产出间的看门狗（逐块重置），上游长时间无产出 → <c>step/end(Suspended) + turn/end(Suspended)</c>，
/// 半截产出只落 attempt、不落 assistant/message；收件箱输入不确认（挂起保留现场）。</description></item>
/// <item><description><b>挂起后 steer 恢复</b>：挂起回合不删除输入；steer（唤醒）→ 新回合在回合边界
/// 认领全部待处理输入（B6 语义），继续跑完并 Completed——029 的 Stuck → 人工介入 → 恢复 语义映射。</description></item>
/// </list>
/// </remarks>
[Collection("XCode")]
public class ReactLoopExitAndSuspendTests
{
    // ---- 1. 出口工具：声明即出口，不执行、step/end(ExitTool)、确认消费 ----
    [Fact]
    public async Task ExitTool_DeclaredNotExecuted_StepEndsExitTool()
    {
        var fixture = new Fixture();
        fixture.Provider.Enqueue(Script.ToolCall("c1", "complete_step", """{"output":"本步产出"}"""));

        var agent = NewAgent(fixture, new AgentOptions
        {
            ExitToolNames = new[] { "complete_step", "request_help" }
        });
        agent.Inbox.Followup(fixture.SessionId, "执行步骤一");
        var frames = await fixture.RunAsync(agent);

        // 出口工具不真实执行（声明不是执行）
        Assert.Empty(fixture.Tools.Executions);

        // 日志事件序：出口工具三件套 + step/end(ExitTool) + turn/end(Completed)
        var types = fixture.EventTypes();
        Assert.Equal(
            new[]
            {
                "turn/start", "step/start", "user/message", "request/header",
                "assistant/message", "tool/call", "tool/result",
                "step/end", "turn/end"
            },
            types);
        var stepEnd = fixture.Store.Replay(fixture.SessionId).OfType<StepEndEvent>().Single();
        Assert.Equal(StepEndReason.ExitTool, stepEnd.Reason);
        var turnEnd = fixture.Store.Replay(fixture.SessionId).OfType<TurnEndEvent>().Single();
        Assert.Equal(TurnEndReason.Completed, turnEnd.Reason);

        // 合成 tool/result 成功闭合（N call 必有 N result）
        var toolResult = fixture.Store.Replay(fixture.SessionId).OfType<ToolResultEvent>().Single();
        Assert.Equal(ToolOutcome.Ok, toolResult.Outcome);

        // 出口信息经帧交给驱动方（029 判 complete_step → 完成 / request_help → 卡住）
        var exit = Assert.Single(frames.OfType<ExitToolInvoked>());
        Assert.Equal("complete_step", exit.ToolName);
        Assert.Contains("本步产出", exit.ArgsJson);
        Assert.Contains(frames, f => f is StepCompleted { Reason: StepEndReason.ExitTool });

        // 本步成功提交 → 确认消费收件箱输入（R2：内存替身按 MessageId 移除）
        Assert.Empty(fixture.Inbox.Peek(fixture.SessionId));
        Assert.Equal(AgentStatus.Idle, agent.Status);
    }

    // ---- 2. 请求级无进展超时 → 挂起（非 Aborted/Failed），保留现场 ----
    [Fact]
    public async Task RequestStall_TurnSuspended_KeepsScene()
    {
        var fixture = new Fixture();
        var provider = new HangThenTextProvider();

        var agent = NewAgent(fixture, new AgentOptions
        {
            StepTimeout = TimeSpan.FromMilliseconds(400)
        }, provider);
        agent.Inbox.Followup(fixture.SessionId, "会挂起的请求");
        // 外层兜底令牌：绿轮 400ms 看门狗先触发（Suspended）；
        // 红轮（旧实现无看门狗）由 2s 兜底取消 → 快速红，避免对无限挂起死等。
        using var bounded = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var frames = await fixture.RunAsync(agent, bounded.Token);

        var events = fixture.Store.Replay(fixture.SessionId).ToList();

        // turn/end = Suspended（挂起，不是 Aborted 也不是 Failed）
        var turnEnd = Assert.Single(events.OfType<TurnEndEvent>());
        Assert.Equal(TurnEndReason.Suspended, turnEnd.Reason);
        var stepEnd = Assert.Single(events.OfType<StepEndEvent>());
        Assert.Equal(StepEndReason.Suspended, stepEnd.Reason);
        Assert.Contains(frames, f => f is TurnCompleted { Reason: TurnEndReason.Suspended });

        // 半截产出不落 assistant/message；attempt 留痕（request-timeout）
        Assert.Empty(events.OfType<AssistantMessageEvent>());
        var attempt = Assert.Single(events.OfType<AssistantAttemptEvent>());
        Assert.Equal("request-timeout", attempt.ErrorKind);

        // 挂起保留现场：收件箱输入未确认（留在待处理集）
        Assert.Single(fixture.Inbox.Peek(fixture.SessionId));
    }

    // ---- 3. 挂起后 steer 恢复：新回合认领全部待处理输入，跑完 Completed ----
    [Fact]
    public async Task SuspendedTurn_ResumesViaSteer()
    {
        var fixture = new Fixture();
        var provider = new HangThenTextProvider();

        var agent = NewAgent(fixture, new AgentOptions
        {
            StepTimeout = TimeSpan.FromMilliseconds(400)
        }, provider);
        agent.Inbox.Followup(fixture.SessionId, "会挂起的请求");

        // 第一回合：挂起（2s 兜底取消防红轮死等，见上）
        using (var bounded = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
        {
            await fixture.RunAsync(agent, bounded.Token);
        }
        Assert.Equal(TurnEndReason.Suspended,
            fixture.Store.Replay(fixture.SessionId).OfType<TurnEndEvent>().Single().Reason);

        // steer（唤醒）→ 第二回合恢复
        fixture.Inbox.Steer(fixture.SessionId, "上游恢复了，继续");
        var frames2 = await fixture.RunAsync(agent);

        var events = fixture.Store.Replay(fixture.SessionId).ToList();
        Assert.Equal(2, events.Count(e => e.Type == "turn/start"));

        // user/message 共 2 条（如实投影，冗余非缺陷）：挂起回合在请求前已提交首条（已提交事实不回滚）；
        // 恢复回合把未确认的原始输入 + steer（回合边界认领取全部，B6 语义）合并为第二条
        var userMessages = events.OfType<UserMessageEvent>().ToList();
        Assert.Equal(2, userMessages.Count);
        Assert.Equal("会挂起的请求", userMessages[0].Content);
        Assert.Contains("会挂起的请求", userMessages[1].Content);
        Assert.Contains("上游恢复了，继续", userMessages[1].Content);

        // 恢复回合正常收束：助手产出落日志、turn/end=Completed、收件箱清空
        Assert.Contains("已恢复", events.OfType<AssistantMessageEvent>().Single().Content);
        Assert.Equal(TurnEndReason.Completed,
            events.OfType<TurnEndEvent>().Last().Reason);
        Assert.Empty(fixture.Inbox.Peek(fixture.SessionId));
        Assert.Contains(frames2, f => f is TurnCompleted { Reason: TurnEndReason.Completed });
    }

    /// <summary>用 Fixture 的脚手架装配自定义 AgentOptions / Provider 的 Agent（出口与挂起语义测试需要）。</summary>
    private static ReactLoopAgent NewAgent(Fixture fixture, AgentOptions options, IAIProvider? provider = null)
        => new(fixture.SessionId, options, new AgentTurnRuntime
        {
            Store = fixture.Store,
            Provider = provider ?? fixture.Provider,
            ModelId = "fake-model",
            ToolExecutor = fixture.Tools,
            SystemPrompt = null,
            ToolDefinitions = null,
            Events = fixture.Events,
            Inbox = fixture.Inbox
        });
}

/// <summary>
/// 挂起测试替身：首次请求先吐一个增量（制造「有进展」）后<b>永久挂起</b>（无 finishReason、无后续产出），
/// 触发请求级无进展看门狗；恢复后的请求返回正常文本收束。
/// </summary>
internal sealed class HangThenTextProvider : IAIProvider
{
    private bool _hung;

    /// <summary>每次请求收到的统一请求（调试观察用）。</summary>
    public List<UnifiedChatRequest> Requests { get; } = new();

    public string ProviderName => "fake-hang";
    public AIProviderType ProviderType => AIProviderType.Custom;
    public List<string> SupportedModels => new() { "fake-model" };
    public bool IsDefault => true;

    public Task<UnifiedChatResponse> ChatAsync(UnifiedChatRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("本测试替身只支持流式");

    public async IAsyncEnumerable<UnifiedStreamChunk> ChatStreamAsync(
        UnifiedChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Requests.Add(request);

        if (!_hung)
        {
            _hung = true;
            yield return new UnifiedStreamChunk { DeltaContent = "开始…" };
            // 永久挂起直到取消（看门狗触发 OCE；非用户取消 → Suspended）
            await Task.Delay(Timeout.Infinite, cancellationToken);
            yield break;
        }

        yield return new UnifiedStreamChunk { DeltaContent = "已恢复" };
        yield return new UnifiedStreamChunk { FinishReason = "stop" };
    }

    public Task<List<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new List<ModelInfo>());
}

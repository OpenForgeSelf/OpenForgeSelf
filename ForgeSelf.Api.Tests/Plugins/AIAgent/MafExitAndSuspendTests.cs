using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AIAgent;

/// <summary>
/// B7 统一循环「出口工具 + 请求级挂起」判据（B5 起由 MafTaskLoopAgent 承载）。
/// </summary>
/// <remarks>
/// <list type="number">
/// <item><description><b>出口工具即声明</b>：命中出口集合的调用不执行 <c>IToolRegistry</c>（声明不是执行），
/// 落 <c>tool/call + 合成 tool/result(Ok) + step/end(ExitTool)</c>，出口信息经 <see cref="ExitToolInvoked"/>
/// 帧交给驱动方；本步成功提交 → 确认消费收件箱输入（R2）。MAF 侧出口声明经 ExtraTools 通道
/// （ExitToolAIFunction 包装）成为模型可见 schema。</description></item>
/// <item><description><b>请求级无进展超时 → 挂起而非失败</b>：<see cref="AgentOptions.StepTimeout"/>
/// 作为相邻产出间的看门狗（逐块重置），上游长时间无产出 → <c>step/end(Suspended) + turn/end(Suspended)</c>，
/// 半截产出只落 attempt、不落 assistant/message；收件箱输入不确认（挂起保留现场）。</description></item>
/// <item><description><b>挂起后 steer 恢复</b>：挂起回合不删除输入；steer（唤醒）→ 新回合在回合边界
/// 认领全部待处理输入（B6 语义），继续跑完并 Completed——029 的 Stuck → 人工介入 → 恢复 语义映射。</description></item>
/// </list>
/// </remarks>
[Collection("XCode")]
public class MafExitAndSuspendTests
{
    private const string CompleteStepSchema =
        """{"type":"object","properties":{"output":{"type":"string"}},"required":["output"]}""";

    // ---- 1. 出口工具：声明即出口，不执行、step/end(ExitTool)、确认消费 ----
    [Fact]
    public async Task ExitTool_DeclaredNotExecuted_StepEndsExitTool()
    {
        var fixture = new Fixture();
        fixture.Provider.Enqueue(Script.ToolCall("c1", "complete_step", """{"output":"本步产出"}"""));

        var agent = fixture.CreateAgent(options: new AgentOptions
        {
            ExitToolNames = new[] { "complete_step", "request_help" },
            ExtraTools = new IToolFunctionExtension[] { new FakeExitTool("complete_step", CompleteStepSchema) }
        });
        agent.Inbox.Followup(fixture.SessionId, "执行步骤一");
        var frames = await fixture.RunAsync(agent);

        // 出口工具不真实执行（声明不是执行）
        Assert.Empty(fixture.Tools.Executions);

        // 日志事件序：出口工具三件套 + step/end(ExitTool) + run/receipt（B2②③：含工具调用的回合冻结收据）+ turn/end(Completed)
        var types = fixture.EventTypes();
        Assert.Equal(
            new[]
            {
                "turn/start", "step/start", "user/message", "request/header",
                "assistant/message", "tool/call", "tool/result",
                "step/end", "run/receipt", "turn/end"
            },
            types);
        var stepEnd = fixture.Store.Replay(fixture.SessionId).OfType<StepEndEvent>().Single();
        Assert.Equal(StepEndReason.ExitTool, stepEnd.Reason);
        var turnEnd = fixture.Store.Replay(fixture.SessionId).OfType<TurnEndEvent>().Single();
        Assert.Equal(TurnEndReason.Completed, turnEnd.Reason);

        // 合成 tool/result 成功闭合（N call 必有 N result）
        var toolResult = fixture.Store.Replay(fixture.SessionId).OfType<ToolResultEvent>().Single();
        Assert.Equal(ToolOutcome.Ok, toolResult.Outcome);

        // 出口信息经帧交给驱动方（029 判 complete_step → 完成 / request_help → 卡住）。
        // 注：MEAI FunctionCallContent 重序列化 args 时非 ASCII 转 \uXXXX（键名 output 不转义），故断言键名。
        var exit = Assert.Single(frames.OfType<ExitToolInvoked>());
        Assert.Equal("complete_step", exit.ToolName);
        Assert.Contains("output", exit.ArgsJson);
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

        var agent = fixture.CreateAgent(options: new AgentOptions
        {
            StepTimeout = TimeSpan.FromMilliseconds(400)
        }, provider: provider);
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

        var agent = fixture.CreateAgent(options: new AgentOptions
        {
            StepTimeout = TimeSpan.FromMilliseconds(400)
        }, provider: provider);
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
}

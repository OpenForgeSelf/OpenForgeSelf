using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Api.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AIAgent;

/// <summary>
/// B6（040 §2.5）门禁 1/2/3：ReactLoopAgent 接宿主收件箱后的三通道行为级判据。
/// </summary>
/// <remarks>
/// <para>
/// 判据与语义表逐条对应：followup（NextTurn/唤醒，下一回合边界）、steer（NextStep/唤醒，
/// 运行中回合的下一个 step 边界）、inject（NextStep/不唤醒，空闲留在 inbox 等唤醒）。
/// 断言打在<b>日志事件</b>与<b>收件箱状态</b>上（一切进模型的东西必须先落日志再派生）。
/// </para>
/// <para>
/// steer 的到达方式：经 <c>agent/turn-stopping</c> 串行监听器在「第 0 步收束检查点」投递，
/// 并返回 false（不收束）迫使再跑一步——steer 即在下一个 step 边界被认领（A2）。
/// </para>
/// <para>
/// 红绿证据：<see cref="ReactLoopAgent.RunAsync"/> 中的两处收件箱认领接线（回合边界 + step 边界）
/// 以 <c>[红轮模拟]</c> 标记做受控复现——注释掉认领调用跑红，还原跑绿，git diff 证明 0 残留。
/// </para>
/// </remarks>
[Collection("XCode")]
public class ReactLoopInboxGateTests
{
    // ---- 门禁 1：空闲 agent 收 followup → 开新 turn，输入落 user/message，收件箱被确认清空 ----
    [Fact]
    public async Task Followup_WakesIdleAgent()
    {
        var fixture = new Fixture();
        fixture.Provider.Enqueue(Script.Text("你好，有什么可以帮你？", finish: "stop"));

        var agent = fixture.CreateAgent();
        fixture.Inbox.Followup(fixture.SessionId, "门禁一：唤醒我");

        var frames = await fixture.RunAsync(agent);

        var types = fixture.EventTypes();
        Assert.Contains("turn/start", types);                       // 开新回合
        Assert.Contains(fixture.Store.Replay(fixture.SessionId).OfType<UserMessageEvent>(),
            m => m.Content.Contains("门禁一：唤醒我"));               // 模型可见 = 已记录
        var turnEnd = Assert.Single(fixture.Store.Replay(fixture.SessionId).OfType<TurnEndEvent>());
        Assert.Equal(TurnEndReason.Completed, turnEnd.Reason);      // 不是 NoInput
        Assert.Contains(frames, f => f is TurnCompleted);
        // R2：step 成功提交后确认 → 收件箱清空。内存测试替身经 IInboxConfirmation 在内存中移除；
        // PersistentInbox 路径的确认会落 agent/inbox/spliced 事件（见 PersistentInboxTests）。
        Assert.Empty(fixture.Inbox.Peek(fixture.SessionId));
    }

    // ---- 门禁 2：运行中发 steer → 下一个 step 边界的 pre-step 上下文含该条，且落 user/message ----
    [Fact]
    public async Task Steer_ConsumedAtNextStepBoundary()
    {
        var fixture = new Fixture();
        fixture.Provider.Enqueue(Script.Text("第一步答复", finish: "stop"), repeatLast: true);

        // 捕获每个 step 的 pre-step 上下文（断言「pre-step 上下文含该条」）
        var captured = new List<PreStepContext>();
        using var _pre = fixture.Events.OnWaterfall<PreStepContext, PreStepDecision>(
            "agent/pre-step",
            (ctx, next) =>
            {
                captured.Add(ctx);
                return next();
            });

        // 第 0 步收束检查点里投递 steer 并要求再跑一步（bail）；第二次检查点收束
        var stopCalls = 0;
        using var _stop = fixture.Events.OnSerial<TurnStoppingContext, bool?>(
            "agent/turn-stopping",
            _ =>
            {
                var call = stopCalls++;
                if (call == 0)
                {
                    fixture.Inbox.Steer(fixture.SessionId, "转向：改用粤语回答");
                    return Task.FromResult<bool?>(false); // 不收束 → 再跑一步
                }
                return Task.FromResult<bool?>(true);      // 收束
            });

        var agent = fixture.CreateAgent();
        fixture.Inbox.Followup(fixture.SessionId, "初始问题");
        await fixture.RunAsync(agent);

        var types = fixture.EventTypes();
        Assert.Equal(2, types.Count(t => t == "step/start"));       // steer 触发了下一个 step

        // A2：第 1 步的 pre-step 上下文携带 steer 内容
        var step1 = Assert.Single(captured, c => c.StepIndex == 1);
        Assert.NotNull(step1.ClaimedInputs);
        Assert.Contains(step1.ClaimedInputs!, c => c.Contains("转向：改用粤语回答"));

        // steer 以 user/message 落日志（模型可见 = 已记录），且在首步的 user/message 之后
        var userMessages = fixture.Store.Replay(fixture.SessionId).OfType<UserMessageEvent>().ToList();
        Assert.Equal(2, userMessages.Count);
        Assert.Contains(userMessages, m => m.Content.Contains("转向：改用粤语回答"));

        // 本步成功提交 → 确认消费 → 收件箱清空
        Assert.Empty(fixture.Inbox.Peek(fixture.SessionId));
    }

    // ---- 门禁 3：空闲时 inject 不唤醒；followup 到达时与 inject 一同被认领 ----
    [Fact]
    public async Task Inject_DoesNotWake()
    {
        var fixture = new Fixture();
        fixture.Provider.Enqueue(Script.Text("收到上下文后作答", finish: "stop"), repeatLast: true);

        var agent = fixture.CreateAgent();

        // 空闲时只投递 inject（不唤醒）
        fixture.Inbox.Inject(fixture.SessionId, "注入：背景资料块");

        // 被动触发一次：不得开回合——无 turn/start、日志零写入
        await fixture.RunAsync(agent);
        var typesAfterInject = fixture.EventTypes();
        Assert.Empty(typesAfterInject);                             // 无 turn/start，日志零写入
        Assert.Single(fixture.Inbox.Peek(fixture.SessionId));       // inject 留在收件箱等唤醒

        // followup 到达 → 唤醒，inject 与 followup 一同被认领
        fixture.Inbox.Followup(fixture.SessionId, "现在开始回答");
        await fixture.RunAsync(agent);

        var types = fixture.EventTypes();
        Assert.Equal(1, types.Count(t => t == "turn/start"));       // 只有 followup 那次开了回合

        // 回合边界认领的多条输入合并为一条 user/message（B5 起语义），inject 与 followup 同条可见
        var userMessages = fixture.Store.Replay(fixture.SessionId).OfType<UserMessageEvent>().ToList();
        Assert.Single(userMessages);
        Assert.Contains(userMessages, m => m.Content.Contains("背景资料块"));   // inject 一同被消费
        Assert.Contains(userMessages, m => m.Content.Contains("现在开始回答"));
        Assert.Empty(fixture.Inbox.Peek(fixture.SessionId));        // 两条全部确认
    }
}

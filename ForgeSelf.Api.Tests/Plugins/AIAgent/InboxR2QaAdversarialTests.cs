using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Api.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AIAgent;

/// <summary>
/// B6 收件箱 R2 确认时序对抗探针（QA 独立编写，实现方不得改写本文件——团队纪律）。
/// </summary>
/// <remarks>
/// <para>
/// 工程师的 <see cref="ReactLoopInboxGateTests"/> 只覆盖<b>成功路径</b>（step 成功提交 → 确认消费）；
/// 本文件用新眼睛补齐 R2 协议的<b>失败/取消/中止路径</b>——「失败不确认 → 输入留 inbox，下一回合重新认领（A5 不丢输入）」
/// 是工程师的声明但当时未见测试，本文件把它钉死为行为冻结：
/// </para>
/// <list type="bullet">
/// <item>请求失败（重试耗尽）→ 输入必须仍在收件箱，且下一回合重新认领（端到端不丢输入）；</item>
/// <item>回合中途取消 → 输入必须仍在收件箱（取消路径同样不得确认）；</item>
/// <item>agent/pre-step 中止 → 输入必须仍在收件箱（user/message 已落日志属设计接受的冗余，见 A5 权衡）；</item>
/// <item>DisposeAsync（keepInbox:true）不清收件箱 vs 显式 Cancel（keepInbox:false）清空——语义分叉实测；</item>
/// <item>回合边界与步边界不双写：turn 前已投递的 steer 随回合边界认领一次、合并进一条 user/message，step 1 不得重复认领；</item>
/// <item>收件箱不带 IInboxConfirmation 能力时，状态机回退为自行落 spliced(op=claimed)——回退路径共用 InboxOps 词汇源。</item>
/// </list>
/// <para>
/// 全部断言打在<b>日志事件</b>与<b>收件箱状态</b>上（一切进模型的东西必须先落日志再派生）。
/// 复用 <see cref="Fixture"/> 脚手架（内存日志 + 脚本化提供方），不触 XCode；仍挂 [Collection("XCode")]
/// 以对齐团队串行纪律、避免与本程序集其他 XCode 用例产生环境争用。
/// </para>
/// </remarks>
[Collection("XCode")]
public class InboxR2QaAdversarialTests
{
    // ───────────── 失败路径 A5：请求失败 → 不确认 → 输入留在 inbox → 下一回合重新认领 ─────────────

    [Fact]
    public async Task Qa_RequestFails_ExhaustedRetries_InputStaysAndNextTurnReclaims()
    {
        var fixture = new Fixture();

        // 第一个回合：请求连续失败（脚本抛异常，同步骤重试 MaxAttemptsPerStep=2 次均失败）
        fixture.Provider.Enqueue(Script.Fail(new InvalidOperationException("模拟上游请求失败")));
        var agent = fixture.CreateAgent();
        fixture.Inbox.Followup(fixture.SessionId, "不能丢的输入");

        await fixture.RunAsync(agent);

        // 失败路径：回合以 Aborted 收束，且 step 提交失败 → 不得确认消费
        var types = fixture.EventTypes();
        Assert.Contains("turn/start", types);
        var turnEnd = Assert.Single(fixture.Store.Replay(fixture.SessionId).OfType<TurnEndEvent>());
        Assert.Equal(TurnEndReason.Aborted, turnEnd.Reason);
        Assert.Contains(fixture.Store.Replay(fixture.SessionId).OfType<StepEndEvent>(),
            s => s.Reason == StepEndReason.Failed);

        // R2/A5 核心断言：输入仍在收件箱（claim 是提案，失败不确认 → 不丢）
        var remaining = fixture.Inbox.Peek(fixture.SessionId);
        Assert.Single(remaining);
        Assert.Equal("不能丢的输入", remaining[0].Content);

        // 端到端 A5：下一回合（followup 唤醒）重新认领原输入，与新增输入一并落 user/message
        fixture.Provider.Enqueue(Script.Text("第二回合成功答复", finish: "stop"));
        fixture.Inbox.Followup(fixture.SessionId, "第二条输入");
        await fixture.RunAsync(fixture.CreateAgent());

        var userMessages = fixture.Store.Replay(fixture.SessionId).OfType<UserMessageEvent>().ToList();
        // 共 2 条：失败回合首步已把输入落 user/message（提交发生在请求之前），第二回合重新认领再落一条——
        // 「宁重复不丢失」（A5）的设计权衡，重复输入在历史中如实可见。
        Assert.Equal(2, userMessages.Count);
        Assert.Equal("不能丢的输入", userMessages[0].Content);           // 失败回合已落的 user/message
        Assert.Contains("不能丢的输入", userMessages[1].Content);        // 第二回合重新认领（不丢）
        Assert.Contains("第二条输入", userMessages[1].Content);
        Assert.Empty(fixture.Inbox.Peek(fixture.SessionId));             // 成功提交 → 本轮确认
    }

    // ───────────── 取消路径 A5：回合中途取消 → 不确认 → 输入留在 inbox ─────────────

    [Fact]
    public async Task Qa_CancelMidTurn_InputStaysInInbox()
    {
        var fixture = new Fixture();
        fixture.Provider.Enqueue(Script.Text("被取消前的一段输出", finish: "stop"));

        // 在 agent/request 拦截点同步触发取消（令牌经 RunAsync(ct) 链入回合 CTS）
        using var cts = new CancellationTokenSource();
        using var _cancel = fixture.Events.OnWaterfall<AgentRequestContext, AgentRequestDecision>(
            "agent/request",
            (_, next) =>
            {
                cts.Cancel();
                return next();
            });

        var agent = fixture.CreateAgent();
        fixture.Inbox.Followup(fixture.SessionId, "取消时正在处理");
        await fixture.RunAsync(agent, cts.Token);

        // 取消路径：回合 Aborted，请求层没有任何 assistant/message 提交
        var turnEnd = Assert.Single(fixture.Store.Replay(fixture.SessionId).OfType<TurnEndEvent>());
        Assert.Equal(TurnEndReason.Aborted, turnEnd.Reason);

        // R2/A5：取消 → 不确认 → 输入仍在收件箱
        var remaining = fixture.Inbox.Peek(fixture.SessionId);
        Assert.Single(remaining);
        Assert.Equal("取消时正在处理", remaining[0].Content);
    }

    // ───────────── 中止路径 A5：agent/pre-step 中止 → 不确认 → 输入留在 inbox ─────────────

    [Fact]
    public async Task Qa_PreStepAborts_InputStaysInInbox()
    {
        var fixture = new Fixture();
        fixture.Provider.Enqueue(Script.Text("不应被执行", finish: "stop"));

        // pre-step 监听器直接中止回合
        using var _abort = fixture.Events.OnWaterfall<PreStepContext, PreStepDecision>(
            "agent/pre-step",
            (_, next) => Task.FromResult<PreStepDecision>(new PreStepDecision.Abort("QA 对抗：测试中止路径")));

        var agent = fixture.CreateAgent();
        fixture.Inbox.Followup(fixture.SessionId, "中止时正在处理");
        await fixture.RunAsync(agent);

        var turnEnd = Assert.Single(fixture.Store.Replay(fixture.SessionId).OfType<TurnEndEvent>());
        Assert.Equal(TurnEndReason.Aborted, turnEnd.Reason);
        Assert.Contains(fixture.Store.Replay(fixture.SessionId).OfType<StepEndEvent>(),
            s => s.Reason == StepEndReason.Aborted);

        // 中止 → 不确认 → 输入仍在收件箱
        //（注：首步的 user/message 已在 pre-step 之前落日志——这是设计接受的冗余：A5「宁重复不丢失」，
        //  与团队裁决「失败不确认」一致；重复输入由下一回合重新认领时随日志自然呈现。）
        var remaining = fixture.Inbox.Peek(fixture.SessionId);
        Assert.Single(remaining);
        Assert.Equal("中止时正在处理", remaining[0].Content);
    }

    // ───────────── 语义分叉：DisposeAsync(keepInbox:true) 保留 vs 显式 Cancel(keepInbox:false) 清空 ─────────────

    [Fact]
    public async Task Qa_DisposeKeepsInbox_ExplicitCancelClearsIt()
    {
        var fixture = new Fixture();
        var agent = fixture.CreateAgent();

        // 空闲时只投 inject（A3 闸门拒绝消费 → agent 保持 idle，输入留在 inbox）
        fixture.Inbox.Inject(fixture.SessionId, "排队中的用户输入");
        await fixture.RunAsync(agent);
        Assert.Single(fixture.Inbox.Peek(fixture.SessionId));             // 前置：闸门拒绝后输入仍在

        // 销毁 Agent：收件箱是持久投影、比运行时对象活得长 → 不得清空排队输入
        await ((IAsyncDisposable)agent).DisposeAsync();
        Assert.Single(fixture.Inbox.Peek(fixture.SessionId));            // keepInbox:true → 保留

        // 显式取消（keepInbox 缺省 false）：清空收件箱——两路径语义分叉
        var agent2 = fixture.CreateAgent();
        await agent2.CancelAsync(new AgentCancelCause.User(), keepInbox: false);
        Assert.Empty(fixture.Inbox.Peek(fixture.SessionId));             // keepInbox:false → 清空
    }

    // ───────────── 回合/步边界不双写：turn 前投递的 steer 只消费一次 ─────────────

    [Fact]
    public async Task Qa_SteerBeforeTurn_ClaimedAtTurnBoundaryOnce_NoDoubleWrite()
    {
        var fixture = new Fixture();
        fixture.Provider.Enqueue(Script.Text("首步答复", finish: "stop"), repeatLast: true);

        // 第 0 步收束检查点要求再跑一步 → 迫使进入 step 1（若步边界重复认领会双写 user/message）
        var stopCalls = 0;
        using var _stop = fixture.Events.OnSerial<TurnStoppingContext, bool?>(
            "agent/turn-stopping",
            _ => Task.FromResult<bool?>(stopCalls++ == 0 ? false : true));

        // steer 在回合开始【之前】投递——回合边界认领应连 steer 一并收走
        fixture.Inbox.Followup(fixture.SessionId, "主问题");
        fixture.Inbox.Steer(fixture.SessionId, "提前到的转向");

        await fixture.RunAsync(fixture.CreateAgent());

        // 双写防线：user/message 只能有一条（turn 边界认领的 followup+steer 合并落一条），
        // step 1 的步边界认领必须是空——提前到的 steer 已随回合边界消费，不得再来一遍
        var userMessages = fixture.Store.Replay(fixture.SessionId).OfType<UserMessageEvent>().ToList();
        Assert.Single(userMessages);
        Assert.Contains("主问题", userMessages[0].Content);
        Assert.Contains("提前到的转向", userMessages[0].Content);

        // 收件箱清空（两条都被确认一次、且仅一次）
        Assert.Empty(fixture.Inbox.Peek(fixture.SessionId));
    }

    // ───────────── 能力接口回退路径：无 IInboxConfirmation 的收件箱 → 状态机自行落 claimed ─────────────

    [Fact]
    public async Task Qa_InboxWithoutConfirmationCapability_FallbackAppendsClaimedEvent()
    {
        var fixture = new Fixture();
        fixture.Provider.Enqueue(Script.Text("回退路径答复", finish: "stop"));

        // 不带确认能力的极简收件箱（模拟第三方实现）——Claim 返回提案、永不自行移除
        var plainInbox = new NoConfirmInbox();
        var agent = new ReactLoopAgent(fixture.SessionId, new AgentOptions { MaxStepsPerTurn = 10 },
            new AgentTurnRuntime
            {
                Store = fixture.Store,
                Provider = fixture.Provider,
                ModelId = "fake-model",
                ToolExecutor = fixture.Tools,
                Events = fixture.Events,
                Inbox = plainInbox
            });

        plainInbox.Send(fixture.SessionId, "回退路径输入", InboxTarget.NextTurn, MessageSource.Api, true);
        await fixture.RunAsync(agent);

        // 回退路径：状态机自行落 agent/inbox/spliced(op=claimed)，且事件带本步消费的输入
        var claimed = fixture.Store.Replay(fixture.SessionId)
            .OfType<InboxSplicedEvent>()
            .Where(e => e.Op == InboxOps.Claimed)
            .ToList();
        Assert.Single(claimed);
        var claimedItem = Assert.Single(claimed[0].Items);
        Assert.Equal("回退路径输入", claimedItem.Content);

        // 极简收件箱本身没有确认实现——确认语义完全由日志事件承载（与 InboxOps 词汇源一致）
        Assert.Single(plainInbox.Items);                                 // 替身自身不变（它不认识 claimed）
    }

    /// <summary>不带确认能力的收件箱替身：只实现 IInbox 四方法（验证 ReactLoopAgent 的能力探测回退）。</summary>
    private sealed class NoConfirmInbox : IInbox
    {
        public readonly List<InboxItem> Items = new();

        public void Send(string sessionId, string content, InboxTarget target, MessageSource source, bool wakeup)
            => Items.Add(new InboxItem(Guid.NewGuid().ToString("N")[..8], content, source));

        public InboxBatch Claim(string sessionId, bool atTurnBoundary)
            => new() { Items = Items.ToArray(), Wakeup = Items.Count > 0 };

        public IReadOnlyList<InboxItem> Peek(string sessionId) => Items.ToArray();

        public void Clear(string sessionId) => Items.Clear();
    }
}

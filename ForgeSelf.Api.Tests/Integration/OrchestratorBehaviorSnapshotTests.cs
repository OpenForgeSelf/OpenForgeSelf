using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Entities;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Api.Services;
using ForgeSelf.Api.Tests.Plugins.AIAgent;
using Moq;
using Xunit;
// 消歧：Abstractions 与 AIAgent.Models 各有一个 AgentStatus；Abstractions 与测试脚手架各有一个 Script
using AgentStatus = ForgeSelf.Abstractions.AgentStatus;
using Script = ForgeSelf.Api.Tests.Plugins.AIAgent.Script;

namespace ForgeSelf.Api.Tests.Integration;

/// <summary>
/// B7（两套循环统一）行为快照：在动 <c>RunOrchestratorService</c> 之前锁死 029 现有行为，
/// 统一改造完成后本组快照必须原样保持绿色（QA 复验 + B6 裁决点名）。
/// </summary>
/// <remarks>
/// <para>
/// 快照覆盖清单（与 B7 派工书一一对应）：
/// <list type="number">
/// <item><description><b>SSE 事件面</b>：plan_created / step_started / （content 透传）/ step_completed /
/// run_stuck / error / done 的<b>类型序列</b>与<b>camelCase payload 结构</b>（029 联调踩坑：
/// 非 camelCase 前端拿到 undefined，见 <c>RunOrchestratorService.SsePayloadOptions</c> 注释）。</description></item>
/// <item><description><b>A3 闸门零事件语义</b>：空闲 Agent 无唤醒输入 → 拒开回合且<b>日志零写入</b>；
/// 仅 inject（不唤醒）→ 同样零写入且输入<b>留在收件箱</b>（R2：claim 是提案，闸门拒绝时不删除）。</description></item>
/// <item><description><b>失败路径 user/message 冗余是日志如实可见</b>：持续请求失败时，
/// user/message <b>单条保留</b>在日志与模型可见投影里（不隐藏、不去重、不回滚）——
/// 冗余是"如实投影"的设计后果，不是缺陷；B7 统一循环后该语义必须原样保留。</description></item>
/// </list>
/// </para>
/// <para>
/// 隔离方式：复用官方 <see cref="XCodeTestFixture"/>（每类一份独立临时库、全部连接名统一注册），
/// 遵守团队规范——新增 XCode 测试类禁止手搓 <c>DAL.AddConnStr</c>（实测会扰动全量，
/// 详见 <c>AIAgentProjectionLateResultsTests</c> remarks）。ReactLoopAgent 驱动类用例不触库，
/// 但与编排器用例同类共存以便快照集中维护。
/// </para>
/// <para>
/// 029 既有 4 个测试文件（RunOrchestratorTests / AgentRunPersistenceTests /
/// AgentRunsControllerTests / AgentRunStatusTests）改造前基线：<b>79/79 全绿</b>（本文件提交前实测）。
/// </para>
/// </remarks>
[Collection("XCode")]
public class OrchestratorBehaviorSnapshotTests : IClassFixture<XCodeTestFixture>
{
    public OrchestratorBehaviorSnapshotTests(XCodeTestFixture fixture)
    {
        _ = fixture; // 官方夹具：临时库目录与全部连接名由夹具统一注册，本类不手搓连接串

        // 本类断言读 AgentRun / AgentStepRun 行，关闭实体缓存避免读到别的用例残留
        AgentRun.Meta.Cache.Clear("snapshot reset");
        AgentStepRun.Meta.Cache.Clear("snapshot reset");
        AgentRun.Meta.Cache.Expire = 0;
        AgentStepRun.Meta.Cache.Expire = 0;
    }

    // ---- 脚手架（B7 切片 3：编排器经 CreateAgent 接缝驱动统一状态机；旧 ad-hoc mock 辅助已随契约删除） ----

    private static Mock<IPlanGeneratorService> MockPlanner(AgentPlan plan)
    {
        var mock = new Mock<IPlanGeneratorService>();
        mock.Setup(m => m.GeneratePlanAsync(
                It.IsAny<AgentRun>(), It.IsAny<long>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        return mock;
    }

    /// <summary>
    /// 脚本化假 Agent：按队列逐次产出预制帧序列（模拟统一状态机对外帧面）。
    /// B7 切片 2 起编排器薄壳化——快照经 <c>CreateAgent</c> 接缝注入假 Agent 锁 SSE 面。
    /// </summary>
    private sealed class FakeLoopAgent : IAgent
    {
        private readonly Queue<IReadOnlyList<TurnFrame>> _scripts;

        public FakeLoopAgent(string sessionId, AgentOptions options, Queue<IReadOnlyList<TurnFrame>> scripts)
        {
            SessionId = sessionId;
            Options = options;
            _scripts = scripts;
        }

        public string SessionId { get; }
        public AgentOptions Options { get; }
        public IInbox Inbox { get; } = new InMemoryInbox();
        public AgentStatus Status => AgentStatus.Idle;

        public IAsyncEnumerable<TurnFrame> RunAsync(CancellationToken ct = default)
        {
            var frames = _scripts.Count > 0 ? _scripts.Dequeue() : Array.Empty<TurnFrame>();
            return EnumerateAsync(frames);
        }

        private static async IAsyncEnumerable<TurnFrame> EnumerateAsync(IReadOnlyList<TurnFrame> frames)
        {
            foreach (var f in frames)
            {
                await Task.Yield();
                yield return f;
            }
        }

        public Task CancelAsync(AgentCancelCause cause, bool keepInbox = false) => Task.CompletedTask;
        public Task WhenIdleAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    /// <summary>一步的统一状态机帧面：content → 出口工具三件套 → step/end(ExitTool) → turn/end(Completed)。</summary>
    private static TurnFrame[] StepFrames(string delta, string callId, string exitTool, string exitArgs)
        => new TurnFrame[]
        {
            new AssistantDelta(delta),
            new ToolStarted(callId, exitTool, exitArgs),
            new ToolCompleted(callId, ToolOutcome.Ok, 1),
            new ExitToolInvoked(exitTool, exitArgs),
            new StepCompleted($"{callId}-step", StepEndReason.ExitTool),
            new TurnCompleted($"{callId}-turn", TurnEndReason.Completed),
        };

    private static Mock<IAIAgentService> MockAgentLoop(params IReadOnlyList<TurnFrame>[] stepScripts)
    {
        var scripts = new Queue<IReadOnlyList<TurnFrame>>(stepScripts);
        var mock = new Mock<IAIAgentService>();
        mock.Setup(m => m.CreateAgent(It.IsAny<string>(), It.IsAny<AgentOptions>()))
            .Returns((string sid, AgentOptions o) => new FakeLoopAgent(sid, o, scripts));
        return mock;
    }

    private static AgentPlan TwoStepPlan()
    {
        return new AgentPlan
        {
            Goal = "统计项目文件行数",
            Steps =
            {
                new AgentPlanStep { Id = "s1", Name = "统计行数", Objective = "统计行数", ExpectedOutput = "行数结果" },
                new AgentPlanStep { Id = "s2", Name = "汇总报告", Objective = "汇总成报告", ExpectedOutput = "报告" }
            }
        };
    }

    private static List<string> EventTypes(List<AgentLoopEvent> events)
        => events.Select(e => e.Type).ToList();

    private static JsonElement Payload(string json)
        => JsonDocument.Parse(json).RootElement.Clone();

    // ==================== 1. SSE 事件面快照（029 编排器端到端，mock 驱动） ====================

    [Fact]
    public async Task RunAsync_TwoStepPlan_SseEventFaceLocked()
    {
        // 每步：content 透传 + complete_step 出口 → 两步全成 → done 终局（B7 薄壳化：经 CreateAgent 接缝）
        var orchestrator = new RunOrchestratorService(
            MockPlanner(TwoStepPlan()).Object,
            MockAgentLoop(
                StepFrames("正在处理…", "c1", "complete_step", """{"output":"产出一"}"""),
                StepFrames("正在处理…", "c2", "complete_step", """{"output":"产出一"}""")).Object,
            inbox: new InMemoryInbox());

        var events = new List<AgentLoopEvent>();
        await foreach (var ev in orchestrator.RunAsync(new RunRequest
        {
            AgentId = "agent.generalist",
            TaskInput = "统计项目文件行数",
            ChatModelId = "fake:fake-model",
        }, CancellationToken.None))
        {
            events.Add(ev);
        }

        // 类型序列快照（029 现状：无中间多余事件；出口事件由编排器转译，不透传 complete_step 原名）
        Assert.Equal(
            new[]
            {
                "plan_created",
                "step_started", "content", "step_completed",
                "step_started", "content", "step_completed",
                "done"
            },
            EventTypes(events));

        // plan_created payload：camelCase（前端解析依赖，029 联调踩坑锚点）
        var planPayload = Payload(events[0].Content!);
        Assert.Equal("统计项目文件行数", planPayload.GetProperty("plan").GetProperty("goal").GetString());

        // step_started payload：camelCase + 字段完整
        var started0 = Payload(events[1].Content!);
        Assert.Equal("s1", started0.GetProperty("stepId").GetString());
        Assert.Equal("统计行数", started0.GetProperty("name").GetString());
        Assert.Equal("统计行数", started0.GetProperty("objective").GetString());
        Assert.Equal(0, started0.GetProperty("stepIndex").GetInt32());

        // step_completed payload：camelCase + 产出
        var completed0 = Payload(events[3].Content!);
        Assert.Equal(0, completed0.GetProperty("stepIndex").GetInt32());
        Assert.Equal("产出一", completed0.GetProperty("output").GetString());
        var completed1 = Payload(events[6].Content!);
        Assert.Equal(1, completed1.GetProperty("stepIndex").GetInt32());
        Assert.Equal("产出一", completed1.GetProperty("output").GetString()); // mock 两步同产出

        // done：终局合成为确定性拼接（标题 = 目标，逐节 = 步骤产出）
        var done = events.Single(e => e.Type == "done");
        Assert.Contains("# 统计项目文件行数", done.Content);
        Assert.Contains("## 步骤 s1", done.Content);
        Assert.Contains("产出一", done.Content);

        // 落库：Run Completed + 两步 Completed（双表运行视图）
        var runPayload = Payload(events[1].Content!);
        var runId = runPayload.GetProperty("runId").GetInt64();
        var run = AgentRun.FindById(runId);
        Assert.NotNull(run);
        Assert.Equal(AgentRunStatus.Completed, (AgentRunStatus)run!.Status);
        var steps = AgentStepRun.FindAllByRunId(runId).OrderBy(s => s.StepIndex).ToList();
        Assert.Equal(2, steps.Count);
        Assert.All(steps, s => Assert.Equal(AgentStepStatus.Completed, (AgentStepStatus)s.Status));
        Assert.Equal("产出一", steps[0].OutputJson);
    }

    [Fact]
    public async Task RunAsync_RequestHelp_SseStuckFaceLocked()
    {
        // 第一步 request_help → run_stuck + yield break 保留现场：无后续 step、无 done
        var orchestrator = new RunOrchestratorService(
            MockPlanner(TwoStepPlan()).Object,
            MockAgentLoop(StepFrames("", "c1", "request_help", """{"reason":"缺少目标文件权限"}""")).Object,
            inbox: new InMemoryInbox());

        var events = new List<AgentLoopEvent>();
        await foreach (var ev in orchestrator.RunAsync(new RunRequest
        {
            AgentId = "agent.generalist",
            TaskInput = "统计项目文件行数",
        }, CancellationToken.None))
        {
            events.Add(ev);
        }

        Assert.Equal(new[] { "plan_created", "step_started", "run_stuck" }, EventTypes(events));

        // run_stuck payload：camelCase + 原因原样透传
        var stuck = Payload(events[2].Content!);
        Assert.Equal(0, stuck.GetProperty("stepIndex").GetInt32());
        Assert.Equal("缺少目标文件权限", stuck.GetProperty("reason").GetString());

        // 落库：Run Stuck + StuckReason；步骤 Stuck；CurrentStepIndex 停在 0（现场保留）
        var runId = Payload(events[1].Content!).GetProperty("runId").GetInt64();
        var run = AgentRun.FindById(runId);
        Assert.NotNull(run);
        Assert.Equal(AgentRunStatus.Stuck, (AgentRunStatus)run!.Status);
        Assert.Equal("缺少目标文件权限", run.StuckReason);
        Assert.Equal(0, run.CurrentStepIndex);
        var step = AgentStepRun.FindAllByRunIdAndStepIndex(runId, 0).Single();
        Assert.Equal(AgentStepStatus.Stuck, (AgentStepStatus)step.Status);
        Assert.Equal("缺少目标文件权限", step.StuckReason);
    }

    [Fact]
    public async Task RunAsync_PlanningFailure_ErrorEventOnly()
    {
        var planner = new Mock<IPlanGeneratorService>();
        planner.Setup(m => m.GeneratePlanAsync(
                It.IsAny<AgentRun>(), It.IsAny<long>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("模型不给力"));
        var orchestrator = new RunOrchestratorService(planner.Object, new Mock<IAIAgentService>().Object);

        var events = new List<AgentLoopEvent>();
        await foreach (var ev in orchestrator.RunAsync(new RunRequest
        {
            AgentId = "agent.generalist",
            TaskInput = "统计项目文件行数",
        }, CancellationToken.None))
        {
            events.Add(ev);
        }

        // 规划失败：单条 error（含原因），无 plan_created / step / done
        var error = Assert.Single(events);
        Assert.Equal("error", error.Type);
        Assert.Contains("规划失败", error.Content);
        Assert.Contains("模型不给力", error.Content);
    }

    // ==================== 2. A3 闸门零事件语义（ReactLoopAgent 真实驱动；B7 统一后必须原样保留） ====================

    [Fact]
    public async Task Gate_EmptyInbox_NoTurn_ZeroLogWrites()
    {
        var fixture = new Fixture(); // 空收件箱、空会话日志
        var agent = fixture.CreateAgent();

        var frames = await fixture.RunAsync(agent);

        // 零事件：不开回合 = 日志一个字节都不写（turn/start 都不许有）
        Assert.Empty(frames);
        Assert.Empty(fixture.Store.Replay(fixture.SessionId));
        Assert.Equal(AgentStatus.Idle, agent.Status);
    }

    [Fact]
    public async Task Gate_InjectOnly_NoWake_ZeroLogWritesAndInputRetained()
    {
        var fixture = new Fixture();
        fixture.Inbox.Inject(fixture.SessionId, new { kind = "context", data = "外部注入块" });
        var agent = fixture.CreateAgent();

        var frames = await fixture.RunAsync(agent);

        // 仅 inject（wakeup=false）→ 拒开回合、日志零写入
        Assert.Empty(frames);
        Assert.Empty(fixture.Store.Replay(fixture.SessionId));

        // R2：Claim 是提案、闸门拒绝消费时不删除 → 输入必须原封不动留在收件箱等 followup 一并认领
        var pending = fixture.Inbox.Peek(fixture.SessionId);
        Assert.Single(pending);
        Assert.Contains("外部注入块", pending[0].Content);
    }

    // ==================== 3. 失败路径 user/message 冗余是日志如实可见（B6 裁决点） ====================

    [Fact]
    public async Task FailurePath_UserMessageRemainsVisible_HonestProjection()
    {
        var fixture = new Fixture();
        fixture.Provider.Enqueue(Script.Fail(new InvalidOperationException("上游 500")), repeatLast: true);

        var agent = fixture.CreateAgent();
        agent.Inbox.Followup(fixture.SessionId, "会一直失败的请求");
        var frames = await fixture.RunAsync(agent);

        var events = fixture.Store.Replay(fixture.SessionId).ToList();

        // user/message 恰好 1 条、原文完整：冗余但如实——失败不回滚、不隐藏、不去重
        var user = Assert.Single(events.OfType<UserMessageEvent>());
        Assert.Equal("会一直失败的请求", user.Content);

        // 失败留痕：assistant/attempt 记录错误（不入模型历史）；绝不落 assistant/message 半截产出
        Assert.NotEmpty(events.OfType<AssistantAttemptEvent>());
        Assert.DoesNotContain(events, e => e is AssistantMessageEvent);

        // 收束语义：step/end=Failed + turn/end=Aborted（TurnEndReason 无 Failed，以 Aborted 收束）
        var stepEnd = Assert.Single(events.OfType<StepEndEvent>());
        Assert.Equal(StepEndReason.Failed, stepEnd.Reason);
        var turnEnd = Assert.Single(events.OfType<TurnEndEvent>());
        Assert.Equal(TurnEndReason.Aborted, turnEnd.Reason);
        Assert.Contains(frames, f => f is TurnFailed);

        // 模型可见投影（Derive）里 user 消息仍然可见——"冗余是如实投影"的可观测面
        Assert.Contains("user", fixture.DerivedRoles());
    }
}

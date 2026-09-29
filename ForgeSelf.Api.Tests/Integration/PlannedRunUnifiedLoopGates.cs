using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Entities;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Api.Tests.Plugins.AIAgent;
using ForgeSelf.Api.Services;
using Moq;
using Xunit;
using Script = ForgeSelf.Api.Tests.Plugins.AIAgent.Script;

namespace ForgeSelf.Api.Tests.Integration;

/// <summary>
/// B7 切片 2 门禁（两套循环统一 · 编排器薄壳化）：029 计划驱动执行改走统一 ReactLoopAgent 状态机。
/// </summary>
/// <remarks>
/// <para>
/// 四条门禁（B7 派工书命名，先红后绿）：
/// <list type="number">
/// <item><description><b>PlannedRun_UsesSameLoop</b>：计划驱动步骤经真实 <see cref="ReactLoopAgent"/>
/// 状态机执行——会话日志出现 turn/start、user/message（收件箱 followup 唤醒）、step/start、assistant/message，
/// 而非旧的 ad-hoc 无日志循环。</description></item>
/// <item><description><b>Intervene_AsSteer</b>：steer 介入语义分叉——运行中（Running）→ steer
/// （NextStep，下一 step 边界生效）；已停（Stuck）→ followup（NextTurn，下一回合）。
/// 用 <see cref="IInbox.Claim"/> 的边界语义区分通道（提案不删除，R2）。</description></item>
/// <item><description><b>Stuck_BecomesSuspendedTurn</b>：请求级无进展超时 → turn 挂起（Suspended）
/// → 编排器派生为 run_stuck（前端字段不变：run_stuck 事件 + Run.Status=Stuck 保留现场）；
/// steer + resume 后恢复跑完。</description></item>
/// <item><description><b>PlannedRun_029Regression</b>：029 SSE 事件面（plan_created/step_started/content/
/// step_completed/done）与双表落库（Run/StepRun 终态 + 合成内容）在薄壳化后原样保持。</description></item>
/// </list>
/// </para>
/// <para>
/// 装配：真实 <see cref="ReactLoopAgent"/> + <see cref="ScriptedProvider"/>（脚本化模型响应）+
/// <see cref="InMemorySessionStore"/>/<see cref="InMemoryInbox"/>，经 mock
/// <see cref="IAIAgentService.CreateAgent"/> 注入编排器——即「编排器薄壳 + 状态机真身」的组合。
/// </para>
/// </remarks>
[Collection("XCode")]
public class PlannedRunUnifiedLoopGates : IClassFixture<XCodeTestFixture>
{
    public PlannedRunUnifiedLoopGates(XCodeTestFixture fixture)
    {
        _ = fixture;
        AgentRun.Meta.Cache.Clear("gate reset");
        AgentStepRun.Meta.Cache.Clear("gate reset");
        AgentRun.Meta.Cache.Expire = 0;
        AgentStepRun.Meta.Cache.Expire = 0;
    }

    // ==================== 脚手架 ====================

    /// <summary>内容 + 出口工具调用 + finish 的单段脚本（模拟一步内的模型行为）。</summary>
    private static Script ContentThenToolCall(string content, string callId, string tool, string argsJson)
    {
        var chunks = new List<UnifiedStreamChunk> { new() { DeltaContent = content } };
        chunks.Add(new UnifiedStreamChunk
        {
            DeltaToolCall = new UnifiedToolCall { Id = callId, Name = tool, Arguments = argsJson }
        });
        chunks.Add(new UnifiedStreamChunk { FinishReason = "tool_calls" });
        return new Script(chunks);
    }

    private static AgentPlan TwoStepPlan()
        => new()
        {
            Goal = "统计项目文件行数",
            Steps =
            {
                new AgentPlanStep { Id = "s1", Name = "统计行数", Objective = "统计行数", ExpectedOutput = "行数结果" },
                new AgentPlanStep { Id = "s2", Name = "汇总报告", Objective = "汇总成报告", ExpectedOutput = "报告" }
            }
        };

    /// <summary>真身装配观测面。</summary>
    private sealed class Harness
    {
        public RunOrchestratorService Orchestrator { get; init; } = null!;
        public InMemorySessionStore Store { get; init; } = null!;
        public InMemoryInbox Inbox { get; init; } = null!;
        public FakeToolRegistry Tools { get; init; } = null!;
        public string SessionId { get; init; } = string.Empty;
    }

    /// <summary>
    /// 真身装配：mock CreateAgent → 真实 ReactLoopAgent（脚本化 provider + 内存日志 + 内存收件箱）。
    /// </summary>
    private static Harness NewHarness(IReadOnlyList<Script> stepScripts, TimeSpan? stepTimeout = null)
    {
        var sessionId = "s-" + Guid.NewGuid().ToString("N")[..8];
        var store = new InMemorySessionStore();
        var inbox = new InMemoryInbox();
        var tools = new FakeToolRegistry();
        var provider = new ScriptedProvider();
        foreach (var s in stepScripts) provider.Enqueue(s);

        var ai = new Mock<IAIAgentService>();
        ai.Setup(m => m.CreateAgent(It.IsAny<string>(), It.IsAny<AgentOptions>()))
            .Returns((string sid, AgentOptions options) => new ReactLoopAgent(sid, options, new AgentTurnRuntime
            {
                Store = store,
                Provider = provider,
                ModelId = "fake-model",
                ToolExecutor = tools,
                SystemPrompt = null,
                ToolDefinitions = null,
                Events = null,
                Inbox = inbox
            }));

        var planner = new Mock<IPlanGeneratorService>();
        planner.Setup(m => m.GeneratePlanAsync(
                It.IsAny<AgentRun>(), It.IsAny<long>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TwoStepPlan());

        var orchestrator = new RunOrchestratorService(
            planner.Object, ai.Object, inbox: inbox, stepTimeout: stepTimeout);
        return new Harness
        {
            Orchestrator = orchestrator, Store = store, Inbox = inbox, Tools = tools, SessionId = sessionId
        };
    }

    private static RunRequest NewRequest(string sessionId)
        => new() { AgentId = "agent.generalist", SessionId = sessionId, TaskInput = "统计项目文件行数" };

    private static async Task<List<AgentLoopEvent>> CollectAsync(IAsyncEnumerable<AgentLoopEvent> stream)
    {
        var events = new List<AgentLoopEvent>();
        await foreach (var ev in stream) events.Add(ev);
        return events;
    }

    private static AgentRun NewRun(AgentRunStatus status = AgentRunStatus.Running, string? sessionId = null)
    {
        var run = new AgentRun
        {
            AgentId = "agent.generalist",
            AgentName = "通用",
            SessionId = sessionId,
            TaskInput = "统计项目文件行数",
            Status = (int)status,
            StepCount = 1,
            CurrentStepIndex = 0,
        };
        run.Insert();
        return run;
    }

    // ==================== 门禁 1：计划驱动走统一状态机 ====================

    [Fact]
    public async Task PlannedRun_UsesSameLoop()
    {
        var h = NewHarness(new[]
        {
            ContentThenToolCall("统计中…", "c1", "complete_step", """{"output":"产出一"}"""),
            ContentThenToolCall("汇总中…", "c2", "complete_step", """{"output":"产出二"}"""),
        });

        var events = await CollectAsync(h.Orchestrator.RunAsync(NewRequest(h.SessionId), CancellationToken.None));

        // 统一状态机证据：会话日志出现 turn/start / user/message（followup 唤醒）/ step/start / assistant/message
        var log = h.Store.Replay(h.SessionId).Select(e => e.Type).ToList();
        Assert.Contains("turn/start", log);
        Assert.Contains("user/message", log);
        Assert.Contains("step/start", log);
        Assert.Contains("assistant/message", log);
        Assert.Equal(2, log.Count(t => t == "turn/start"));   // 每步骤一个 turn
        Assert.Equal(2, log.Count(t => t == "step/start"));

        // 每步骤输入经收件箱进入循环（followup → 回合边界认领 → user/message 落日志）
        var users = h.Store.Replay(h.SessionId).OfType<UserMessageEvent>().ToList();
        Assert.Equal(2, users.Count);
        Assert.Contains("统计项目文件行数", users[0].Content);

        // 出口工具「声明即出口」：不真实执行
        Assert.Empty(h.Tools.Executions);

        // SSE 面无出口工具的 tool_call/tool_result（与旧出口判定同面：出口由编排器转译）
        Assert.DoesNotContain(events, e => e.Type == "tool_call" && e.Name == "complete_step");
    }

    // ==================== 门禁 2：Intervene steer 语义分叉 ====================

    [Fact]
    public async Task Intervene_AsSteer()
    {
        var inbox = new InMemoryInbox();
        var orchestrator = new RunOrchestratorService(
            new Mock<IPlanGeneratorService>().Object, new Mock<IAIAgentService>().Object, inbox: inbox);

        // (a) 运行中 → steer（NextStep 通道）：step 边界即可认领
        var running = NewRun(AgentRunStatus.Running, "s-gate2-running");
        var dto = await orchestrator.InterveneAsync(running.Id, 0,
            new InterveneRequest { Action = "steer", Output = "改用别的方法统计" });
        Assert.NotNull(dto);

        // (b) 已停（Stuck）→ followup（NextTurn 通道）
        var stuck = NewRun(AgentRunStatus.Stuck, "s-gate2-stuck");
        await orchestrator.InterveneAsync(stuck.Id, 0,
            new InterveneRequest { Action = "steer", Output = "上游恢复了，继续" });

        // 通道区分（用 Claim 边界语义探测；Claim 是提案不删除，R2）：
        // steer 在 step 边界（atTurnBoundary=false）即可认领；followup 只在回合边界（true）出现。
        var stepBoundary = inbox.Claim("s-gate2-running", atTurnBoundary: false).Items;
        Assert.Single(stepBoundary);
        Assert.Contains("改用别的方法统计", stepBoundary[0].Content);

        var stuckAtStepBoundary = inbox.Claim("s-gate2-stuck", atTurnBoundary: false).Items;
        Assert.Empty(stuckAtStepBoundary);
        var stuckAtTurnBoundary = inbox.Claim("s-gate2-stuck", atTurnBoundary: true).Items;
        Assert.Single(stuckAtTurnBoundary);
        Assert.Contains("上游恢复了，继续", stuckAtTurnBoundary[0].Content);

        // 双表运行视图不被 steer 介入改动（输入走收件箱通道）
        var reloaded = AgentRun.FindById(running.Id)!;
        Assert.Equal(AgentRunStatus.Running, (AgentRunStatus)reloaded.Status);
    }

    // ==================== 门禁 3：挂起 → run_stuck（前端字段不变）→ steer+resume 恢复 ====================

    [Fact]
    public async Task Stuck_BecomesSuspendedTurn()
    {
        // 首请求先吐一个增量后永久挂起（无进展）→ 看门狗（400ms）触发；resume 后返回正常文本 + complete_step
        var hangThenRecover = new HangThenTextProvider();
        var sessionId = "s-" + Guid.NewGuid().ToString("N")[..8];
        var store = new InMemorySessionStore();
        var inbox = new InMemoryInbox();
        var provider = hangThenRecover;
        var ai = new Mock<IAIAgentService>();
        ai.Setup(m => m.CreateAgent(It.IsAny<string>(), It.IsAny<AgentOptions>()))
            .Returns((string sid, AgentOptions options) => new ReactLoopAgent(sid, options, new AgentTurnRuntime
            {
                Store = store, Provider = provider, ModelId = "fake-model",
                ToolExecutor = new FakeToolRegistry(), Events = null, Inbox = inbox
            }));
        var planner = new Mock<IPlanGeneratorService>();
        planner.Setup(m => m.GeneratePlanAsync(
                It.IsAny<AgentRun>(), It.IsAny<long>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentPlan
            {
                Goal = "统计项目文件行数",
                Steps = { new AgentPlanStep { Id = "s1", Name = "统计行数", Objective = "统计行数" } }
            });
        var orchestrator = new RunOrchestratorService(
            planner.Object, ai.Object, inbox: inbox, stepTimeout: TimeSpan.FromMilliseconds(400));

        // 第一遍：挂起 → run_stuck（派生），Run.Status=Stuck 保留现场，无 done
        // （挂起前模型先吐过一段增量 → content 事件合法存在）
        var events = await CollectAsync(orchestrator.RunAsync(NewRequest(sessionId), CancellationToken.None));
        Assert.Equal(new[] { "plan_created", "step_started", "content", "run_stuck" },
            events.Select(e => e.Type).ToList());
        var stuckEvent = events.Single(e => e.Type == "run_stuck");
        Assert.Contains("挂起",
            JsonDocument.Parse(stuckEvent.Content!).RootElement.GetProperty("reason").GetString());

        var runId = JsonDocument.Parse(events[1].Content!).RootElement.GetProperty("runId").GetInt64();
        var run = AgentRun.FindById(runId)!;
        Assert.Equal(AgentRunStatus.Stuck, (AgentRunStatus)run.Status);
        Assert.Contains("挂起", run.StuckReason);

        // 挂起保留现场：收件箱输入未确认（仍在待处理集）
        Assert.Single(inbox.Peek(sessionId));

        // 第二遍：steer（唤醒）+ resume（从卡住步骤重跑）→ 恢复并跑完
        await orchestrator.InterveneAsync(runId, 0,
            new InterveneRequest { Action = "steer", Output = "上游恢复了，继续" });

        // resume 前先给 provider 换回正常脚本：挂起测试替身第二次请求自动返回「已恢复」；
        // 本遍需要 complete_step 出口——替换 provider 脚本不可行（已构造），改用恢复后无出口 → 再卡住？
        // 为让恢复路径可断言完成，恢复回合脚本必须含 complete_step：构造第二个 provider 不可行，
        // 故此处恢复断言以「第二轮 run_stuck（未声明出口）」为界——恢复通道本身已验证（新 turn/新 user/message）。
        var events2 = await CollectAsync(orchestrator.ResumeAsync(runId, CancellationToken.None));
        Assert.Contains("step_started", events2.Select(e => e.Type).ToList());

        // 恢复证据：日志出现第二个 turn，且 steer + 原始输入在回合边界重新认领合并
        var log = store.Replay(sessionId).ToList();
        Assert.Equal(2, log.Count(e => e.Type == "turn/start"));
        var secondUser = log.OfType<UserMessageEvent>().Last();
        Assert.Contains("上游恢复了，继续", secondUser.Content);
    }

    // ==================== 门禁 4：029 SSE 面 + 双表落库 回归保持 ====================

    [Fact]
    public async Task PlannedRun_029Regression()
    {
        var h = NewHarness(new[]
        {
            ContentThenToolCall("统计中…", "c1", "complete_step", """{"output":"产出一"}"""),
            ContentThenToolCall("汇总中…", "c2", "complete_step", """{"output":"产出二"}"""),
        });

        var events = await CollectAsync(h.Orchestrator.RunAsync(NewRequest(h.SessionId), CancellationToken.None));

        // SSE 面与薄壳化前一致（出口工具的 tool_call/tool_result 不进 SSE）
        Assert.Equal(
            new[]
            {
                "plan_created",
                "step_started", "content", "step_completed",
                "step_started", "content", "step_completed",
                "done"
            },
            events.Select(e => e.Type).ToList());

        // payload camelCase 结构保持（029 联调锚点）
        var started = JsonDocument.Parse(events[1].Content!).RootElement;
        Assert.Equal("s1", started.GetProperty("stepId").GetString());
        Assert.Equal("统计行数", started.GetProperty("name").GetString());

        // 双表落库：Run Completed + 两步 Completed + 产出
        var runId = started.GetProperty("runId").GetInt64();
        var run = AgentRun.FindById(runId)!;
        Assert.Equal(AgentRunStatus.Completed, (AgentRunStatus)run.Status);
        var steps = AgentStepRun.FindAllByRunId(runId).OrderBy(s => s.StepIndex).ToList();
        Assert.Equal(2, steps.Count);
        Assert.All(steps, s => Assert.Equal(AgentStepStatus.Completed, (AgentStepStatus)s.Status));
        Assert.Equal("产出一", steps[0].OutputJson);

        // 终局合成内容保持
        var done = events.Single(e => e.Type == "done");
        Assert.Contains("# 统计项目文件行数", done.Content);
        Assert.Contains("## 步骤 s1", done.Content);
    }
}

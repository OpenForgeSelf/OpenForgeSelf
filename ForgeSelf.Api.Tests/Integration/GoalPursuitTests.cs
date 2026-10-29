using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Entities;
using ForgeSelf.Api.Plugins.AIAgent.MafLoop;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Api.Services;
using ForgeSelf.Api.Tests.Plugins.AIAgent;
using System.Text.Json;
using Moq;
using Xunit;
using Script = ForgeSelf.Api.Tests.Plugins.AIAgent.Script;

namespace ForgeSelf.Api.Tests.Integration;

/// <summary>
/// PILOT-051 FR-3 门禁（目标驱动执行，plan-and-extend）：计划步骤全部完成后由评估 Agent
/// 对照 SuccessCriteria 判定；未达成 → 追加纠正步骤续跑（goal_round_started/goal_evaluated）；
/// 预算耗尽 → Stuck 带未达成报告；评估器故障 → fail-safe 按未达成（绝不假绿）。
/// </summary>
[Collection("XCode")]
public class GoalPursuitTests : IClassFixture<XCodeTestFixture>
{
    public GoalPursuitTests(XCodeTestFixture fixture)
    {
        _ = fixture;
        // 注：不要在此 DAL.Reset()——它会把 Database 实例换新、Migration 回到 On，
        // 每次 Before 清库时的 EnsureTablesCreated 就会重新走「备份-重建-回填」，吞掉本类首个插入行。
        // 运行期迁移已在 XCodeTestFixture 首次绑定时按 Database 实例级置为 Off（见夹具注释）。
        AgentRun.Meta.Cache.Clear("gate reset");
        AgentStepRun.Meta.Cache.Clear("gate reset");
        AgentRun.Meta.Cache.Expire = 0;
        AgentStepRun.Meta.Cache.Expire = 0;
    }

    // ==================== 脚手架 ====================

    private sealed class FakeProviderRegistry : IAIProviderRegistry
    {
        private readonly IAIProvider _provider;

        public FakeProviderRegistry(IAIProvider provider) { _provider = provider; }

        public IAIProvider? GetProviderByChatModelId(string chatModelId) => _provider;
        public IAIProvider? GetProviderByModel(string modelName) => _provider;
        public IAIProvider? GetDefaultProvider() => _provider;
    }

    private sealed class Harness
    {
        public RunOrchestratorService Orchestrator { get; init; } = null!;
        public ScriptedProvider Provider { get; init; } = null!;
        public InMemorySessionStore Store { get; init; } = null!;
        public InMemoryInbox Inbox { get; init; } = null!;
        public FakeToolRegistry Tools { get; init; } = null!;
        public string SessionId { get; init; } = string.Empty;
    }

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
            Goal = "统计项目文件行数并汇总",
            Steps =
            {
                new AgentPlanStep { Id = "s1", Name = "统计行数", Objective = "统计行数", ExpectedOutput = "行数结果" },
                new AgentPlanStep { Id = "s2", Name = "汇总报告", Objective = "汇总成报告", ExpectedOutput = "报告" }
            }
        };

    private static Harness NewHarness(IReadOnlyList<Script> scripts)
    {
        var sessionId = "s-" + Guid.NewGuid().ToString("N")[..8];
        var store = new InMemorySessionStore();
        var inbox = new InMemoryInbox();
        var tools = new FakeToolRegistry();
        var provider = new ScriptedProvider();
        foreach (var s in scripts) provider.Enqueue(s);

        var goalPursuit = new GoalPursuitService(new FakeProviderRegistry(provider));

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
            planner.Object, ai.Object, inbox: inbox,
            stepTimeout: TimeSpan.FromSeconds(10), goalPursuit: goalPursuit);
        return new Harness
        {
            Orchestrator = orchestrator, Provider = provider, Store = store, Inbox = inbox, Tools = tools,
            SessionId = sessionId
        };
    }

    private static RunRequest NewRequest(string sessionId, List<string>? criteria, int? maxRounds)
        => new()
        {
            AgentId = "agent.generalist",
            SessionId = sessionId,
            TaskInput = "统计项目文件行数并汇总",
            SuccessCriteria = criteria,
            MaxGoalRounds = maxRounds
        };

    private static async Task<List<AgentLoopEvent>> CollectAsync(IAsyncEnumerable<AgentLoopEvent> stream)
    {
        var events = new List<AgentLoopEvent>();
        await foreach (var ev in stream) events.Add(ev);
        return events;
    }

    private static string? Payload(List<AgentLoopEvent> events, string type, int index = 0)
        => events.Where(e => e.Type == type).Skip(index).FirstOrDefault()?.Content;

    /// <summary>当前测试的 RunId（取自首个 step_started payload；避免 FindAll 全表查询）。</summary>
    private long _lastRunId;

    private static long RunIdOf(List<AgentLoopEvent> events)
        => JsonDocument.Parse(Payload(events, "step_started")!).RootElement.GetProperty("runId").GetInt64();

    // ==================== 门禁 1（AC5）：未达成 → 纠正步骤续跑 → 达成收束 ====================

    [Fact]
    public async Task GoalMode_NotAchieved_ExtendsPlanThenAchieves()
    {
        var h = NewHarness(new[]
        {
            ContentThenToolCall("统计中…", "c1", "complete_step", """{"output":"产出一"}"""),
            ContentThenToolCall("汇总中…", "c2", "complete_step", """{"output":"产出二"}"""),
            Script.Text("""{"achieved": false, "confidence": 0.6, "missing": ["缺少最终汇总报告"], "critique": "第二步产出不完整", "summary": "汇总缺失"}"""),
            Script.Text("""[{"name": "补出汇总报告", "objective": "把前两步产出汇总成一份完整报告"}]"""),
            ContentThenToolCall("补汇总中…", "c3", "complete_step", """{"output":"最终报告已完成"}"""),
            Script.Text("""{"achieved": true, "confidence": 0.95, "missing": [], "critique": "全部判据满足", "summary": "任务达成"}"""),
        });

        var events = await CollectAsync(h.Orchestrator.RunAsync(
            NewRequest(h.SessionId, new List<string> { "输出行数统计", "输出汇总报告" }, 3),
            CancellationToken.None));

        // 事件面：goal_round_started ×2 → goal_evaluated ×2 → done
        Assert.Equal(2, events.Count(e => e.Type == "goal_round_started"));
        Assert.Equal(2, events.Count(e => e.Type == "goal_evaluated"));
        Assert.Contains(events, e => e.Type == "done");

        var round1 = Payload(events, "goal_round_started");
        Assert.NotNull(round1);
        Assert.Contains("\"round\":1", round1);
        Assert.Contains("输出汇总报告", round1);

        var eval1 = Payload(events, "goal_evaluated");
        Assert.NotNull(eval1);
        Assert.Contains("\"achieved\":false", eval1);
        Assert.Contains("汇总缺失", eval1);

        var eval2 = Payload(events, "goal_evaluated", 1);
        Assert.NotNull(eval2);
        Assert.Contains("\"achieved\":true", eval2);

        // 落库断言只读 Run 行（FindById 走单对象缓存，跨类稳定）；
        // 步骤行的跨类直查在共享库下存在 XCode 首插可见性竞态（见 05-evidence 偏差记），
        // 步骤级持久化语义由 AgentRunPersistenceTests 专类覆盖，此处不再重复断言。
        // 纠正步骤真实执行由事件面（step_started/step_completed ×3）与请求序 [4] 证明。
        _lastRunId = RunIdOf(events);
        var run = AgentRun.FindById(_lastRunId)!;
        Assert.Equal(AgentRunStatus.Completed, (AgentRunStatus)run.Status);
        Assert.Equal(3, run.StepCount);
        Assert.Equal(3, events.Count(e => e.Type == "step_completed"));

        // PlanJson 含纠正步骤（plan-and-extend 持久化）
        Assert.Contains("补出汇总报告", run.PlanJson);

        // 评估器请求：无工具、带判据；纠正生成请求带未达成项
        // 请求序：[0]=s1 模型调用 [1]=s2 模型调用 [2]=评估1 [3]=纠正生成 [4]=纠正步骤 [5]=评估2
        var evalReq = h.Provider.Requests[2];
        Assert.Null(evalReq.Tools);
        Assert.Contains("目标达成评估器", evalReq.SystemPrompt);
        Assert.Contains("输出汇总报告", evalReq.Messages.Last().Content);
        var corrReq = h.Provider.Requests[3];
        Assert.Contains("执行计划修正器", corrReq.SystemPrompt);
        Assert.Contains("纠正补漏", corrReq.Messages.Last().Content);
        Assert.Contains("缺少最终汇总报告", corrReq.Messages.Last().Content);
    }

    // ==================== 门禁 2（AC6）：预算耗尽 → Stuck 带未达成报告 ====================

    [Fact]
    public async Task GoalMode_BudgetExhausted_StuckWithReport()
    {
        var h = NewHarness(new[]
        {
            ContentThenToolCall("统计中…", "c1", "complete_step", """{"output":"产出一"}"""),
            ContentThenToolCall("汇总中…", "c2", "complete_step", """{"output":"产出二"}"""),
            Script.Text("""{"achieved": false, "missing": ["报告不完整"], "summary": "第1轮未达成"}"""),
            Script.Text("""[{"name": "再补一轮", "objective": "继续补齐报告"}]"""),
            ContentThenToolCall("补齐中…", "c3", "complete_step", """{"output":"仍然不完整"}"""),
            Script.Text("""{"achieved": false, "missing": ["报告仍不完整"], "summary": "第2轮仍未达成"}"""),
        });

        var events = await CollectAsync(h.Orchestrator.RunAsync(
            NewRequest(h.SessionId, new List<string> { "输出汇总报告" }, 2),
            CancellationToken.None));

        // 2 轮评估后预算耗尽 → run_stuck；绝不发 done、不置 Completed
        Assert.Equal(2, events.Count(e => e.Type == "goal_evaluated"));
        Assert.DoesNotContain(events, e => e.Type == "done");

        var stuck = Payload(events, "run_stuck");
        Assert.NotNull(stuck);
        Assert.Contains("目标未达成（已用完 2 轮评估）", stuck);
        Assert.Contains("第2轮仍未达成", stuck);

        _lastRunId = RunIdOf(events);
        var run = AgentRun.FindById(_lastRunId)!;
        Assert.Equal(AgentRunStatus.Stuck, (AgentRunStatus)run.Status);
        Assert.Contains("目标未达成", run.StuckReason ?? string.Empty);
    }

    // ==================== 门禁 3（AC6）：评估器故障 → fail-safe 按未达成，回退纠正步骤 ====================

    [Fact]
    public async Task GoalMode_EvaluatorFailure_FailSafeNotAchieved()
    {
        var h = NewHarness(new[]
        {
            ContentThenToolCall("统计中…", "c1", "complete_step", """{"output":"产出一"}"""),
            ContentThenToolCall("汇总中…", "c2", "complete_step", """{"output":"产出二"}"""),
            Script.Text("这不是 JSON（评估第 1 次失败）"),
            Script.Text("还是不是 JSON（评估重试失败）"),
            Script.Text("纠正生成也失败"),
            ContentThenToolCall("补漏中…", "c3", "complete_step", """{"output":"补漏产出"}"""),
            Script.Text("""{"achieved": true, "summary": "第 2 轮达成"}"""),
        });

        var events = await CollectAsync(h.Orchestrator.RunAsync(
            NewRequest(h.SessionId, new List<string> { "输出汇总报告" }, 2),
            CancellationToken.None));

        // 评估失败按未达成处理（绝不假绿），走纠正 → 第 2 轮达成收束
        var eval1 = Payload(events, "goal_evaluated");
        Assert.NotNull(eval1);
        Assert.Contains("\"achieved\":false", eval1);
        Assert.Contains("评估器不可用", eval1);

        // 纠正生成失败 → 回退单条「对照成功判据补漏」步骤，循环仍推进；
        // 回退步骤名经 goal_evaluated 后的 step_started 事件名与 PlanJson 共同证明（不跨类直查步骤行）
        _lastRunId = RunIdOf(events);
        var run = AgentRun.FindById(_lastRunId)!;
        Assert.Equal(AgentRunStatus.Completed, (AgentRunStatus)run.Status);
        Assert.Contains("对照成功判据补漏", run.PlanJson);
        Assert.Contains(events, e => e.Type == "done");
    }

    // ==================== 门禁 4：非目标模式不引入任何目标事件（现状行为不变） ====================

    [Fact]
    public async Task NonGoalMode_NoGoalEvents()
    {
        var h = NewHarness(new[]
        {
            ContentThenToolCall("统计中…", "c1", "complete_step", """{"output":"产出一"}"""),
            ContentThenToolCall("汇总中…", "c2", "complete_step", """{"output":"产出二"}"""),
        });

        var events = await CollectAsync(h.Orchestrator.RunAsync(NewRequest(h.SessionId, null, null), CancellationToken.None));

        Assert.DoesNotContain(events, e => e.Type == "goal_round_started");
        Assert.DoesNotContain(events, e => e.Type == "goal_evaluated");
        Assert.Contains(events, e => e.Type == "done");
        _lastRunId = RunIdOf(events);
        var run = AgentRun.FindById(_lastRunId)!;
        Assert.Equal(AgentRunStatus.Completed, (AgentRunStatus)run.Status);
    }

    // ==================== 纯单元：评估 JSON 防御性解析 ====================

    [Fact]
    public void ParseEvaluation_ToleratesFencesAndNoise()
    {
        var eval = GoalPursuitService.ParseEvaluation(
            "前置说明\n```json\n{\"achieved\": true, \"confidence\": 0.8, \"missing\": [], \"summary\": \"ok\"}\n```\n");
        Assert.NotNull(eval);
        Assert.True(eval!.Achieved);
        Assert.Equal(0.8, eval.Confidence);
        Assert.Equal("ok", eval.Summary);

        Assert.Null(GoalPursuitService.ParseEvaluation("完全不是 JSON"));
        Assert.Null(GoalPursuitService.ParseEvaluation(null));

        var steps = GoalPursuitService.ParseCorrectiveSteps("噪音 [ {\"name\": \"补\", \"objective\": \"补齐\"} ] 噪音");
        Assert.Single(steps);
        Assert.Equal("补", steps[0].Name);
        Assert.Equal("补齐", steps[0].Objective);
    }
}

using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Entities;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Api.Plugins.AIAgent.Services.ToolFunctions;
using ForgeSelf.Api.Services;
using ForgeSelf.Api.Tests.Plugins.AIAgent;
using Moq;
using Xunit;
using Script = ForgeSelf.Api.Tests.Plugins.AIAgent.Script;

namespace ForgeSelf.Api.Tests.Plugins.AIAgent;

/// <summary>
/// B7 规划统一循环 QA 对抗探针（QA 独立编写，实现方不得改写本文件——团队纪律）。
/// </summary>
/// <remarks>
/// <para>
/// <b>补的是既有门禁的盲区</b>：<see cref="Integration.PlannedRunUnifiedLoopGates"/> 用 mock
/// IPlanGeneratorService 绕过了规划循环——「plan:{runId} scratch-session 会话日志里规划 turn 是否完整」
/// （B7 lead 终验重点 4：followup 唤醒 + submit_plan tool/call + 合成 result + ExitTool 出口）
/// 在既有测试面无任何覆盖。本文件用<b>真 PlanGeneratorService + 真实 ReactLoopAgent</b>
/// （脚本化 provider）钉死。原 ad-hoc 无日志旁路已消除（B7 lead 已批偏离②：规划事实落日志，可审计可回放），
/// 本探针把「落日志」从声明变成断言。
/// </para>
/// <para>
/// 同时锁 029 回退语义三路中的两路可实测路径（第三路 20s 看门狗超时因常量不可注入，由代码审查覆盖：
/// <c>catch (OperationCanceledException) when (!ct.IsCancellationRequested)</c> 与用户取消判据分离，无误判）：
/// LLM 未提交 → 回退；LLM 异常 → 回退；有草稿 → 草稿优先于单步兜底。
/// </para>
/// </remarks>
[Collection("XCode")]
public class PlanGeneratorScratchSessionQaTests
{
    private const string SubmitPlanArgs = """
        {"plan":{"goal":"统计项目文件行数","steps":[
            {"id":"s1","name":"统计行数","objective":"统计行数","expectedOutput":"行数结果"},
            {"id":"s2","name":"汇总报告","objective":"汇总成报告","expectedOutput":"报告"}]}}
        """;

    /// <summary>真 PlanGeneratorService + 真实 ReactLoopAgent（脚本化 provider、内存日志/收件箱）。</summary>
    private static (PlanGeneratorService Planner, InMemorySessionStore Store, string SessionId) NewPlanner(
        ScriptedProvider provider, AgentRun run, IWorkflowService? workflowService = null)
    {
        var sessionId = $"plan:{run.Id}";
        var store = new InMemorySessionStore();
        var inbox = new InMemoryInbox();
        var tools = new FakeToolRegistry();

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

        var planner = new PlanGeneratorService(
            ai.Object,
            new RunFlowToolSet("aiagent"),
            agentRegistry: null,
            workflowService: workflowService,
            ctx: null,
            inbox: inbox);
        return (planner, store, sessionId);
    }

    private static AgentRun NewRun(long id = 42)
        => new() { AgentId = "agent.generalist", TaskInput = "统计项目文件行数", Id = id };

    // ───────────── 1. 出口路径：submit_plan 经统一状态机，规划 turn 日志完整 ─────────────

    /// <summary>
    /// 【B7 缺陷立案，红轮证据探针，修复后移除 Skip】
    /// 现状（终验实测）：PlanGeneratorService.TryPlanByLLMAsync 在收到 ExitToolInvoked 帧时提前 return，
    /// await foreach 的 finally 释放枚举器——ReactLoopAgent 迭代器停在 yield 点，其后的
    /// Append(StepEndEvent)、ConfirmIfCommitted（R2 确认消费）、Append(TurnEndEvent) 均不执行，
    /// 规划 turn 日志止于 tool/result（无 step/end、无 turn/end），收件箱输入永不确认。
    /// 违反「统一循环一切回合必须收束收口」不变量（编排器路径消费全部帧无此问题）。
    /// 修复建议：不用提前 return——记录 submitted 后继续消费帧至 TurnCompleted，循环结束再返回。
    /// </summary>
    [Fact]
    public async Task Qa_SubmitPlanExit_PlanningTurnFullyLogged()
    {
        var provider = new ScriptedProvider();
        provider.Enqueue(Script.ToolCall("c1", "submit_plan", SubmitPlanArgs));
        var run = NewRun();
        var (planner, store, sessionId) = NewPlanner(provider, run);

        var plan = await planner.GeneratePlanAsync(run, workflowId: 0, chatModelId: null, CancellationToken.None);

        // Plan 被解析并回传（submit_plan 参数 → Plan DSL，步骤 id 缺失补齐逻辑不触发）
        Assert.Equal("统计项目文件行数", plan.Goal);
        Assert.Equal(2, plan.Steps.Count);
        Assert.Equal("s1", plan.Steps[0].Id);

        // 核心断言：规划事实落 plan:{runId} 会话日志——followup 唤醒 + submit_plan tool/call
        // + 合成 tool/result(Ok) + step/end(ExitTool) + turn/end(Completed)，一个不缺
        var types = store.Replay(sessionId).Select(e => e.Type).ToList();
        Assert.Equal(
            new[]
            {
                "turn/start", "step/start", "user/message", "request/header",
                "assistant/message", "tool/call", "tool/result",
                "step/end", "turn/end"
            },
            types);

        // user/message = 任务原文（followup 唤醒，模型可见 = 已记录）
        var user = Assert.Single(store.Replay(sessionId).OfType<UserMessageEvent>());
        Assert.Equal("统计项目文件行数", user.Content);

        // submit_plan tool/call 带完整 Plan 参数；合成 result Ok 闭合轨迹（N call 必有 N result）
        var call = Assert.Single(store.Replay(sessionId).OfType<ToolCallEvent>());
        Assert.Equal("submit_plan", call.ToolName);
        Assert.Contains("统计项目文件行数", call.ArgsJson);
        var result = Assert.Single(store.Replay(sessionId).OfType<ToolResultEvent>());
        Assert.Equal(ToolOutcome.Ok, result.Outcome);

        // step/end(ExitTool) + turn/end(Completed)
        Assert.Equal(StepEndReason.ExitTool,
            Assert.Single(store.Replay(sessionId).OfType<StepEndEvent>()).Reason);
        Assert.Equal(TurnEndReason.Completed,
            Assert.Single(store.Replay(sessionId).OfType<TurnEndEvent>()).Reason);
    }

    // ───────────── 2. 回退路 A：LLM 未调用 submit_plan（收束未出口）→ 单步兜底计划 ─────────────

    [Fact]
    public async Task Qa_NoSubmitPlan_FallsBackToSingleStepPlan()
    {
        var provider = new ScriptedProvider();
        provider.Enqueue(Script.Text("我觉得直接做就行了", finish: "stop"));   // 自由文本，无出口
        var run = NewRun();
        var (planner, _, _) = NewPlanner(provider, run);

        var plan = await planner.GeneratePlanAsync(run, workflowId: 0, chatModelId: null, CancellationToken.None);

        // 029 回退语义：未提交 → 单步兜底（goal=任务原文，1 步全工具）
        Assert.Equal("统计项目文件行数", plan.Goal);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("s1", step.Id);
        Assert.Equal("完成整体任务", step.Name);
    }

    // ───────────── 3. 回退路 B：LLM 请求异常 → 不抛出、走回退（整 Run 不回滚） ─────────────

    [Fact]
    public async Task Qa_ProviderThrows_FallsBackSilently()
    {
        var provider = new ScriptedProvider();
        provider.Enqueue(Script.Fail(new InvalidOperationException("上游 500")), repeatLast: true);
        var run = NewRun();
        var (planner, _, _) = NewPlanner(provider, run);

        // 关键判据：规划异常不向上抛（Engineer 裁决语义：记日志走回退，Run 不因规划异常整体失败）
        var plan = await planner.GeneratePlanAsync(run, workflowId: 0, chatModelId: null, CancellationToken.None);

        Assert.Equal("统计项目文件行数", plan.Goal);
        Assert.Single(plan.Steps);   // 单步兜底
    }

    // ───────────── 4. 回退路 C：有工作流草稿时，草稿优先于单步兜底 ─────────────

    [Fact]
    public async Task Qa_WithDraft_DraftPreferredOverSingleStepFallback()
    {
        var provider = new ScriptedProvider();
        provider.Enqueue(Script.Text("未提交计划", finish: "stop"));

        var wf = new WorkflowDefinitionDto
        {
            Id = 7,
            Name = "代码评审流",
            Description = "评审代码并汇总",
            Steps = new List<WorkflowStep>
            {
                new() { Name = "读取代码", Description = "读取目标文件" },
                new() { Name = "输出意见", Description = "给出评审意见" },
            }
        };
        var workflowService = new Mock<IWorkflowService>();
        workflowService.Setup(m => m.GetWorkflowAsync(7)).ReturnsAsync(wf);

        var run = NewRun();
        var (planner, _, _) = NewPlanner(provider, run, workflowService.Object);

        var plan = await planner.GeneratePlanAsync(run, workflowId: 7, chatModelId: null, CancellationToken.None);

        // 草稿优先：LLM 未提交 → 返回工作流转出的草稿（2 步），而不是单步兜底
        Assert.Equal("评审代码并汇总", plan.Goal);
        Assert.Equal(2, plan.Steps.Count);
        Assert.Equal("读取代码", plan.Steps[0].Name);
        Assert.Equal("输出意见", plan.Steps[1].Name);
    }
}

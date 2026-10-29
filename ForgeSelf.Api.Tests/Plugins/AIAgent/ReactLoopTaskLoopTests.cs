using System.Text.Json;
using ForgeSelf.Abstractions;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AIAgent;

/// <summary>
/// PILOT-051 AC3：ReactLoopAgent（后备引擎）的任务循环语义——与 <see cref="MafTaskLoopAgentTests"/>
/// 同一判据集，保证两引擎行为一致（回滚点：关 UseMafRuntime 即切回本引擎，语义不回退）。
/// </summary>
[Collection("XCode")]
public class ReactLoopTaskLoopTests
{
    // ---- 工具调用后模型只回文本 → 续跑到 task_complete 出口 ----
    [Fact]
    public async Task ToolCallThenInterimText_ContinuesUntilTaskComplete()
    {
        var fx = new Fixture();
        fx.Provider.Enqueue(Script.ToolCall("call-1", "get_current_time", "{}"));
        fx.Provider.Enqueue(Script.Text("现在是 12:00", finish: "stop"));
        fx.Provider.Enqueue(Script.ToolCall("exit-1", "task_complete", "{\"summary\":\"时间已告知，任务完成\"}"));

        var agent = fx.CreateAgent(options: new AgentOptions { TaskLoopMode = true });
        agent.Inbox.Followup(fx.SessionId, "现在几点？告诉我后就完成任务");
        var frames = await fx.RunAsync(agent);

        var types = fx.EventTypes();
        Assert.True(types.Count(t => t == "step/start") >= 2, "模型纯文本未出口必须续跑，不得结束回合");
        Assert.Equal(2, types.Count(t => t == "tool/call"));
        Assert.Equal(2, types.Count(t => t == "tool/result"));

        var stepEnds = fx.Store.Replay(fx.SessionId).OfType<StepEndEvent>().ToList();
        Assert.Equal(StepEndReason.ExitTool, stepEnds.Last().Reason);
        Assert.Contains(frames, f => f is TurnCompleted { Reason: TurnEndReason.Completed });
        Assert.Contains(frames, f => f is ExitToolInvoked { ToolName: "task_complete" });

        // get_current_time 走六闸门执行器；task_complete 走出口短路
        Assert.Equal(new[] { "get_current_time" }, fx.Tools.Executions.Select(e => e.Name).ToArray());
        var taskCompleteResult = fx.Store.Replay(fx.SessionId).OfType<ToolResultEvent>()
            .Single(r => r.ToolName == "task_complete");
        Assert.Equal(ToolOutcome.Ok, taskCompleteResult.Outcome);

        // 续跑步强制工具调用
        Assert.Equal("required", fx.Provider.Requests[2].ToolChoice);
        // 任务循环协议注入系统提示
        var header = fx.Store.Replay(fx.SessionId).OfType<RequestHeaderEvent>().First();
        Assert.Contains("任务循环协议", header.RenderedSystemPrompt);
    }

    // ---- 纯问答（零工具调用）→ 照旧收束 ----
    [Fact]
    public async Task PureQA_CompletesNormally()
    {
        var fx = new Fixture();
        fx.Provider.Enqueue(Script.Text("你好", finish: "stop"));

        var agent = fx.CreateAgent(options: new AgentOptions { TaskLoopMode = true });
        agent.Inbox.Followup(fx.SessionId, "你好");
        await fx.RunAsync(agent);

        Assert.Equal(1, fx.EventTypes().Count(t => t == "step/start"));
        Assert.Null(fx.Provider.Requests.Single().ToolChoice);
        Assert.Equal(TurnEndReason.Completed, fx.Store.Replay(fx.SessionId).OfType<TurnEndEvent>().Single().Reason);
    }

    // ---- 计划驱动路径（ExitToolNames 显式给出）不受影响：全程 required（AC14 语义保持） ----
    [Fact]
    public async Task PlanDrivenExitTools_KeepRequiredToolChoice()
    {
        var fx = new Fixture();
        fx.Provider.Enqueue(Script.ToolCall("exit-1", "complete_step", "{\"output\":\"done\"}"));

        var agent = fx.CreateAgent(options: new AgentOptions
        {
            ExitToolNames = new[] { "complete_step", "request_help" }
        });
        agent.Inbox.Followup(fx.SessionId, "执行计划步骤");
        await fx.RunAsync(agent);

        Assert.Equal("required", Assert.Single(fx.Provider.Requests).ToolChoice);
    }

    // ---- 续跑有界：反复纯文本 → MaxStepsPerTurn 强制收束 ----
    [Fact]
    public async Task Continuation_BoundedByMaxStepsPerTurn()
    {
        var fx = new Fixture(maxStepsPerTurn: 3);
        fx.Provider.Enqueue(Script.ToolCall("call-1", "get_current_time", "{}"));
        fx.Provider.Enqueue(Script.Text("还没做完", finish: "stop"), repeatLast: true);

        var agent = fx.CreateAgent(options: new AgentOptions { TaskLoopMode = true, MaxStepsPerTurn = 3 });
        agent.Inbox.Followup(fx.SessionId, "开始任务");
        var frames = await fx.RunAsync(agent);

        Assert.Equal(3, fx.EventTypes().Count(t => t == "step/start"));
        Assert.DoesNotContain(frames, f => f is TurnFailed);
        Assert.Equal(AgentStatus.Idle, agent.Status);
    }
}

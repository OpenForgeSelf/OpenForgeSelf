using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Controllers;
using ForgeSelf.Api.Plugins.AIAgent.Entities;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Api.Plugins.AIAgent.MafLoop;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Api.Services;
using ForgeSelf.Api.Tests.Plugins.AIAgent;
using Moq;
using Script = ForgeSelf.Api.Tests.Plugins.AIAgent.Script;

namespace ForgeSelf.Api.Tests.Plugins.AIAgent;

/// <summary>
/// PILOT-077 门禁（goal 事件 SSE 映射 + 步骤工具轨迹证据链）：
/// ① goal_round_started/goal_evaluated 必须走 {type,payload} 封套（前端统一读 obj.payload），
///    既有五类事件字节面不回归；② 真实调用工具的步骤 ToolCallsJson 必须非空（防「副作用已落定、证据未随行」），
///    出口工具不入轨迹；③ 评估器提示词必须携带步骤产出与工具轨迹（评估有据可依）。
/// </summary>
[Collection("XCode")]
public class GoalRunSseEvidenceTests : IClassFixture<XCodeTestFixture>
{
    public GoalRunSseEvidenceTests(XCodeTestFixture fixture)
    {
        _ = fixture;
        AgentRun.Meta.Cache.Clear("gate reset");
        AgentStepRun.Meta.Cache.Clear("gate reset");
        AgentRun.Meta.Cache.Expire = 0;
        AgentStepRun.Meta.Cache.Expire = 0;
    }

    // ==================== AC-1/AC-2：SSE 事件封套映射 ====================

    private static async Task<JsonElement> CaptureAsync(AgentLoopEvent ev)
    {
        object? captured = null;
        await AgentRunsController.WriteSseEventAsync(ev, o => { captured = o; return Task.CompletedTask; });
        Assert.NotNull(captured);
        var json = JsonSerializer.Serialize(captured);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    [Theory]
    [InlineData("goal_round_started")]
    [InlineData("goal_evaluated")]
    public async Task WriteSseEvent_GoalEvents_UsePayloadEnvelope(string type)
    {
        var content = type == "goal_round_started"
            ? """{"runId":1,"round":1,"maxRounds":3}"""
            : """{"runId":1,"round":1,"achieved":false}""";
        var root = await CaptureAsync(new AgentLoopEvent { Type = type, Content = content });

        Assert.Equal(type, root.GetProperty("type").GetString());
        Assert.Equal(content, root.GetProperty("payload").GetString());
    }

    [Theory]
    [InlineData("plan_created")]
    [InlineData("step_started")]
    [InlineData("step_completed")]
    [InlineData("run_stuck")]
    [InlineData("done")]
    public async Task WriteSseEvent_LegacyFiveEvents_KeepPayloadEnvelope(string type)
    {
        var root = await CaptureAsync(new AgentLoopEvent { Type = type, Content = """{"k":1}""" });

        Assert.Equal(type, root.GetProperty("type").GetString());
        Assert.Equal("""{"k":1}""", root.GetProperty("payload").GetString());
    }

    [Fact]
    public async Task WriteSseEvent_OtherTypedEvents_KeepDedicatedShape()
    {
        var contentEv = await CaptureAsync(new AgentLoopEvent { Type = "content", Content = "增量" });
        Assert.Equal("增量", contentEv.GetProperty("content").GetString());
        Assert.False(contentEv.TryGetProperty("payload", out _));

        var callEv = await CaptureAsync(new AgentLoopEvent { Type = "tool_call", Name = "write_file", Arguments = """{"p":1}""" });
        Assert.Equal("write_file", callEv.GetProperty("name").GetString());
        Assert.Equal("""{"p":1}""", callEv.GetProperty("arguments").GetString());

        var resultEv = await CaptureAsync(new AgentLoopEvent { Type = "tool_result", Name = "write_file", Result = "ok", Success = true });
        Assert.Equal("ok", resultEv.GetProperty("result").GetString());
        Assert.True(resultEv.GetProperty("success").GetBoolean());

        var usageEv = await CaptureAsync(new AgentLoopEvent { Type = "usage" });
        Assert.True(usageEv.TryGetProperty("usage", out _));
    }

    // ==================== AC-3/AC-4：步骤工具轨迹 + 评估证据 ====================

    private sealed class FakeProviderRegistry : IAIProviderRegistry
    {
        private readonly IAIProvider _provider;
        public FakeProviderRegistry(IAIProvider provider) { _provider = provider; }
        public IAIProvider? GetProviderByChatModelId(string chatModelId) => _provider;
        public IAIProvider? GetProviderByModel(string modelName) => _provider;
        public IAIProvider? GetDefaultProvider() => _provider;
    }

    private static AgentPlan SingleStepPlan()
        => new()
        {
            Goal = "写文件并读回校验",
            Steps = { new AgentPlanStep { Id = "s1", Name = "写入文件", Objective = "把内容写入目标文件", ExpectedOutput = "写入结果" } }
        };

    [Fact]
    public async Task StepWithRealToolCall_TracePersistedAndEvaluatorSeesEvidence()
    {
        var sessionId = "s-" + Guid.NewGuid().ToString("N")[..8];
        var store = new InMemorySessionStore();
        var inbox = new InMemoryInbox();
        var tools = new FakeToolRegistry();
        var provider = new ScriptedProvider();
        // 请求序：[0] s1 调真工具 write_file → [1] s1 续跑出口 complete_step → [2] 评估（达成收束）
        provider.Enqueue(Script.ToolCall("c1", "write_file", """{"path":"D:\\tmp\\x.txt","content":"hi"}"""));
        provider.Enqueue(Script.ToolCall("c2", "complete_step", """{"output":"文件已写入并读回：hi"}"""));
        provider.Enqueue(Script.Text("""{"achieved": true, "confidence": 0.9, "missing": [], "summary": "证据充分，任务达成"}"""));

        var goalPursuit = new GoalPursuitService(new FakeProviderRegistry(provider));
        var ai = new Mock<IAIAgentService>();
        ai.Setup(m => m.CreateAgent(It.IsAny<string>(), It.IsAny<AgentOptions>()))
            .Returns((string sid, AgentOptions options) => new ReactLoopAgent(sid, options, new AgentTurnRuntime
            {
                Store = store, Provider = provider, ModelId = "fake-model", ToolExecutor = tools,
                SystemPrompt = null, ToolDefinitions = null, Events = null, Inbox = inbox
            }));
        var planner = new Mock<IPlanGeneratorService>();
        planner.Setup(m => m.GeneratePlanAsync(
                It.IsAny<AgentRun>(), It.IsAny<long>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SingleStepPlan());
        var orchestrator = new RunOrchestratorService(
            planner.Object, ai.Object, inbox: inbox,
            stepTimeout: TimeSpan.FromSeconds(10), goalPursuit: goalPursuit);

        var events = new List<AgentLoopEvent>();
        await foreach (var ev in orchestrator.RunAsync(new RunRequest
        {
            AgentId = "agent.generalist", SessionId = sessionId, TaskInput = "写文件并读回校验",
            SuccessCriteria = new List<string> { "文件已写入" }
        }, CancellationToken.None))
        {
            events.Add(ev);
        }

        // 达成收束（证据充分 → 第 1 轮即达成）
        Assert.Contains(events, e => e.Type == "done");
        Assert.Contains(events, e => e.Type == "goal_evaluated");

        // AC-3：真实工具轨迹落库非空；出口工具不入轨迹
        var runId = JsonDocument.Parse(events.First(e => e.Type == "step_started").Content!)
            .RootElement.GetProperty("runId").GetInt64();
        var stepRun = AgentStepRun.FindAllByRunIdAndStepIndex(runId, 0).FirstOrDefault();
        Assert.NotNull(stepRun);
        Assert.NotEqual("[]", stepRun!.ToolCallsJson);
        using var traces = JsonDocument.Parse(stepRun.ToolCallsJson!);
        var arr = traces.RootElement;
        Assert.Equal(JsonValueKind.Array, arr.ValueKind);
        var trace0 = arr[0];
        Assert.Equal("write_file", trace0.GetProperty("Name").GetString());
        Assert.True(trace0.GetProperty("Success").GetBoolean());
        Assert.Contains("x.txt", trace0.GetProperty("ArgumentsSummary").GetString());
        Assert.DoesNotContain("complete_step", stepRun.ToolCallsJson);

        // AC-4：评估器提示词携带步骤产出与工具轨迹（评估有据可依）
        var evalPrompt = provider.Requests[2].Messages.Last().Content ?? string.Empty;
        Assert.Contains("文件已写入并读回", evalPrompt);   // 步骤产出
        Assert.Contains("工具轨迹", evalPrompt);            // 轨迹节标签
        Assert.Contains("write_file", evalPrompt);          // 轨迹工具名
        Assert.Contains("x.txt", evalPrompt);               // 轨迹参数摘要
    }
}

using System.Runtime.CompilerServices;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using Xunit;

namespace ForgeSelf.Api.Plugins.AIAgent.Tests;

public class AgentFacadeExecutorTests
{
    [Fact]
    public async Task OpenRun_MapsTextAndToolsThenCompletes()
    {
        var facade = new AgentFacadeService();
        var agent = new FakeAgent
        {
            Frames =
            {
                new AssistantDelta("你好"),
                new ToolStarted("c1", "fs.read", """{"path":"/tmp/a"}"""),
                new ToolCompleted("c1", ToolOutcome.Ok, 3),
                new AssistantDelta("，已读完"),
                new TurnCompleted("t1", TurnEndReason.Completed)
            }
        };
        var exec = new AgentFacadeRunExecutor(facade, agentService: new StubAgentService(), registry: new StubRegistry(agent));
        facade.AttachExecutor(exec);

        var session = facade.CreateSession(Req("ai-agent"));
        var (status, body) = facade.PostMessage(session.Id, new PostAgentMessageRequest { Text = "读一下文件" });
        Assert.Equal(202, status);
        var runId = ((AcceptedRunResponse)body).RunId;

        var types = await DrainUntil(facade, session.Id, "run.completed");
        Assert.Contains("message.delta", types);
        Assert.Contains("tool.started", types);
        Assert.Contains("tool.completed", types);
        Assert.Contains("message.completed", types);
        Assert.Contains("run.completed", types);
        var detail = facade.GetSession(session.Id)!;
        Assert.Equal("completed", detail.Session.Status);
        Assert.Contains(detail.Messages, m => m.Role == "assistant" && m.Content.Contains("你好"));
        Assert.Equal(runId, detail.CurrentRun!.Id);
    }

    [Fact]
    public async Task ConfirmPlan_RunsConfirmedPlanAndMapsStepEvents()
    {
        var facade = new AgentFacadeService();
        var orch = new FakeOrchestrator
        {
            Events =
            {
                new AgentLoopEvent { Type = "step_started", Content = """{"runId":41,"stepIndex":0,"stepId":"s1","name":"准备","objective":"确认目标"}""" },
                new AgentLoopEvent { Type = "content", Content = "开始准备" },
                new AgentLoopEvent { Type = "tool_call", Name = "fs.ls", Arguments = """{"cwd":"/tmp"}""" },
                new AgentLoopEvent { Type = "tool_result", Name = "fs.ls", Result = "a.txt", Success = true },
                new AgentLoopEvent { Type = "step_completed", Content = """{"runId":41,"stepIndex":0,"output":"ok"}""" },
                new AgentLoopEvent { Type = "done", Content = "计划已执行完" }
            }
        };
        var exec = new AgentFacadeRunExecutor(facade, orchestrator: orch);
        facade.AttachExecutor(exec);

        var session = facade.CreateSession(Req("ai-agent", mode: "planned"));
        var (_, body) = facade.PostMessage(session.Id, new PostAgentMessageRequest { Text = "整理目录" });
        Assert.DoesNotContain(facade.ReplayEvents(session.Id, 0), e => e.Type == "step.started");
        facade.ConfirmPlan(session.Id);

        var types = await DrainUntil(facade, session.Id, "run.completed");
        Assert.NotNull(orch.LastPlan);
        Assert.Equal("整理目录", orch.LastRequest?.TaskInput);
        Assert.True(orch.LastPlan!.Steps.Count >= 1);
        Assert.Contains("step.started", types);
        Assert.Contains("message.delta", types);
        Assert.Contains("tool.started", types);
        Assert.Contains("tool.completed", types);
        Assert.Contains("step.completed", types);
        Assert.Contains("message.completed", types);
        Assert.Equal(41, facade.GetExecution(session.Id, ((AcceptedRunResponse)body).RunId)?.NativeRunId);
        Assert.Equal("completed", facade.GetSession(session.Id)!.Session.Status);
    }

    [Fact]
    public void ConfirmPlan_DoesNotExecuteBeforeConfirm()
    {
        var facade = new AgentFacadeService();
        var orch = new FakeOrchestrator { Events = { new AgentLoopEvent { Type = "done", Content = "不该出现" } } };
        facade.AttachExecutor(new AgentFacadeRunExecutor(facade, orchestrator: orch));
        var session = facade.CreateSession(Req("ai-agent", mode: "planned"));
        facade.PostMessage(session.Id, new PostAgentMessageRequest { Text = "先出计划" });
        Assert.Null(orch.LastPlan);
        Assert.Equal("awaiting_plan", facade.GetSession(session.Id)!.Session.Status);
    }

    private static CreateAgentSessionRequest Req(string pluginId, string mode = "open") => new()
    {
        Mode = mode,
        Embed = new AgentEmbedOptions
        {
            Surface = "page",
            Context = new AgentEmbedContext { PluginId = pluginId }
        }
    };

    private static async Task<List<string>> DrainUntil(AgentFacadeService facade, string sessionId, string terminal)
    {
        var types = new List<string>();
        var after = 0L;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!types.Contains(terminal))
        {
            var ev = await facade.WaitEventAsync(sessionId, after, cts.Token);
            Assert.NotNull(ev);
            types.Add(ev!.Type);
            after = ev.Seq;
        }
        return types;
    }

    private sealed class FakeAgent : IAgent
    {
        public string SessionId { get; set; } = "s";
        public AgentOptions Options { get; set; } = new();
        public IInbox Inbox { get; } = new FakeInbox();
        public ForgeSelf.Abstractions.AgentStatus Status { get; set; } = ForgeSelf.Abstractions.AgentStatus.Idle;
        public List<TurnFrame> Frames { get; } = new();
        public IAsyncEnumerable<TurnFrame> RunAsync(CancellationToken ct = default) => AsAsync(Frames, ct);
        public Task CancelAsync(AgentCancelCause cause, bool keepInbox = false) => Task.CompletedTask;
        public Task WhenIdleAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeInbox : IInbox
    {
        public readonly List<string> Sent = new();
        public void Send(string sessionId, string content, InboxTarget target, MessageSource source, bool wakeup) => Sent.Add(content);
        public InboxBatch Claim(string sessionId, bool atTurnBoundary) => new();
        public IReadOnlyList<InboxItem> Peek(string sessionId) => Array.Empty<InboxItem>();
        public void Clear(string sessionId) { }
    }

    private sealed class StubRegistry : IAgentRegistry
    {
        private readonly IAgent _agent;
        public StubRegistry(IAgent agent) => _agent = agent;
        public Task<IAgent> GetOrCreateAsync(string sessionId, AgentOptions? options = null, CancellationToken ct = default)
        {
            if (_agent is FakeAgent fake) fake.SessionId = sessionId;
            return Task.FromResult(_agent);
        }
        public Task<IAgent?> TryGetAsync(string sessionId) => Task.FromResult<IAgent?>(_agent);
        public Task<bool> DisposeAsync(string sessionId, CancellationToken ct = default) => Task.FromResult(false);
    }

    private sealed class StubAgentService : IAIAgentService
    {
        public AgentOptions BuildAgentOptions(string? chatModelId = null, string? agentId = null, List<string>? enabledToolNames = null, List<string>? skillIds = null)
            => new() { ModelId = chatModelId ?? "", ToolAllowlist = enabledToolNames };
        public IAgent CreateAgent(string sessionId, AgentOptions options) => throw new NotSupportedException();
        public Task<string> ChatAsync(List<AIChatMessage> messages, bool enableTools = true) => throw new NotSupportedException();
        public IAsyncEnumerable<string> ChatStreamAsync(List<AIChatMessage> messages, bool enableTools = true, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<AIChatMessage>> ChatWithToolsAsync(List<AIChatMessage> messages, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<AgentLoopEvent> RunAgentLoopAsync(string sessionId, string? chatModelId = null, string? agentId = null, List<string>? enabledToolNames = null, List<string>? skillIds = null, bool enableTools = true, CancellationToken cancellationToken = default, List<IToolFunctionExtension>? extraTools = null) => throw new NotSupportedException();
        public Task<List<WorkflowRecommendationDto>> GetRecommendedWorkflowsAsync(string userMessage, int limit = 5) => throw new NotSupportedException();
        public Task<GenerateScriptResponse> GenerateScriptAsync(string language, string description, string? requirements = null) => throw new NotSupportedException();
        public Task<AnalyzeScriptErrorResponse> AnalyzeScriptErrorAsync(string language, string code, string errorMessage) => throw new NotSupportedException();
        public Task<SuggestScriptFixResponse> SuggestScriptFixAsync(string language, string code, string errorMessage) => throw new NotSupportedException();
        public Task<List<ScriptTemplate>> GetScriptTemplatesAsync(string? category = null) => throw new NotSupportedException();
    }

    private sealed class FakeOrchestrator : IRunOrchestratorService
    {
        public List<AgentLoopEvent> Events { get; } = new();
        public AgentPlan? LastPlan { get; private set; }
        public RunRequest? LastRequest { get; private set; }

        public IAsyncEnumerable<AgentLoopEvent> RunAsync(RunRequest request, CancellationToken ct) => AsAsync(Events, ct);
        public IAsyncEnumerable<AgentLoopEvent> ExecuteConfirmedPlanAsync(RunRequest request, AgentPlan plan, CancellationToken ct)
        {
            LastRequest = request;
            LastPlan = plan;
            return AsAsync(Events, ct);
        }
        public IAsyncEnumerable<AgentLoopEvent> ResumeAsync(long id, CancellationToken ct) => AsAsync(Events, ct);
        public Task<AgentRunDto?> RestartAsync(long id) => Task.FromResult<AgentRunDto?>(null);
        public Task<AgentRunDto?> CancelAsync(long id) => Task.FromResult<AgentRunDto?>(null);
        public Task<AgentRunDto?> InterveneAsync(long id, int stepIndex, InterveneRequest request) => Task.FromResult<AgentRunDto?>(null);
        public Task<AgentRunListResponse> ListRunsAsync(string? sessionId, AgentRunStatus? status, int page, int pageSize) => Task.FromResult(new AgentRunListResponse());
        public Task<AgentRunDto?> GetRunAsync(long id) => Task.FromResult<AgentRunDto?>(null);
        public Task<AgentRunDetailResponse?> GetRunDetailAsync(long id) => Task.FromResult<AgentRunDetailResponse?>(null);
    }

    private static async IAsyncEnumerable<T> AsAsync<T>(IEnumerable<T> items, [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return item;
        }
    }
}

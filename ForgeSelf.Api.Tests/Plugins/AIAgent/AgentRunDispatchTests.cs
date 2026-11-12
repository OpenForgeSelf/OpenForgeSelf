using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AIAgent;

/// <summary>
/// PILOT-079 AC1/AC2 门禁：IAgentRunDispatch 契约校验矩阵 + AgentRunDispatchService 派发行为。
/// orchestrator / agent 注册表用接口替身（不依赖真实 MAF 引擎与 DB）；
/// XCodeTestFixture 仅按 PC-014 规约挂载（本类不直查库），保证与全量同基座隔离。
/// </summary>
[Collection("XCode")]
public class AgentRunDispatchTests : IClassFixture<XCodeTestFixture>
{
    public AgentRunDispatchTests(XCodeTestFixture fixture) => _ = fixture;

    private const string Session = "imtask:s-test-1";

    // ---- AC1：Request 校验 + AgentRef 三态 ----

    [Fact]
    public async Task Submit_EmptyTaskInput_FailsWithChineseError()
    {
        var svc = CreateService(new FakeDispatchOrchestrator(), new FakeAgentRegistry());
        var outcome = await svc.SubmitAsync(new AgentRunDispatchRequest { TaskInput = "  ", SessionId = Session });
        Assert.False(outcome.Success);
        Assert.Contains("任务原文", outcome.Error);
    }

    [Fact]
    public async Task Submit_EmptySessionId_FailsWithChineseError()
    {
        var svc = CreateService(new FakeDispatchOrchestrator(), new FakeAgentRegistry());
        var outcome = await svc.SubmitAsync(new AgentRunDispatchRequest { TaskInput = "写个报告", SessionId = " " });
        Assert.False(outcome.Success);
        Assert.Contains("会话", outcome.Error);
    }

    [Fact]
    public async Task Submit_AgentRefById_Hits()
    {
        var registry = new FakeAgentRegistry();
        registry.Seed("agent.programmer", "程序员");
        var fake = new FakeDispatchOrchestrator();
        var svc = CreateService(fake, registry);

        var outcome = await svc.SubmitAsync(new AgentRunDispatchRequest
        {
            TaskInput = "整理文档",
            SessionId = Session,
            AgentRef = "agent.programmer",
        });
        await WaitDrainedAsync(fake);

        Assert.True(outcome.Success);
        Assert.Equal("agent.programmer", outcome.AgentId);
        Assert.Equal("程序员", outcome.AgentName);
    }

    [Fact]
    public async Task Submit_AgentRefByExactDisplayName_Hits()
    {
        var registry = new FakeAgentRegistry();
        registry.Seed("agent.programmer", "程序员");
        var fake = new FakeDispatchOrchestrator();
        var svc = CreateService(fake, registry);

        var outcome = await svc.SubmitAsync(new AgentRunDispatchRequest
        {
            TaskInput = "整理文档",
            SessionId = Session,
            AgentRef = "程序员",
        });
        await WaitDrainedAsync(fake);

        Assert.True(outcome.Success);
        Assert.Equal("agent.programmer", outcome.AgentId);
        // RunRequest.AgentId 收到的是解析后的 id，不是显示名
        Assert.Equal("agent.programmer", fake.RunRequests.Single().AgentId);
    }

    [Fact]
    public async Task Submit_AgentRefMiss_FailsWithCandidates()
    {
        var registry = new FakeAgentRegistry();
        registry.Seed("agent.programmer", "程序员");
        registry.Seed("agent.writer", "写手");
        var svc = CreateService(new FakeDispatchOrchestrator(), registry);

        var outcome = await svc.SubmitAsync(new AgentRunDispatchRequest
        {
            TaskInput = "整理文档",
            SessionId = Session,
            AgentRef = "不存在的",
        });

        Assert.False(outcome.Success);
        Assert.Contains("不存在的", outcome.Error);
        Assert.Contains("agent.programmer", outcome.Error);
        Assert.Contains("程序员", outcome.Error);
        Assert.Contains("agent.writer", outcome.Error);
    }

    // ---- AC2：派发行为 ----

    [Fact]
    public async Task Submit_ReturnsTaskKeyImmediately_RunIdNull()
    {
        var fake = new FakeDispatchOrchestrator();
        var svc = CreateService(fake, new FakeAgentRegistry());

        var outcome = await svc.SubmitAsync(new AgentRunDispatchRequest { TaskInput = "写个报告", SessionId = Session });

        Assert.True(outcome.Success);
        Assert.False(string.IsNullOrWhiteSpace(outcome.TaskKey));
        Assert.Null(outcome.RunId);           // 规划完成前 RunId 恒 null
        Assert.Equal("pending", outcome.Status);
        await WaitDrainedAsync(fake);
    }

    [Fact]
    public async Task Submit_TaskInputComposedWithApprovalNote()
    {
        var fake = new FakeDispatchOrchestrator();
        var svc = CreateService(fake, new FakeAgentRegistry());

        var outcome = await svc.SubmitAsync(new AgentRunDispatchRequest
        {
            TaskInput = "帮我重建索引",
            SessionId = Session,
            ChatModelId = "prov:m1",
        });
        await WaitDrainedAsync(fake);

        Assert.True(outcome.Success);
        var req = fake.RunRequests.Single();
        Assert.Equal(Session, req.SessionId);
        Assert.Equal("prov:m1", req.ChatModelId);
        Assert.StartsWith("帮我重建索引", req.TaskInput);
        Assert.Contains("审批", req.TaskInput);   // 审批策略说明已拼接（ComposePrompt 语义）
    }

    [Fact]
    public async Task Submit_DrivesToTerminal_FindReturnsTerminalAndResultText()
    {
        var fake = new FakeDispatchOrchestrator();
        fake.RunEvents.Add(Ev("step_started", RunIdPayload(42)));
        fake.RunEvents.Add(Ev("done", "任务已完成的终局文本"));
        var svc = CreateService(fake, new FakeAgentRegistry());

        var outcome = await svc.SubmitAsync(new AgentRunDispatchRequest { TaskInput = "写个报告", SessionId = Session });
        Assert.True(outcome.Success);
        var taskKey = outcome.TaskKey!;

        var snap = await FindEventuallyAsync(svc, taskKey, s => s.Terminal);
        Assert.NotNull(snap);
        Assert.Equal(42, snap!.RunId);
        Assert.Equal("completed", snap.Status);
        Assert.True(snap.Terminal);
        Assert.Equal("任务已完成的终局文本", snap.ResultText);
        Assert.NotNull(snap.EndedAt);
    }

    [Fact]
    public async Task Submit_StuckEvent_SnapshotStuckNotTerminalWithReason()
    {
        var fake = new FakeDispatchOrchestrator();
        fake.RunEvents.Add(Ev("step_started", RunIdPayload(7)));
        fake.RunEvents.Add(Ev("run_stuck", JsonSerializer.Serialize(new { runId = 7L, stepIndex = 1, reason = "等待 write_file 审批" })));
        var svc = CreateService(fake, new FakeAgentRegistry());

        var outcome = await svc.SubmitAsync(new AgentRunDispatchRequest { TaskInput = "写文件", SessionId = Session });
        var snap = await FindEventuallyAsync(svc, outcome.TaskKey!, s => s.Status == "stuck");

        Assert.NotNull(snap);
        Assert.Equal("stuck", snap!.Status);
        Assert.False(snap.Terminal);              // stuck 可续跑，不算终态
        Assert.Contains("write_file", snap.ResultText);
    }

    [Fact]
    public async Task Submit_SecondConcurrentForSameSession_Rejected()
    {
        var fake = new FakeDispatchOrchestrator();
        fake.RunEvents.Add(Ev("step_started", RunIdPayload(11)));
        fake.RunEvents.Add(Ev("done", "ok"));
        fake.RunGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var svc = CreateService(fake, new FakeAgentRegistry());

        var first = await svc.SubmitAsync(new AgentRunDispatchRequest { TaskInput = "任务一", SessionId = Session });
        Assert.True(first.Success);

        var second = await svc.SubmitAsync(new AgentRunDispatchRequest { TaskInput = "任务二", SessionId = Session });
 Assert.False(second.Success);
        Assert.Contains("已有任务在执行", second.Error);

        fake.RunGate.SetResult();                 // 释放，让后台驱动收尾
        await WaitDrainedAsync(fake);
    }

    [Fact]
    public async Task Submit_OpenRunInDbBlocksNewSubmit()
    {
        var fake = new FakeDispatchOrchestrator();
        fake.OpenRuns.Add(Dto(9, AgentRunStatus.Stuck, sessionId: Session));
        var svc = CreateService(fake, new FakeAgentRegistry());

        var outcome = await svc.SubmitAsync(new AgentRunDispatchRequest { TaskInput = "新任务", SessionId = Session });

        Assert.False(outcome.Success);
        Assert.Contains("已有任务", outcome.Error);
        Assert.Contains("#9", outcome.Error);
    }

    [Fact]
    public async Task Find_NumericKey_FallsBackToRunIdProjection()
    {
        var fake = new FakeDispatchOrchestrator();
        fake.Runs[77] = Dto(77, AgentRunStatus.Completed, receipt: "{\"summary\":\"收据摘要\"}", sessionId: Session);
        var svc = CreateService(fake, new FakeAgentRegistry());

        var snap = await svc.FindAsync("77");
        Assert.NotNull(snap);
        Assert.Equal(77, snap!.RunId);
        Assert.Equal("completed", snap.Status);
        Assert.True(snap.Terminal);
        Assert.Equal("收据摘要", snap.ResultText);

        // 非数字且无内存记录 → null
        Assert.Null(await svc.FindAsync("not-a-key"));
    }

    [Fact]
    public async Task Resume_NonStuckNonFailed_Rejected()
    {
        var fake = new FakeDispatchOrchestrator();
        fake.Runs[7] = Dto(7, AgentRunStatus.Running, sessionId: Session);
        var svc = CreateService(fake, new FakeAgentRegistry());

        var outcome = await svc.ResumeAsync("7", note: "测试");

        Assert.False(outcome.Success);
        Assert.Contains("不可续跑", outcome.Error);
        Assert.Empty(fake.ResumeCalls);
    }

    [Fact]
    public async Task Resume_Stuck_RestartsDriveToTerminal()
    {
        var fake = new FakeDispatchOrchestrator();
        fake.RunEvents.Add(Ev("step_started", RunIdPayload(7)));
        fake.RunEvents.Add(Ev("run_stuck", JsonSerializer.Serialize(new { runId = 7L, stepIndex = 1, reason = "等待审批" })));
        var svc = CreateService(fake, new FakeAgentRegistry());

        var submit = await svc.SubmitAsync(new AgentRunDispatchRequest { TaskInput = "写文件", SessionId = Session });
        Assert.True(submit.Success);
        await FindEventuallyAsync(svc, submit.TaskKey!, s => s.Status == "stuck");

        fake.Runs[7] = Dto(7, AgentRunStatus.Stuck, sessionId: Session);
        fake.ResumeEvents.Add(Ev("step_started", RunIdPayload(7)));
        fake.ResumeEvents.Add(Ev("done", "续跑后的终局文本"));

        var resume = await svc.ResumeAsync(submit.TaskKey!, note: "imgateway:wecom 经 IM 决策");
        Assert.True(resume.Success);
        Assert.Equal(7, resume.RunId);
        Assert.Equal("running", resume.Status);

        var snap = await FindEventuallyAsync(svc, submit.TaskKey!, s => s.Terminal);
        Assert.NotNull(snap);
        Assert.Equal("completed", snap!.Status);
        Assert.Equal("续跑后的终局文本", snap.ResultText);
        Assert.Contains(7, fake.ResumeCalls);
    }

    [Fact]
    public async Task Resume_UnknownTaskKey_Fails()
    {
        var fake = new FakeDispatchOrchestrator();
        var svc = CreateService(fake, new FakeAgentRegistry());

        var outcome = await svc.ResumeAsync("deadbeef000000000000000000000000", note: null);

        Assert.False(outcome.Success);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Error));
        Assert.Empty(fake.ResumeCalls);
    }

    [Fact]
    public async Task Resume_NumericKey_RestoresSessionFromDb()
    {
        var fake = new FakeDispatchOrchestrator();
        fake.Runs[8] = Dto(8, AgentRunStatus.Failed, sessionId: "imtask:s-restored", stuckReason: "上游 500");
        fake.ResumeEvents.Add(Ev("step_started", RunIdPayload(8)));
        fake.ResumeEvents.Add(Ev("done", "恢复后的终局"));
        var svc = CreateService(fake, new FakeAgentRegistry());

        var resume = await svc.ResumeAsync("8", note: "宿主重启后人工续跑");
        Assert.True(resume.Success);
        Assert.Equal(8, resume.RunId);

        var snap = await FindEventuallyAsync(svc, "8", s => s.Terminal);
        Assert.NotNull(snap);
        Assert.Equal("completed", snap!.Status);
        Assert.Contains(8, fake.ResumeCalls);
    }

    // ---- 替身与助手 ----

    private static AgentRunDispatchService CreateService(FakeDispatchOrchestrator fake, FakeAgentRegistry registry)
        => new(fake, registry);

    private static AgentLoopEvent Ev(string type, string? content = null) => new() { Type = type, Content = content };

    private static string RunIdPayload(long runId) => JsonSerializer.Serialize(new { runId });

    private static AgentRunDto Dto(long id, AgentRunStatus status, string? stuckReason = null,
        string? receipt = null, string? sessionId = null, string? agentName = "程序员") => new()
    {
        Id = id,
        Status = status,
        StuckReason = stuckReason,
        ResultReceipt = receipt,
        SessionId = sessionId,
        AgentId = "agent.programmer",
        AgentName = agentName,
    };

    /// <summary>等待后台驱动事件被消费完（替身流走完即触发 Completed）。</summary>
    private static async Task WaitDrainedAsync(FakeDispatchOrchestrator fake, int timeoutMs = 5000)
    {
        var finished = await Task.WhenAny(fake.Completed.Task, Task.Delay(timeoutMs));
        Assert.Equal(TaskStatus.RanToCompletion, finished.Status);
    }

    /// <summary>轮询快照直到谓词命中（后台驱动落内存记录与断言之间无同步栅栏，用短轮询收口）。</summary>
    private static async Task<AgentRunDispatchSnapshot?> FindEventuallyAsync(
        AgentRunDispatchService svc, string taskKey, Func<AgentRunDispatchSnapshot, bool> predicate, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var snap = await svc.FindAsync(taskKey);
            if (snap != null && predicate(snap)) return snap;
            await Task.Delay(20);
        }
        return null;
    }

    /// <summary>编排器替身：RunAsync/ResumeAsync 出脚本化事件流，GetRunAsync/ListRunsAsync 出预制 DTO。</summary>
    internal sealed class FakeDispatchOrchestrator : IRunOrchestratorService
    {
        public List<AgentLoopEvent> RunEvents { get; } = new();
        public List<AgentLoopEvent> ResumeEvents { get; } = new();
        public List<RunRequest> RunRequests { get; } = new();
        public List<long> ResumeCalls { get; } = new();
        public Dictionary<long, AgentRunDto> Runs { get; } = new();
        public List<AgentRunDto> OpenRuns { get; } = new();

        /// <summary>非空时 RunAsync 先等它（制造「在途未终态」窗口，测并发闸）。</summary>
        public TaskCompletionSource? RunGate { get; set; }

        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async IAsyncEnumerable<AgentLoopEvent> RunAsync(
            RunRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            RunRequests.Add(request);
            if (RunGate != null) await RunGate.Task;
            foreach (var ev in RunEvents) yield return ev;
            Completed.TrySetResult();
        }

        public async IAsyncEnumerable<AgentLoopEvent> ResumeAsync(
            long id, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            ResumeCalls.Add(id);
            foreach (var ev in ResumeEvents) yield return ev;
            Completed.TrySetResult();
        }

        public Task<AgentRunDto?> GetRunAsync(long id)
            => Task.FromResult(Runs.TryGetValue(id, out var dto) ? dto : null);

        public Task<AgentRunListResponse> ListRunsAsync(string? sessionId, AgentRunStatus? status, int page, int pageSize)
            => Task.FromResult(new AgentRunListResponse
            {
                Success = true,
                Items = OpenRuns.Where(r => sessionId == null || r.SessionId == sessionId).ToList(),
                Total = OpenRuns.Count,
                Page = page,
                PageSize = pageSize,
            });

        public Task<AgentRunDto?> CancelAsync(long id) => throw new NotSupportedException();
        public Task<AgentRunDto?> InterveneAsync(long id, int stepIndex, InterveneRequest request) => throw new NotSupportedException();
        public Task<AgentRunDto?> RestartAsync(long id) => throw new NotSupportedException();
        public Task<AgentRunDetailResponse?> GetRunDetailAsync(long id) => throw new NotSupportedException();
    }

    /// <summary>Agent 注册表替身：内存字典，无 DB。</summary>
    internal sealed class FakeAgentRegistry : IAgentRegistryService
    {
        private readonly Dictionary<string, AgentDefinition> _agents = new(StringComparer.OrdinalIgnoreCase);

        public void Seed(string id, string name)
            => _agents[id] = new AgentDefinition { Id = id, Name = name, IsEnabled = true };

        public AgentDefinition? GetAgent(string agentId) => _agents.TryGetValue(agentId, out var a) ? a : null;
        public List<AgentDefinition> GetAllAgents() => _agents.Values.Where(a => a.IsEnabled).OrderBy(a => a.SortOrder).ToList();

        public void RegisterAgent(AgentDefinition agent) => throw new NotSupportedException();
        public void UnregisterAgent(string agentId) => throw new NotSupportedException();
        public List<AgentDefinition> GetAgentsByType(AgentType type) => throw new NotSupportedException();
        public List<AgentDefinition> FindAgentsByCapability(string capability) => throw new NotSupportedException();
        public AgentDefinition? GetBestAgentForTask(string taskDescription, List<string> requiredCapabilities) => throw new NotSupportedException();
        public AgentDefinition CreateAgent(AgentDefinition agent) => throw new NotSupportedException();
        public AgentDefinition? UpdateAgent(string agentId, AgentDefinition agent) => throw new NotSupportedException();
        public bool DeleteAgent(string agentId) => throw new NotSupportedException();
    }
}

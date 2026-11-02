using System.Text.Json;
using ForgeSelf.Abstractions;
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
/// B2② 门禁：Run 级 Verify 闸门。
/// ① <see cref="RunVerifyService"/> 确定性断言（file_exists / output_contains / files_changed_not_empty，
///    未知类型 fail-safe）；② 计划任务编排器收束闸门——ExitCriteria 配置时先 Verify 再 Completed，
///    未过置 Stuck（BR-2：不验证不放行）；③ 未配置 = 现状行为不变（无 Verify 阶段）。
/// </summary>
[Collection("XCode")]
public class RunVerifyGateTests : IClassFixture<XCodeTestFixture>
{
    public RunVerifyGateTests(XCodeTestFixture fixture)
    {
        _ = fixture;
        AgentRun.Meta.Cache.Clear("gate reset");
        AgentStepRun.Meta.Cache.Clear("gate reset");
        AgentRun.Meta.Cache.Expire = 0;
        AgentStepRun.Meta.Cache.Expire = 0;
    }

    // ==================== RunVerifyService 单元（确定性断言） ====================

    [Fact]
    public async Task FileExists_RelativePath_InWorkspace_Passes()
    {
        var root = Directory.CreateTempSubdirectory("b2verify-").FullName;
        try
        {
            var target = Path.Combine(root, "out", "result.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await File.WriteAllTextAsync(target, "demo");

            var svc = new RunVerifyService(new StubWorkspace(root));
            var outcome = await svc.VerifyAsync(
                [new RunExitCriterion("out/result.txt", "file_exists")],
                new RunVerifyInputs("产出", []));

            Assert.Equal(RunVerifyStatus.Passed, outcome.Status);
            Assert.All(outcome.Items, i => Assert.True(i.Passed));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // ---- B2② 走查实证（2026-11-02）：句式描述判据的断言字面量必须取 Value，而非整句 Text ----

    [Fact]
    public async Task OutputContains_SentenceTextWithValueLiteral_UsesValueAsNeedle()
    {
        var svc = new RunVerifyService(null);
        var outcome = await svc.VerifyAsync(
            [new RunExitCriterion("产出包含「验收通过」", "output_contains", Value: "验收通过")],
            new RunVerifyInputs("验收通过", []));

        var item = Assert.Single(outcome.Items);
        Assert.True(item.Passed);
        Assert.StartsWith("产出包含", item.Detail); // needle=「验收通过」，不是整句描述
    }

    [Fact]
    public async Task OutputContains_BareTextWithoutValue_KeepsTextAsNeedle()
    {
        var svc = new RunVerifyService(null);
        var outcome = await svc.VerifyAsync(
            [new RunExitCriterion("验收通过", "output_contains")],
            new RunVerifyInputs("一句话：验收通过", []));

        Assert.Equal(RunVerifyStatus.Passed, outcome.Status);
    }

    [Fact]
    public async Task FileExists_SentenceTextWithValuePath_UsesValueAsTarget()
    {
        var root = Directory.CreateTempSubdirectory("b2verify-").FullName;
        try
        {
            var target = Path.Combine(root, "out", "r.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await File.WriteAllTextAsync(target, "x");

            var svc = new RunVerifyService(new StubWorkspace(root));
            var outcome = await svc.VerifyAsync(
                [new RunExitCriterion("产出文件 out/r.txt 已生成", "file_exists", Value: "out/r.txt")],
                new RunVerifyInputs("产出", []));

            Assert.Equal(RunVerifyStatus.Passed, outcome.Status);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FileExists_AbsolutePathOutsideWorkspace_Fails()
    {
        var root = Directory.CreateTempSubdirectory("b2verify-").FullName;
        try
        {
            var svc = new RunVerifyService(new StubWorkspace(root));
            var outcome = await svc.VerifyAsync(
                [new RunExitCriterion(@"C:\Windows\notepad.exe", "file_exists")],
                new RunVerifyInputs("产出", []));

            Assert.Equal(RunVerifyStatus.Failed, outcome.Status);
            Assert.Contains("越界", outcome.Items[0].Detail);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FileExists_NoWorkspace_FailsWithReason()
    {
        var svc = new RunVerifyService(null);
        var outcome = await svc.VerifyAsync(
            [new RunExitCriterion("a.txt", "file_exists")],
            new RunVerifyInputs("产出", []));

        Assert.Equal(RunVerifyStatus.Failed, outcome.Status);
        Assert.Contains("工作区未设置", outcome.Items[0].Detail);
    }

    [Fact]
    public async Task OutputContains_MatchesCaseInsensitive()
    {
        var svc = new RunVerifyService(null);
        var ok = await svc.VerifyAsync(
            [new RunExitCriterion("已写入 3 个文件", "output_contains")],
            new RunVerifyInputs("完成：已写入 3 个文件并读回校验", []));
        Assert.Equal(RunVerifyStatus.Passed, ok.Status);

        var miss = await svc.VerifyAsync(
            [new RunExitCriterion("已写入 5 个文件", "output_contains")],
            new RunVerifyInputs("完成：已写入 3 个并读回验", []));
        Assert.Equal(RunVerifyStatus.Failed, miss.Status);
    }

    [Fact]
    public async Task FilesChangedNotEmpty_EmptyAndNonEmpty()
    {
        var svc = new RunVerifyService(null);
        var empty = await svc.VerifyAsync(
            [new RunExitCriterion("本次必须有文件改动", "files_changed_not_empty")],
            new RunVerifyInputs("产出", []));
        Assert.Equal(RunVerifyStatus.Failed, empty.Status);

        var filled = await svc.VerifyAsync(
            [new RunExitCriterion("本次必须有文件改动", "files_changed_not_empty")],
            new RunVerifyInputs("产出", ["a.txt"]));
        Assert.Equal(RunVerifyStatus.Passed, filled.Status);
    }

    [Fact]
    public async Task UnknownAssertionType_FailsSafe_WithReason()
    {
        var svc = new RunVerifyService(null);
        var outcome = await svc.VerifyAsync(
            [new RunExitCriterion("任意", "command_pass")],
            new RunVerifyInputs("产出", ["a.txt"]));

        Assert.Equal(RunVerifyStatus.Failed, outcome.Status);
        Assert.Contains("未知断言类型", outcome.Items[0].Detail);
    }

    [Fact]
    public async Task MixedCriteria_AllMustPass()
    {
        var svc = new RunVerifyService(null);
        var outcome = await svc.VerifyAsync(
        [
            new RunExitCriterion("完成标记", "output_contains"),
            new RunExitCriterion("必须有文件改动", "files_changed_not_empty")
        ], new RunVerifyInputs("完成标记已出现", ["a.txt"]));
        Assert.Equal(RunVerifyStatus.Passed, outcome.Status);

        var oneFails = await svc.VerifyAsync(
        [
            new RunExitCriterion("完成标记", "output_contains"),
            new RunExitCriterion("必须有文件改动", "files_changed_not_empty")
        ], new RunVerifyInputs("完成标记已出现", []));
        Assert.Equal(RunVerifyStatus.Failed, oneFails.Status);
    }

    [Fact]
    public async Task VerifierAbsent_FailsSafe()
    {
        var absent = RunVerifyService.VerifierAbsent(2);
        Assert.Equal(RunVerifyStatus.Failed, absent.Status);
        Assert.Single(absent.Items);
        Assert.False(absent.Items[0].Passed);
    }

    // ==================== 编排器收束闸门（计划任务 Run） ====================

    private static AgentPlan SingleStepPlan()
        => new()
        {
            Goal = "写文件并读回校验",
            Steps = { new AgentPlanStep { Id = "s1", Name = "写入文件", Objective = "把内容写入目标文件", ExpectedOutput = "写入结果" } }
        };

    private static (RunOrchestratorService Orch, InMemorySessionStore Store) BuildOrchestrator(
        ScriptedProvider provider, IRunVerifyService? runVerify)
    {
        var store = new InMemorySessionStore();
        var inbox = new InMemoryInbox();
        var ai = new Mock<IAIAgentService>();
        ai.Setup(m => m.CreateAgent(It.IsAny<string>(), It.IsAny<AgentOptions>()))
            .Returns((string sid, AgentOptions options) => new ReactLoopAgent(sid, options, new AgentTurnRuntime
            {
                Store = store, Provider = provider, ModelId = "fake-model", ToolExecutor = new FakeToolRegistry(),
                SystemPrompt = null, ToolDefinitions = null, Events = null, Inbox = inbox
            }));
        var planner = new Mock<IPlanGeneratorService>();
        planner.Setup(m => m.GeneratePlanAsync(
                It.IsAny<AgentRun>(), It.IsAny<long>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SingleStepPlan());
        var orch = new RunOrchestratorService(
            planner.Object, ai.Object, inbox: inbox,
            stepTimeout: TimeSpan.FromSeconds(10), runVerify: runVerify, sessionStore: store);
        return (orch, store);
    }

    private static ScriptedProvider WriteFileScript()
    {
        var provider = new ScriptedProvider();
        provider.Enqueue(Script.ToolCall("c1", "write_file", """{"path":"D:\\tmp\\x.txt","content":"hi"}"""));
        provider.Enqueue(Script.ToolCall("c2", "complete_step", """{"output":"文件已写入并读回：hi"}"""));
        return provider;
    }

    // AC-3：ExitCriteria 全过 → Completed + VerifyStatus=Passed + run/verify 事件 + 收据冻结
    [Fact]
    public async Task VerifyGate_AllPassed_RunCompletedWithReceipt()
    {
        var provider = WriteFileScript();
        var (orch, store) = BuildOrchestrator(provider, new RunVerifyService(null)); // files_changed_not_empty 用真实校验器（无需工作区）

        var events = new List<AgentLoopEvent>();
        await foreach (var ev in orch.RunAsync(new RunRequest
        {
            AgentId = "agent.generalist", SessionId = null, TaskInput = "写文件并读回校验",
            ExitCriteria = [new RunExitCriterion("本次必须有文件改动", "files_changed_not_empty")]
        }, CancellationToken.None))
        {
            events.Add(ev);
        }

        Assert.Contains(events, e => e.Type == "done");
        Assert.DoesNotContain(events, e => e.Type == "run_stuck");

        var runId = JsonDocument.Parse(events.First(e => e.Type == "step_started").Content!)
            .RootElement.GetProperty("runId").GetInt64();
        var run = AgentRun.FindById(runId)!;
        Assert.Equal((int)AgentRunStatus.Completed, run.Status);
        Assert.Equal((int)RunVerifyStatus.Passed, run.VerifyStatus);
        Assert.False(string.IsNullOrWhiteSpace(run.VerifyReport));
        using (var report = JsonDocument.Parse(run.VerifyReport!))
        {
            Assert.Equal("Passed", report.RootElement.GetProperty("status").GetString());
        }

        // B2③：收据冻结（文件清单非空 + Verify 结论随行；ReceiptJsonOptions 为 camelCase）
        var receipt = JsonDocument.Parse(run.ResultReceipt!).RootElement;
        Assert.Equal("Completed", receipt.GetProperty("status").GetString());
        Assert.True(receipt.GetProperty("filesChanged").GetArrayLength() > 0);
        Assert.Equal(1, receipt.GetProperty("toolCallCount").GetInt32());

        // B2②：run/verify 事件进会话日志（sessionId 空 → run:{id} 会话）
        var runSession = $"run:{runId}";
        var verifyEvents = store.Replay(runSession).OfType<RunVerifyEvent>().ToList();
        Assert.Single(verifyEvents);
        Assert.Equal(RunVerifyStatus.Passed.ToString(), verifyEvents[0].Status);
    }

    // AC-4：ExitCriteria 未过 → Stuck 保留现场 + VerifyStatus=Failed + 收据如实冻结
    [Fact]
    public async Task VerifyGate_Failed_RunStuckNotCompleted()
    {
        var provider = WriteFileScript();
        var (orch, store) = BuildOrchestrator(provider, new RunVerifyService(null));

        var events = new List<AgentLoopEvent>();
        await foreach (var ev in orch.RunAsync(new RunRequest
        {
            AgentId = "agent.generalist", SessionId = null, TaskInput = "写文件并读回校验",
            ExitCriteria = [new RunExitCriterion("不存在的完成标记XYZ", "output_contains")]
        }, CancellationToken.None))
        {
            events.Add(ev);
        }

        Assert.DoesNotContain(events, e => e.Type == "done");
        Assert.Contains(events, e => e.Type == "run_stuck");

        var runId = JsonDocument.Parse(events.First(e => e.Type == "step_started").Content!)
            .RootElement.GetProperty("runId").GetInt64();
        var run = AgentRun.FindById(runId)!;
        Assert.Equal((int)AgentRunStatus.Stuck, run.Status);
        Assert.Equal((int)RunVerifyStatus.Failed, run.VerifyStatus);
        Assert.Contains("出口验收未通过", run.StuckReason);

        var receipt = JsonDocument.Parse(run.ResultReceipt!).RootElement;
        Assert.Equal("Stuck", receipt.GetProperty("status").GetString());

        var runSession = $"run:{runId}";
        var verifyEvents = store.Replay(runSession).OfType<RunVerifyEvent>().ToList();
        Assert.Single(verifyEvents);
        Assert.Equal(RunVerifyStatus.Failed.ToString(), verifyEvents[0].Status);
    }

    // AC-6：未配置 ExitCriteria → 无 Verify 阶段（现状行为），收据仍冻结
    [Fact]
    public async Task NoExitCriteria_BehaviorUnchanged_NoVerify()
    {
        var provider = WriteFileScript();
        var (orch, store) = BuildOrchestrator(provider, new RunVerifyService(null));

        var events = new List<AgentLoopEvent>();
        await foreach (var ev in orch.RunAsync(new RunRequest
        {
            AgentId = "agent.generalist", SessionId = null, TaskInput = "写文件并读回校验"
        }, CancellationToken.None))
        {
            events.Add(ev);
        }

        Assert.Contains(events, e => e.Type == "done");

        var runId = JsonDocument.Parse(events.First(e => e.Type == "step_started").Content!)
            .RootElement.GetProperty("runId").GetInt64();
        var run = AgentRun.FindById(runId)!;
        Assert.Equal((int)AgentRunStatus.Completed, run.Status);
        Assert.Equal((int)RunVerifyStatus.None, run.VerifyStatus);
        Assert.True(string.IsNullOrWhiteSpace(run.VerifyReport));
        Assert.True(string.IsNullOrWhiteSpace(run.ExitCriteria));
        Assert.False(string.IsNullOrWhiteSpace(run.ResultReceipt), "收据与验收解耦：无验收也要有结果收据");

        Assert.Empty(store.Replay($"run:{runId}").OfType<RunVerifyEvent>());
    }
}

/// <summary>IProjectWorkspaceService 最小替身：只支持 ProjectRoot + ResolveSafePath。</summary>
internal sealed class StubWorkspace : IProjectWorkspaceService
{
    public StubWorkspace(string root) { ProjectRoot = root; }

    public bool IsProjectSet => !string.IsNullOrWhiteSpace(ProjectRoot);
    public string? ProjectRoot { get; }
    public string? DefaultRoot => ProjectRoot;

    public bool TrySetProjectRoot(string root, out string? error) { error = "stub"; return false; }

    public string ResolveSafePath(string relativePath) => Path.Combine(ProjectRoot!, relativePath);

    public List<ProjectFileEntry> ListEntries(string relativePath) => [];
    public string ReadFile(string relativePath) => string.Empty;
    public void WriteFile(string relativePath, string content) { }
    public void DeleteFile(string relativePath) { }
}

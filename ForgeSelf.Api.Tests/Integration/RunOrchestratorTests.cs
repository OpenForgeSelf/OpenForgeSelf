using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Entities;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Api.Plugins.AIAgent.Services.ToolFunctions;
using Moq;
using XCode;
using XCode.DataAccessLayer;
using AbMsg = ForgeSelf.Abstractions.AIChatMessage;

namespace ForgeSelf.Api.Tests.Integration;

/// <summary>
/// 计划驱动执行引擎核心编排逻辑（tasks.md T009-T011）：
/// StepRunLoopService 出口事件消费（complete_step/request_help/迭代超限）用 mock IAIAgentService 驱动；
/// RunOrchestratorService 的介入/恢复/重开/取消走真实 XCode 双表（[Collection("XCode")] 独立临时库）。
/// </summary>
[Collection("XCode")]
public class RunOrchestratorTests
{
    private readonly string _dbDir;

    public RunOrchestratorTests()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfRunOrch_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dbDir);

        DAL.AddConnStr("AIAgent", $"Data Source={Path.Combine(_dbDir, "AIAgent.db")}", null, "SQLite");
        EntityFactory.InitConnection("AIAgent");

        AgentRun.Meta.Cache.Clear("test reset");
        AgentStepRun.Meta.Cache.Clear("test reset");
        AgentRun.Meta.Cache.Expire = 0;
        AgentStepRun.Meta.Cache.Expire = 0;
    }

    private static AgentRun NewRun(AgentRunStatus status = AgentRunStatus.Running)
    {
        var run = new AgentRun
        {
            AgentId = "agent.generalist",
            AgentName = "通用",
            TaskInput = "统计项目文件行数",
            Status = (int)status,
            StepCount = 1,
            CurrentStepIndex = 0,
        };
        run.Insert();
        return run;
    }

    private static AgentStepRun NewStep(AgentRun run, AgentStepStatus status, int index = 0)
    {
        var step = new AgentStepRun
        {
            RunId = run.Id,
            StepIndex = index,
            StepId = "s1",
            Name = "步骤一",
            Objective = "完成第一件事",
            Status = (int)status,
        };
        step.Insert();
        return step;
    }

    private static AgentPlan OneStepPlan()
    {
        return new AgentPlan
        {
            Goal = "统计项目文件行数",
            Steps =
            {
                new AgentPlanStep { Id = "s1", Name = "统计行数", Objective = "统计行数", ExpectedOutput = "行数结果" }
            }
        };
    }

    // 备注（B7）：原「StepRunLoopService 出口逻辑（mock 驱动）」3 条直测
    // （CompleteStepToolCall_MarksCompletedAndYieldsExitEvent / RequestHelpToolCall_MarksStuckAndYieldsExitEvent /
    // LoopEndWithoutExit_MarksStuck）随 StepRunLoopService 删除而退役——其语义已有状态机等价归属：
    // 出口工具「声明即出口」→ ReactLoopExitAndSuspendTests.ExitTool_DeclaredNotExecuted_StepEndsExitTool；
    // 出口判定→落库/SSE → PlannedRunUnifiedLoopGates + OrchestratorBehaviorSnapshotTests。
    // 遵守最小改写纪律：仅删除契约已不存在的用例，不新增断言。

    #region RunOrchestratorService 介入（skip/override）

    [Fact]
    public async Task Intervene_Skip_MarksStepSkippedAndAdvancesRun()
    {
        var orchestrator = new RunOrchestratorService(null!, null!);
        var run = NewRun(AgentRunStatus.Stuck);
        NewStep(run, AgentStepStatus.Stuck);

        var dto = await orchestrator.InterveneAsync(run.Id, 0, new InterveneRequest { Action = "skip", Note = "此步可跳过" });

        dto.Should().NotBeNull();
        var step = AgentStepRun.FindAllByRunIdAndStepIndex(run.Id, 0).First();
        ((AgentStepStatus)step.Status).Should().Be(AgentStepStatus.Skipped);
        step.HumanNote.Should().Be("此步可跳过");
        var reloaded = AgentRun.FindById(run.Id)!;
        reloaded.CurrentStepIndex.Should().Be(1);
        ((AgentRunStatus)reloaded.Status).Should().Be(AgentRunStatus.Running);
    }

    [Fact]
    public async Task Intervene_Override_CompletesStepWithOutput()
    {
        var orchestrator = new RunOrchestratorService(null!, null!);
        var run = NewRun(AgentRunStatus.Stuck);
        NewStep(run, AgentStepStatus.Stuck);

        await orchestrator.InterveneAsync(run.Id, 0, new InterveneRequest
        {
            Action = "override",
            Output = "人工补位产出",
            Note = "手动填写"
        });

        var step = AgentStepRun.FindAllByRunIdAndStepIndex(run.Id, 0).First();
        ((AgentStepStatus)step.Status).Should().Be(AgentStepStatus.Completed);
        step.OutputJson.Should().Be("人工补位产出");
        step.HumanOverride.Should().Be("人工补位产出");
    }

    [Fact]
    public async Task Intervene_IllegalAction_Throws()
    {
        var orchestrator = new RunOrchestratorService(null!, null!);
        var run = NewRun(AgentRunStatus.Stuck);
        NewStep(run, AgentStepStatus.Stuck);

        var act = async () => await orchestrator.InterveneAsync(run.Id, 0, new InterveneRequest { Action = "bogus" });
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Intervene_TerminalRun_Throws()
    {
        var orchestrator = new RunOrchestratorService(null!, null!);
        var run = NewRun(AgentRunStatus.Completed);

        var act = async () => await orchestrator.InterveneAsync(run.Id, 0, new InterveneRequest { Action = "skip" });
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Intervene_RunningStep_Throws()
    {
        var orchestrator = new RunOrchestratorService(null!, null!);
        var run = NewRun(AgentRunStatus.Running);
        NewStep(run, AgentStepStatus.Running);

        var act = async () => await orchestrator.InterveneAsync(run.Id, 0, new InterveneRequest { Action = "skip" });
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region Resume / Restart / Cancel

    [Fact]
    public async Task Resume_NonExistentRun_YieldsError()
    {
        var orchestrator = new RunOrchestratorService(null!, null!);

        AgentLoopEvent? first = null;
        await foreach (var ev in orchestrator.ResumeAsync(999999, CancellationToken.None)) { first = ev; break; }

        first.Should().NotBeNull();
        first!.Type.Should().Be("error");
        first.Content.Should().Contain("不存在");
    }

    [Fact]
    public async Task Resume_NonStuckStatus_YieldsError()
    {
        var orchestrator = new RunOrchestratorService(null!, null!);
        var run = NewRun(AgentRunStatus.Running);
        run.PlanJson = System.Text.Json.JsonSerializer.Serialize(OneStepPlan());
        run.Update();

        AgentLoopEvent? first = null;
        await foreach (var ev in orchestrator.ResumeAsync(run.Id, CancellationToken.None)) { first = ev; break; }

        first.Should().NotBeNull();
        first!.Type.Should().Be("error");
        first.Content.Should().Contain("不可恢复");
    }

    [Fact]
    public async Task Restart_SamePlan_CreatesNewRun()
    {
        var orchestrator = new RunOrchestratorService(null!, null!);
        var run = NewRun(AgentRunStatus.Stuck);
        var planJson = System.Text.Json.JsonSerializer.Serialize(OneStepPlan());
        run.PlanJson = planJson;
        run.Update();

        var dto = await orchestrator.RestartAsync(run.Id);

        dto.Should().NotBeNull();
        dto!.Id.Should().NotBe(run.Id);
        dto.PlanJson.Should().Be(planJson);
        dto.Status.Should().Be(AgentRunStatus.Running);
    }

    [Fact]
    public async Task Cancel_CompletedRun_Throws()
    {
        var orchestrator = new RunOrchestratorService(null!, null!);
        var run = NewRun(AgentRunStatus.Completed);

        var act = async () => await orchestrator.CancelAsync(run.Id);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Cancel_StuckRun_BecomesCancelled()
    {
        var orchestrator = new RunOrchestratorService(null!, null!);
        var run = NewRun(AgentRunStatus.Stuck);

        var dto = await orchestrator.CancelAsync(run.Id);

        dto.Should().NotBeNull();
        dto!.Status.Should().Be(AgentRunStatus.Cancelled);
    }

    #endregion
}

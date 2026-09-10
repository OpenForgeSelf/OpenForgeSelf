using ForgeSelf.Api.Plugins.AIAgent.Models;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// 计划驱动执行状态机测试（tasks.md T006）：合法/非法迁移判定。
/// </summary>
public class AgentRunStatusTests
{
    // ---------- AgentRunStatus ----------

    [Theory]
    [InlineData(AgentRunStatus.Pending, AgentRunStatus.Planning)]
    [InlineData(AgentRunStatus.Pending, AgentRunStatus.Cancelled)]
    [InlineData(AgentRunStatus.Planning, AgentRunStatus.Running)]
    [InlineData(AgentRunStatus.Planning, AgentRunStatus.Failed)]
    [InlineData(AgentRunStatus.Planning, AgentRunStatus.Cancelled)]
    [InlineData(AgentRunStatus.Running, AgentRunStatus.Completed)]
    [InlineData(AgentRunStatus.Running, AgentRunStatus.Stuck)]
    [InlineData(AgentRunStatus.Running, AgentRunStatus.Failed)]
    [InlineData(AgentRunStatus.Running, AgentRunStatus.Cancelled)]
    [InlineData(AgentRunStatus.Stuck, AgentRunStatus.Running)]
    [InlineData(AgentRunStatus.Stuck, AgentRunStatus.Cancelled)]
    [InlineData(AgentRunStatus.Failed, AgentRunStatus.Running)]
    [InlineData(AgentRunStatus.Failed, AgentRunStatus.Cancelled)]
    public void Run_ValidTransitions_Allowed(AgentRunStatus from, AgentRunStatus to)
    {
        RunStateMachine.CanTransitionRun(from, to).Should().BeTrue();
    }

    [Theory]
    [InlineData(AgentRunStatus.Completed, AgentRunStatus.Planning)]
    [InlineData(AgentRunStatus.Completed, AgentRunStatus.Running)]
    [InlineData(AgentRunStatus.Completed, AgentRunStatus.Stuck)]
    [InlineData(AgentRunStatus.Cancelled, AgentRunStatus.Running)]
    [InlineData(AgentRunStatus.Cancelled, AgentRunStatus.Completed)]
    [InlineData(AgentRunStatus.Pending, AgentRunStatus.Completed)]
    [InlineData(AgentRunStatus.Pending, AgentRunStatus.Stuck)]
    [InlineData(AgentRunStatus.Planning, AgentRunStatus.Completed)]
    [InlineData(AgentRunStatus.Planning, AgentRunStatus.Stuck)]
    [InlineData(AgentRunStatus.Stuck, AgentRunStatus.Completed)]
    [InlineData(AgentRunStatus.Stuck, AgentRunStatus.Planning)]
    [InlineData(AgentRunStatus.Running, AgentRunStatus.Planning)]
    public void Run_InvalidTransitions_Rejected(AgentRunStatus from, AgentRunStatus to)
    {
        RunStateMachine.CanTransitionRun(from, to).Should().BeFalse();
    }

    // ---------- AgentStepStatus ----------

    [Theory]
    [InlineData(AgentStepStatus.Pending, AgentStepStatus.Running)]
    [InlineData(AgentStepStatus.Pending, AgentStepStatus.Skipped)]
    [InlineData(AgentStepStatus.Pending, AgentStepStatus.Failed)]
    [InlineData(AgentStepStatus.Running, AgentStepStatus.Completed)]
    [InlineData(AgentStepStatus.Running, AgentStepStatus.Stuck)]
    [InlineData(AgentStepStatus.Running, AgentStepStatus.Failed)]
    [InlineData(AgentStepStatus.Stuck, AgentStepStatus.Running)]
    [InlineData(AgentStepStatus.Stuck, AgentStepStatus.Skipped)]
    [InlineData(AgentStepStatus.Stuck, AgentStepStatus.Completed)]
    [InlineData(AgentStepStatus.Stuck, AgentStepStatus.Failed)]
    [InlineData(AgentStepStatus.Failed, AgentStepStatus.Running)]
    public void Step_ValidTransitions_Allowed(AgentStepStatus from, AgentStepStatus to)
    {
        RunStateMachine.CanTransitionStep(from, to).Should().BeTrue();
    }

    [Theory]
    [InlineData(AgentStepStatus.Completed, AgentStepStatus.Running)]
    [InlineData(AgentStepStatus.Completed, AgentStepStatus.Stuck)]
    [InlineData(AgentStepStatus.Completed, AgentStepStatus.Pending)]
    [InlineData(AgentStepStatus.Skipped, AgentStepStatus.Running)]
    [InlineData(AgentStepStatus.Skipped, AgentStepStatus.Completed)]
    [InlineData(AgentStepStatus.Pending, AgentStepStatus.Completed)]
    [InlineData(AgentStepStatus.Pending, AgentStepStatus.Stuck)]
    [InlineData(AgentStepStatus.Running, AgentStepStatus.Pending)]
    [InlineData(AgentStepStatus.Running, AgentStepStatus.Skipped)]
    [InlineData(AgentStepStatus.Failed, AgentStepStatus.Completed)]
    [InlineData(AgentStepStatus.Failed, AgentStepStatus.Stuck)]
    [InlineData(AgentStepStatus.Failed, AgentStepStatus.Skipped)]
    public void Step_InvalidTransitions_Rejected(AgentStepStatus from, AgentStepStatus to)
    {
        RunStateMachine.CanTransitionStep(from, to).Should().BeFalse();
    }
}

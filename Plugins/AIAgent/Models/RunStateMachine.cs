namespace ForgeSelf.Api.Plugins.AIAgent.Models;

/// <summary>
/// 计划驱动执行状态机（design.md §3.3）：迁移合法性统一判定，供 RunOrchestrator 与单测复用。
/// </summary>
/// <remarks>
/// 状态机规则（design.md §3.3 + contracts/agent-runs-api.md resume 语义 Status∈{Stuck,Failed}）：
/// - AgentRun：Pending → Planning → Running → Completed；Running ↘ Stuck → Running（人工）；Running ↘ Failed（resume → Running）；任意非终态可 Cancelled。
/// - AgentStep：Pending → Running → Completed；Running ↘ Stuck →（重试→Running / 跳过→Skipped / 补位→Completed）；Running ↘ Failed（resume 重试 → Running）。
/// </remarks>
public static class RunStateMachine
{
    /// <summary>AgentRun 是否允许从 <paramref name="from"/> 迁移到 <paramref name="to"/>。</summary>
    public static bool CanTransitionRun(AgentRunStatus from, AgentRunStatus to) => from switch
    {
        AgentRunStatus.Pending => to is AgentRunStatus.Planning or AgentRunStatus.Cancelled,
        AgentRunStatus.Planning => to is AgentRunStatus.Running or AgentRunStatus.Failed or AgentRunStatus.Cancelled,
        AgentRunStatus.Running => to is AgentRunStatus.Completed or AgentRunStatus.Stuck or AgentRunStatus.Failed or AgentRunStatus.Cancelled,
        AgentRunStatus.Stuck => to is AgentRunStatus.Running or AgentRunStatus.Cancelled,
        AgentRunStatus.Failed => to is AgentRunStatus.Running or AgentRunStatus.Cancelled,
        _ => false, // Completed / Cancelled 为终态
    };

    /// <summary>AgentStepRun 是否允许从 <paramref name="from"/> 迁移到 <paramref name="to"/>。</summary>
    public static bool CanTransitionStep(AgentStepStatus from, AgentStepStatus to) => from switch
    {
        AgentStepStatus.Pending => to is AgentStepStatus.Running or AgentStepStatus.Skipped or AgentStepStatus.Failed,
        AgentStepStatus.Running => to is AgentStepStatus.Completed or AgentStepStatus.Stuck or AgentStepStatus.Failed,
        AgentStepStatus.Stuck => to is AgentStepStatus.Running or AgentStepStatus.Skipped or AgentStepStatus.Completed or AgentStepStatus.Failed,
        AgentStepStatus.Failed => to is AgentStepStatus.Running, // resume 重试从本步重跑
        _ => false, // Completed / Skipped 为终态
    };
}

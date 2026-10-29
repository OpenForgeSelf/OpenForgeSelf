namespace ForgeSelf.Api.Plugins.AIAgent.Models;

/// <summary>
/// 计划驱动执行实例状态。
/// </summary>
public enum AgentRunStatus
{
    /// <summary>待执行（Run 已创建，未进入规划）。</summary>
    Pending = 0,

    /// <summary>规划中（LLM 生成 Plan）。</summary>
    Planning = 1,

    /// <summary>执行中（逐步驱动 LLM）。</summary>
    Running = 2,

    /// <summary>已完成（全部步骤完成，终局合成交付）。</summary>
    Completed = 3,

    /// <summary>卡住（等待人工介入；介入后回 Running）。</summary>
    Stuck = 4,

    /// <summary>失败（异常/不可恢复）。</summary>
    Failed = 5,

    /// <summary>已取消（用户取消）。</summary>
    Cancelled = 6
}

/// <summary>
/// 计划驱动执行步骤状态。
/// </summary>
public enum AgentStepStatus
{
    /// <summary>待执行。</summary>
    Pending = 0,

    /// <summary>执行中。</summary>
    Running = 1,

    /// <summary>已完成（complete_step 或人工补位）。</summary>
    Completed = 2,

    /// <summary>卡住（等待人工介入）。</summary>
    Stuck = 3,

    /// <summary>已跳过（人工介入 skip）。</summary>
    Skipped = 4,

    /// <summary>失败。</summary>
    Failed = 5
}

/// <summary>
/// 计划驱动执行的执行计划（Plan DSL，存 AgentRun.PlanJson）。
/// </summary>
public class AgentPlan
{
    /// <summary>整体目标一句话。</summary>
    public string Goal { get; set; } = string.Empty;

    /// <summary>步骤列表（顺序执行）。</summary>
    public List<AgentPlanStep> Steps { get; set; } = new();

    /// <summary>
    /// 目标模式（PILOT-051）验收标准清单；空/缺省 = 非目标模式（做完计划步骤即 Completed）。
    /// 随 PlanJson 持久化（不改 DB 结构）。
    /// </summary>
    public List<string>? SuccessCriteria { get; set; }

    /// <summary>
    /// 目标模式最大评估轮数（1–10）；null = 默认 3。与 <see cref="SuccessCriteria"/> 同存于 PlanJson。
    /// </summary>
    public int? MaxGoalRounds { get; set; }
}

/// <summary>
/// 执行计划中的单个步骤。
/// </summary>
public class AgentPlanStep
{
    /// <summary>步骤 id（如 s1）。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>步骤名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>步骤目标（给 LLM 的执行指令）。</summary>
    public string Objective { get; set; } = string.Empty;

    /// <summary>期望产出物（LLM 完成声明时对照；V1 仅记录）。</summary>
    public string ExpectedOutput { get; set; } = string.Empty;

    /// <summary>
    /// 本步允许的工具白名单；空 = 使用 Agent 全部工具（向后兼容）。
    /// </summary>
    public List<string>? AllowedTools { get; set; }

    /// <summary>
    /// 是否为必须步骤（V1 仅记录不启用强校验；V2 出口校验用）。
    /// </summary>
    public bool Mandatory { get; set; }
}

/// <summary>
/// 创建计划驱动执行的请求体（POST /api/ai-agent/runs）。
/// </summary>
public class RunRequest
{
    /// <summary>关联聊天会话 id。</summary>
    public string? SessionId { get; set; }

    /// <summary>所选 Agent id（决定人格/工具/关联工作流 + 执行模式）。</summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>任务原文（必填）。</summary>
    public string TaskInput { get; set; } = string.Empty;

    /// <summary>关联工作流 id；大于 0 时从工作流定义生成 Plan 草稿，否则 Agent 自规划。</summary>
    public long WorkflowId { get; set; }

    /// <summary>聊天模型 id（形如 provider:upstreamModelId）；空则回退默认 provider。</summary>
    public string? ChatModelId { get; set; }

    /// <summary>
    /// 目标模式（PILOT-051）验收标准，每条一句；null/空 = 非目标模式（现状行为）。
    /// 提供后：计划步骤全部完成 → 评估 Agent 对照标准判定 → 未达标自动追加补救步骤续跑，
    /// 轮次预算（<see cref="MaxGoalRounds"/>）用尽仍未达标 → Run 置 Stuck 带未达成报告。
    /// </summary>
    public List<string>? SuccessCriteria { get; set; }

    /// <summary>目标模式最大评估轮数（默认 3，钳制 1–10）。</summary>
    public int? MaxGoalRounds { get; set; }
}

/// <summary>
/// 人工介入请求体（PATCH /api/ai-agent/runs/{id}/steps/{index}）。
/// </summary>
public class InterveneRequest
{
    /// <summary>介入动作：skip 跳过 / override 补位（retry 走 resume 端点）。</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>override 时的人工补位产出。</summary>
    public string? Output { get; set; }

    /// <summary>人工批注。</summary>
    public string? Note { get; set; }
}

/// <summary>
/// 执行实例 DTO（列表/详情返回）。
/// </summary>
public class AgentRunDto
{
    public long Id { get; set; }
    public string AgentId { get; set; } = string.Empty;
    public string? AgentName { get; set; }
    public string? SessionId { get; set; }
    public long? WorkflowId { get; set; }
    public string? WorkflowName { get; set; }
    public string? TaskInput { get; set; }
    public string? PlanJson { get; set; }
    public AgentRunStatus Status { get; set; }
    public int? CurrentStepIndex { get; set; }
    public string? StuckReason { get; set; }
    public int? StepCount { get; set; }
    public long? TotalTokens { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
}

/// <summary>
/// 执行步骤 DTO（Run 详情返回）。
/// </summary>
public class AgentStepRunDto
{
    public long Id { get; set; }
    public long RunId { get; set; }
    public int StepIndex { get; set; }
    public string? StepId { get; set; }
    public string? Name { get; set; }
    public string? Objective { get; set; }
    public AgentStepStatus Status { get; set; }
    public string? InputJson { get; set; }
    public string? OutputJson { get; set; }
    public string? ToolCallsJson { get; set; }
    public string? ErrorMessage { get; set; }
    public string? StuckReason { get; set; }
    public string? HumanNote { get; set; }
    public string? HumanOverride { get; set; }
    public int? RetryCount { get; set; }
    public long? TokensUsed { get; set; }
    public long? DurationMs { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Run 列表分页响应。
/// </summary>
public class AgentRunListResponse
{
    public bool Success { get; set; }
    public long Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<AgentRunDto> Items { get; set; } = new();
}

/// <summary>
/// Run 详情响应（Run + 步骤列表）。
/// </summary>
public class AgentRunDetailResponse
{
    public AgentRunDto Run { get; set; } = new();
    public List<AgentStepRunDto> Steps { get; set; } = new();
}

/// <summary>
/// 步骤进度 SSE 事件载荷（step_started）。
/// </summary>
/// <summary>goal_round_started SSE payload（目标模式，PILOT-051）。</summary>
public class GoalRoundStartedPayload
{
    public long RunId { get; set; }

    /// <summary>当前评估轮次（1 起）。</summary>
    public int Round { get; set; }

    /// <summary>轮次预算（钳制后的 MaxGoalRounds）。</summary>
    public int MaxRounds { get; set; }

    /// <summary>成功判据清单。</summary>
    public List<string> Criteria { get; set; } = new();
}

/// <summary>goal_evaluated SSE payload（目标模式，PILOT-051）。</summary>
public class GoalEvaluatedPayload
{
    public long RunId { get; set; }

    public int Round { get; set; }

    /// <summary>评估判定：全部判据已达成。</summary>
    public bool Achieved { get; set; }

    public double? Confidence { get; set; }

    /// <summary>未达成项清单。</summary>
    public List<string> Missing { get; set; } = new();

    public string? Critique { get; set; }

    public string? Summary { get; set; }
}

/// <summary>step_started SSE payload。</summary>
public class StepStartedPayload
{
    public long RunId { get; set; }
    public int StepIndex { get; set; }
    public string? StepId { get; set; }
    public string? Name { get; set; }
    public string? Objective { get; set; }
}

/// <summary>
/// 步骤完成 SSE 事件载荷（step_completed）。
/// </summary>
public class StepCompletedPayload
{
    public long RunId { get; set; }
    public int StepIndex { get; set; }
    public string? Output { get; set; }
}

/// <summary>
/// 卡住 SSE 事件载荷（run_stuck）。
/// </summary>
public class RunStuckPayload
{
    public long RunId { get; set; }
    public int StepIndex { get; set; }
    public string? Reason { get; set; }
}

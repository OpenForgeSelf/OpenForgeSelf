namespace ForgeSelf.Abstractions;

/// <summary>
/// 计划任务到点后，在本机宿主进程内触发一次 AIAgent MAF Run。
/// 提供方：ai-agent（<c>Apply</c> 里 eager 构造并 <c>ctx.Register</c>）。
/// 消费方：scheduler，每次 <c>ctx.Get&lt;IScheduledAgentRunTrigger&gt;()</c>，禁止把实例缓存为字段。
/// 不走 HTTP 自调。写操作仍走现有审批；本契约不关闭审批、不代跑系统命令、不做云端。
/// </summary>
public interface IScheduledAgentRunTrigger
{
    /// <summary>
    /// 触发一次计划驱动执行（<c>IRunOrchestratorService.RunAsync</c> 的同进程调用）。
    /// 同一计划任务已有未结束 Run（含等待审批的 Stuck）时跳过，不另开一条。
    /// 失败返回 <see cref="ScheduledAgentRunResult.Success"/>=false，不抛业务异常。
    /// </summary>
    Task<ScheduledAgentRunResult> TriggerAsync(ScheduledAgentRunRequest request, CancellationToken cancellationToken = default);
}

/// <summary>调度器交给本机 Agent 的一次触发。</summary>
public sealed class ScheduledAgentRunRequest
{
    /// <summary>计划任务 Id。幂等键，必须大于 0。</summary>
    public long ScheduledTaskId { get; init; }

    /// <summary>Bot / Agent id（如 <c>agent.programmer</c>）。</summary>
    public string AgentId { get; init; } = string.Empty;

    /// <summary>工作区目录（绝对路径）。执行前设为当前项目根，文件工具相对该根。</summary>
    public string? Workspace { get; init; }

    /// <summary>任务原文。</summary>
    public string Prompt { get; init; } = string.Empty;

    /// <summary>
    /// 审批策略说明。只追加进任务原文，不改变审批挂起逻辑。
    /// 空则由实现补上默认说明：写操作仍走现有审批，不得无人值守执行系统命令。
    /// </summary>
    public string? ApprovalPolicy { get; init; }
}

/// <summary>一次触发的结果，供调度器写入任务日志。</summary>
public sealed class ScheduledAgentRunResult
{
    /// <summary>是否已跳过（同一任务仍有未结束 Run）。跳过也算成功落日志，不记失败次数。</summary>
    public bool Skipped { get; init; }

    /// <summary>触发或跳过是否按预期结束。false = 没能创建 Run。</summary>
    public bool Success { get; init; }

    /// <summary>本机 Run id。跳过时为已在跑的那条；失败且尚未建 Run 时为 null。</summary>
    public long? RunId { get; init; }

    /// <summary>Run 状态或末事件类型。</summary>
    public string? Status { get; init; }

    /// <summary>给任务日志的中文说明。</summary>
    public string Message { get; init; } = string.Empty;

    public static ScheduledAgentRunResult Skip(long? runId, string? status, string message) =>
        new() { Skipped = true, Success = true, RunId = runId, Status = status, Message = message };

    public static ScheduledAgentRunResult Ok(long runId, string? status, string message) =>
        new() { Success = true, RunId = runId, Status = status, Message = message };

    public static ScheduledAgentRunResult Fail(string message) =>
        new() { Success = false, Message = message };
}

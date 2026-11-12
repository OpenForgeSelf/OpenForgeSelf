namespace ForgeSelf.Api.Plugins.Scheduler.Models;

public enum ScheduledTaskType
{
    Workflow = 0,
    SystemCommand = 1,
    HttpWebhook = 2,
    /// <summary>到点在本机宿主进程内创建一次智能体 MAF Run。不代跑系统命令。</summary>
    AgentRun = 3
}

public enum ScheduledTaskStatus
{
    Enabled = 0,
    Disabled = 1,
    Paused = 2,
    Error = 3
}

public enum ScheduleType
{
    Cron = 0,
    Interval = 1,
    Once = 2,
    Daily = 3,
    Weekly = 4,
    Monthly = 5
}

public enum ScheduledTaskLogStatus
{
    Success = 0,
    Failed = 1,
    Running = 2
}

/// <summary>
/// 单次任务执行的结果归类。B5（2026-11-08）：
/// <see cref="Skipped"/> 表示触发被消费方拒绝（如同一 Agent 会话已有未结束 Run），
/// 调度器不得推进 LastRunTime/RunCount/NextRunTime/Status（Once 不被空耗）。
/// </summary>
public enum TaskExecutionOutcome
{
    /// <summary>任务实际执行（成功或失败都算已消费一次触发）。</summary>
    Executed = 0,

    /// <summary>触发被跳过，本轮不算消费，下轮到点重试。</summary>
    Skipped = 1
}

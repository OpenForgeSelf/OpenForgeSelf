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

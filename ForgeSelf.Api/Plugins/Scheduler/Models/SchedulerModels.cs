namespace ForgeSelf.Api.Plugins.Scheduler.Models;

public class ScheduledTask
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ScheduledTaskType TaskType { get; set; }
    public string TargetId { get; set; } = string.Empty;
    public ScheduleType ScheduleType { get; set; }
    public string? CronExpression { get; set; }
    public int? IntervalMinutes { get; set; }
    public DateTime? RunAt { get; set; }
    public string? WeekDays { get; set; }
    public int? DayOfMonth { get; set; }
    public TimeSpan? TimeOfDay { get; set; }
    public string TimeZone { get; set; } = "Asia/Shanghai";
    public ScheduledTaskStatus Status { get; set; }
    public DateTime? LastRunTime { get; set; }
    public DateTime? NextRunTime { get; set; }
    public int RunCount { get; set; }
    public int FailureCount { get; set; }
    public string? InputParameters { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ScheduledTaskLog
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public ScheduledTaskLogStatus Status { get; set; }
    public string? ResultMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public double DurationMs { get; set; }
}

public class ScheduledTaskDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ScheduledTaskType TaskType { get; set; }
    public string TargetId { get; set; } = string.Empty;
    public ScheduleType ScheduleType { get; set; }
    public string? CronExpression { get; set; }
    public int? IntervalMinutes { get; set; }
    public DateTime? RunAt { get; set; }
    public string? WeekDays { get; set; }
    public int? DayOfMonth { get; set; }
    public TimeSpan? TimeOfDay { get; set; }
    public string TimeZone { get; set; } = string.Empty;
    public ScheduledTaskStatus Status { get; set; }
    public DateTime? LastRunTime { get; set; }
    public DateTime? NextRunTime { get; set; }
    public int RunCount { get; set; }
    public int FailureCount { get; set; }
    public string? InputParameters { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ScheduledTaskLogDto
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public ScheduledTaskLogStatus Status { get; set; }
    public string? ResultMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public double DurationMs { get; set; }
}

public class CreateScheduledTaskRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ScheduledTaskType TaskType { get; set; }
    public string TargetId { get; set; } = string.Empty;
    public ScheduleType ScheduleType { get; set; }
    public string? CronExpression { get; set; }
    public int? IntervalMinutes { get; set; }
    public DateTime? RunAt { get; set; }
    public string? WeekDays { get; set; }
    public int? DayOfMonth { get; set; }
    public TimeSpan? TimeOfDay { get; set; }
    public string TimeZone { get; set; } = "Asia/Shanghai";
    public string? InputParameters { get; set; }
}

public class UpdateScheduledTaskRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ScheduledTaskType TaskType { get; set; }
    public string TargetId { get; set; } = string.Empty;
    public ScheduleType ScheduleType { get; set; }
    public string? CronExpression { get; set; }
    public int? IntervalMinutes { get; set; }
    public DateTime? RunAt { get; set; }
    public string? WeekDays { get; set; }
    public int? DayOfMonth { get; set; }
    public TimeSpan? TimeOfDay { get; set; }
    public string TimeZone { get; set; } = "Asia/Shanghai";
    public string? InputParameters { get; set; }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class CronParseRequest
{
    public string CronExpression { get; set; } = string.Empty;
    public int Count { get; set; } = 5;
    public string TimeZone { get; set; } = "Asia/Shanghai";
}

public class CronParseResult
{
    public bool Valid { get; set; }
    public string? ErrorMessage { get; set; }
    public List<DateTime> NextRunTimes { get; set; } = new();
}

public class ToggleTaskStatusRequest
{
    public bool Enabled { get; set; }
}

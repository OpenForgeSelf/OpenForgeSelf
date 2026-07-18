using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using OpenForgeSelf.Backend.Plugins.Scheduler.Models;

namespace OpenForgeSelf.Backend.Plugins.Scheduler.Data;

[Table("ScheduledTasks")]
public class ScheduledTaskEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("Name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    [Column("Description")]
    public string? Description { get; set; }

    [Column("TaskType")]
    public ScheduledTaskType TaskType { get; set; }

    [MaxLength(500)]
    [Column("TargetId")]
    public string TargetId { get; set; } = string.Empty;

    [Column("ScheduleType")]
    public ScheduleType ScheduleType { get; set; }

    [MaxLength(100)]
    [Column("CronExpression")]
    public string? CronExpression { get; set; }

    [Column("IntervalMinutes")]
    public int? IntervalMinutes { get; set; }

    [Column("RunAt")]
    public DateTime? RunAt { get; set; }

    [MaxLength(100)]
    [Column("WeekDays")]
    public string? WeekDays { get; set; }

    [Column("DayOfMonth")]
    public int? DayOfMonth { get; set; }

    [Column("TimeOfDay")]
    public TimeSpan? TimeOfDay { get; set; }

    [MaxLength(100)]
    [Column("TimeZone")]
    public string TimeZone { get; set; } = "Asia/Shanghai";

    [Column("Status")]
    public ScheduledTaskStatus Status { get; set; }

    [Column("LastRunTime")]
    public DateTime? LastRunTime { get; set; }

    [Column("NextRunTime")]
    public DateTime? NextRunTime { get; set; }

    [Column("RunCount")]
    public int RunCount { get; set; }

    [Column("FailureCount")]
    public int FailureCount { get; set; }

    [Column("InputParameters")]
    public string? InputParameters { get; set; }

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; }

    [Column("UpdatedAt")]
    public DateTime UpdatedAt { get; set; }
}

[Table("ScheduledTaskLogs")]
public class ScheduledTaskLogEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Column("TaskId")]
    public long TaskId { get; set; }

    [Column("StartTime")]
    public DateTime StartTime { get; set; }

    [Column("EndTime")]
    public DateTime? EndTime { get; set; }

    [Column("Status")]
    public ScheduledTaskLogStatus Status { get; set; }

    [MaxLength(2000)]
    [Column("ResultMessage")]
    public string? ResultMessage { get; set; }

    [MaxLength(4000)]
    [Column("ErrorMessage")]
    public string? ErrorMessage { get; set; }

    [Column("DurationMs")]
    public double DurationMs { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenForgeSelf.Backend.Models.UsageStats;

public enum WorkflowExecutionStatus
{
    Success = 0,
    Failed = 1,
    Cancelled = 2
}

public class WorkflowUsageRecord
{
    [Key]
    public long Id { get; set; }

    [Required]
    public long WorkflowId { get; set; }

    [Required]
    [MaxLength(200)]
    public string WorkflowName { get; set; } = string.Empty;

    [Required]
    public long ExecutionId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public WorkflowExecutionStatus Status { get; set; }

    public double DurationSeconds { get; set; }

    public string? InputVariablesJson { get; set; }

    public string? OutputResultJson { get; set; }

    public int ToolCallCount { get; set; }

    public int StepCount { get; set; }

    [MaxLength(100)]
    public string? TriggeredBy { get; set; }

    [MaxLength(50)]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    public string? ErrorMessage { get; set; }
}

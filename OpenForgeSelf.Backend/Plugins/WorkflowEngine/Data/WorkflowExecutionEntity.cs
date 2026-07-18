using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenForgeSelf.Backend.Plugins.WorkflowEngine.Data;

[Table("WorkflowExecutions")]
public class WorkflowExecutionEntity
{
    [Key]
    public long Id { get; set; }

    public long WorkflowId { get; set; }

    [MaxLength(200)]
    public string? WorkflowName { get; set; }

    public int Status { get; set; }

    public DateTime StartTime { get; set; } = DateTime.Now;

    public DateTime? EndTime { get; set; }

    [MaxLength(100)]
    public string? CurrentStepId { get; set; }

    public string LogsJson { get; set; } = "[]";

    public string? ResultsJson { get; set; }

    public string? ErrorMessage { get; set; }

    public string? VariablesJson { get; set; }

    public string? StepResultsJson { get; set; }

    public double Progress { get; set; }

    [MaxLength(200)]
    public string? TriggeredBy { get; set; }
}

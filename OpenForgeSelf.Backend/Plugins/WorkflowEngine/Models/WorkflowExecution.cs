namespace OpenForgeSelf.Backend.Plugins.WorkflowEngine.Models;

public class WorkflowExecution
{
    public long Id { get; set; }
    public long WorkflowId { get; set; }
    public string? WorkflowName { get; set; }
    public WorkflowStatus Status { get; set; }
    public DateTime StartTime { get; set; } = DateTime.Now;
    public DateTime? EndTime { get; set; }
    public string? CurrentStepId { get; set; }
    public List<ExecutionLogEntry> Logs { get; set; } = new();
    public string? ResultsJson { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object?>? Variables { get; set; }
    public Dictionary<string, object?>? StepResults { get; set; }
    public double Progress { get; set; }
    public string? TriggeredBy { get; set; }
}

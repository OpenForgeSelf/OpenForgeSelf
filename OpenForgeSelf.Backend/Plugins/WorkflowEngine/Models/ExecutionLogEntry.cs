namespace OpenForgeSelf.Backend.Plugins.WorkflowEngine.Models;

public class ExecutionLogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string? StepId { get; set; }
    public string? StepName { get; set; }
    public string LogLevel { get; set; } = "Info";
    public string Message { get; set; } = string.Empty;
    public string? DataJson { get; set; }
}

using OpenForgeSelf.Backend.Plugins.WorkflowEngine.Models;

namespace OpenForgeSelf.Backend.Plugins.WorkflowEngine.Services;

public interface IWorkflowScheduler
{
    int MaxConcurrentExecutions { get; set; }
    int CurrentRunningCount { get; }
    int QueuedCount { get; }

    event EventHandler<WorkflowExecution>? ExecutionStarted;
    event EventHandler<WorkflowExecution>? ExecutionCompleted;
    event EventHandler<WorkflowExecution>? ExecutionFailed;
    event EventHandler<WorkflowExecution>? ExecutionProgressChanged;

    Task<WorkflowExecution> QueueExecutionAsync(long workflowId, Dictionary<string, object?>? inputVariables = null, string? triggeredBy = null);
    Task PauseExecutionAsync(long executionId);
    Task ResumeExecutionAsync(long executionId);
    Task CancelExecutionAsync(long executionId);
    Task<WorkflowExecution?> GetExecutionAsync(long executionId);
    List<WorkflowExecution> GetRunningExecutions();
    List<WorkflowExecution> GetQueuedExecutions();
}

using OpenForgeSelf.Backend.Plugins.WorkflowEngine.Models;

namespace OpenForgeSelf.Backend.Plugins.WorkflowEngine.Services;

public interface IWorkflowExecutor
{
    Task<WorkflowExecution> ExecuteAsync(long workflowId, Dictionary<string, object?>? inputVariables = null, string? triggeredBy = null);
    Task PauseAsync(long executionId);
    Task ResumeAsync(long executionId);
    Task CancelAsync(long executionId);
    Task<WorkflowExecution?> GetExecutionAsync(long executionId);
}

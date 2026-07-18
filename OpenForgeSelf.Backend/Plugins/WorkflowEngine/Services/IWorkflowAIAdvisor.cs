using OpenForgeSelf.Backend.Plugins.WorkflowEngine.Models;

namespace OpenForgeSelf.Backend.Plugins.WorkflowEngine.Services;

public interface IWorkflowAIAdvisor
{
    Task<RetryAdvice> GetRetryAdviceAsync(WorkflowStep step, string errorMessage, int retryCount, Dictionary<string, object?>? context = null);
}

public class RetryAdvice
{
    public bool ShouldRetry { get; set; }
    public string? AdjustedParameters { get; set; }
    public string? AlternativeToolName { get; set; }
    public int DelayMs { get; set; } = 1000;
    public string? Reason { get; set; }
}

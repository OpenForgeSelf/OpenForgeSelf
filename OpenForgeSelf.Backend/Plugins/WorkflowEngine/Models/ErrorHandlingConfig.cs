namespace OpenForgeSelf.Backend.Plugins.WorkflowEngine.Models;

public class ErrorHandlingConfig
{
    public int MaxRetries { get; set; } = 0;
    public int RetryDelayMs { get; set; } = 1000;
    public bool ContinueOnError { get; set; } = false;
}

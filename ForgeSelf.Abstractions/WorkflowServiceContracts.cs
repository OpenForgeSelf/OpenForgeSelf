namespace ForgeSelf.Abstractions;

public interface IWorkflowService
{
    Task<WorkflowDefinitionDto> CreateWorkflowAsync(CreateWorkflowRequest request);
    Task<WorkflowDefinitionDto?> UpdateWorkflowAsync(long id, UpdateWorkflowRequest request);
    Task<bool> DeleteWorkflowAsync(long id);
    Task<WorkflowDefinitionDto?> GetWorkflowAsync(long id);
    Task<PagedResult<WorkflowDefinitionDto>> ListWorkflowsAsync(string? keyword = null, string? category = null, int page = 1, int pageSize = 20);
    Task<PagedResult<WorkflowExecutionDto>> GetExecutionsAsync(long? workflowId = null, int page = 1, int pageSize = 20);
    Task<WorkflowExecutionDetailDto?> GetExecutionDetailAsync(long executionId);
    Task<bool> FavoriteWorkflowAsync(long id, bool isFavorite);
    Task<bool> IncrementUsageCountAsync(long id);
    Task<List<WorkflowTemplateDto>> GetTemplatesAsync();
    Task<WorkflowExecutionDto> ExecuteWorkflowAsync(long id, ExecuteWorkflowRequest request);
}

public interface IWorkflowExecutor
{
    Task<WorkflowExecution> ExecuteAsync(long workflowId, Dictionary<string, object?>? inputVariables = null, string? triggeredBy = null);
    Task PauseAsync(long executionId);
    Task ResumeAsync(long executionId);
    Task CancelAsync(long executionId);
    Task<WorkflowExecution?> GetExecutionAsync(long executionId);
}

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

using OpenForgeSelf.Backend.Plugins.WorkflowEngine.Models;

namespace OpenForgeSelf.Backend.Plugins.WorkflowEngine.Services;

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

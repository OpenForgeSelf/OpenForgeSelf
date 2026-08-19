using OpenForgeSelf.Abstractions;

namespace OpenForgeSelf.Backend.Tests.E2E.ApiClient;

/// <summary>
/// 工作流 API 客户端
/// </summary>
public class WorkflowClient : BaseApiClient
{
    public WorkflowClient(HttpClient client, string? baseUrl = null) : base(client, baseUrl)
    {
    }

    /// <summary>
    /// 获取工作流列表
    /// </summary>
    public async Task<PagedResult<WorkflowDefinitionDto>?> GetWorkflowsAsync(string? keyword = null, string? category = null, int page = 1, int pageSize = 20)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["page"] = page.ToString(),
            ["pageSize"] = pageSize.ToString()
        };

        if (!string.IsNullOrWhiteSpace(keyword))
            queryParams["keyword"] = keyword;

        if (!string.IsNullOrWhiteSpace(category))
            queryParams["category"] = category;

        return await GetAsync<PagedResult<WorkflowDefinitionDto>>("api/workflows", queryParams);
    }

    /// <summary>
    /// 获取工作流详情
    /// </summary>
    public async Task<WorkflowDefinitionDto?> GetWorkflowAsync(long id)
    {
        return await GetAsync<WorkflowDefinitionDto>($"api/workflows/{id}");
    }

    /// <summary>
    /// 创建工作流
    /// </summary>
    public async Task<WorkflowDefinitionDto?> CreateWorkflowAsync(CreateWorkflowRequest request)
    {
        return await PostAsync<WorkflowDefinitionDto>("api/workflows", request);
    }

    /// <summary>
    /// 更新工作流
    /// </summary>
    public async Task<WorkflowDefinitionDto?> UpdateWorkflowAsync(long id, UpdateWorkflowRequest request)
    {
        return await PutAsync<WorkflowDefinitionDto>($"api/workflows/{id}", request);
    }

    /// <summary>
    /// 删除工作流
    /// </summary>
    public async Task<bool> DeleteWorkflowAsync(long id)
    {
        var response = await DeleteAsync<ApiResponse>($"api/workflows/{id}");
        return response?.Success == true;
    }

    /// <summary>
    /// 收藏/取消收藏工作流
    /// </summary>
    public async Task<bool> FavoriteWorkflowAsync(long id, bool isFavorite)
    {
        var response = await PostAsync<ApiResponse>($"api/workflows/{id}/favorite", new { isFavorite });
        return response?.Success == true;
    }

    /// <summary>
    /// 执行工作流
    /// </summary>
    public async Task<WorkflowExecutionDto?> ExecuteWorkflowAsync(long id, ExecuteWorkflowRequest? request = null)
    {
        return await PostAsync<WorkflowExecutionDto>($"api/workflows/{id}/execute", request ?? new ExecuteWorkflowRequest());
    }

    /// <summary>
    /// 获取执行记录列表
    /// </summary>
    public async Task<PagedResult<WorkflowExecutionDto>?> GetExecutionsAsync(long? workflowId = null, int page = 1, int pageSize = 20)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["page"] = page.ToString(),
            ["pageSize"] = pageSize.ToString()
        };

        if (workflowId.HasValue)
            queryParams["workflowId"] = workflowId.Value.ToString();

        return await GetAsync<PagedResult<WorkflowExecutionDto>>("api/workflows/executions", queryParams);
    }

    /// <summary>
    /// 获取执行详情
    /// </summary>
    public async Task<WorkflowExecutionDetailDto?> GetExecutionDetailAsync(long id)
    {
        return await GetAsync<WorkflowExecutionDetailDto>($"api/workflows/executions/{id}");
    }

    /// <summary>
    /// 暂停工作流执行
    /// </summary>
    public async Task<bool> PauseExecutionAsync(long id)
    {
        var response = await PostAsync<ApiResponse>($"api/workflows/executions/{id}/pause", null);
        return response?.Success == true;
    }

    /// <summary>
    /// 继续工作流执行
    /// </summary>
    public async Task<bool> ResumeExecutionAsync(long id)
    {
        var response = await PostAsync<ApiResponse>($"api/workflows/executions/{id}/resume", null);
        return response?.Success == true;
    }

    /// <summary>
    /// 取消工作流执行
    /// </summary>
    public async Task<bool> CancelExecutionAsync(long id)
    {
        var response = await PostAsync<ApiResponse>($"api/workflows/executions/{id}/cancel", null);
        return response?.Success == true;
    }

    /// <summary>
    /// 获取工作流模板
    /// </summary>
    public async Task<List<WorkflowTemplateDto>?> GetTemplatesAsync()
    {
        return await GetAsync<List<WorkflowTemplateDto>>("api/workflows/templates");
    }
}

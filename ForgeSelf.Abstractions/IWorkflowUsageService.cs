namespace ForgeSelf.Abstractions;

public interface IWorkflowUsageService
{
    Task<long> RecordWorkflowExecutionAsync(RecordWorkflowExecutionRequest request, string? ipAddress = null);

    Task UpdateWorkflowExecutionAsync(long executionId, WorkflowExecutionStatus status, DateTime endTime, double durationSeconds, object? outputResult = null, string? errorMessage = null, int toolCallCount = 0);

    Task<WorkflowStatsDto?> GetWorkflowStatsAsync(long workflowId, DateTime? startDate = null, DateTime? endDate = null);

    Task<List<PopularWorkflowDto>> GetPopularWorkflowsAsync(int limit = 10, string period = "30d");

    Task<List<WorkflowUsageTrendDto>> GetWorkflowUsageTrendAsync(long workflowId, int days = 7);

    Task<WorkflowTotalStatsDto> GetTotalStatsAsync(DateTime? startDate = null, DateTime? endDate = null);

    Task<List<WorkflowToolRankingDto>> GetToolUsageInWorkflowsAsync(DateTime? startDate = null, DateTime? endDate = null, int limit = 10);

    Task RecordToolUsageInWorkflowAsync(string pluginId, string toolId, string actionType, long durationMs, long workflowExecutionId, string? stepId = null, Dictionary<string, object>? metadata = null);
}

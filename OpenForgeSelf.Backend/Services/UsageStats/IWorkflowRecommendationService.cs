using OpenForgeSelf.Backend.Models.UsageStats;

namespace OpenForgeSelf.Backend.Services.UsageStats;

public interface IWorkflowRecommendationService
{
    Task<List<WorkflowRecommendationDto>> GetRecommendedWorkflowsAsync(string? userId = null, string? context = null, int limit = 5);

    Task<ContextualRecommendationDto> GetContextualRecommendationsAsync(ContextualRecommendationRequest request);

    Task<SaveAsSuggestionDto> GetSaveAsSuggestionAsync(SaveAsSuggestionRequest request);
}

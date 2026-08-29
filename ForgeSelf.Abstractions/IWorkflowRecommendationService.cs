namespace ForgeSelf.Abstractions;

public interface IWorkflowRecommendationService
{
    Task<List<WorkflowRecommendationDto>> GetRecommendedWorkflowsAsync(string? userId = null, string? context = null, int limit = 5);

    Task<ContextualRecommendationDto> GetContextualRecommendationsAsync(ContextualRecommendationRequest request);

    Task<SaveAsSuggestionDto> GetSaveAsSuggestionAsync(SaveAsSuggestionRequest request);
}

using ForgeSelf.Abstractions;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

public class AIWorkflowAdvisor : IWorkflowAIAdvisor
{
    private readonly IAIWorkflowAssistant _assistant;

    public AIWorkflowAdvisor(IAIWorkflowAssistant assistant)
    {
        _assistant = assistant;
    }

    public async Task<RetryAdvice> GetRetryAdviceAsync(WorkflowStep step, string errorMessage, int retryCount, Dictionary<string, object?>? context = null)
    {
        try
        {
            XTrace.Log.Debug("[AIWorkflowAdvisor] 获取AI重试建议，步骤: {0}, 重试: {1}", step.Name, retryCount);

            var suggestion = await _assistant.GetRetrySuggestionAsync(step, errorMessage, retryCount);

            return new RetryAdvice
            {
                ShouldRetry = suggestion.ShouldRetry,
                AdjustedParameters = suggestion.AdjustedParameters,
                AlternativeToolName = suggestion.AlternativeToolName,
                DelayMs = suggestion.DelayMs,
                Reason = suggestion.Reason
            };
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[AIWorkflowAdvisor] 获取AI重试建议失败: {0}", ex.Message);
            return new RetryAdvice
            {
                ShouldRetry = retryCount < (step.ErrorHandling?.MaxRetries ?? 3),
                DelayMs = step.ErrorHandling?.RetryDelayMs ?? 1000,
                Reason = "AI服务不可用，使用默认重试策略"
            };
        }
    }
}

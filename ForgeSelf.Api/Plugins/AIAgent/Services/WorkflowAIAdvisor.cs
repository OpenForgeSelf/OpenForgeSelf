using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

/// <summary>
/// 029 工作流 AI 重试顾问：把「步骤失败后是否重试 / 如何调整」的决策委托给
/// <see cref="IAIWorkflowAssistant"/>（LLM 驱动），并把 RetrySuggestion 映射为宿主契约 RetryAdvice。
///
/// 设计要点：
/// - 构造只收 IServiceProvider（延迟解析 assistant）。AIAgentPlugin.Apply 的 eager 块
///   （BuildServiceProvider().CreateScope() 后 GetRequiredService&lt;IWorkflowAIAdvisor&gt;）
///   若构造直接依赖 IAIWorkflowAssistant，会连带触发 AIWorkflowAssistant 的 IContext 构造依赖，
///   而 IContext 在 Apply 阶段尚未 seed 进该 scope，eager 解析会抛异常导致插件加载失败。
///   延迟到 GetRetryAdviceAsync 调用时再解析，此时运行期上下文已就绪。
/// - 任何解析/调用失败均降级为「保守不重试」，绝不让顾问自身异常打断工作流主链路。
/// </summary>
public class WorkflowAIAdvisor : IWorkflowAIAdvisor
{
    private readonly IServiceProvider _services;

    public WorkflowAIAdvisor(IServiceProvider services)
    {
        _services = services;
    }

    public async Task<RetryAdvice> GetRetryAdviceAsync(
        WorkflowStep step,
        string errorMessage,
        int retryCount,
        Dictionary<string, object?>? context = null)
    {
        try
        {
            var assistant = _services.GetService(typeof(IAIWorkflowAssistant)) as IAIWorkflowAssistant;
            if (assistant == null)
            {
                return NoRetry("AI 工作流助手不可用，保守不重试。");
            }

            var suggestion = await assistant.GetRetrySuggestionAsync(step, errorMessage, retryCount);
            if (suggestion == null)
            {
                return NoRetry("AI 未给出重试建议，保守不重试。");
            }

            return new RetryAdvice
            {
                ShouldRetry = suggestion.ShouldRetry,
                AdjustedParameters = suggestion.AdjustedParameters,
                AlternativeToolName = suggestion.AlternativeToolName,
                DelayMs = suggestion.DelayMs,
                Reason = suggestion.Reason
            };
        }
        catch (Exception)
        {
            // 顾问自身异常不得打断工作流：降级为不重试，由上层按原失败逻辑处理。
            return NoRetry("AI 重试建议生成异常，保守不重试。");
        }
    }

    private static RetryAdvice NoRetry(string reason) => new RetryAdvice
    {
        ShouldRetry = false,
        Reason = reason
    };
}

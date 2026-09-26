using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Core;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

public interface IAIWorkflowAssistant
{
    Task<FailureAnalysis> AnalyzeFailureAsync(WorkflowStep step, string errorMessage, Dictionary<string, object?>? context = null);
    Task<RetrySuggestion> GetRetrySuggestionAsync(WorkflowStep step, string errorMessage, int retryCount);
    Task<string?> SuggestAlternativeToolAsync(WorkflowStep step, string errorMessage);
    Task<string?> AdjustParametersAsync(WorkflowStep step, string errorMessage, string currentParameters);
}

public class FailureAnalysis
{
    public string ErrorType { get; set; } = string.Empty;
    public string RootCause { get; set; } = string.Empty;
    public bool Retryable { get; set; }
    public List<string> Suggestions { get; set; } = new();
    public string? SuggestedFix { get; set; }
}

public class RetrySuggestion
{
    public bool ShouldRetry { get; set; }
    public string? AdjustedParameters { get; set; }
    public string? AlternativeToolName { get; set; }
    public int DelayMs { get; set; } = 1000;
    public string? Reason { get; set; }
}

public class AIWorkflowAssistant : IAIWorkflowAssistant
{
    private readonly IAIAgentService _aiAgentService;
    private readonly IContext _ctx;
    private readonly IToolSelectorService _toolSelectorService;

    public AIWorkflowAssistant(
        IAIAgentService aiAgentService,
        IContext ctx,
        IToolSelectorService toolSelectorService)
    {
        _aiAgentService = aiAgentService;
        // 宿主契约（IToolRegistry）经 Cordis 上下文在运行期以 ctx.Get<T>() 获取（软依赖探测）。
        // 不在构造时解析：宿主契约在 ProvideHostServices 阶段才 seed 进根上下文，晚于插件 Apply
        // （本实例可能被 AIAgentPlugin 在 Apply 阶段 eager 构造），构造期 Get 恒为 null 会抛异常；
        // 延迟到首次使用时解析（此时宿主契约已就绪）。
        _ctx = ctx;
        _toolSelectorService = toolSelectorService;
    }

    public async Task<FailureAnalysis> AnalyzeFailureAsync(WorkflowStep step, string errorMessage, Dictionary<string, object?>? context = null)
    {
        try
        {
            XTrace.Log.Info("[AIWorkflowAssistant] 分析步骤失败原因，步骤: {0}, 错误: {1}", step.Name, errorMessage);

            var systemPrompt = GetAnalysisSystemPrompt();
            var userPrompt = BuildAnalysisUserPrompt(step, errorMessage, context);

            var messages = new List<AIChatMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = userPrompt }
            };

            var aiResponse = await _aiAgentService.ChatAsync(messages, false);
            var analysis = ParseAnalysisResponse(aiResponse);

            XTrace.Log.Info("[AIWorkflowAssistant] 失败分析完成，错误类型: {0}, 可重试: {1}", analysis.ErrorType, analysis.Retryable);

            return analysis;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIWorkflowAssistant] 失败分析异常: {0}", ex.Message);
            return GetFallbackAnalysis(errorMessage);
        }
    }

    public async Task<RetrySuggestion> GetRetrySuggestionAsync(WorkflowStep step, string errorMessage, int retryCount)
    {
        try
        {
            XTrace.Log.Info("[AIWorkflowAssistant] 获取重试建议，步骤: {0}, 重试次数: {1}", step.Name, retryCount);

            var analysis = await AnalyzeFailureAsync(step, errorMessage);
            var suggestion = new RetrySuggestion
            {
                ShouldRetry = analysis.Retryable,
                DelayMs = step.ErrorHandling?.RetryDelayMs ?? 1000,
                Reason = analysis.SuggestedFix ?? analysis.RootCause
            };

            if (analysis.Retryable)
            {
                var adjustedParams = await AdjustParametersAsync(step, errorMessage, step.ParametersJson ?? "{}");
                if (adjustedParams != null)
                {
                    suggestion.AdjustedParameters = adjustedParams;
                }

                if (adjustedParams == null)
                {
                    var altTool = await SuggestAlternativeToolAsync(step, errorMessage);
                    if (altTool != null)
                    {
                        suggestion.AlternativeToolName = altTool;
                    }
                }
            }

            return suggestion;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIWorkflowAssistant] 获取重试建议异常: {0}", ex.Message);
            return new RetrySuggestion
            {
                ShouldRetry = retryCount < (step.ErrorHandling?.MaxRetries ?? 3),
                DelayMs = step.ErrorHandling?.RetryDelayMs ?? 1000,
                Reason = "默认重试策略"
            };
        }
    }

    public async Task<string?> SuggestAlternativeToolAsync(WorkflowStep step, string errorMessage)
    {
        try
        {
            XTrace.Log.Info("[AIWorkflowAssistant] 寻找替代工具，原工具: {0}", step.ToolName);

            var taskDesc = $"{step.Description} {step.Name}";
            var rankedTools = _toolSelectorService.RankTools(taskDesc);
            var alternatives = rankedTools
                .Where(t => t.Tool.Name != step.ToolName && t.MatchScore > 0.3)
                .Take(3)
                .ToList();

            if (alternatives.Count > 0)
            {
                XTrace.Log.Info("[AIWorkflowAssistant] 找到 {0} 个可能的替代工具", alternatives.Count);
                return alternatives[0].Tool.Name;
            }

            return null;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIWorkflowAssistant] 寻找替代工具异常: {0}", ex.Message);
            return null;
        }
    }

    public async Task<string?> AdjustParametersAsync(WorkflowStep step, string errorMessage, string currentParameters)
    {
        try
        {
            XTrace.Log.Info("[AIWorkflowAssistant] 智能调整参数，步骤: {0}", step.Name);

            var toolRegistry = _ctx.Get<IToolRegistry>();
            if (toolRegistry == null) return null;
            var tool = toolRegistry.GetTool(step.ToolName ?? string.Empty);
            if (tool == null) return null;

            var systemPrompt = GetParamAdjustSystemPrompt();
            var userPrompt = BuildParamAdjustUserPrompt(step, errorMessage, currentParameters, tool.ParametersJsonSchema);

            var messages = new List<AIChatMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = userPrompt }
            };

            var aiResponse = await _aiAgentService.ChatAsync(messages, false);
            var adjustedParams = ParseAdjustedParameters(aiResponse, currentParameters);

            return adjustedParams;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIWorkflowAssistant] 参数调整异常: {0}", ex.Message);
            return null;
        }
    }

    private string GetAnalysisSystemPrompt()
    {
        return @"
你是一个专业的工作流故障分析专家。你的任务是分析工作流步骤执行失败的原因，并提供修复建议。

## 分析维度
1. 错误类型分类（参数错误、网络问题、权限问题、数据问题、系统错误等）
2. 根本原因分析
3. 是否可重试
4. 具体的修复建议

## 输出格式
输出一个 JSON 对象：
```json
{
  ""errorType"": ""参数错误|网络问题|权限问题|数据问题|系统错误|其他"",
  ""rootCause"": ""根本原因描述"",
  ""retryable"": true/false,
  ""suggestions"": [""建议1"", ""建议2""],
  ""suggestedFix"": ""建议的修复方案""
}
```
";
    }

    private string BuildAnalysisUserPrompt(WorkflowStep step, string errorMessage, Dictionary<string, object?>? context)
    {
        var contextInfo = context != null ? JsonSerializer.Serialize(context) : "无";
        return $@"
## 步骤信息
- 步骤名称: {step.Name}
- 步骤类型: {step.Type}
- 工具名称: {step.ToolName ?? "无"}
- 步骤描述: {step.Description ?? "无"}
- 当前参数: {step.ParametersJson ?? "{}"}

## 错误信息
{errorMessage}

## 上下文信息
{contextInfo}

请分析这个步骤失败的原因，并输出 JSON 格式的分析结果。
";
    }

    private FailureAnalysis ParseAnalysisResponse(string aiResponse)
    {
        try
        {
            var jsonStart = aiResponse.IndexOf('{');
            var jsonEnd = aiResponse.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = aiResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);
                using var doc = JsonDocument.Parse(jsonStr);
                var root = doc.RootElement;

                var analysis = new FailureAnalysis
                {
                    ErrorType = root.TryGetProperty("errorType", out var errorTypeProp) ? errorTypeProp.GetString() ?? "未知" : "未知",
                    RootCause = root.TryGetProperty("rootCause", out var rootCauseProp) ? rootCauseProp.GetString() ?? "未知原因" : "未知原因",
                    Retryable = root.TryGetProperty("retryable", out var retryableProp) && retryableProp.GetBoolean(),
                    SuggestedFix = root.TryGetProperty("suggestedFix", out var fixProp) ? fixProp.GetString() : null
                };

                if (root.TryGetProperty("suggestions", out var suggestionsProp))
                {
                    foreach (var suggestion in suggestionsProp.EnumerateArray())
                    {
                        analysis.Suggestions.Add(suggestion.GetString() ?? string.Empty);
                    }
                }

                return analysis;
            }
        }
        catch (JsonException ex)
        {
            XTrace.Log.Warn("[AIWorkflowAssistant] 解析分析响应JSON失败: {0}", ex.Message);
        }

        return new FailureAnalysis
        {
            ErrorType = "未知",
            RootCause = "无法解析AI响应",
            Retryable = true,
            Suggestions = new List<string> { "请检查参数和网络连接" }
        };
    }

    private FailureAnalysis GetFallbackAnalysis(string errorMessage)
    {
        return new FailureAnalysis
        {
            ErrorType = "未知",
            RootCause = errorMessage,
            Retryable = true,
            Suggestions = new List<string> { "请稍后重试", "检查输入参数" },
            SuggestedFix = "请检查参数后重试"
        };
    }

    private string GetParamAdjustSystemPrompt()
    {
        return @"
你是一个参数调优专家。你的任务是根据错误信息分析参数问题，并提供修正后的参数。

## 输出格式
只输出修正后的 JSON 参数对象，不要包含任何其他说明文字。
如果无法确定如何修正，请返回原始参数。
";
    }

    private string BuildParamAdjustUserPrompt(WorkflowStep step, string errorMessage, string currentParameters, string paramSchema)
    {
        return $@"
## 步骤信息
- 步骤名称: {step.Name}
- 工具名称: {step.ToolName}

## 错误信息
{errorMessage}

## 当前参数
```json
{currentParameters}
```

## 参数 Schema
```json
{paramSchema}
```

请根据错误信息调整参数，输出修正后的 JSON。
";
    }

    private string? ParseAdjustedParameters(string aiResponse, string originalParams)
    {
        try
        {
            var jsonStart = aiResponse.IndexOf('{');
            var jsonEnd = aiResponse.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = aiResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);
                using var doc = JsonDocument.Parse(jsonStr);
                return doc.RootElement.GetRawText();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }
}

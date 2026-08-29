using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Core;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

public class AIAgentService : IAIAgentService
{
    private readonly IContext _ctx;
    private readonly HttpClient _httpClient;
    private readonly IWorkflowRecommendationService? _workflowRecommendationService;
    private readonly IScriptTemplateService? _scriptTemplateService;
    private IConfigurationService? _configService;
    private ILogService? _logService;
    private IToolRegistry? _toolRegistry;
    private AIConfig? _aiConfig;

    public AIAgentService(
        IContext ctx,
        IWorkflowRecommendationService? workflowRecommendationService = null,
        IScriptTemplateService? scriptTemplateService = null)
    {
        // 宿主契约（配置/日志/工具注册表）经 Cordis 上下文在运行期以 ctx.Get<T>() 获取（软依赖探测）：
        // 不在构造时解析——宿主契约在 ProvideHostServices 阶段才 seed 进根上下文，晚于插件 Apply
        // （本实例可能被 AIAgentPlugin 在 Apply 阶段 eager 构造），构造期 Get 恒为 null 会抛异常；
        // 延迟到首次使用时解析（此时宿主契约已就绪）。
        // HttpClient 为无状态 HTTP 客户端由本服务自建；推荐服务与脚本模板服务为可选扩展（未注册时置空、运行期降级）。
        _ctx = ctx;
        _httpClient = new HttpClient();
        _workflowRecommendationService = workflowRecommendationService;
        _scriptTemplateService = scriptTemplateService;
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
    }

    private IConfigurationService ConfigService => _configService ??= _ctx.Get<IConfigurationService>()
        ?? throw new InvalidOperationException("宿主未提供 IConfigurationService 契约，无法初始化 AI 代理");

    private ILogService LogService => _logService ??= _ctx.Get<ILogService>()
        ?? throw new InvalidOperationException("宿主未提供 ILogService 契约，无法初始化 AI 代理");

    private IToolRegistry ToolRegistry => _toolRegistry ??= _ctx.Get<IToolRegistry>()
        ?? throw new InvalidOperationException("宿主未提供 IToolRegistry 契约，无法初始化 AI 代理");

    private AIConfig AiConfig
    {
        get
        {
            if (_aiConfig == null)
            {
                var coreConfig = ConfigService.GetAIConfig();
                _aiConfig = new AIConfig
                {
                    ApiEndpoint = coreConfig.ApiEndpoint,
                    ApiKey = coreConfig.ApiKey,
                    ModelName = coreConfig.ModelName
                };
            }
            return _aiConfig;
        }
    }

    public async Task<string> ChatAsync(List<AIChatMessage> messages, bool enableTools = true)
    {
        try
        {
            var toolMessages = await ChatWithToolsAsync(messages);
            var lastMessage = toolMessages.LastOrDefault();
            return lastMessage?.Content ?? string.Empty;
        }
        catch (Exception ex)
        {
            LogService.Error("[AIAgentPlugin] AI请求失败: {0}", ex.Message);
            throw;
        }
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        List<AIChatMessage> messages,
        bool enableTools = true,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new AIChatRequest
        {
            Model = AiConfig.ModelName,
            Messages = messages,
            Stream = true
        };

        if (enableTools)
        {
            var tools = ToolRegistry.GetToolDefinitions();
            if (tools.Count > 0)
            {
                request.Tools = tools;
                request.ToolChoice = "auto";
            }
        }

        var jsonContent = JsonSerializer.Serialize(request);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, AiConfig.ApiEndpoint);
        httpRequest.Content = content;
        httpRequest.Headers.Add("Authorization", $"Bearer {AiConfig.ApiKey}");

        XTrace.Log.Info("[AIAgentPlugin] 发送AI流式请求: {0}", AiConfig.ApiEndpoint);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] AI流式请求失败: {0}", ex.Message);
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);

            if (line == null)
                break;

            if (string.IsNullOrEmpty(line))
                continue;

            if (line.StartsWith("data: "))
            {
                var data = line[6..];

                if (data == "[DONE]")
                {
                    XTrace.Log.Info("[AIAgentPlugin] AI流式响应完成");
                    yield break;
                }

                AIChatResponse? chunk;
                try
                {
                    chunk = JsonSerializer.Deserialize<AIChatResponse>(data, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (JsonException ex)
                {
                    XTrace.Log.Warn("[AIAgentPlugin] 解析流式响应失败: {0}", ex.Message);
                    continue;
                }

                if (chunk?.Choices?.Count > 0)
                {
                    var delta = chunk.Choices[0].Delta;
                    if (delta != null && !string.IsNullOrEmpty(delta.Content))
                    {
                        yield return delta.Content;
                    }
                }
            }
        }
    }

    public async Task<List<AIChatMessage>> ChatWithToolsAsync(List<AIChatMessage> messages, CancellationToken cancellationToken = default)
    {
        var allMessages = new List<AIChatMessage>(messages);
        var maxIterations = 10;
        var currentIteration = 0;
        var totalToolCalls = 0;

        XTrace.Log.Info("[AIAgentPlugin] ========================================");
        XTrace.Log.Info("[AIAgentPlugin] 开始AI对话（支持工具调用）");
        XTrace.Log.Info("[AIAgentPlugin] 初始消息数: {0}", allMessages.Count);
        XTrace.Log.Info("[AIAgentPlugin] 最大迭代次数: {0}", maxIterations);

        var tools = ToolRegistry.GetToolDefinitions();
        XTrace.Log.Info("[AIAgentPlugin] 可用工具数量: {0}", tools.Count);
        foreach (var tool in tools)
        {
            XTrace.Log.Debug("[AIAgentPlugin]   - {0}: {1}", tool.Function.Name, tool.Function.Description);
        }

        await InjectRelevantMemoriesAsync(allMessages, tools, cancellationToken);

        while (currentIteration < maxIterations)
        {
            currentIteration++;
            XTrace.Log.Info("[AIAgentPlugin] --- 第 {0} 轮迭代 ---", currentIteration);

            var request = new AIChatRequest
            {
                Model = AiConfig.ModelName,
                Messages = allMessages,
                Stream = false
            };

            if (tools.Count > 0)
            {
                request.Tools = tools;
                request.ToolChoice = "auto";
            }

            var jsonContent = JsonSerializer.Serialize(request);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, AiConfig.ApiEndpoint);
            httpRequest.Content = content;
            httpRequest.Headers.Add("Authorization", $"Bearer {AiConfig.ApiKey}");

            XTrace.Log.Info("[AIAgentPlugin] 发送AI请求到: {0}", AiConfig.ApiEndpoint);
            XTrace.Log.Debug("[AIAgentPlugin] 请求模型: {0}", AiConfig.ModelName);

            try
            {
                var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var aiResponse = JsonSerializer.Deserialize<AIChatResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (aiResponse?.Choices?.Count == 0)
                {
                    XTrace.Log.Warn("[AIAgentPlugin] AI响应为空，结束对话");
                    break;
                }

                var choice = aiResponse!.Choices[0];
                var message = choice.Message;

                if (message == null)
                {
                    XTrace.Log.Warn("[AIAgentPlugin] AI响应消息为空，结束对话");
                    break;
                }

                XTrace.Log.Info("[AIAgentPlugin] AI回复角色: {0}", message.Role);
                XTrace.Log.Debug("[AIAgentPlugin] AI回复内容: {0}", message.Content);

                allMessages.Add(new AIChatMessage
                {
                    Role = message.Role,
                    Content = message.Content ?? string.Empty,
                    ToolCalls = message.ToolCalls
                });

                if (message.ToolCalls == null || message.ToolCalls.Count == 0)
                {
                    XTrace.Log.Info("[AIAgentPlugin] AI未请求工具调用，对话结束");
                    break;
                }

                totalToolCalls += message.ToolCalls.Count;
                XTrace.Log.Info("[AIAgentPlugin] AI请求调用 {0} 个工具", message.ToolCalls.Count);

                foreach (var toolCall in message.ToolCalls)
                {
                    var toolName = toolCall.Function.Name;
                    var toolArgs = toolCall.Function.Arguments;

                    XTrace.Log.Info("[AIAgentPlugin] >>> 调用工具: {0}", toolName);
                    XTrace.Log.Debug("[AIAgentPlugin]     工具ID: {0}", toolCall.Id);
                    XTrace.Log.Debug("[AIAgentPlugin]     工具参数: {0}", toolArgs);

                    var toolResult = await ToolRegistry.ExecuteToolWithTimeoutAsync(toolName, toolArgs, 30, cancellationToken);

                    XTrace.Log.Info("[AIAgentPlugin] <<< 工具执行结果: {0} (耗时: {1}ms)",
                        toolResult.Success ? "成功" : "失败",
                        toolResult.DurationMs);

                    if (!toolResult.Success)
                    {
                        XTrace.Log.Warn("[AIAgentPlugin]     错误信息: {0}", toolResult.ErrorMessage);
                    }

                    allMessages.Add(new AIChatMessage
                    {
                        Role = "tool",
                        Content = toolResult.Result,
                        ToolCallId = toolCall.Id,
                        Name = toolName
                    });
                }

                if (choice.FinishReason == "stop" || choice.FinishReason == "length")
                {
                    XTrace.Log.Info("[AIAgentPlugin] AI完成原因: {0}，结束对话", choice.FinishReason);
                    break;
                }
            }
            catch (Exception ex)
            {
                XTrace.Log.Error("[AIAgentPlugin] AI请求异常: {0}", ex.Message);
                XTrace.Log.Debug("[AIAgentPlugin] 异常堆栈: {0}", ex.StackTrace);
                throw;
            }
        }

        if (currentIteration >= maxIterations)
        {
            XTrace.Log.Warn("[AIAgentPlugin] 达到最大工具调用迭代次数: {0}，强制结束", maxIterations);
        }

        XTrace.Log.Info("[AIAgentPlugin] 对话结束，总消息数: {0}", allMessages.Count);
        XTrace.Log.Info("[AIAgentPlugin] 总迭代次数: {0}", currentIteration);
        XTrace.Log.Info("[AIAgentPlugin] 总工具调用次数: {0}", totalToolCalls);
        XTrace.Log.Info("[AIAgentPlugin] ========================================");

        return allMessages;
    }

    public async Task<List<WorkflowRecommendationDto>> GetRecommendedWorkflowsAsync(string userMessage, int limit = 5)
    {
        try
        {
            XTrace.Log.Info("[AIAgentPlugin] 获取工作流推荐，用户消息: {0}", userMessage);

            if (_workflowRecommendationService == null)
            {
                XTrace.Log.Warn("[AIAgentPlugin] 工作流推荐服务未注册");
                return new List<WorkflowRecommendationDto>();
            }

            var recommendations = await _workflowRecommendationService.GetRecommendedWorkflowsAsync(
                null,
                userMessage,
                limit);

            XTrace.Log.Info("[AIAgentPlugin] 找到 {0} 个推荐工作流", recommendations.Count);

            return recommendations;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] 获取工作流推荐失败: {0}", ex.Message);
            return new List<WorkflowRecommendationDto>();
        }
    }

    public async Task<GenerateScriptResponse> GenerateScriptAsync(string language, string description, string? requirements = null)
    {
        try
        {
            XTrace.Log.Info("[AIAgentPlugin] 生成脚本，语言: {0}, 描述: {1}", language, description);

            var systemPrompt = BuildScriptGeneratorSystemPrompt();
            var userPrompt = BuildScriptGeneratorUserPrompt(language, description, requirements);

            var messages = new List<AIChatMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = userPrompt }
            };

            var aiResponse = await ChatAsync(messages, false);
            var response = ParseGenerateScriptResponse(aiResponse, language);

            XTrace.Log.Info("[AIAgentPlugin] 脚本生成成功，代码长度: {0}", response.Code.Length);

            return response;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] 脚本生成失败: {0}", ex.Message);
            return new GenerateScriptResponse
            {
                Code = string.Empty,
                Description = description,
                Language = language,
                Parameters = []
            };
        }
    }

    public async Task<AnalyzeScriptErrorResponse> AnalyzeScriptErrorAsync(string language, string code, string errorMessage)
    {
        try
        {
            XTrace.Log.Info("[AIAgentPlugin] 分析脚本错误，语言: {0}", language);

            var systemPrompt = BuildScriptDebugSystemPrompt();
            var userPrompt = BuildAnalyzeErrorUserPrompt(language, code, errorMessage);

            var messages = new List<AIChatMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = userPrompt }
            };

            var aiResponse = await ChatAsync(messages, false);
            var response = ParseAnalyzeErrorResponse(aiResponse, errorMessage);

            XTrace.Log.Info("[AIAgentPlugin] 错误分析完成");

            return response;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] 错误分析失败: {0}", ex.Message);
            return new AnalyzeScriptErrorResponse
            {
                ErrorAnalysis = $"分析失败: {ex.Message}",
                PossibleCauses = [],
                Suggestion = "请检查代码逻辑和语法"
            };
        }
    }

    public async Task<SuggestScriptFixResponse> SuggestScriptFixAsync(string language, string code, string errorMessage)
    {
        try
        {
            XTrace.Log.Info("[AIAgentPlugin] 建议脚本修复，语言: {0}", language);

            var systemPrompt = BuildScriptDebugSystemPrompt();
            var userPrompt = BuildSuggestFixUserPrompt(language, code, errorMessage);

            var messages = new List<AIChatMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = userPrompt }
            };

            var aiResponse = await ChatAsync(messages, false);
            var response = ParseSuggestFixResponse(aiResponse, code, errorMessage);

            XTrace.Log.Info("[AIAgentPlugin] 修复建议生成完成");

            return response;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] 修复建议生成失败: {0}", ex.Message);
            return new SuggestScriptFixResponse
            {
                OriginalCode = code,
                FixedCode = code,
                ErrorAnalysis = $"分析失败: {ex.Message}",
                Changes = [],
                Explanation = "无法生成修复建议，请手动检查代码"
            };
        }
    }

    public async Task<List<ScriptTemplate>> GetScriptTemplatesAsync(string? category = null)
    {
        try
        {
            XTrace.Log.Info("[AIAgentPlugin] 获取脚本模板，分类: {0}", category ?? "全部");

            if (_scriptTemplateService == null)
            {
                XTrace.Log.Warn("[AIAgentPlugin] 脚本模板服务未注册");
                return [];
            }

            var templates = await _scriptTemplateService.GetTemplatesAsync(category);
            XTrace.Log.Info("[AIAgentPlugin] 找到 {0} 个模板", templates.Count);

            return templates;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] 获取脚本模板失败: {0}", ex.Message);
            return [];
        }
    }

    private static string BuildScriptGeneratorSystemPrompt()
    {
        return @"你是一个专业的脚本生成助手，擅长根据用户需求生成高质量的脚本代码。

你的任务：
1. 根据用户提供的语言和需求描述，生成功能完整的脚本
2. 确保代码质量高、可读性好、结构清晰
3. 包含必要的注释，说明关键逻辑和使用方法
4. 添加适当的错误处理和边界情况处理
5. 提供参数化支持，让脚本更灵活易用

输出格式要求（严格按照 JSON 格式输出，不要包含 Markdown 代码块标记）：
{
  ""code"": ""完整的脚本代码"",
  ""description"": ""脚本功能描述"",
  ""parameters"": [
    {
      ""name"": ""参数名称"",
      ""type"": ""参数类型：String/Number/Boolean/Select/FilePath/DirectoryPath"",
      ""description"": ""参数说明"",
      ""defaultValue"": ""默认值"",
      ""isRequired"": true/false,
      ""options"": [""选项1"", ""选项2""]
    }
  ]
}

注意事项：
- 只输出 JSON，不要输出其他解释性文字
- 代码中的双引号要正确转义
- 确保 JSON 格式正确，可以被直接解析";
    }

    private static string BuildScriptGeneratorUserPrompt(string language, string description, string? requirements)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"请生成一个 {language} 脚本，功能描述如下：");
        sb.AppendLine(description);
        sb.AppendLine();

        if (!string.IsNullOrEmpty(requirements))
        {
            sb.AppendLine("附加要求：");
            sb.AppendLine(requirements);
            sb.AppendLine();
        }

        sb.AppendLine("请按照指定的 JSON 格式输出结果。");
        return sb.ToString();
    }

    private static string BuildScriptDebugSystemPrompt()
    {
        return @"你是一个专业的脚本调试助手，擅长分析和修复各种脚本语言的错误。

你的能力：
1. 准确分析错误原因，定位问题所在
2. 提供多种可能的原因分析
3. 给出具体的修复建议
4. 提供修复后的完整代码
5. 解释修改的原因，帮助用户理解

分析错误时需要考虑：
- 语法错误
- 逻辑错误
- 运行时环境问题
- 依赖缺失
- 路径问题
- 权限问题
- 编码问题

输出格式（分析错误时，严格按 JSON 格式）：
{
  ""errorAnalysis"": ""错误原因的详细分析"",
  ""possibleCauses"": [""可能原因1"", ""可能原因2"", ""可能原因3""],
  ""suggestion"": ""修复建议概述""
}

输出格式（建议修复时，严格按 JSON 格式）：
{
  ""errorAnalysis"": ""错误原因分析"",
  ""fixedCode"": ""修复后的完整代码"",
  ""changes"": [""修改1"", ""修改2""],
  ""explanation"": ""详细的修改说明和原理解释""
}

注意事项：
- 只输出 JSON，不要输出其他解释性文字
- 代码中的双引号要正确转义
- 确保 JSON 格式正确，可以被直接解析";
    }

    private static string BuildAnalyzeErrorUserPrompt(string language, string code, string errorMessage)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"请分析以下 {language} 脚本的错误：");
        sb.AppendLine();
        sb.AppendLine("错误信息：");
        sb.AppendLine(errorMessage);
        sb.AppendLine();
        sb.AppendLine("脚本代码：");
        sb.AppendLine("```");
        sb.AppendLine(code);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("请按照分析错误的 JSON 格式输出结果。");
        return sb.ToString();
    }

    private static string BuildSuggestFixUserPrompt(string language, string code, string errorMessage)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"请修复以下 {language} 脚本的错误：");
        sb.AppendLine();
        sb.AppendLine("错误信息：");
        sb.AppendLine(errorMessage);
        sb.AppendLine();
        sb.AppendLine("原始代码：");
        sb.AppendLine("```");
        sb.AppendLine(code);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("请按照建议修复的 JSON 格式输出结果，包含修复后的完整代码。");
        return sb.ToString();
    }

    private static GenerateScriptResponse ParseGenerateScriptResponse(string aiResponse, string language)
    {
        try
        {
            var json = ExtractJson(aiResponse);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var code = root.GetProperty("code").GetString() ?? string.Empty;
            var description = root.GetProperty("description").GetString() ?? string.Empty;
            var parameters = new List<ScriptParameter>();

            if (root.TryGetProperty("parameters", out var paramsProp) && paramsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var param in paramsProp.EnumerateArray())
                {
                    var sp = new ScriptParameter
                    {
                        Name = param.GetProperty("name").GetString() ?? string.Empty,
                        Description = param.GetProperty("description").GetString() ?? string.Empty,
                        DefaultValue = param.GetProperty("defaultValue").GetString(),
                        IsRequired = param.GetProperty("isRequired").GetBoolean()
                    };

                    if (param.TryGetProperty("type", out var typeProp))
                    {
                        var typeStr = typeProp.GetString() ?? "String";
                        if (Enum.TryParse<ScriptParameterType>(typeStr, true, out var paramType))
                        {
                            sp.Type = paramType;
                        }
                    }

                    if (param.TryGetProperty("options", out var optionsProp) && optionsProp.ValueKind == JsonValueKind.Array)
                    {
                        sp.Options = optionsProp.EnumerateArray().Select(o => o.GetString() ?? string.Empty).ToList();
                    }

                    parameters.Add(sp);
                }
            }

            return new GenerateScriptResponse
            {
                Code = code,
                Description = description,
                Language = language,
                Parameters = parameters
            };
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[AIAgentPlugin] 解析脚本生成响应失败: {0}", ex.Message);
            return new GenerateScriptResponse
            {
                Code = aiResponse,
                Description = "AI 生成的脚本",
                Language = language,
                Parameters = []
            };
        }
    }

    private static AnalyzeScriptErrorResponse ParseAnalyzeErrorResponse(string aiResponse, string originalError)
    {
        try
        {
            var json = ExtractJson(aiResponse);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var errorAnalysis = root.GetProperty("errorAnalysis").GetString() ?? originalError;
            var possibleCauses = new List<string>();

            if (root.TryGetProperty("possibleCauses", out var causesProp) && causesProp.ValueKind == JsonValueKind.Array)
            {
                possibleCauses = causesProp.EnumerateArray().Select(c => c.GetString() ?? string.Empty).ToList();
            }

            var suggestion = root.GetProperty("suggestion").GetString() ?? "请检查代码";

            return new AnalyzeScriptErrorResponse
            {
                ErrorAnalysis = errorAnalysis,
                PossibleCauses = possibleCauses,
                Suggestion = suggestion
            };
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[AIAgentPlugin] 解析错误分析响应失败: {0}", ex.Message);
            return new AnalyzeScriptErrorResponse
            {
                ErrorAnalysis = aiResponse,
                PossibleCauses = [originalError],
                Suggestion = "请检查代码逻辑"
            };
        }
    }

    private static SuggestScriptFixResponse ParseSuggestFixResponse(string aiResponse, string originalCode, string errorMessage)
    {
        try
        {
            var json = ExtractJson(aiResponse);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var errorAnalysis = root.GetProperty("errorAnalysis").GetString() ?? errorMessage;
            var fixedCode = root.GetProperty("fixedCode").GetString() ?? originalCode;
            var changes = new List<string>();

            if (root.TryGetProperty("changes", out var changesProp) && changesProp.ValueKind == JsonValueKind.Array)
            {
                changes = changesProp.EnumerateArray().Select(c => c.GetString() ?? string.Empty).ToList();
            }

            var explanation = root.GetProperty("explanation").GetString() ?? string.Empty;

            return new SuggestScriptFixResponse
            {
                OriginalCode = originalCode,
                FixedCode = fixedCode,
                ErrorAnalysis = errorAnalysis,
                Changes = changes,
                Explanation = explanation
            };
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[AIAgentPlugin] 解析修复建议响应失败: {0}", ex.Message);
            return new SuggestScriptFixResponse
            {
                OriginalCode = originalCode,
                FixedCode = originalCode,
                ErrorAnalysis = aiResponse,
                Changes = [],
                Explanation = "无法解析修复建议"
            };
        }
    }

    private async Task InjectRelevantMemoriesAsync(List<AIChatMessage> allMessages, List<AIToolDefinition> tools, CancellationToken cancellationToken)
    {
        try
        {
            var hasMemoryTool = tools.Any(t => t.Function.Name == "get_relevant_memories");
            if (!hasMemoryTool)
            {
                XTrace.Log.Debug("[AIAgentPlugin] 未检测到记忆检索工具，跳过记忆注入");
                return;
            }

            var lastUserMessage = allMessages.LastOrDefault(m => m.Role == "user");
            if (lastUserMessage == null || string.IsNullOrWhiteSpace(lastUserMessage.Content))
            {
                XTrace.Log.Debug("[AIAgentPlugin] 无用户消息，跳过记忆注入");
                return;
            }

            XTrace.Log.Info("[AIAgentPlugin] 正在检索相关记忆...");

            var query = lastUserMessage.Content;
            if (query.Length > 500)
                query = query[..500];

            var toolParams = JsonSerializer.Serialize(new { query = query, limit = 5 });
            var toolResult = await ToolRegistry.ExecuteToolWithTimeoutAsync(
                "get_relevant_memories",
                toolParams,
                15,
                cancellationToken);

            if (!toolResult.Success)
            {
                XTrace.Log.Warn("[AIAgentPlugin] 记忆检索失败: {0}", toolResult.ErrorMessage);
                return;
            }

            using var doc = JsonDocument.Parse(toolResult.Result);
            var root = doc.RootElement;

            if (!root.TryGetProperty("success", out var successProp) || !successProp.GetBoolean())
            {
                XTrace.Log.Warn("[AIAgentPlugin] 记忆检索返回失败");
                return;
            }

            if (!root.TryGetProperty("memories", out var memoriesProp) || memoriesProp.ValueKind != JsonValueKind.Array)
            {
                XTrace.Log.Debug("[AIAgentPlugin] 没有找到相关记忆");
                return;
            }

            var memories = memoriesProp.EnumerateArray().ToList();
            if (memories.Count == 0)
            {
                XTrace.Log.Info("[AIAgentPlugin] 没有找到相关记忆");
                return;
            }

            XTrace.Log.Info("[AIAgentPlugin] 找到 {0} 条相关记忆，注入到对话中", memories.Count);

            var sb = new StringBuilder();
            sb.AppendLine("## 相关记忆（长期记忆）");
            sb.AppendLine("以下是从你的长期记忆库中检索到的与当前对话相关的信息。请在回答时参考这些记忆，提供更个性化、更准确的回答。");
            sb.AppendLine();

            for (int i = 0; i < memories.Count; i++)
            {
                var mem = memories[i];
                var title = mem.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "无标题" : "无标题";
                var content = mem.TryGetProperty("content", out var contentProp) ? contentProp.GetString() ?? string.Empty : string.Empty;
                var type = mem.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "fact" : "fact";
                var importance = mem.TryGetProperty("importance", out var impProp) ? impProp.GetString() ?? "medium" : "medium";
                var relevanceScore = mem.TryGetProperty("relevanceScore", out var scoreProp) ? scoreProp.GetDouble() : 0;

                sb.AppendLine($"### 记忆 {i + 1}: {title}");
                sb.AppendLine($"- 类型: {type}");
                sb.AppendLine($"- 重要程度: {importance}");
                sb.AppendLine($"- 相关度: {relevanceScore:F2}");
                sb.AppendLine($"- 内容: {content}");
                sb.AppendLine();
            }

            sb.AppendLine("---");
            sb.AppendLine("请根据以上记忆提供回答。如果记忆中的信息与当前问题无关，可以忽略。");
            sb.AppendLine("不要在回答中主动提及你使用了记忆，直接基于记忆内容回答即可。");

            var memoryMessage = sb.ToString();

            var systemMessage = allMessages.FirstOrDefault(m => m.Role == "system");
            if (systemMessage != null)
            {
                systemMessage.Content = memoryMessage + Environment.NewLine + Environment.NewLine + systemMessage.Content;
            }
            else
            {
                allMessages.Insert(0, new AIChatMessage
                {
                    Role = "system",
                    Content = memoryMessage
                });
            }

            XTrace.Log.Info("[AIAgentPlugin] 记忆注入完成，记忆内容长度: {0}", memoryMessage.Length);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] 记忆注入失败: {0}", ex.Message);
        }
    }

    private static string ExtractJson(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "{}";

        var firstBrace = input.IndexOf('{');
        var lastBrace = input.LastIndexOf('}');

        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            return input[firstBrace..(lastBrace + 1)];
        }

        return input.Trim();
    }
}

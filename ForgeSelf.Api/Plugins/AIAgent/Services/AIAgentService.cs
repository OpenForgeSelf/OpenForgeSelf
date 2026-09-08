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
    private readonly IAgentRegistryService? _agentRegistry;
    private IConfigurationService? _configService;
    private ILogService? _logService;
    private IToolRegistry? _toolRegistry;
    private IAIProviderRegistry? _providerRegistry;
    private AIConfig? _aiConfig;

    public AIAgentService(
        IContext ctx,
        IWorkflowRecommendationService? workflowRecommendationService = null,
        IScriptTemplateService? scriptTemplateService = null,
        IAgentRegistryService? agentRegistry = null)
    {
        // 宿主契约（配置/日志/工具注册表/provider 注册表）经 Cordis 上下文在运行期以 ctx.Get<T>() 获取（软依赖探测）：
        // 不在构造时解析——宿主契约在 ProvideHostServices 阶段才 seed 进根上下文，晚于插件 Apply
        // （本实例可能被 AIAgentPlugin 在 Apply 阶段 eager 构造），构造期 Get 恒为 null 会抛异常；
        // 延迟到首次使用时解析（此时宿主契约已就绪）。
        // HttpClient 为无状态 HTTP 客户端由本服务自建；推荐服务与脚本模板服务为可选扩展（未注册时置空、运行期降级）。
        _ctx = ctx;
        _httpClient = new HttpClient();
        _workflowRecommendationService = workflowRecommendationService;
        _scriptTemplateService = scriptTemplateService;
        _agentRegistry = agentRegistry;
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
    }

    private IConfigurationService ConfigService => _configService ??= _ctx.Get<IConfigurationService>()
        ?? throw new InvalidOperationException("宿主未提供 IConfigurationService 契约，无法初始化 AI 代理");

    private ILogService LogService => _logService ??= _ctx.Get<ILogService>()
        ?? throw new InvalidOperationException("宿主未提供 ILogService 契约，无法初始化 AI 代理");

    private IToolRegistry ToolRegistry => _toolRegistry ??= _ctx.Get<IToolRegistry>()
        ?? throw new InvalidOperationException("宿主未提供 IToolRegistry 契约，无法初始化 AI 代理");

    /// <summary>宿主 AI 提供方注册表（软依赖）：经它按 chatModelId 解析上游 provider。</summary>
    private IAIProviderRegistry? ProviderRegistry => _providerRegistry ??= _ctx.Get<IAIProviderRegistry>();

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
        await foreach (var ev in RunAgentLoopAsync(messages, null, null, enableTools, cancellationToken))
        {
            if (ev.Type == "content" && !string.IsNullOrEmpty(ev.Content))
                yield return ev.Content;
        }
    }

    public async Task<List<AIChatMessage>> ChatWithToolsAsync(List<AIChatMessage> messages, CancellationToken cancellationToken = default)
    {
        // 复用同一工具循环（驱动到完成、忽略事件流）；传入副本避免污染调用方列表。
        var grown = new List<AIChatMessage>(messages);
        await foreach (var _ in RunAgentLoopAsync(grown, null, null, true, cancellationToken))
        {
            // 事件由流式调用方消费，此处仅驱动循环
        }
        return grown;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AgentLoopEvent> RunAgentLoopAsync(
        List<AIChatMessage> messages,
        string? chatModelId = null,
        string? agentId = null,
        bool enableTools = true,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var provider = ResolveProvider(chatModelId);
        if (provider == null)
        {
            yield return new AgentLoopEvent { Type = "error", Content = "未找到可用的 AI 提供方（检查提供方配置与模型路由）" };
            yield break;
        }

        // 工具范围：只挂本插件自己的工具（时间/计算/项目文件/工作流等 ~9 个），
        // 而不是宿主 IToolRegistry 的全部 ~77 个——全量工具会把 prompt 撑爆，
        // 本地小模型（如 gemma-4-e4b）上下文不足导致上游 400（实测 12s 后 400）。
        var tools = enableTools ? ResolveOwnToolDefinitions() : new List<AIToolDefinition>();
        var unifiedTools = tools.Count > 0
            ? tools.Select(t => new UnifiedToolDefinition
            {
                Name = t.Function.Name,
                Description = t.Function.Description,
                Parameters = t.Function.Parameters
            }).ToList()
            : null;

        // 记忆注入（get_relevant_memories 工具注册前为 no-op，见 Stage 2）。
        if (enableTools && tools.Count > 0)
            await InjectRelevantMemoriesAsync(messages, tools, cancellationToken);

        var systemPrompt = ResolveSystemPrompt(agentId);
        var model = ResolveModelId(chatModelId);

        XTrace.Log.Info("[AIAgentPlugin] 开始 Agent 工具循环，provider: {0}, model: {1}, agent: {2}, 工具数: {3}",
            provider.ProviderName, model, agentId ?? "(默认)", unifiedTools?.Count ?? 0);

        const int maxIterations = 10;
        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            var request = new UnifiedChatRequest
            {
                Model = model,
                Messages = messages.Select(ToUnifiedMessage).ToList(),
                Tools = unifiedTools,
                SystemPrompt = systemPrompt,
                Stream = true,
                StreamOptions = new UnifiedStreamOptions { IncludeUsage = true }
            };

            var contentBuilder = new StringBuilder();
            var pendingToolCalls = new List<UnifiedToolCall>();
            UnifiedUsage? usage = null;

            await foreach (var chunk in provider.ChatStreamAsync(request, cancellationToken))
            {
                if (!string.IsNullOrEmpty(chunk.DeltaContent))
                {
                    contentBuilder.Append(chunk.DeltaContent);
                    yield return new AgentLoopEvent { Type = "content", Content = chunk.DeltaContent };
                }
                if (chunk.DeltaToolCall != null)
                    AccumulateToolCall(pendingToolCalls, chunk.DeltaToolCall);
                if (chunk.Usage != null)
                    usage = chunk.Usage;
            }

            var assistantContent = contentBuilder.ToString();

            // 无工具调用 → 最终回答，结束。
            if (pendingToolCalls.Count == 0)
            {
                messages.Add(new AIChatMessage { Role = "assistant", Content = assistantContent });
                yield return new AgentLoopEvent { Type = "done", Content = assistantContent, Usage = usage };
                yield break;
            }

            // 有工具调用 → 记录 assistant 消息（含 toolCalls），逐个执行后继续。
            messages.Add(new AIChatMessage
            {
                Role = "assistant",
                Content = assistantContent,
                ToolCalls = pendingToolCalls.Select(tc => new AIToolCall
                {
                    Id = tc.Id,
                    Function = new AIFunctionCall { Name = tc.Name, Arguments = tc.Arguments }
                }).ToList()
            });

            foreach (var toolCall in pendingToolCalls)
            {
                yield return new AgentLoopEvent { Type = "tool_call", Name = toolCall.Name, Arguments = toolCall.Arguments };

                ToolExecutionResult toolResult;
                try
                {
                    toolResult = await ToolRegistry.ExecuteToolWithTimeoutAsync(toolCall.Name, toolCall.Arguments, 30, cancellationToken);
                }
                catch (Exception ex)
                {
                    XTrace.Log.Error("[AIAgentPlugin] 工具 {0} 执行异常: {1}", toolCall.Name, ex.Message);
                    toolResult = new ToolExecutionResult { Success = false, Result = string.Empty, ErrorMessage = ex.Message };
                }

                yield return new AgentLoopEvent { Type = "tool_result", Name = toolCall.Name, Result = toolResult.Result, Success = toolResult.Success };

                messages.Add(new AIChatMessage
                {
                    Role = "tool",
                    Content = toolResult.Result,
                    ToolCallId = toolCall.Id,
                    Name = toolCall.Name
                });
            }
        }

        // 达到最大迭代次数，兜底结束。
        XTrace.Log.Warn("[AIAgentPlugin] 达到最大工具调用迭代次数，强制结束");
        yield return new AgentLoopEvent { Type = "done", Content = string.Empty, Usage = null };
    }

    /// <summary>按 chatModelId 解析上游 provider；未命中回退默认 provider。</summary>
    private IAIProvider? ResolveProvider(string? chatModelId)
    {
        var registry = ProviderRegistry;
        if (registry == null) return null;

        if (!string.IsNullOrWhiteSpace(chatModelId))
        {
            var byId = registry.GetProviderByChatModelId(chatModelId);
            if (byId != null) return byId;
        }
        return registry.GetDefaultProvider();
    }

    /// <summary>
    /// Agent 工具范围：本插件自己的工具 + 记忆系统（memory-system）的 5 个记忆工具。
    /// 不挂全部宿主工具（~77 个会撑爆本地小模型 prompt → 400）。
    /// 记忆工具经 L1 契约（IMemoryService）由 MemorySystem 提供，Agent 可检索注入、也可主动 add_memory。
    /// </summary>
    private List<AIToolDefinition> ResolveOwnToolDefinitions()
    {
        var pluginId = _ctx.Get<PluginMetadata>()?.Id ?? string.Empty;
        var allowedPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            pluginId,        // 本插件自带工具（时间/计算/文件/工作流）
            "memory-system"  // 记忆检索/保存/管理
        };
        var result = new List<AIToolDefinition>();
        foreach (var tool in ToolRegistry.GetAllTools())
        {
            if (!allowedPlugins.Contains(tool.PluginId)) continue;
            object? parameters;
            try
            {
                parameters = JsonDocument.Parse(tool.ParametersJsonSchema).RootElement.Clone();
            }
            catch (Exception ex)
            {
                XTrace.Log.Warn("[AIAgentPlugin] 工具 {0} schema 解析失败，已跳过: {1}", tool.Name, ex.Message);
                continue;
            }
            result.Add(new AIToolDefinition
            {
                Function = new AIFunctionDefinition
                {
                    Name = tool.Name,
                    Description = tool.Description,
                    Parameters = parameters
                }
            });
        }
        return result;
    }

    /// <summary>chatModelId（形如 provider:upstreamModelId）剥前缀取上游模型 id；空则退回宿主默认配置模型。</summary>
    private string ResolveModelId(string? chatModelId)
    {
        if (!string.IsNullOrWhiteSpace(chatModelId))
        {
            var idx = chatModelId.IndexOf(':');
            return idx > 0 ? chatModelId[(idx + 1)..] : chatModelId;
        }
        return AiConfig.ModelName;
    }

    /// <summary>取选中 Agent 的 SystemPrompt；未选/未命中返回 null（不注入）。</summary>
    private string? ResolveSystemPrompt(string? agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId) || _agentRegistry == null) return null;
        var prompt = _agentRegistry.GetAgent(agentId)?.SystemPrompt;
        return string.IsNullOrWhiteSpace(prompt) ? null : prompt;
    }

    /// <summary>Abstractions 消息 → 统一请求消息（含工具调用字段）。</summary>
    private static UnifiedChatMessage ToUnifiedMessage(AIChatMessage m)
    {
        return new UnifiedChatMessage
        {
            Role = m.Role,
            Content = m.Content,
            Name = m.Name,
            ToolCallId = m.ToolCallId,
            ToolCalls = m.ToolCalls?.Select(tc => new UnifiedToolCall
            {
                Id = tc.Id,
                Name = tc.Function.Name,
                Arguments = tc.Function.Arguments
            }).ToList()
        };
    }

    /// <summary>累积流式工具调用增量：首块带 Id 视为新调用，后续块向末位追加参数。</summary>
    private static void AccumulateToolCall(List<UnifiedToolCall> pending, UnifiedToolCall delta)
    {
        if (!string.IsNullOrEmpty(delta.Id))
        {
            pending.Add(new UnifiedToolCall { Id = delta.Id, Name = delta.Name, Arguments = delta.Arguments });
            return;
        }
        if (pending.Count == 0) return;
        var last = pending[^1];
        if (!string.IsNullOrEmpty(delta.Name)) last.Name += delta.Name;
        last.Arguments += delta.Arguments;
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

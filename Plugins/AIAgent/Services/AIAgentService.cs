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
    private IProjectSkillScannerService? _skillScanner;
    private IProjectWorkspaceService? _workspace;
    private ISessionStore? _sessionStore;
    private IInbox? _inbox;
    private AIConfig? _aiConfig;

    public AIAgentService(
        IContext ctx,
        IWorkflowRecommendationService? workflowRecommendationService = null,
        IScriptTemplateService? scriptTemplateService = null,
        IAgentRegistryService? agentRegistry = null,
        IProjectSkillScannerService? skillScanner = null,
        IProjectWorkspaceService? workspace = null)
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
        _skillScanner = skillScanner;
        _workspace = workspace;
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

    /// <summary>
    /// 宿主会话事件日志（B4/040 写路径改序后的真相源，软依赖）：
    /// 经 Cordis 上下文运行期获取（宿主在 ProvideHostServices 阶段才 seed 进根上下文）。
    /// </summary>
    private ISessionStore? SessionStore => _sessionStore ??= _ctx.Get<ISessionStore>();

    /// <summary>
    /// 宿主收件箱（B6/040 §2.5，软依赖）：followup/steer/inject 三通道统一入口，
    /// 注册实现为 <c>PersistentInbox</c>（日志投影）。未注册时为 null，Agent 回合按空收件箱处理。
    /// </summary>
    private IInbox? Inbox => _inbox ??= _ctx.Get<IInbox>();

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
        await foreach (var ev in RunAdHocLoopAsync(messages, null, null, null, null, enableTools, cancellationToken))
        {
            if (ev.Type == "content" && !string.IsNullOrEmpty(ev.Content))
                yield return ev.Content;
        }
    }

    public async Task<List<AIChatMessage>> ChatWithToolsAsync(List<AIChatMessage> messages, CancellationToken cancellationToken = default)
    {
        // 复用同一工具循环（驱动到完成、忽略事件流）；传入副本避免污染调用方列表。
        var grown = new List<AIChatMessage>(messages);
        await foreach (var _ in RunAdHocLoopAsync(grown, null, null, null, null, true, cancellationToken))
        {
            // 事件由流式调用方消费，此处仅驱动循环
        }
        return grown;
    }

    /// <summary>
    /// 无会话上下文的一次性工具循环（ad-hoc 核心，<b>私有</b>）：调用方自带消息列表，<b>不落任何持久事实</b>
    /// （没有 sessionId 就没有日志归属）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// B5（041）：旧的「消息列表版 <c>RunAgentLoopAsync</c>」重载已删除 —— 它被 IM 网关
    /// （经 <c>IChatCompletion</c>）当成会话主路径使用，绕过了会话日志（QA 定性的旁路）。
    /// 会话场景一律走 <see cref="RunAgentLoopAsync(string, string?, string?, List{string}?, List{string}?, bool, CancellationToken, List{IToolFunctionExtension}?)"/>
    /// 或 <see cref="IAgentRegistry"/>。
    /// </para>
    /// <para>
    /// <b>B7（两套循环统一）定性：合法例外</b>——本私有核心仅供 <c>IChatCompletion</c> 旧面
    /// （ChatAsync/ChatStreamAsync/ChatWithToolsAsync：脚本生成/错误分析等一次性结构化产出）
    /// 复用工具循环能力，不产生会话历史、不在两套循环的主入口上（B6 定性，见 B7 报告）。
    /// 029 的两个旧调用方（工作流规划 / 计划驱动步骤执行）已迁入统一状态机路径，
    /// 公共接口面已删除本方法；<b>新的无会话结构化产出一律走
    /// <see cref="CreateAgent"/> + <see cref="AgentOptions.ExitToolNames"/> 出口语义，禁止再开公共 ad-hoc 口子</b>。
    /// </para>
    /// </remarks>
    private IAsyncEnumerable<AgentLoopEvent> RunAdHocLoopAsync(
        List<AIChatMessage> messages,
        string? chatModelId = null,
        string? agentId = null,
        List<string>? enabledToolNames = null,
        List<string>? skillIds = null,
        bool enableTools = true,
        CancellationToken cancellationToken = default,
        List<IToolFunctionExtension>? extraTools = null)
        => RunAdHocCoreAsync(messages, chatModelId, agentId, enabledToolNames, skillIds, enableTools, cancellationToken, extraTools);

    /// <inheritdoc />
    public async IAsyncEnumerable<AgentLoopEvent> RunAgentLoopAsync(
        string sessionId,
        string? chatModelId = null,
        string? agentId = null,
        List<string>? enabledToolNames = null,
        List<string>? skillIds = null,
        bool enableTools = true,
        [EnumeratorCancellation] CancellationToken cancellationToken = default,
        List<IToolFunctionExtension>? extraTools = null)
    {
        // B5（041）：循环逻辑已外迁到 ReactLoopAgent；本方法退化为编排层
        // （构建运行期依赖 → 跑一个回合 → 把 live 帧适配成既有的 AgentLoopEvent 事件流）。
        var agent = CreateAgent(sessionId, BuildAgentOptions(chatModelId, agentId, enabledToolNames, skillIds));

        var content = new StringBuilder();
        var toolNames = new Dictionary<string, string>(StringComparer.Ordinal);
        string? error = null;
        var aborted = false;

        await foreach (var frame in agent.RunAsync(cancellationToken))
        {
            switch (frame)
            {
                case AssistantDelta delta:
                    content.Append(delta.Content);
                    yield return new AgentLoopEvent { Type = "content", Content = delta.Content };
                    break;

                case ToolStarted call:
                    toolNames[call.CallId] = call.ToolName;
                    yield return new AgentLoopEvent { Type = "tool_call", Name = call.ToolName, Arguments = call.ArgsJson };
                    break;

                case ToolCompleted done:
                    yield return new AgentLoopEvent
                    {
                        Type = "tool_result",
                        Name = toolNames.TryGetValue(done.CallId, out var name) ? name : string.Empty,
                        Success = done.Outcome == ToolOutcome.Ok
                    };
                    break;

                case TurnFailed failed:
                    error = failed.Error;
                    break;

                case TurnCompleted turn:
                    if (turn.Reason == TurnEndReason.Aborted)
                    {
                        aborted = true;
                    }
                    break;
            }
        }

        if (agent is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync();
        }

        if (error != null)
        {
            yield return new AgentLoopEvent { Type = "error", Content = error };
            yield break;
        }

        // done 携带本回合最终文本与用量（用量从日志最后一条 assistant/message 取，单一真源）
        yield return new AgentLoopEvent
        {
            Type = "done",
            Content = content.ToString(),
            Usage = aborted ? null : ReadLastUsage(sessionId)
        };
    }

    /// <summary>
    /// 从会话日志取最后一条助手消息的用量（<see cref="AssistantMessageEvent.Usage"/> → 统一用量）。
    /// </summary>
    private UnifiedUsage? ReadLastUsage(string sessionId)
    {
        var store = SessionStore;
        if (store == null)
        {
            return null;
        }

        var last = store.Replay(sessionId).OfType<AssistantMessageEvent>().LastOrDefault();
        if (last?.Usage == null)
        {
            return null;
        }

        return new UnifiedUsage
        {
            PromptTokens = (int)last.Usage.PromptTokens,
            CompletionTokens = (int)last.Usage.CompletionTokens,
            TotalTokens = (int)(last.Usage.PromptTokens + last.Usage.CompletionTokens)
        };
    }

    /// <summary>
    /// 构建 Agent 运行选项（模型 / 已渲染系统提示 / 工具白名单）。
    /// 系统提示在此一次性渲染完成：Agent 人设 → 选中技能 → 关联工作流（与旧循环同口径）。
    /// </summary>
    public AgentOptions BuildAgentOptions(
        string? chatModelId = null,
        string? agentId = null,
        List<string>? enabledToolNames = null,
        List<string>? skillIds = null)
    {
        var systemPrompt = ResolveSystemPrompt(agentId);
        if (skillIds is { Count: > 0 })
        {
            systemPrompt = AppendSelectedSkills(systemPrompt, skillIds);
        }
        systemPrompt = AppendAssociatedWorkflows(systemPrompt, agentId);

        return new AgentOptions
        {
            ModelId = chatModelId ?? string.Empty,
            SystemPrompt = systemPrompt,
            ToolAllowlist = enabledToolNames
        };
    }

    /// <summary>
    /// 创建会话 Agent（状态机本体）：解析 provider / 工具 schema / 模型 id，装配
    /// <see cref="AgentTurnRuntime"/>，交付给 <see cref="IAgentRegistry"/> 或一次性调用方。
    /// </summary>
    public IAgent CreateAgent(string sessionId, AgentOptions options)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("sessionId 不能为空", nameof(sessionId));
        var effective = options ?? throw new ArgumentNullException(nameof(options));

        var store = SessionStore
            ?? throw new InvalidOperationException("宿主未提供 ISessionStore 契约，无法以会话日志为真相源驱动 Agent 循环");

        var provider = ResolveProvider(string.IsNullOrWhiteSpace(effective.ModelId) ? null : effective.ModelId)
            ?? throw new InvalidOperationException("未找到可用的 AI 提供方（检查提供方配置与模型路由）");

        var tools = ResolveOwnToolDefinitions();
        if (effective.ToolAllowlist is { Count: > 0 })
        {
            var allowed = new HashSet<string>(effective.ToolAllowlist, StringComparer.OrdinalIgnoreCase);
            tools = tools.Where(t => allowed.Contains(t.Function.Name)).ToList();
            XTrace.Log.Info("[AIAgentPlugin] 工具白名单过滤：请求 {0} 个，挂载 {1} 个", allowed.Count, tools.Count);
        }

        var unifiedTools = tools.Count > 0
            ? tools.Select(t => new UnifiedToolDefinition
            {
                Name = t.Function.Name,
                Description = t.Function.Description,
                Parameters = t.Function.Parameters
            }).ToList()
            : null;

        // B7 统一循环：AgentOptions.ExtraTools（出口工具 schema 等）并入本回合工具定义。
        // 不随 ToolAllowlist 过滤（显式传入即视为意图挂载）；出口工具是声明不是执行，无需在宿主注册。
        if (effective.ExtraTools is { Count: > 0 })
        {
            var merged = unifiedTools ?? new List<UnifiedToolDefinition>();
            foreach (var extra in effective.ExtraTools)
            {
                object? parameters;
                try
                {
                    parameters = JsonDocument.Parse(extra.ParametersJsonSchema).RootElement.Clone();
                }
                catch (Exception ex)
                {
                    XTrace.Log.Warn("[AIAgentPlugin] 额外工具 {0} schema 解析失败，已跳过: {1}", extra.Name, ex.Message);
                    continue;
                }
                merged.Add(new UnifiedToolDefinition
                {
                    Name = extra.Name,
                    Description = extra.Description,
                    Parameters = parameters
                });
            }
            unifiedTools = merged;
        }

        var runtime = new AgentTurnRuntime
        {
            Store = store,
            Provider = provider,
            ModelId = ResolveModelId(effective.ModelId),
            ToolExecutor = ToolRegistry,
            SystemPrompt = effective.SystemPrompt,
            ToolDefinitions = unifiedTools,
            Events = _ctx.Events,
            MemoryPromptProvider = BuildMemoryPromptAsync,
            Inbox = Inbox
        };

        return new ReactLoopAgent(sessionId, effective, runtime);
    }

    /// <summary>
    /// 一次性工具循环核心（ad-hoc，无会话日志）：驱动到「无工具调用」或达到迭代上限。
    /// 与会话版（<see cref="ReactLoopAgent"/>）的差别：不落任何事件、不认领收件箱、不走 turn/step 结构。
    /// </summary>
    private async IAsyncEnumerable<AgentLoopEvent> RunAdHocCoreAsync(
        List<AIChatMessage> messages,
        string? chatModelId,
        string? agentId,
        List<string>? enabledToolNames,
        List<string>? skillIds,
        bool enableTools,
        [EnumeratorCancellation] CancellationToken cancellationToken,
        List<IToolFunctionExtension>? extraTools)
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

        // 工具白名单过滤：composer 🔧 多选非空时只启用所选工具（仍限白名单插件）。
        // 空/未传 = 默认全挂（向后兼容）。
        if (enableTools && enabledToolNames is { Count: > 0 })
        {
            var allowed = new HashSet<string>(enabledToolNames, StringComparer.OrdinalIgnoreCase);
            tools = tools.Where(t => allowed.Contains(t.Function.Name)).ToList();
            XTrace.Log.Info("[AIAgentPlugin] 工具白名单过滤：请求 {0} 个，挂载 {1} 个",
                allowed.Count, tools.Count);
        }

        // 追加额外显式挂载的特殊工具（计划驱动运行流 submit_plan/complete_step/request_help，R4）：
        // 不随白名单过滤；默认不进入 FreeLoop（调用方不传即不挂载，只由 PlanDriven 步骤循环显式传入）。
        if (extraTools is { Count: > 0 })
        {
            foreach (var extra in extraTools)
            {
                object? parameters;
                try
                {
                    parameters = JsonDocument.Parse(extra.ParametersJsonSchema).RootElement.Clone();
                }
                catch (Exception ex)
                {
                    XTrace.Log.Warn("[AIAgentPlugin] 特殊工具 {0} schema 解析失败，已跳过: {1}", extra.Name, ex.Message);
                    continue;
                }
                tools.Add(new AIToolDefinition
                {
                    Function = new AIFunctionDefinition
                    {
                        Name = extra.Name,
                        Description = extra.Description,
                        Parameters = parameters
                    }
                });
            }
        }

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
        // 技能注入：选中技能的 名称 + 描述 + 相对路径 追加到 system prompt（不全文注入，V1）。
        if (skillIds is { Count: > 0 })
            systemPrompt = AppendSelectedSkills(systemPrompt, skillIds);
        // 关联工作流注入：把选中 Agent 关联的工作流（名称+id+描述）追加到 system prompt，
        // 供 LLM 用 execute_workflow 工具按需执行（多工作流由 LLM 决策）。
        systemPrompt = AppendAssociatedWorkflows(systemPrompt, agentId);

        var model = ResolveModelId(chatModelId);

        XTrace.Log.Info("[AIAgentPlugin] 开始 Agent 工具循环，provider: {0}, model: {1}, agent: {2}, 工具数: {3}, 技能数: {4}",
            provider.ProviderName, model, agentId ?? "(默认)", unifiedTools?.Count ?? 0, skillIds?.Count ?? 0);

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

                // ad-hoc 循环不落日志（无 sessionId 归属）；会话场景由 ReactLoopAgent 落 tool/call + tool/result。
                // B9 迁六闸门执行面：30s 超时以 linked CTS 注入（管线以 exec.Signal 竞速工具体，语义等价旧 WithTimeout）。
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                ToolExecutionResult toolResult;
                try
                {
                    using var toolTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    toolTimeoutCts.CancelAfter(TimeSpan.FromSeconds(30));
                    toolResult = await ToolRegistry.ExecuteAsync(new ToolExecution
                    {
                        CallId = string.Empty,
                        ToolName = toolCall.Name,
                        ArgsJson = toolCall.Arguments,
                        SessionId = string.Empty
                    }, toolTimeoutCts.Token);
                }
                catch (Exception ex)
                {
                    XTrace.Log.Error("[AIAgentPlugin] 工具 {0} 执行异常: {1}", toolCall.Name, ex.Message);
                    toolResult = new ToolExecutionResult { Success = false, Result = string.Empty, ErrorMessage = ex.Message };
                }
                stopwatch.Stop();

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
            // 计划驱动运行流特殊工具不进 FreeLoop（R4）：仅 PlanDriven 步骤循环经 extraTools 显式挂载。
            if (tool.Name is "submit_plan" or "complete_step" or "request_help") continue;
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

    /// <summary>
    /// 技能注入（V1）：把选中技能的 名称 + 描述 + 相对路径 追加到 system prompt，注明「如需按技能工作请读取其内容」。
    /// 不全文注入——本地小模型 prompt 预算有限（14 工具即 400 过），全文注入留 V2。
    /// 解析依赖项目技能扫描（IProjectSkillScannerService，未注册/未选目录时静默跳过）。
    /// </summary>
    private string? AppendSelectedSkills(string? systemPrompt, List<string> skillIds)
    {
        try
        {
            // 懒解析（构造期宿主/插件子容器可能未就绪；与 ToolRegistry 同模式）。
            _skillScanner ??= _ctx.Get<IProjectSkillScannerService>();
            _workspace ??= _ctx.Get<IProjectWorkspaceService>();

            if (_skillScanner == null || _workspace?.ProjectRoot == null)
            {
                XTrace.Log.Debug("[AIAgentPlugin] 技能注入跳过：扫描服务未注册或未选项目目录");
                return systemPrompt;
            }

            var all = _skillScanner.Scan(_workspace.ProjectRoot);
            if (all.Count == 0)
            {
                XTrace.Log.Debug("[AIAgentPlugin] 技能注入跳过：项目目录未识别到技能");
                return systemPrompt;
            }

            var wanted = new HashSet<string>(skillIds, StringComparer.OrdinalIgnoreCase);
            var selected = all.Where(s => wanted.Contains(s.Id)).ToList();
            if (selected.Count == 0)
            {
                XTrace.Log.Warn("[AIAgentPlugin] 技能注入：请求 {0} 个技能 id，全部未命中已识别技能列表", skillIds.Count);
                return systemPrompt;
            }

            var sb = new StringBuilder();
            sb.AppendLine("## 本会话启用的技能");
            sb.AppendLine("用户为本会话勾选了以下技能。如需按某个技能工作，请先用文件工具读取其内容（SKILL.md）再按其要求执行；不读取则按通用能力回答。");
            sb.AppendLine();
            foreach (var s in selected)
            {
                sb.AppendLine($"- 技能「{s.Name}」（来源: {s.Source}，路径: {s.Path}）：{s.Description}");
            }
            sb.AppendLine();
            sb.AppendLine("---");

            var skillText = sb.ToString();
            XTrace.Log.Info("[AIAgentPlugin] 技能注入完成：{0} 个技能加入 system prompt", selected.Count);
            return systemPrompt == null
                ? skillText.TrimEnd()
                : skillText + systemPrompt;
        }
        catch (Exception ex)
        {
            // 技能注入失败不阻断对话：记日志、返回原 system prompt。
            XTrace.Log.Error("[AIAgentPlugin] 技能注入失败: {0}", ex.Message);
            return systemPrompt;
        }
    }

    /// <summary>
    /// 关联工作流注入：把选中 Agent 关联的工作流（id+名称+描述）追加到 system prompt（V1，仅元信息不全文）。
    /// 提示 LLM：任务匹配其中某个工作流时用 execute_workflow 工具执行；无关则不强行调用。
    /// 失败不阻断对话（返回原 system prompt）。
    /// </summary>
    private string? AppendAssociatedWorkflows(string? systemPrompt, string? agentId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(agentId) || _agentRegistry == null) return systemPrompt;

            var agent = _agentRegistry.GetAgent(agentId);
            var workflows = agent?.Workflows;
            if (workflows == null || workflows.Count == 0) return systemPrompt;

            var sb = new StringBuilder();
            sb.AppendLine("## 本 Agent 关联的工作流");
            sb.AppendLine("本 Agent 已关联以下工作流（其定义已在项目 WorkflowEngine 中维护）。当用户任务匹配其中某个工作流时，");
            sb.AppendLine("用 execute_workflow 工具传入对应 workflowId 与所需 inputVariables 执行；任务不匹配时不要强行调用。");
            sb.AppendLine();
            foreach (var w in workflows)
            {
                sb.AppendLine($"- 工作流「{w.Name}」（workflowId: {w.WorkflowId}）：{w.Description}");
            }
            sb.AppendLine();
            sb.AppendLine("---");

            var text = sb.ToString();
            XTrace.Log.Info("[AIAgentPlugin] 关联工作流注入完成：{0} 个", workflows.Count);
            return systemPrompt == null ? text.TrimEnd() : text + systemPrompt;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] 关联工作流注入失败: {0}", ex.Message);
            return systemPrompt;
        }
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
            var unified = tools.Select(t => new UnifiedToolDefinition { Name = t.Function.Name }).ToList();
            var lastUserMessage = allMessages.LastOrDefault(m => m.Role == "user");
            if (lastUserMessage == null || string.IsNullOrWhiteSpace(lastUserMessage.Content))
            {
                XTrace.Log.Debug("[AIAgentPlugin] 无用户消息，跳过记忆注入");
                return;
            }

            var memoryMessage = await BuildMemoryPromptAsync(lastUserMessage.Content, unified, cancellationToken);
            if (string.IsNullOrWhiteSpace(memoryMessage))
            {
                return;
            }

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

    /// <summary>
    /// 检索长期记忆并渲染成「记忆上下文块」（供 ReactLoopAgent 经 <see cref="AgentTurnRuntime.MemoryPromptProvider"/>
    /// 拼到系统提示前；ad-hoc 循环经 <see cref="InjectRelevantMemoriesAsync"/> 直接注入消息列表）。
    /// </summary>
    /// <param name="query">检索 query（取最后一条用户消息，内部截断到 500 字）。</param>
    /// <param name="tools">本回合挂载的工具；不含 get_relevant_memories 时直接返回 null。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>记忆上下文块文本；无记忆/未挂载记忆工具/失败时返回 null。</returns>
    public async Task<string?> BuildMemoryPromptAsync(
        string query,
        List<UnifiedToolDefinition>? tools,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (tools == null || !tools.Any(t => t.Name == "get_relevant_memories"))
            {
                XTrace.Log.Debug("[AIAgentPlugin] 未检测到记忆检索工具，跳过记忆注入");
                return null;
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                XTrace.Log.Debug("[AIAgentPlugin] 无用户消息，跳过记忆注入");
                return null;
            }

            XTrace.Log.Info("[AIAgentPlugin] 正在检索相关记忆...");

            var trimmed = query.Length > 500 ? query[..500] : query;
            var toolParams = JsonSerializer.Serialize(new { query = trimmed, limit = 5 });
            // B9 迁六闸门执行面：15s 超时以 linked CTS 注入（语义等价旧 WithTimeout）。
            ToolExecutionResult toolResult;
            using (var toolTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                toolTimeoutCts.CancelAfter(TimeSpan.FromSeconds(15));
                toolResult = await ToolRegistry.ExecuteAsync(new ToolExecution
                {
                    CallId = string.Empty,
                    ToolName = "get_relevant_memories",
                    ArgsJson = toolParams,
                    SessionId = string.Empty
                }, toolTimeoutCts.Token);
            }

            if (!toolResult.Success)
            {
                XTrace.Log.Warn("[AIAgentPlugin] 记忆检索失败: {0}", toolResult.ErrorMessage);
                return null;
            }

            using var doc = JsonDocument.Parse(toolResult.Result);
            var root = doc.RootElement;

            if (!root.TryGetProperty("success", out var successProp) || !successProp.GetBoolean())
            {
                XTrace.Log.Warn("[AIAgentPlugin] 记忆检索返回失败");
                return null;
            }

            if (!root.TryGetProperty("memories", out var memoriesProp) || memoriesProp.ValueKind != JsonValueKind.Array)
            {
                XTrace.Log.Debug("[AIAgentPlugin] 没有找到相关记忆");
                return null;
            }

            var memories = memoriesProp.EnumerateArray().ToList();
            if (memories.Count == 0)
            {
                XTrace.Log.Info("[AIAgentPlugin] 没有找到相关记忆");
                return null;
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

            return sb.ToString();
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] 记忆注入失败: {0}", ex.Message);
            return null;
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

using System.ClientModel;
using System.ClientModel.Primitives;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ForgeSelf.Abstractions;
using NewLife.Log;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;

// Responses API 在 openai-dotnet 2.10.0 带 OPENAI001 实验标记（02-spec U-5 实证），适配层内集中抑制
#pragma warning disable OPENAI001

namespace ForgeSelf.Api.Services.AI.Providers;

/// <summary>
/// PILOT-074：Unified 契约 ↔ openai-dotnet SDK 类型的唯一映射层（路线 A1，raw SDK 类型保 delta 语义）。
/// 出站 chat（OpenAI/Custom）与 Responses（OpenAIResponses）的协议细节全部交由官方 SDK（PC-008），
/// 本类只做：客户端构造（Endpoint base 推导 + 代理注入 + Retries=0 单次语义）与 Unified↔SDK 类型转换。
/// 探针实证事实（03-plan 偏差表）：SDK 流式自动置 include_usage；Responses 流式需显式 StreamingEnabled=true；
/// CompleteChatAsync 收 IEnumerable&lt;ChatMessage&gt;（options 为可选参）。
/// </summary>
internal static class OpenAISdkAdapter
{
    // ==================== 客户端构造 ====================

    /// <summary>
    /// 由配置构造 chat 用 OpenAIClient。
    /// OpenAI/Custom：base = 既有 GetChatEndpointByProviderType 推导结果去 /chat/completions 后缀（SDK 自行追加相对路径）。
    /// Anthropic（输入9 重申输入6 标准 2026-10-28：现成优先+正确能用）：走 Anthropic 官方 OpenAI 兼容层——base 推导剥 /v1/messages 系后缀归一到 …/v1，
    /// 鉴权 Bearer、max_completion_tokens、stream/tools/system-hoist 均被兼容层支持（官方文档实证 2026-10-28，存 .temp/compat-jina2.md）。
    /// Transport 注入宿主 HttpClient（实时代理随 IHttpClientFactory 注入）。
    /// </summary>
    public static OpenAIClient CreateChatClient(AIProviderConfig config, HttpClient httpClient)
    {
        var baseUrl = config.ProviderType == AIProviderType.Anthropic
            ? DeriveAnthropicCompatBaseUrl(config.Endpoint)
            : DeriveOpenAICompatBaseUrl(config);
        return CreateClient(config, httpClient, baseUrl);
    }

    /// <summary>OpenAI/Custom chat base 推导：完整 chat 接口地址剥 /chat/completions 后缀（沿用既有 001 文档四形态规则）。</summary>
    private static string DeriveOpenAICompatBaseUrl(AIProviderConfig config)
    {
        var chatUrl = OpenAICompatibleProvider.GetChatEndpointByProviderType(
            NormalizeType(config.ProviderType), config.Endpoint)
            ?? throw new InvalidOperationException($"无法从 Endpoint 推导 chat 接口地址: {config.Endpoint}");
        return chatUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
            ? chatUrl[..^"/chat/completions".Length]
            : chatUrl;
    }

    /// <summary>
    /// Anthropic 兼容层 base 推导：…/v1/messages 或 …/messages 剥后缀；…/v1 原样；其余补 /v1
    /// （与 DeriveResponsesBaseUrl 同构；兼容端点 = base + /chat/completions）。
    /// </summary>
    public static string DeriveAnthropicCompatBaseUrl(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new InvalidOperationException("Anthropic Endpoint 为空，无法推导兼容层 base 地址");
        var trimmed = endpoint.TrimEnd('/');
        if (trimmed.EndsWith("/v1/messages", StringComparison.OrdinalIgnoreCase))
            return trimmed[..^"/messages".Length];
        if (trimmed.EndsWith("/messages", StringComparison.OrdinalIgnoreCase))
        {
            var withoutMessages = trimmed[..^"/messages".Length];
            return withoutMessages.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
                ? withoutMessages
                : withoutMessages + "/v1";
        }
        if (trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            return trimmed;
        return trimmed + "/v1";
    }

    /// <summary>由配置构造 Responses 用 OpenAIClient。base 推导：…/responses 剥后缀；…/v1 原样；其余补 /v1。</summary>
    public static OpenAIClient CreateResponsesClient(AIProviderConfig config, HttpClient httpClient)
        => CreateClient(config, httpClient, DeriveResponsesBaseUrl(config.Endpoint));

 private static OpenAIClient CreateClient(AIProviderConfig config, HttpClient httpClient, string baseUrl)
    {
        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri(baseUrl),
            Transport = new HttpClientPipelineTransport(httpClient),
        };
        // BC-2：显式单次请求语义（SDK 默认重试会放大失败时延，与既有手写路径不一致；U-6 修正：属性名为 RetryPolicy）
        options.RetryPolicy = new ClientRetryPolicy(maxRetries: 0);
        // U-6f 偏差：SDK 要求非空 ApiKeyCredential（空串在构造期抛异常，手写版是发「Bearer 」空参）；
        // 空 key 用无害占位符，键鉴权上游不会用到，键免鉴权上游（LM Studio/Ollama）忽略该头
        var apiKey = string.IsNullOrWhiteSpace(config.ApiKey) ? "EMPTY" : config.ApiKey;
        return new OpenAIClient(new ApiKeyCredential(apiKey), options);
    }

    /// <summary>OpenAI/Custom 共用 OpenAI 兼容推导规则（Custom 在既有 TryGetProviderPathParts 会抛异常，适配器视为 OpenAI 兼容，BC-4）。</summary>
    private static AIProviderType NormalizeType(AIProviderType type)
        => type == AIProviderType.Custom ? AIProviderType.OpenAI : type;

    /// <summary>Responses base 推导（与 001 文档测试连接四形态规则同构）：…/responses 剥后缀；…/v1 原样；其余补 /v1。</summary>
    public static string DeriveResponsesBaseUrl(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new InvalidOperationException("Responses Endpoint 为空，无法推导 base 地址");
        var trimmed = endpoint.TrimEnd('/');
        if (trimmed.EndsWith("/responses", StringComparison.OrdinalIgnoreCase))
            return trimmed[..^"/responses".Length];
        if (trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            return trimmed;
        return trimmed + "/v1";
    }

    // ==================== chat：Unified → SDK ====================

    private static ChatImageDetailLevel MapImageDetail(string? detail) => detail switch
    {
        "low" => ChatImageDetailLevel.Low,
        "high" => ChatImageDetailLevel.High,
        _ => ChatImageDetailLevel.Auto
    };

    /// <summary>多模态内容块 → SDK 内容部件（text/image_url/image_base64；未知类型跳过并告警，与手写版一致）。</summary>
    private static List<ChatMessageContentPart> BuildContentParts(List<ContentBlock> blocks)
    {
        var parts = new List<ChatMessageContentPart>();
        foreach (var block in blocks)
        {
            if (block.Type == "image_url" && !string.IsNullOrEmpty(block.ImageUrl))
            {
                parts.Add(ChatMessageContentPart.CreateImagePart(
                    new Uri(block.ImageUrl), MapImageDetail(block.ImageDetail)));
            }
            else if (block.Type == "image_base64" && !string.IsNullOrEmpty(block.ImageBase64))
            {
                parts.Add(ChatMessageContentPart.CreateImagePart(
                    BinaryData.FromBytes(Convert.FromBase64String(block.ImageBase64)),
                    block.ImageMediaType ?? "image/png", MapImageDetail(block.ImageDetail)));
            }
            else if (block.Type == "text" && !string.IsNullOrEmpty(block.Text))
            {
                parts.Add(ChatMessageContentPart.CreateTextPart(block.Text));
            }
        }
        return parts;
    }

    /// <summary>单条 Unified 消息 → SDK 消息（tool 角色走 CreateToolMessage）。</summary>
    private static ChatMessage ToChatMessage(UnifiedChatMessage msg)
    {
        return msg.Role switch
        {
            "assistant" => ChatMessage.CreateAssistantMessage(msg.Content ?? string.Empty),
            "system" => ChatMessage.CreateSystemMessage(msg.Content ?? string.Empty),
            "tool" => ChatMessage.CreateToolMessage(msg.ToolCallId ?? string.Empty, msg.Content ?? string.Empty),
            _ => ChatMessage.CreateUserMessage(msg.Content ?? string.Empty)
        };
    }

    /// <summary>
    /// 构建 SDK 消息序列。多模态语义与手写版一致：ContentBlocks 存在时仅发 system + 单条多模态用户消息。
    /// assistant 携带工具调用时，SDK 无法在单条消息内同时表达文本与 tool_calls，
    /// 展开为「文本 assistant 消息（若有）+ 工具调用 assistant 消息」两条（wire 形状偏差，已记 03 偏差表）。
    /// </summary>
    public static List<ChatMessage> BuildChatMessages(UnifiedChatRequest request)
    {
        var messages = new List<ChatMessage>();
        if (!string.IsNullOrEmpty(request.SystemPrompt))
            messages.Add(ChatMessage.CreateSystemMessage(request.SystemPrompt));

        if (request.ContentBlocks is { Count: > 0 })
        {
            messages.Add(ChatMessage.CreateUserMessage(BuildContentParts(request.ContentBlocks)));
            return messages;
        }

        foreach (var msg in request.Messages)
        {
            if (msg.Role == "assistant" && msg.ToolCalls is { Count: > 0 })
            {
                if (!string.IsNullOrEmpty(msg.Content))
                    messages.Add(ChatMessage.CreateAssistantMessage(msg.Content));
                messages.Add(ChatMessage.CreateAssistantMessage(msg.ToolCalls.Select(tc =>
                    ChatToolCall.CreateFunctionToolCall(
                        tc.Id, tc.Name, BinaryData.FromString(string.IsNullOrEmpty(tc.Arguments) ? "{}" : tc.Arguments)))));
                continue;
            }
            messages.Add(ToChatMessage(msg));
        }
        return messages;
    }

    /// <summary>构建 chat 请求参数（Temperature/TopP/输出上限/工具/工具选择；消息经参数另行传入）。</summary>
    public static ChatCompletionOptions BuildChatOptions(UnifiedChatRequest request)
    {
        var options = new ChatCompletionOptions
        {
            Temperature = (float)request.Temperature,
            TopP = (float)request.TopP,
        };
        if (request.MaxTokens is int max)
            options.MaxOutputTokenCount = max;

        if (request.Tools is { Count: > 0 })
        {
            foreach (var tool in request.Tools)
            {
                options.Tools.Add(ChatTool.CreateFunctionTool(
                    tool.Name,
                    tool.Description ?? string.Empty,
                    tool.Parameters == null ? null : BinaryData.FromObjectAsJson(tool.Parameters)));
            }

            if (!string.IsNullOrWhiteSpace(request.ToolChoice))
            {
                options.ToolChoice = request.ToolChoice switch
                {
                    "auto" => ChatToolChoice.CreateAutoChoice(),
                    "none" => ChatToolChoice.CreateNoneChoice(),
                    "required" => ChatToolChoice.CreateRequiredChoice(),
                    _ => ChatToolChoice.CreateFunctionChoice(request.ToolChoice)
                };
            }
        }
        return options;
    }

    // ==================== chat：SDK → Unified（非流式） ====================

    /// <summary>ChatFinishReason → 手写版透传的 wire 字符串（stop/tool_calls/length/content_filter…）。</summary>
    public static string MapFinishReason(ChatFinishReason? reason)
    {
        if (reason is null)
            return string.Empty;
        var s = reason.Value.ToString();
        if (string.IsNullOrEmpty(s))
            return string.Empty;
        return char.ToLowerInvariant(s[0]) + s[1..];
    }

    public static UnifiedChatResponse ToUnifiedResponse(ChatCompletion completion, string requestModel, string providerName, bool reasoningFallback = false)
    {
        // 手写版语义：content 数组时拼接全部 text 部件
        var text = string.Concat(completion.Content
            .Where(p => !string.IsNullOrEmpty(p.Text))
            .Select(p => p.Text!));

        var unified = new UnifiedChatResponse
        {
            Id = string.IsNullOrEmpty(completion.Id) ? $"chatcmpl-{Guid.NewGuid():N}" : completion.Id,
            Model = string.IsNullOrEmpty(completion.Model) ? requestModel : completion.Model,
            Created = completion.CreatedAt.ToUnixTimeSeconds(),
            ProviderName = providerName,
            FinishReason = MapFinishReason(completion.FinishReason),
            Choices = new List<UnifiedChatMessage>()
        };

        var choice = new UnifiedChatMessage
        {
            Role = "assistant",
            Content = text ?? string.Empty
        };
        if (reasoningFallback && string.IsNullOrEmpty(choice.Content))
        {
            // U-2 放行：正文空时经 ModelReaderWriter 往返补 reasoning_content（AIService 回退语义）
            var reasoning = ExtractReasoningViaRoundtrip(completion);
            if (!string.IsNullOrEmpty(reasoning))
                choice.ReasoningContent = reasoning;
        }
        if (completion.ToolCalls is { Count: > 0 })
        {
            choice.ToolCalls = completion.ToolCalls.Select(call => new UnifiedToolCall
            {
                Id = call.Id ?? string.Empty,
                Name = call.FunctionName ?? string.Empty,
                Arguments = call.FunctionArguments?.ToString() ?? string.Empty
            }).ToList();
        }
        unified.Choices.Add(choice);

        if (completion.Usage is { } usage)
        {
            unified.Usage = new UnifiedUsage
            {
                PromptTokens = usage.InputTokenCount,
                CompletionTokens = usage.OutputTokenCount,
                TotalTokens = usage.TotalTokenCount
            };
        }
        return unified;
    }

    /// <summary>流式单更新经 ModelReaderWriter 往返提取 delta.reasoning_content（仅回退模式；往返不支持该类型时静默跳过）。</summary>
    private static string? ExtractDeltaReasoningViaRoundtrip(StreamingChatCompletionUpdate upd)
    {
        try
        {
            var json = ModelReaderWriter.Write(upd).ToString();
            if (string.IsNullOrEmpty(json)) return null;
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var first = choices[0];
                if (first.ValueKind == JsonValueKind.Object &&
                    first.TryGetProperty("delta", out var delta) &&
                    delta.TryGetProperty("reasoning_content", out var rc) &&
                    rc.ValueKind == JsonValueKind.String)
                {
                    return rc.GetString();
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Debug($"流式 reasoning_content 往返提取失败（忽略）: {ex.Message}");
        }
        return null;
    }

    // ==================== chat 流式：SDK → Unified ====================

    /// <summary>
    /// chat 流式：CompleteChatStreamingAsync → UnifiedStreamChunk。
    /// 增量保真：文本 delta / 工具调用增量 / usage 哨兵分片逐块透传；枚举结束（SDK 无 [DONE] 暴露）后补 IsDone 终止块（与手写版 [DONE] 语义对齐）。
    /// </summary>
    public static async IAsyncEnumerable<UnifiedStreamChunk> ChatStreamAsync(
        AIProviderConfig config,
        UnifiedChatRequest request,
        HttpClient httpClient,
        string providerName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default,
        bool reasoningAsContentFallback = false)
    {
        XTrace.Log.Info($"[{providerName}] 发送流式 Chat 请求（SDK），模型: {request.Model}");
        var chat = CreateChatClient(config, httpClient).GetChatClient(request.Model);
        var messages = BuildChatMessages(request);
        var options = BuildChatOptions(request);

        var id = $"chatcmpl-{Guid.NewGuid():N}";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string? lastFinishReason = null;

        await foreach (var upd in chat.CompleteChatStreamingAsync(messages, options, cancellationToken))
        {
            // U-6 修正：StreamingChatCompletionUpdate 无 Id 成员，流分片复用本地生成的会话 id
            var chunkId = id;
            var model = string.IsNullOrEmpty(upd.Model) ? request.Model : upd.Model;
            var chunkCreated = upd.CreatedAt.ToUnixTimeSeconds();
            var text = string.Concat(upd.ContentUpdate
                .Where(p => !string.IsNullOrEmpty(p.Text))
                .Select(p => p.Text!));

            if (upd.ToolCallUpdates is { Count: > 0 })
            {
                foreach (var tc in upd.ToolCallUpdates)
                {
                    yield return new UnifiedStreamChunk
                    {
                        Id = chunkId,
                        Model = model,
                        Created = chunkCreated,
                        ChoiceIndex = 0,
                        DeltaContent = string.Empty,
                        DeltaToolCall = new UnifiedToolCall
                        {
                            Id = tc.ToolCallId ?? string.Empty,
                            Name = tc.FunctionName ?? string.Empty,
                            Arguments = tc.FunctionArgumentsUpdate?.ToString() ?? string.Empty
                        },
                        FinishReason = MapFinishReason(upd.FinishReason)
                    };
                    if (upd.FinishReason is not null) lastFinishReason = MapFinishReason(upd.FinishReason);
                }
            }
            else
            {
                if (upd.FinishReason is not null) lastFinishReason = MapFinishReason(upd.FinishReason);

                if (upd.Usage is { } usage)
                {
                    // usage 哨兵分片（手写版 :133-155 语义：choices 空但带 usage 的分片单独透传）
                    yield return new UnifiedStreamChunk
                    {
                        Id = chunkId,
                        Model = model,
                        Created = chunkCreated,
                        ChoiceIndex = 0,
                        Usage = new UnifiedUsage
                        {
                            PromptTokens = usage.InputTokenCount,
                            CompletionTokens = usage.OutputTokenCount,
                            TotalTokens = usage.TotalTokenCount
                        }
                    };
                }

                var deltaText = text;
                if (reasoningAsContentFallback && string.IsNullOrEmpty(deltaText))
                {
                    // 回退模式：无正文增量时尝试经往返提取 reasoning_content 增量（对齐 AIService 旧回退语义）
                    deltaText = ExtractDeltaReasoningViaRoundtrip(upd) ?? string.Empty;
                }

                yield return new UnifiedStreamChunk
                {
                    Id = chunkId,
                    Model = model,
                    Created = chunkCreated,
                    ChoiceIndex = 0,
                    DeltaContent = deltaText,
                    FinishReason = MapFinishReason(upd.FinishReason)
                };
            }
        }

        // 终止块：与手写版收到 [DONE] 时等价（FinishReason 缺省 stop）
        yield return new UnifiedStreamChunk
        {
            Id = id,
            Model = request.Model,
            Created = created,
            ChoiceIndex = 0,
            FinishReason = string.IsNullOrEmpty(lastFinishReason) ? "stop" : lastFinishReason,
            IsDone = true
        };
    }

    /// <summary>chat 非流式：CompleteChatAsync → UnifiedChatResponse（BC-1：URL 归一化随 SDK base 推导统一）。</summary>
    public static async Task<UnifiedChatResponse> ChatAsync(
        AIProviderConfig config,
        UnifiedChatRequest request,
        HttpClient httpClient,
        string providerName,
        CancellationToken cancellationToken = default,
        bool reasoningAsContentFallback = false)
    {
        XTrace.Log.Info($"[{providerName}] 发送 Chat 请求（SDK），模型: {request.Model}, 消息数: {request.Messages.Count}");
        var chat = CreateChatClient(config, httpClient).GetChatClient(request.Model);
        var completion = (await chat.CompleteChatAsync(
            BuildChatMessages(request), BuildChatOptions(request), cancellationToken)).Value;
        return ToUnifiedResponse(completion, request.Model, providerName, reasoningAsContentFallback);
    }

    /// <summary>
    /// U-2 实证放行（探针 2026-10-28）：SDK 类型面无 reasoning_content，但 ModelReaderWriter 往返保留未知字段。
    /// 在正文为空时经往返提取 deepseek/qwen 系 reasoning_content（仅 reasoningAsContentFallback 开启方执行，主链路不付此成本）。
    /// </summary>
    private static string? ExtractReasoningViaRoundtrip(ChatCompletion completion)
    {
        try
        {
            var json = ModelReaderWriter.Write(completion).ToString();
            if (string.IsNullOrEmpty(json)) return null;
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var message = choices[0];
                if (message.ValueKind == JsonValueKind.Object &&
                    message.TryGetProperty("message", out var msg) &&
                    msg.TryGetProperty("reasoning_content", out var rc) &&
                    rc.ValueKind == JsonValueKind.String)
                {
                    return rc.GetString();
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Debug($"reasoning_content 往返提取失败（忽略）: {ex.Message}");
        }
        return null;
    }

    // ==================== Responses：Unified → SDK ====================

    /// <summary>构建 Responses 请求：SystemPrompt→instructions；消息→input items；工具→扁平定义；store=false（BR-2 无状态）。</summary>
    public static CreateResponseOptions BuildResponseOptions(UnifiedChatRequest request)
    {
        var options = new CreateResponseOptions
        {
            Model = request.Model,
            StoredOutputEnabled = false
        };
        if (!string.IsNullOrEmpty(request.SystemPrompt))
            options.Instructions = request.SystemPrompt;

        if (request.ContentBlocks is { Count: > 0 })
        {
            // 与 chat 侧一致的多模态语义：仅发单条多模态用户消息
            options.InputItems.Add(BuildMultimodalUserItem(request.ContentBlocks));
        }
        else
        {
            foreach (var msg in request.Messages)
            {
                if (msg.Role == "assistant" && msg.ToolCalls is { Count: > 0 })
                {
                    if (!string.IsNullOrEmpty(msg.Content))
                        options.InputItems.Add(ResponseItem.CreateAssistantMessageItem(msg.Content));
                    foreach (var tc in msg.ToolCalls)
                    {
                        // Responses 规范：function_call 的 arguments 为 JSON 字符串
                        options.InputItems.Add(ResponseItem.CreateFunctionCallItem(
                            tc.Id, tc.Name, BinaryData.FromString(string.IsNullOrEmpty(tc.Arguments) ? "{}" : tc.Arguments)));
                    }
                    continue;
                }

                if (msg.Role == "tool")
                {
                    options.InputItems.Add(ResponseItem.CreateFunctionCallOutputItem(
                        msg.ToolCallId ?? string.Empty, msg.Content ?? string.Empty));
                    continue;
                }

                options.InputItems.Add(msg.Role switch
                {
                    "assistant" => ResponseItem.CreateAssistantMessageItem(msg.Content ?? string.Empty),
                    "system" => ResponseItem.CreateSystemMessageItem(msg.Content ?? string.Empty),
                    _ => ResponseItem.CreateUserMessageItem(msg.Content ?? string.Empty)
                });
            }
        }

        if (request.MaxTokens is int max)
            options.MaxOutputTokenCount = max;
        options.Temperature = (float)request.Temperature;
        options.TopP = (float)request.TopP;

        if (request.Tools is { Count: > 0 })
        {
            foreach (var tool in request.Tools)
            {
                options.Tools.Add(ResponseTool.CreateFunctionTool(
                    tool.Name,
                    tool.Parameters == null ? null : BinaryData.FromObjectAsJson(tool.Parameters),
                    strictModeEnabled: false,
                    functionDescription: tool.Description ?? string.Empty));
            }

            if (!string.IsNullOrWhiteSpace(request.ToolChoice))
            {
                options.ToolChoice = request.ToolChoice switch
                {
                    "auto" => ResponseToolChoice.CreateAutoChoice(),
                    "none" => ResponseToolChoice.CreateNoneChoice(),
                    "required" => ResponseToolChoice.CreateRequiredChoice(),
                    _ => ResponseToolChoice.CreateFunctionChoice(request.ToolChoice)
                };
            }
        }
        return options;
    }

    /// <summary>多模态用户消息 item（text/image_url/image_base64 → input_text/input_image）。</summary>
    private static ResponseItem BuildMultimodalUserItem(List<ContentBlock> blocks)
    {
        var parts = new List<ResponseContentPart>();
        foreach (var block in blocks)
        {
            if (block.Type == "image_url" && !string.IsNullOrEmpty(block.ImageUrl))
            {
                parts.Add(ResponseContentPart.CreateInputImagePart(
                    new Uri(block.ImageUrl), MapResponseImageDetail(block.ImageDetail)));
            }
            else if (block.Type == "image_base64" && !string.IsNullOrEmpty(block.ImageBase64))
            {
                // U-6 修正：ResponsesCreateInputImagePart(BinaryData,…) 无 mediaType 重载，mediaType 并入 data URI
                parts.Add(ResponseContentPart.CreateInputImagePart(
                    BinaryData.FromString($"data:{block.ImageMediaType ?? "image/png"};base64,{block.ImageBase64}"),
                    MapResponseImageDetail(block.ImageDetail)));
            }
            else if (block.Type == "text" && !string.IsNullOrEmpty(block.Text))
            {
                parts.Add(ResponseContentPart.CreateInputTextPart(block.Text));
            }
        }
        return parts.Count == 0
            ? ResponseItem.CreateUserMessageItem(string.Empty)
            : ResponseItem.CreateUserMessageItem(parts);
    }

    private static ResponseImageDetailLevel MapResponseImageDetail(string? detail) => detail switch
    {
        "low" => ResponseImageDetailLevel.Low,
        "high" => ResponseImageDetailLevel.High,
        _ => ResponseImageDetailLevel.Auto
    };

    // ==================== Responses：SDK → Unified（非流式） ====================

    /// <summary>
    /// ResponseResult → UnifiedChatResponse。message→正文；function_call→UnifiedToolCall（arguments 字符串原样）。
    /// PC-013：status=failed/incomplete 抛异常（不当成功）；reasoning/web_search 等 BR-4 项跳过并告警。
    /// </summary>
    public static UnifiedChatResponse ToUnifiedResponse(ResponseResult result, string requestModel, string providerName)
    {
        var status = result.Status?.ToString() ?? string.Empty;
        if (status is "failed" or "incomplete")
        {
            var err = result.Error?.ToString();
            throw new InvalidOperationException($"Responses 上游返回失败态 status={status}, error={err}");
        }

        var text = new StringBuilder();
        var toolCalls = new List<UnifiedToolCall>();
        foreach (var item in result.OutputItems)
        {
            if (item is MessageResponseItem message)
            {
                foreach (var part in message.Content)
                {
                    if (!string.IsNullOrEmpty(part.Text))
                        text.Append(part.Text);
                }
            }
            else if (item is FunctionCallResponseItem call)
            {
                toolCalls.Add(new UnifiedToolCall
                {
                    Id = call.CallId ?? string.Empty,
                    Name = call.FunctionName ?? string.Empty,
                    Arguments = call.FunctionArguments?.ToString() ?? string.Empty
                });
            }
            else if (item is ReasoningResponseItem)
            {
                // BR-4：Responses-only 能力（推理摘要）本期不映射
                XTrace.Log.Debug($"[{providerName}] Responses 输出含 reasoning item，已跳过");
            }
            else
            {
                // BR-4 前向兼容：web_search 等未知 item 类型跳过并告警
                XTrace.Log.Warn($"[{providerName}] Responses 输出含未映射 item 类型: {item.GetType().Name}，已跳过");
            }
        }

        var unified = new UnifiedChatResponse
        {
            Id = string.IsNullOrEmpty(result.Id) ? $"resp-{Guid.NewGuid():N}" : result.Id,
            Model = string.IsNullOrEmpty(result.Model) ? requestModel : result.Model,
            Created = result.CreatedAt.ToUnixTimeSeconds(),
            ProviderName = providerName,
            FinishReason = toolCalls.Count > 0 ? "tool_calls" : "stop",
            Choices = new List<UnifiedChatMessage>()
        };
        unified.Choices.Add(new UnifiedChatMessage
        {
            Role = "assistant",
            Content = text.ToString(),
            ToolCalls = toolCalls.Count > 0 ? toolCalls : null
        });

        if (result.Usage is { } usage)
        {
            unified.Usage = new UnifiedUsage
            {
                PromptTokens = usage.InputTokenCount,
                CompletionTokens = usage.OutputTokenCount,
                TotalTokens = usage.TotalTokenCount
            };
        }
        return unified;
    }

    /// <summary>Responses 非流式入口。</summary>
    public static async Task<UnifiedChatResponse> ResponsesChatAsync(
        AIProviderConfig config,
        UnifiedChatRequest request,
        HttpClient httpClient,
        string providerName,
        CancellationToken cancellationToken = default)
    {
        XTrace.Log.Info($"[{providerName}] 发送 Responses 请求（SDK），模型: {request.Model}, 消息数: {request.Messages.Count}");
        var responses = CreateResponsesClient(config, httpClient).GetResponsesClient();
        var result = (await responses.CreateResponseAsync(BuildResponseOptions(request), cancellationToken)).Value;
        return ToUnifiedResponse(result, request.Model, providerName);
    }

    // ==================== Responses 流式：SDK → Unified ====================

    /// <summary>
    /// Responses 流式：CreateResponseStreamingAsync（需显式 StreamingEnabled=true，SDK 实证）→ UnifiedStreamChunk。
    /// output_text.delta→DeltaContent；function_call 增量→DeltaToolCall；completed→usage+终止；failed/error→抛异常终止（PC-013）。
    /// </summary>
    public static async IAsyncEnumerable<UnifiedStreamChunk> ResponsesStreamAsync(
        AIProviderConfig config,
        UnifiedChatRequest request,
        HttpClient httpClient,
        string providerName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        XTrace.Log.Info($"[{providerName}] 发送 Responses 流式请求（SDK），模型: {request.Model}");
        var responses = CreateResponsesClient(config, httpClient).GetResponsesClient();
        var options = BuildResponseOptions(request);
        options.StreamingEnabled = true;

        var id = $"resp-{Guid.NewGuid():N}";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        UnifiedUsage? finalUsage = null;

        await foreach (var upd in responses.CreateResponseStreamingAsync(options, cancellationToken))
        {
            switch (upd)
            {
                case StreamingResponseOutputTextDeltaUpdate textDelta:
                    // U-6 修正：该事件类型无 Id 成员，流分片复用本地生成的会话 id
                    yield return new UnifiedStreamChunk
                    {
                        Id = id,
                        Model = request.Model,
                        Created = created,
                        ChoiceIndex = 0,
                        DeltaContent = textDelta.Delta ?? string.Empty
                    };
                    break;

                case StreamingResponseOutputItemAddedUpdate itemAdded when itemAdded.Item is FunctionCallResponseItem fn:
                    yield return new UnifiedStreamChunk
                    {
                        Id = id,
                        Model = request.Model,
                        Created = created,
                        ChoiceIndex = 0,
                        DeltaToolCall = new UnifiedToolCall
                        {
                            Id = fn.CallId ?? string.Empty,
                            Name = fn.FunctionName ?? string.Empty,
                            Arguments = string.Empty
                        }
                    };
                    break;

                case StreamingResponseFunctionCallArgumentsDeltaUpdate argsDelta:
                    yield return new UnifiedStreamChunk
                    {
                        Id = id,
                        Model = request.Model,
                        Created = created,
                        ChoiceIndex = 0,
                        DeltaToolCall = new UnifiedToolCall
                        {
                            Id = string.Empty,
                            Name = string.Empty,
                            Arguments = argsDelta.Delta?.ToString() ?? string.Empty
                        }
                    };
                    break;

                case StreamingResponseCompletedUpdate done:
                    if (done.Response?.Usage is { } usage)
                    {
                        finalUsage = new UnifiedUsage
                        {
                            PromptTokens = usage.InputTokenCount,
                            CompletionTokens = usage.OutputTokenCount,
                            TotalTokens = usage.TotalTokenCount
                        };
                        yield return new UnifiedStreamChunk
                        {
                            Id = string.IsNullOrEmpty(done.Response.Id) ? id : done.Response.Id,
                            Model = string.IsNullOrEmpty(done.Response.Model) ? request.Model : done.Response.Model,
                            Created = done.Response.CreatedAt.ToUnixTimeSeconds(),
                            ChoiceIndex = 0,
                            Usage = finalUsage
                        };
                    }
                    break;

                case StreamingResponseFailedUpdate failed:
                    // U-6 修正：Response.Error 为 ResponseError 类型，取其 Message（空则类型名兜底）
                    var failMsg = failed.Response?.Error?.Message;
                    if (string.IsNullOrEmpty(failMsg))
                        failMsg = failed.Response?.Error?.ToString() ?? "unknown";
                    throw new InvalidOperationException($"Responses 上游流式失败: {failMsg}");

                default:
                    // 未知事件类型跳过（前向兼容，PC-008 前向兼容语义）
                    XTrace.Log.Debug($"[{providerName}] Responses 流式未处理事件: {upd.GetType().Name}");
                    break;
            }
        }

        yield return new UnifiedStreamChunk
        {
            Id = id,
            Model = request.Model,
            Created = created,
            ChoiceIndex = 0,
            FinishReason = finalUsage != null ? "stop" : "stop",
            IsDone = true
        };
    }
}

using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using OpenForgeSelf.Backend.Entities;
using OpenForgeSelf.Backend.Services;
using OpenForgeSelf.Backend.Services.AI;
using OpenForgeSelf.Backend.Services.AI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Controllers.UnifiedAI;

[ApiController]
[Route("v1")]
[Authorize("ApiKeyPolicy")]
public class OpenAIChatController : ControllerBase
{
    private readonly AIProviderRegistry _registry;
    private readonly ILogService _logService;
    private readonly IChatTurnService _chatTurnService;
    private readonly IChatTurnStreamRecorder _streamRecorder;
    private readonly IChatSessionService _chatSessionService;
    private readonly MultimodalProcessor? _multimodalProcessor;

    // 序列化选项：保留非 ASCII 字符（中文等）不被转义为 \uXXXX，同时仍转义 HTML 敏感字符以防 XSS
    private static readonly JsonSerializerOptions StreamSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public OpenAIChatController(AIProviderRegistry registry, ILogService logService, IChatTurnService chatTurnService, IChatTurnStreamRecorder streamRecorder, IChatSessionService chatSessionService, IImageRecognitionCache imageRecognitionCache)
    {
        _registry = registry;
        _logService = logService;
        _chatTurnService = chatTurnService;
        _streamRecorder = streamRecorder;
        _chatSessionService = chatSessionService;
        // 尝试获取多模态处理器
        var defaultConfig = registry.GetDefaultProvider()?.GetType()
            .GetField("_config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(registry.GetDefaultProvider()) as AIProviderConfig;

        if (defaultConfig?.EnableMultimodal == true && !string.IsNullOrEmpty(defaultConfig.VisionModel))
        {
            _multimodalProcessor = new MultimodalProcessor(registry, defaultConfig, imageRecognitionCache);
            XTrace.Log.Info("多模态处理器已启用，视觉模型: {0}", defaultConfig.VisionModel);
        }
    }

    [HttpPost("chat/completions")]
    public async Task<IActionResult> ChatCompletions([FromBody] OpenAIChatCompletionRequest request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        ChatTurn? turn = null;
        ChatSession? chatSession = null;

        try
        {
            if (request == null || request.Messages == null || request.Messages.Count == 0)
                return BadRequest(new { error = new { message = "messages is required", type = "invalid_request_error" } });

            if (string.IsNullOrEmpty(request.Model))
                return BadRequest(new { error = new { message = "model is required", type = "invalid_request_error" } });

            // 解析提供方前缀和上游模型 ID（格式：provider:model_id）
            var (provider, upstreamModelId) = ResolveProviderAndModel(request.Model);
            if (provider == null)
                return BadRequest(new { error = new { message = $"No provider found for model '{request.Model}'", type = "invalid_request_error" } });

            // 使用上游模型 ID 替换请求中的模型名，确保上游 API 收到正确的模型名
            request.Model = upstreamModelId;
            var unifiedRequest = ConvertToUnifiedRequest(request);

            // 会话归并：解析稳定会话键（提前到多模态处理前，兼作图片识别缓存的分区键）
            var conversationKey = ChatSessionResolver.ResolveConversationKey(Request, request);

            // 检查并处理多模态图片（传入会话键：同会话内相同图片的识别结果走本地缓存）
            if (_multimodalProcessor != null && _multimodalProcessor.HasImages(unifiedRequest))
            {
                _logService.Info("检测到图片内容，启用多模态处理...");
                unifiedRequest = await _multimodalProcessor.ProcessAsync(unifiedRequest, conversationKey, cancellationToken);
            }

            _logService.Info("OpenAI Chat 请求 - 模型: {0}, 提供者: {1}, 消息数: {2}",
                request.Model, provider.ProviderName, request.Messages.Count);

            // 会话归并：统一 upsert 会话（复用上面已解析的会话键）
            var clientKind = ChatSessionResolver.DeriveClientKind(Request);
            var firstUserMsg = ChatSessionResolver.ExtractFirstUserMessage(request);
            chatSession = await _chatSessionService.UpsertSessionAsync(conversationKey, SessionSource.Proxy, request.Model, clientKind, "OpenAI_Chat", firstUserMsg, request.Messages?.Count ?? 0, provider: provider.ProviderName);

            // 创建聊天轮次（会话内一轮），填好会话归属外键
            turn = new ChatTurn
            {
                ChatSessionId = chatSession.Id,
                TurnIndex = chatSession.RequestCount,
                SessionKey = chatSession.SessionKey,
                UserPreview = ChatSessionResolver.Truncate(firstUserMsg, 500),
                Style = "OpenAI_Chat",
                Model = request.Model,
                RequestMethod = "POST",
                RequestPath = "/v1/chat/completions",
                RequestHeaders = JsonSerializer.Serialize(Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString())),
                RequestBody = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = false }),
                Temperature = request.Temperature,
                MaxTokens = request.MaxTokens ?? 0,
                MessageCount = request.Messages?.Count ?? 0,
                ToolCallCount = request.Messages?.Sum(m => m.ToolCalls?.Count ?? 0) ?? 0,
                HasReasoning = false,
                CreatedTime = DateTime.Now
            };

            if (request.Stream)
            {
                await HandleStreamAsync(unifiedRequest, provider, request.Model, turn, stopwatch, cancellationToken);
                return new EmptyResult();
            }
            else
            {
                var response = await provider.ChatAsync(unifiedRequest, cancellationToken);
                var openaiResponse = ConvertToOpenAIResponse(response, request.Model);

                // 更新轮次
                stopwatch.Stop();
                turn.ResponseStatus = 200;
                turn.ResponseHeaders = JsonSerializer.Serialize(Response.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()));
                turn.ResponseBody = JsonSerializer.Serialize(openaiResponse, new JsonSerializerOptions { WriteIndented = false });
                turn.DurationMs = stopwatch.ElapsedMilliseconds;
                turn.HasReasoning = response.Choices?.Any(c => !string.IsNullOrEmpty(c.ReasoningContent)) ?? false;
                turn.PromptTokens = response.Usage?.PromptTokens ?? 0;
                turn.CompletionTokens = response.Usage?.CompletionTokens ?? 0;
                turn.TotalTokens = response.Usage?.TotalTokens ?? 0;
                turn.AssistantPreview = ChatSessionResolver.Truncate(ExtractContent(response), 500);
                await _chatTurnService.SaveTurnAsync(turn);
                await _chatSessionService.RecordTurnStatsAsync(chatSession.Id, 200, turn.PromptTokens, turn.CompletionTokens);

                return Ok(openaiResponse);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // 更新错误轮次
            if (turn != null)
            {
                turn.ResponseStatus = 500;
                turn.ResponseBody = JsonSerializer.Serialize(new { error = ex.Message });
                turn.DurationMs = stopwatch.ElapsedMilliseconds;
                turn.ErrorMessage = ChatSessionResolver.Truncate(ex.Message, 1000);
                await _chatTurnService.SaveTurnAsync(turn);
                await _chatSessionService.RecordTurnStatsAsync(chatSession?.Id ?? 0, 500, 0, 0);
            }

            _logService.Error("OpenAI Chat Completions 处理失败: {0}", ex.Message);
            return StatusCode(500, new { error = new { message = ex.Message, type = "internal_error" } });
        }
    }

    private async Task HandleStreamAsync(UnifiedChatRequest request, IAIProvider provider, string modelName, ChatTurn turn, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("X-Accel-Buffering", "no");

        var id = $"chatcmpl-{Guid.NewGuid():N}";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var responseBody = new StringBuilder();
        ChatTurnStreamSession? streamSession = null;
        // 流式期间累计用量（上游 include_usage 哨兵分片于末尾回传）
        var lastPrompt = 0;
        var lastCompletion = 0;
        var lastTotal = 0;

        try
        {
            streamSession = await _streamRecorder.BeginAsync(turn);
            var firstChunk = CreateStreamChunk(id, modelName, created, 0, role: "assistant");
            await WriteSseAsync(firstChunk);
            responseBody.Append(JsonSerializer.Serialize(firstChunk, StreamSerializerOptions));

            await foreach (var chunk in provider.ChatStreamAsync(request, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var hasContent = !string.IsNullOrEmpty(chunk.DeltaContent)
                    || chunk.DeltaRole != null
                    || chunk.DeltaToolCall != null
                    || chunk.FinishReason != null;

                var streamChunk = new OpenAIStreamChunk
                {
                    Id = chunk.Id ?? id,
                    Object = "chat.completion.chunk",
                    Created = chunk.Created > 0 ? chunk.Created : created,
                    Model = chunk.Model ?? modelName,
                    // usage 哨兵分片（上游末尾，choices 为空）保持空 choices，贴合 OpenAI 语义；其余按内容构造 choices
                    Choices = hasContent
                        ? new List<OpenAIStreamChoice>
                        {
                            new()
                            {
                                Index = chunk.ChoiceIndex,
                                Delta = new OpenAIStreamDelta
                                {
                                    Content = string.IsNullOrEmpty(chunk.DeltaContent) ? null : chunk.DeltaContent,
                                    Role = chunk.DeltaRole,
                                    ToolCalls = chunk.DeltaToolCall == null
                                        ? null
                                        : new List<OpenAIToolCall>
                                        {
                                            new()
                                            {
                                                Index = chunk.ChoiceIndex,
                                                Id = chunk.DeltaToolCall.Id,
                                                Type = "function",
                                                Function = new OpenAIFunctionCall
                                                {
                                                    Name = chunk.DeltaToolCall.Name,
                                                    Arguments = chunk.DeltaToolCall.Arguments
                                                }
                                            }
                                        }
                                },
                                FinishReason = chunk.FinishReason
                            }
                        }
                        : new List<OpenAIStreamChoice>()
                };

                // 透传 usage（已用 token 字段）：上游哨兵分片或带 usage 的内容分片均回填
                if (chunk.Usage != null)
                {
                    streamChunk.Usage = new OpenAIUsage
                    {
                        PromptTokens = chunk.Usage.PromptTokens,
                        CompletionTokens = chunk.Usage.CompletionTokens,
                        TotalTokens = chunk.Usage.TotalTokens
                    };
                    lastPrompt = chunk.Usage.PromptTokens;
                    lastCompletion = chunk.Usage.CompletionTokens;
                    lastTotal = chunk.Usage.TotalTokens;
                }

                await WriteSseAsync(streamChunk);
                responseBody.Append(JsonSerializer.Serialize(streamChunk, StreamSerializerOptions));
                if (streamSession != null)
                    await streamSession.AppendChunkAsync(chunk.DeltaContent);

                // 首 token 延迟（分析响应速度）
                if (turn.FirstTokenMs == 0 && !string.IsNullOrEmpty(chunk.DeltaContent))
                    turn.FirstTokenMs = stopwatch.ElapsedMilliseconds;
            }

            var doneChunk = CreateStreamChunk(id, modelName, created, 0, finishReason: "stop");
            await WriteSseAsync(doneChunk);
            await WriteSseRawAsync("[DONE]");
            responseBody.Append("[DONE]");

            // 更新轮次
            stopwatch.Stop();
            turn.ResponseStatus = 200;
            turn.ResponseHeaders = JsonSerializer.Serialize(Response.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()));
            turn.ResponseBody = responseBody.ToString();
            turn.DurationMs = stopwatch.ElapsedMilliseconds;
            turn.PromptTokens = lastPrompt;
            turn.CompletionTokens = lastCompletion;
            turn.TotalTokens = lastTotal;
            turn.AssistantPreview = ChatSessionResolver.Truncate(turn.ResponseText, 500);
            if (streamSession != null)
            {
                await streamSession.CompleteAsync(responseBody.ToString(), 200, stopwatch.ElapsedMilliseconds);
                await _chatSessionService.RecordTurnStatsAsync(turn.ChatSessionId, 200, turn.PromptTokens, turn.CompletionTokens);
            }
            else
            {
                await _chatTurnService.SaveTurnAsync(turn);
                await _chatSessionService.RecordTurnStatsAsync(turn.ChatSessionId, 200, turn.PromptTokens, turn.CompletionTokens);
            }
        }
        catch (Exception ex)
        {
            _logService.Error("流式响应异常: {0}", ex.Message);

            // 更新错误轮次
            stopwatch.Stop();
            turn.ResponseStatus = 500;
            turn.ResponseBody = JsonSerializer.Serialize(new { error = ex.Message }, StreamSerializerOptions);
            turn.DurationMs = stopwatch.ElapsedMilliseconds;
            turn.ErrorMessage = ChatSessionResolver.Truncate(ex.Message, 1000);
            if (streamSession != null)
            {
                await streamSession.FailAsync(500, stopwatch.ElapsedMilliseconds);
                await _chatSessionService.RecordTurnStatsAsync(turn.ChatSessionId, 500, 0, 0);
            }
            else
            {
                await _chatTurnService.SaveTurnAsync(turn);
                await _chatSessionService.RecordTurnStatsAsync(turn.ChatSessionId, 500, 0, 0);
            }
        }
    }

    private async Task WriteSseAsync<T>(T data)
    {
        var json = JsonSerializer.Serialize(data, StreamSerializerOptions);
        await Response.WriteAsync($"data: {json}\n\n");
        await Response.Body.FlushAsync();
    }

    private async Task WriteSseRawAsync(string data)
    {
        await Response.WriteAsync($"data: {data}\n\n");
        await Response.Body.FlushAsync();
    }

    private static OpenAIStreamChunk CreateStreamChunk(string id, string model, long created, int index, string? role = null, string? finishReason = null)
    {
        return new OpenAIStreamChunk
        {
            Id = id,
            Object = "chat.completion.chunk",
            Created = created,
            Model = model,
            Choices = new List<OpenAIStreamChoice>
            {
                new()
                {
                    Index = index,
                    Delta = new OpenAIStreamDelta { Role = role },
                    FinishReason = finishReason
                }
            }
        };
    }

    /// <summary>
    /// 解析模型名中的提供方前缀，返回 (提供方, 上游模型ID)。
    /// 支持格式 "provider:model_id" 和 "model_id"（无前缀时使用默认提供方）。
    /// </summary>
    private (IAIProvider? Provider, string UpstreamModelId) ResolveProviderAndModel(string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
            return (null, modelName);

        var idx = modelName.IndexOf(':');
        if (idx > 0)
        {
            var providerName = modelName[..idx];
            var upstreamModelId = modelName[(idx + 1)..];
            var provider = _registry.GetProviderByName(providerName);
            if (provider != null)
                return (provider, upstreamModelId);
        }

        // 无前缀或提供方不存在，回退到 GetProviderByModel
        var fallbackProvider = _registry.GetProviderByModel(modelName);
        return (fallbackProvider, modelName);
    }

    private UnifiedChatRequest ConvertToUnifiedRequest(OpenAIChatCompletionRequest request)
    {
        var unified = new UnifiedChatRequest
        {
            Model = request.Model,
            Temperature = request.Temperature,
            TopP = request.TopP,
            MaxTokens = request.MaxTokens,
            Stream = request.Stream,
            StreamOptions = request.StreamOptions == null
                ? null
                : new UnifiedStreamOptions { IncludeUsage = request.StreamOptions.IncludeUsage },
            Messages = new List<UnifiedChatMessage>()
        };

        string? systemPrompt = null;
        foreach (var msg in request.Messages)
        {
            // 处理系统消息
            if (msg.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
            {
                var sysText = msg.GetTextContent();
                systemPrompt = string.IsNullOrEmpty(systemPrompt)
                    ? sysText
                    : $"{systemPrompt}\n{sysText}";
                continue;
            }

            // 获取文本内容
            var textContent = msg.GetTextContent();

            // 检查是否有多模态内容（图片）
            List<ContentBlock>? contentBlocks = null;
            if (msg.Content is IEnumerable<object> contentList)
            {
                contentBlocks = ConvertToContentBlocks(contentList);
            }

            var unifiedMsg = new UnifiedChatMessage
            {
                Role = msg.Role,
                Content = textContent ?? string.Empty,
                Name = msg.Name,
                ContentBlocks = contentBlocks
            };

            if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
            {
                unifiedMsg.ToolCalls = msg.ToolCalls.Select(tc => new UnifiedToolCall
                {
                    Id = tc.Id,
                    Name = tc.Function?.Name ?? string.Empty,
                    Arguments = tc.Function?.Arguments ?? string.Empty
                }).ToList();
            }

            if (!string.IsNullOrEmpty(msg.ToolCallId))
                unifiedMsg.ToolCallId = msg.ToolCallId;

            unified.Messages.Add(unifiedMsg);
        }

        unified.SystemPrompt = systemPrompt;

        if (request.Tools != null && request.Tools.Count > 0)
        {
            unified.Tools = request.Tools.Where(t => t.Function != null).Select(t => new UnifiedToolDefinition
            {
                Name = t.Function!.Name,
                Description = t.Function.Description ?? string.Empty,
                Parameters = t.Function.Parameters
            }).ToList();
        }

        return unified;
    }

    /// <summary>
    /// 将 OpenAI 多模态内容数组转换为统一内容块
    /// </summary>
    private List<ContentBlock> ConvertToContentBlocks(IEnumerable<object> contentList)
    {
        var blocks = new List<ContentBlock>();

        foreach (var item in contentList)
        {
            if (item is JsonElement je)
            {
                var type = je.TryGetProperty("type", out var t) ? t.GetString() : "text";

                if (type == "text")
                {
                    var text = je.TryGetProperty("text", out var txt) ? txt.GetString() : "";
                    blocks.Add(new ContentBlock { Type = "text", Text = text });
                }
                else if (type == "image_url")
                {
                    if (je.TryGetProperty("image_url", out var imgUrl))
                    {
                        var url = imgUrl.ValueKind == JsonValueKind.String
                            ? imgUrl.GetString()
                            : (imgUrl.TryGetProperty("url", out var u) ? u.GetString() : null);
                        var detail = imgUrl.ValueKind != JsonValueKind.String && imgUrl.TryGetProperty("detail", out var d)
                            ? d.GetString()
                            : "auto";
                        blocks.Add(new ContentBlock
                        {
                            Type = "image_url",
                            ImageUrl = url,
                            ImageDetail = detail
                        });
                    }
                }
            }
            else if (item is IDictionary<string, object> dict)
            {
                var type = dict.TryGetValue("type", out var t) ? t?.ToString() : "text";

                if (type == "text")
                {
                    var text = dict.TryGetValue("text", out var txt) ? txt?.ToString() : "";
                    blocks.Add(new ContentBlock { Type = "text", Text = text });
                }
                else if (type == "image_url")
                {
                    if (dict.TryGetValue("image_url", out var imgUrl))
                    {
                        string? url = null;
                        string? detail = "auto";

                        if (imgUrl is JsonElement je2)
                        {
                            url = je2.ValueKind == JsonValueKind.String
                                ? je2.GetString()
                                : (je2.TryGetProperty("url", out var u) ? u.GetString() : null);
                            if (je2.ValueKind != JsonValueKind.String && je2.TryGetProperty("detail", out var d))
                                detail = d.GetString();
                        }
                        else if (imgUrl is IDictionary<string, object> imgDict)
                        {
                            url = imgDict.TryGetValue("url", out var u) ? u?.ToString() : null;
                            detail = imgDict.TryGetValue("detail", out var d) ? d?.ToString() ?? "auto" : "auto";
                        }

                        blocks.Add(new ContentBlock
                        {
                            Type = "image_url",
                            ImageUrl = url,
                            ImageDetail = detail
                        });
                    }
                }
            }
        }

        return blocks;
    }

    private object ConvertToOpenAIResponse(UnifiedChatResponse response, string requestModel)
    {
        return new
        {
            id = response.Id,
            @object = "chat.completion",
            created = response.Created,
            model = string.IsNullOrEmpty(response.Model) ? requestModel : response.Model,
            choices = response.Choices.Select((c, i) => new
            {
                index = i,
                message = new
                {
                    role = c.Role,
                    content = c.Content,
                    tool_calls = c.ToolCalls?.Select(tc => new
                    {
                        id = tc.Id,
                        type = "function",
                        function = new { name = tc.Name, arguments = tc.Arguments }
                    }).ToList()
                },
                finish_reason = string.IsNullOrEmpty(response.FinishReason) ? "stop" : response.FinishReason
            }).ToList(),
            usage = new
            {
                prompt_tokens = response.Usage?.PromptTokens ?? 0,
                completion_tokens = response.Usage?.CompletionTokens ?? 0,
                total_tokens = response.Usage?.TotalTokens ?? 0
            }
        };
    }

    /// <summary>
    /// 从统一响应提取助手纯文本内容（用于轮次速览）。
    /// </summary>
    private static string? ExtractContent(UnifiedChatResponse response)
    {
        var first = response.Choices.FirstOrDefault();
        return first?.Content;
    }
}

#region OpenAI Request DTOs

public class OpenAIChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<OpenAIChatMessage>? Messages { get; set; }

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 1.0;

    [JsonPropertyName("top_p")]
    public double TopP { get; set; } = 1.0;

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("stream_options")]
    public OpenAIStreamOptions? StreamOptions { get; set; }

    [JsonPropertyName("tools")]
    public List<OpenAITool>? Tools { get; set; }

    [JsonPropertyName("tool_choice")]
    public string? ToolChoice { get; set; }

    [JsonPropertyName("n")]
    public int N { get; set; } = 1;

    [JsonPropertyName("stop")]
    public string? Stop { get; set; }

    [JsonPropertyName("presence_penalty")]
    public double PresencePenalty { get; set; } = 0;

    [JsonPropertyName("frequency_penalty")]
    public double FrequencyPenalty { get; set; } = 0;
}

public class OpenAIStreamOptions
{
    [JsonPropertyName("include_usage")]
    public bool IncludeUsage { get; set; }
}

public class OpenAIChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// OpenAI 多模态消息 content 可以是 string（纯文本）或 array（多模态内容）
    /// 数组格式: [{type: "text", text: "..."}, {type: "image_url", image_url: {...}}]
    /// </summary>
    [JsonPropertyName("content")]
    [JsonConverter(typeof(ChatContentJsonConverter))]
    public object? Content { get; set; }

    /// <summary>
    /// 获取纯文本内容（如果有）
    /// </summary>
    public string? GetTextContent()
    {
        if (Content == null) return null;
        if (Content is string s) return s;
        // 如果是数组，尝试提取文本部分
        if (Content is IEnumerable<object> list)
        {
            var textParts = list
                .Where(c => c is JsonElement je && je.TryGetProperty("type", out var t) && t.GetString() == "text")
                .Select(c => (c as JsonElement?)?.GetProperty("text").GetString())
                .Where(s => !string.IsNullOrEmpty(s));
            return string.Join("\n", textParts);
        }
        return Content.ToString();
    }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("tool_calls")]
    public List<OpenAIToolCall>? ToolCalls { get; set; }

    [JsonPropertyName("tool_call_id")]
    public string? ToolCallId { get; set; }
}

/// <summary>
/// 处理 OpenAI 多模态消息的 Content 字段
/// </summary>
public class ChatContentJsonConverter : JsonConverter<object>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return reader.GetString();

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var list = new List<object>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                var item = JsonSerializer.Deserialize<object>(ref reader, options);
                list.Add(item!);
            }
            return list;
        }

        return JsonSerializer.Deserialize<object>(ref reader, options);
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
        }
        else if (value is string s)
        {
            writer.WriteStringValue(s);
        }
        else if (value is IEnumerable<object> list)
        {
            writer.WriteStartArray();
            foreach (var item in list)
                JsonSerializer.Serialize(writer, item, options);
            writer.WriteEndArray();
        }
        else
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}

public class OpenAITool
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public OpenAIFunctionDef? Function { get; set; }
}

public class OpenAIFunctionDef
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("parameters")]
    public object? Parameters { get; set; }
}

public class OpenAIToolCall
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public OpenAIFunctionCall? Function { get; set; }
}

public class OpenAIFunctionCall
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public string Arguments { get; set; } = string.Empty;
}

public class OpenAIStreamChunk
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = "chat.completion.chunk";

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<OpenAIStreamChoice>? Choices { get; set; }

    /// <summary>
    /// 流式用量（已用 token）。上游在 include_usage=true 时于末尾发送 usage 哨兵分片，网关需透传，否则前端拿不到 token 统计。
    /// </summary>
    [JsonPropertyName("usage")]
    public OpenAIUsage? Usage { get; set; }
}

/// <summary>
/// 流式 / 非流式共用的 token 用量结构（prompt / completion / total）。
/// </summary>
public class OpenAIUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

public class OpenAIStreamChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("delta")]
    public OpenAIStreamDelta? Delta { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public class OpenAIStreamDelta
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("tool_calls")]
    public List<OpenAIToolCall>? ToolCalls { get; set; }
}

#endregion

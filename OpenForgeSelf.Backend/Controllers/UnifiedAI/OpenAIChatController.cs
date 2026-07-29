using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    private readonly IChatRecordService _chatRecordService;
    private readonly MultimodalProcessor? _multimodalProcessor;

    public OpenAIChatController(AIProviderRegistry registry, ILogService logService, IChatRecordService chatRecordService)
    {
        _registry = registry;
        _logService = logService;
        _chatRecordService = chatRecordService;

        // 尝试获取多模态处理器
        var defaultConfig = registry.GetDefaultProvider()?.GetType()
            .GetField("_config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(registry.GetDefaultProvider()) as AIProviderConfig;

        if (defaultConfig?.EnableMultimodal == true && !string.IsNullOrEmpty(defaultConfig.VisionModel))
        {
            _multimodalProcessor = new MultimodalProcessor(registry, defaultConfig);
            XTrace.Log.Info("多模态处理器已启用，视觉模型: {0}", defaultConfig.VisionModel);
        }
    }

    [HttpGet("models")]
    public async Task<IActionResult> ListModels(CancellationToken cancellationToken)
    {
        var allModels = await _registry.GetAllModelsAsync(cancellationToken);
        var response = new
        {
            @object = "list",
            data = allModels.Select(m => new
            {
                id = m.Id,
                @object = "model",
                created = m.Created,
                owned_by = m.Owner
            }).ToList()
        };
        return Ok(response);
    }

    [HttpGet("models/{model}")]
    public IActionResult GetModel(string model)
    {
        var modelInfo = _registry.GetModelById(model);
        if (modelInfo == null)
            return NotFound(new { error = new { message = $"Model '{model}' not found", type = "not_found" } });

        return Ok(new
        {
            id = modelInfo.Id,
            @object = "model",
            created = modelInfo.Created,
            owned_by = modelInfo.Owner
        });
    }

    [HttpPost("chat/completions")]
    public async Task<IActionResult> ChatCompletions([FromBody] OpenAIChatCompletionRequest request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        ChatRecord? record = null;

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

            // 检查并处理多模态图片
            if (_multimodalProcessor != null && _multimodalProcessor.HasImages(unifiedRequest))
            {
                _logService.Info("检测到图片内容，启用多模态处理...");
                unifiedRequest = await _multimodalProcessor.ProcessAsync(unifiedRequest, cancellationToken);
            }

            _logService.Info("OpenAI Chat 请求 - 模型: {0}, 提供者: {1}, 消息数: {2}",
                request.Model, provider.ProviderName, request.Messages.Count);

            // 创建聊天记录
            var sessionId = Request.Headers["X-Session-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
            record = new ChatRecord
            {
                SessionId = sessionId,
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
                await HandleStreamAsync(unifiedRequest, provider, request.Model, record, stopwatch, cancellationToken);
                return new EmptyResult();
            }
            else
            {
                var response = await provider.ChatAsync(unifiedRequest, cancellationToken);
                var openaiResponse = ConvertToOpenAIResponse(response, request.Model);

                // 更新记录
                stopwatch.Stop();
                record.ResponseStatus = 200;
                record.ResponseHeaders = JsonSerializer.Serialize(Response.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()));
                record.ResponseBody = JsonSerializer.Serialize(openaiResponse, new JsonSerializerOptions { WriteIndented = false });
                record.DurationMs = stopwatch.ElapsedMilliseconds;
                record.HasReasoning = response.Choices?.Any(c => !string.IsNullOrEmpty(c.ReasoningContent)) ?? false;
                await _chatRecordService.SaveRecordAsync(record);

                return Ok(openaiResponse);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // 更新错误记录
            if (record != null)
            {
                record.ResponseStatus = 500;
                record.ResponseBody = JsonSerializer.Serialize(new { error = ex.Message });
                record.DurationMs = stopwatch.ElapsedMilliseconds;
                await _chatRecordService.SaveRecordAsync(record);
            }

            _logService.Error("OpenAI Chat Completions 处理失败: {0}", ex.Message);
            return StatusCode(500, new { error = new { message = ex.Message, type = "internal_error" } });
        }
    }

    private async Task HandleStreamAsync(UnifiedChatRequest request, IAIProvider provider, string modelName, ChatRecord record, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("X-Accel-Buffering", "no");

        var id = $"chatcmpl-{Guid.NewGuid():N}";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var responseBody = new StringBuilder();

        try
        {
            var firstChunk = CreateStreamChunk(id, modelName, created, 0, role: "assistant");
            await WriteSseAsync(firstChunk);
            responseBody.Append(JsonSerializer.Serialize(firstChunk));

            await foreach (var chunk in provider.ChatStreamAsync(request, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var streamChunk = new OpenAIStreamChunk
                {
                    Id = chunk.Id ?? id,
                    Object = "chat.completion.chunk",
                    Created = chunk.Created > 0 ? chunk.Created : created,
                    Model = chunk.Model ?? modelName,
                    Choices = new List<OpenAIStreamChoice>
                    {
                        new()
                        {
                            Index = chunk.ChoiceIndex,
                            Delta = new OpenAIStreamDelta
                            {
                                Content = string.IsNullOrEmpty(chunk.DeltaContent) ? null : chunk.DeltaContent,
                                Role = chunk.DeltaRole
                            },
                            FinishReason = chunk.FinishReason
                        }
                    }
                };
                await WriteSseAsync(streamChunk);
                responseBody.Append(JsonSerializer.Serialize(streamChunk));
            }

            var doneChunk = CreateStreamChunk(id, modelName, created, 0, finishReason: "stop");
            await WriteSseAsync(doneChunk);
            await WriteSseRawAsync("[DONE]");
            responseBody.Append("[DONE]");

            // 更新记录
            stopwatch.Stop();
            record.ResponseStatus = 200;
            record.ResponseHeaders = JsonSerializer.Serialize(Response.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()));
            record.ResponseBody = responseBody.ToString();
            record.DurationMs = stopwatch.ElapsedMilliseconds;
            await _chatRecordService.SaveRecordAsync(record);
        }
        catch (Exception ex)
        {
            _logService.Error("流式响应异常: {0}", ex.Message);

            // 更新错误记录
            stopwatch.Stop();
            record.ResponseStatus = 500;
            record.ResponseBody = JsonSerializer.Serialize(new { error = ex.Message });
            record.DurationMs = stopwatch.ElapsedMilliseconds;
            await _chatRecordService.SaveRecordAsync(record);
        }
    }

    private async Task WriteSseAsync<T>(T data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
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

using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenForgeSelf.Backend.Services.AI.Models;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Services.AI.Providers;

public class OpenAICompatibleProvider : IAIProvider
{
    private readonly AIProviderConfig _config;
    private readonly HttpClient _httpClient;

    public string ProviderName => _config.Name;
    public AIProviderType ProviderType => _config.ProviderType;
    public List<string> SupportedModels => _config.SupportedModels;
    public bool IsDefault => _config.IsDefault;

    public OpenAICompatibleProvider(AIProviderConfig config, HttpClient httpClient)
    {
        _config = config;
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
    }

    public async Task<UnifiedChatResponse> ChatAsync(UnifiedChatRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var openaiRequest = BuildOpenAIRequest(request);
            var json = JsonSerializer.Serialize(openaiRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, _config.Endpoint);
            httpRequest.Content = content;
            httpRequest.Headers.Add("Authorization", $"Bearer {_config.ApiKey}");

            XTrace.Log.Info("[{0}] 发送 Chat 请求，模型: {1}, 消息数: {2}",
                ProviderName, request.Model, request.Messages.Count);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var openaiResponse = JsonSerializer.Deserialize<OpenAIChatResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return ConvertToUnifiedResponse(openaiResponse, request.Model);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[{0}] Chat 请求失败: {1}", ProviderName, ex.Message);
            throw;
        }
    }

    public async IAsyncEnumerable<UnifiedStreamChunk> ChatStreamAsync(UnifiedChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var openaiRequest = BuildOpenAIRequest(request);
        openaiRequest.Stream = true;

        var json = JsonSerializer.Serialize(openaiRequest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, _config.Endpoint);
        httpRequest.Content = content;
        httpRequest.Headers.Add("Authorization", $"Bearer {_config.ApiKey}");

        XTrace.Log.Info("[{0}] 发送流式 Chat 请求，模型: {1}", ProviderName, request.Model);

        var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var id = $"chatcmpl-{Guid.NewGuid():N}";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null) break;
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line[6..];
            if (data == "[DONE]")
            {
                yield return new UnifiedStreamChunk
                {
                    Id = id,
                    Model = request.Model,
                    Created = created,
                    ChoiceIndex = 0,
                    FinishReason = "stop",
                    IsDone = true
                };
                yield break;
            }

            OpenAIStreamChunk? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<OpenAIStreamChunk>(data, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException ex)
            {
                    XTrace.Log.Warn("[{0}] 解析流式响应失败: {1}", ProviderName, ex.Message);
                continue;
            }

            if (chunk?.Choices == null || chunk.Choices.Count == 0)
                continue;

            var choice = chunk.Choices[0];
            var delta = choice.Delta;

            var unifiedChunk = new UnifiedStreamChunk
            {
                Id = chunk.Id ?? id,
                Model = chunk.Model ?? request.Model,
                Created = chunk.Created ?? created,
                ChoiceIndex = choice.Index,
                DeltaContent = delta?.Content ?? string.Empty,
                DeltaRole = delta?.Role,
                FinishReason = choice.FinishReason
            };

            if (delta?.ToolCalls != null && delta.ToolCalls.Count > 0)
            {
                var tc = delta.ToolCalls[0];
                unifiedChunk.DeltaToolCall = new UnifiedToolCall
                {
                    Id = tc.Id ?? string.Empty,
                    Name = tc.Function?.Name ?? string.Empty,
                    Arguments = tc.Function?.Arguments ?? string.Empty
                };
            }

            yield return unifiedChunk;
        }
    }

    public Task<List<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        var models = _config.SupportedModels.Select(m => new ModelInfo
        {
            Id = m,
            Name = m,
            Owner = ProviderName,
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ProviderName = ProviderName,
            SupportsStreaming = true
        }).ToList();

        return Task.FromResult(models);
    }

    private OpenAIChatRequest BuildOpenAIRequest(UnifiedChatRequest request)
    {
        var messages = new List<OpenAIMessage>();

        if (!string.IsNullOrEmpty(request.SystemPrompt))
        {
            messages.Add(new OpenAIMessage { Role = "system", Content = request.SystemPrompt });
        }

        // 处理多模态内容块
        if (request.ContentBlocks != null && request.ContentBlocks.Count > 0)
        {
            var firstUserMsg = request.Messages.FirstOrDefault(m => m.Role == "user");
            var contentBlocks = ConvertToOpenAIMultiModalContent(request.ContentBlocks);

            if (firstUserMsg != null)
            {
                // 将第一条用户消息转换为多模态消息
                var multiModalMsg = new OpenAIMessage
                {
                    Role = "user",
                    Content = contentBlocks
                };
                messages.Add(multiModalMsg);
            }
            else
            {
                // 没有用户消息，直接添加一个多模态用户消息
                messages.Add(new OpenAIMessage { Role = "user", Content = contentBlocks });
            }
        }
        else
        {
            // 普通消息处理
            foreach (var msg in request.Messages)
            {
                var openaiMsg = new OpenAIMessage { Role = msg.Role, Content = msg.Content };
                if (!string.IsNullOrEmpty(msg.Name))
                    openaiMsg.Name = msg.Name;

                if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
                {
                    openaiMsg.ToolCalls = msg.ToolCalls.Select(tc => new OpenAIToolCall
                    {
                        Id = tc.Id,
                        Type = "function",
                        Function = new OpenAIFunctionCall
                        {
                            Name = tc.Name,
                            Arguments = tc.Arguments
                        }
                    }).ToList();
                }

                if (!string.IsNullOrEmpty(msg.ToolCallId))
                    openaiMsg.ToolCallId = msg.ToolCallId;

                messages.Add(openaiMsg);
            }
        }

        var openaiRequest = new OpenAIChatRequest
        {
            Model = request.Model,
            Messages = messages,
            Temperature = request.Temperature,
            TopP = request.TopP,
            MaxTokens = request.MaxTokens,
            Stream = request.Stream
        };

        if (request.Tools != null && request.Tools.Count > 0)
        {
            openaiRequest.Tools = request.Tools.Select(t => new OpenAIToolDefinition
            {
                Type = "function",
                Function = new OpenAIFunctionDefinition
                {
                    Name = t.Name,
                    Description = t.Description,
                    Parameters = t.Parameters
                }
            }).ToList();
        }

        return openaiRequest;
    }

    /// <summary>
    /// 将统一多模态内容块转换为 OpenAI 格式
    /// OpenAI 格式: content 可以是 string 或 array [{type, text/url/base64}]
    /// </summary>
    private object ConvertToOpenAIMultiModalContent(List<ContentBlock> blocks)
    {
        // 检查是否包含图片
        var hasImages = blocks.Any(b => b.Type == "image_url" || b.Type == "image_base64");

        if (!hasImages)
        {
            // 只有文本，返回字符串
            var textParts = blocks.Where(b => b.Type == "text" && !string.IsNullOrEmpty(b.Text))
                                  .Select(b => b.Text);
            return string.Join("\n", textParts);
        }

        // 有图片，返回数组格式
        var contentArray = new List<object>();

        foreach (var block in blocks)
        {
            if (block.Type == "text" && !string.IsNullOrEmpty(block.Text))
            {
                contentArray.Add(new { type = "text", text = block.Text });
            }
            else if (block.Type == "image_url" && !string.IsNullOrEmpty(block.ImageUrl))
            {
                var imageObj = new Dictionary<string, object>
                {
                    { "type", "image_url" },
                    { "image_url", new { url = block.ImageUrl } }
                };
                if (!string.IsNullOrEmpty(block.ImageDetail))
                {
                    ((Dictionary<string, object>)imageObj["image_url"])["detail"] = block.ImageDetail;
                }
                contentArray.Add(imageObj);
            }
            else if (block.Type == "image_base64" && !string.IsNullOrEmpty(block.ImageBase64))
            {
                var mediaType = block.ImageMediaType ?? "image/png";
                contentArray.Add(new
                {
                    type = "image_url",
                    image_url = new
                    {
                        url = $"data:{mediaType};base64,{block.ImageBase64}",
                        detail = block.ImageDetail ?? "auto"
                    }
                });
            }
        }

        return contentArray;
    }

    private UnifiedChatResponse ConvertToUnifiedResponse(OpenAIChatResponse? response, string model)
    {
        var unified = new UnifiedChatResponse
        {
            Id = response?.Id ?? $"chatcmpl-{Guid.NewGuid():N}",
            Model = response?.Model ?? model,
            Created = response?.Created ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ProviderName = ProviderName
        };

        if (response?.Choices != null)
        {
            foreach (var choice in response.Choices)
            {
                // 处理多模态响应（Content 可能是数组）
                string content = string.Empty;
                if (choice.Message?.Content != null)
                {
                    if (choice.Message.Content is string s)
                        content = s;
                    else if (choice.Message.Content is IEnumerable<object> list)
                    {
                        // 提取文本内容
                        foreach (var item in list)
                        {
                            if (item is JsonElement je && je.TryGetProperty("text", out var txt))
                                content += txt.GetString() ?? "";
                        }
                    }
                }

                var msg = new UnifiedChatMessage
                {
                    Role = choice.Message?.Role ?? "assistant",
                    Content = content
                };

                if (choice.Message?.ToolCalls != null && choice.Message.ToolCalls.Count > 0)
                {
                    msg.ToolCalls = choice.Message.ToolCalls.Select(tc => new UnifiedToolCall
                    {
                        Id = tc.Id,
                        Name = tc.Function?.Name ?? string.Empty,
                        Arguments = tc.Function?.Arguments ?? string.Empty
                    }).ToList();
                }

                unified.Choices.Add(msg);
                if (!string.IsNullOrEmpty(choice.FinishReason))
                    unified.FinishReason = choice.FinishReason;
            }
        }

        if (response?.Usage != null)
        {
            unified.Usage = new UnifiedUsage
            {
                PromptTokens = response.Usage.PromptTokens,
                CompletionTokens = response.Usage.CompletionTokens,
                TotalTokens = response.Usage.TotalTokens
            };
        }

        return unified;
    }
}

#region OpenAI DTOs

public class OpenAIChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<OpenAIMessage> Messages { get; set; } = new();

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 1.0;

    [JsonPropertyName("top_p")]
    public double TopP { get; set; } = 1.0;

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("tools")]
    public List<OpenAIToolDefinition>? Tools { get; set; }

    [JsonPropertyName("tool_choice")]
    public string? ToolChoice { get; set; }
}

public class OpenAIMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// 内容可以是字符串（纯文本）或数组（多模态消息）
    /// 数组格式: [{type: "text", text: "..."}, {type: "image_url", image_url: {...}}]
    /// </summary>
    [JsonPropertyName("content")]
    [JsonConverter(typeof(ContentJsonConverter))]
    public object Content { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("tool_calls")]
    public List<OpenAIToolCall>? ToolCalls { get; set; }

    [JsonPropertyName("tool_call_id")]
    public string? ToolCallId { get; set; }
}

/// <summary>
/// 处理 OpenAI 多模态消息的 Content 字段
/// 支持 string 和 object[] 两种格式
/// </summary>
public class ContentJsonConverter : System.Text.Json.Serialization.JsonConverter<object>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString() ?? string.Empty;
        }

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
        if (value is string str)
        {
            writer.WriteStringValue(str);
        }
        else if (value is IEnumerable<object> list)
        {
            writer.WriteStartArray();
            foreach (var item in list)
            {
                JsonSerializer.Serialize(writer, item, options);
            }
            writer.WriteEndArray();
        }
        else
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}

public class OpenAIToolDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public OpenAIFunctionDefinition Function { get; set; } = new();
}

public class OpenAIFunctionDefinition
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
    public OpenAIFunctionCall Function { get; set; } = new();
}

public class OpenAIFunctionCall
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public string Arguments { get; set; } = string.Empty;
}

public class OpenAIChatResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = "chat.completion";

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<OpenAIChoice> Choices { get; set; } = new();

    [JsonPropertyName("usage")]
    public OpenAIUsage? Usage { get; set; }
}

public class OpenAIChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public OpenAIMessage? Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public class OpenAIUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

public class OpenAIStreamChunk
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("object")]
    public string? Object { get; set; }

    [JsonPropertyName("created")]
    public long? Created { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

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

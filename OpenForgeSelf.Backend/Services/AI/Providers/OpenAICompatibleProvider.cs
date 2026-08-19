using System.Runtime.CompilerServices;
using OpenForgeSelf.Abstractions;
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

    /// <summary>
    /// 暴露底层提供方配置，供需要直连上游（如 Agent Framework 测试接口）的场景读取 Endpoint/ApiKey。
    /// </summary>
    public AIProviderConfig Config => _config;

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

            XTrace.Log.Info($"[{ProviderName}] 发送 Chat 请求，模型: {request.Model}, 消息数: {request.Messages.Count}");

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
            XTrace.Log.Error($"[{ProviderName}] Chat 请求失败: {ex.Message}");
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
        var endpoint = GetChatEndpointByProviderType(_config.ProviderType, _config.Endpoint);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Content = content;
        httpRequest.Headers.Add("Authorization", $"Bearer {_config.ApiKey}");

        XTrace.Log.Info($"[{ProviderName}] 发送流式 Chat 请求，模型: {request.Model}");

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
                XTrace.Log.Warn($"[{ProviderName}] 解析流式响应失败: {ex.Message}");
                continue;
            }

            if (chunk?.Choices == null || chunk.Choices.Count == 0)
            {
                // OpenAI 在 stream_options.include_usage=true 时，于末尾发送一个 choices 为空、携带 usage 的哨兵分片；
                // 必须捕获 usage，否则已用 token 字段在流式场景下丢失。
                if (chunk?.Usage != null)
                {
                    yield return new UnifiedStreamChunk
                    {
                        Id = chunk.Id ?? id,
                        Model = chunk.Model ?? request.Model,
                        Created = chunk.Created ?? created,
                        ChoiceIndex = 0,
                        Usage = new UnifiedUsage
                        {
                            PromptTokens = chunk.Usage.PromptTokens,
                            CompletionTokens = chunk.Usage.CompletionTokens,
                            TotalTokens = chunk.Usage.TotalTokens
                        }
                    };
                }

                continue;
            }

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

    /// <summary>
    /// 根据 ProviderType 和 Endpoint 推导出 Chat 接口的 URL。
    /// </summary>
    /// <param name="providerType">AI 服务提供商类型</param>
    /// <param name="endpoint">用户配置的基础终结点，可能是完整 URL、带路径的前缀，或仅域名</param>
    /// <returns>推导后的 Chat 接口完整 URL；若无法推导则返回 null</returns>
    private string? GetChatEndpointByProviderType(AIProviderType providerType, string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return null;

        // 1. 先尝试识别是否已经是完整的 chat 接口地址，若是则直接返回（去除尾部斜杠）
        string trimmedEndpoint = endpoint.TrimEnd('/');
        if (IsAlreadyChatEndpoint(providerType, trimmedEndpoint))
            return trimmedEndpoint;

        // 2. 自动补全协议（若无）
        if (!trimmedEndpoint.Contains("://"))
            trimmedEndpoint = "https://" + trimmedEndpoint;

        // 3. 尝试解析为绝对 URI
        if (!Uri.TryCreate(trimmedEndpoint, UriKind.Absolute, out Uri? uri))
            return null;

        // 4. 提取基础部分：协议 + 主机 + 路径（去除尾部斜杠），忽略查询和片段
        string basePath = uri.AbsolutePath.TrimEnd('/');
        string baseUrl = $"{uri.Scheme}://{uri.Authority}{basePath}";

        // 5. 获取当前提供商的路径前缀和后缀
        if (!TryGetProviderPathParts(providerType, out string prefix, out string suffix))
            return null; // 对于无法自动拼接的提供商（如 Azure），返回 null

        // 6. 判断路径是否已包含版本前缀（即 basePath 非空且非根）
        bool hasCustomPath = !string.IsNullOrEmpty(basePath) && basePath != "/";

        // 7. 构造需要追加的完整路径
        string fullPathToAppend;
        if (hasCustomPath)
        {
            // 用户已提供路径（如 /v5），只追加接口后缀
            fullPathToAppend = suffix;
        }
        else
        {
            // 用户未提供路径，追加标准前缀和后缀
            fullPathToAppend = prefix + suffix;
        }

        // 8. 组合最终 URL，避免重复添加
        if (baseUrl.EndsWith(fullPathToAppend, StringComparison.OrdinalIgnoreCase))
            return baseUrl;

        // 注意：fullPathToAppend 以 '/' 开头，直接拼接即可
        return baseUrl + fullPathToAppend;
    }

    /// <summary>
    /// 检查当前终结点是否已经是对应提供商的完整 chat 路径。
    /// </summary>
    private bool IsAlreadyChatEndpoint(AIProviderType providerType, string endpoint)
    {
        return providerType switch
        {
            AIProviderType.OpenAI => endpoint.Contains("/chat/completions", StringComparison.OrdinalIgnoreCase),
            AIProviderType.Anthropic => endpoint.Contains("/messages", StringComparison.OrdinalIgnoreCase),
            // 可扩展其他提供商（如 Ollama、Google 等）
            _ => false
        };
    }

    /// <summary>
    /// 获取提供商的路径前缀和接口后缀。
    /// </summary>
    /// <returns>成功获取返回 true，无法自动拼接（如缺少必要参数）返回 false</returns>
    private bool TryGetProviderPathParts(AIProviderType providerType, out string prefix, out string suffix)
    {
        switch (providerType)
        {
            case AIProviderType.OpenAI:
                prefix = "/v1";
                suffix = "/chat/completions";
                return true;

            case AIProviderType.Anthropic:
                prefix = "/v1";
                suffix = "/messages";
                return true;

            // 其他提供商可继续添加：
            // case AIProviderType.Ollama:
            //     prefix = "/api";
            //     suffix = "/chat";
            //     return true;

            // case AIProviderType.AzureOpenAI:
            //     // Azure OpenAI 需要部署名和 API 版本，无法仅靠 endpoint 自动拼接，需由调用方提供完整 URL
            //     prefix = null;
            //     suffix = null;
            //     return false;

            default:
                throw new NotImplementedException($"不支持的 AIProviderType: {providerType}");
        }
    }

    /// <summary>
    /// 拉取上游模型列表：OpenAI/Custom 走 GET {Endpoint}/models（Bearer 认证）；
    /// Anthropic 走 GET https://api.anthropic.com/v1/models（x-api-key + anthropic-version 认证）。
    /// 解析 data[].id / owned_by 映射为 ModelInfo。失败抛异常由调用方转译为错误响应。
    /// </summary>
    public async Task<List<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return _config.ProviderType switch
            {
                AIProviderType.Anthropic => await FetchAnthropicModelsAsync(cancellationToken),
                _ => await FetchOpenAIModelsAsync(cancellationToken)
            };
        }
        catch (Exception ex)
        {
            XTrace.Log.Error($"[{ProviderName}] 拉取上游模型列表失败: {ex.Message}");
            throw;
        }
    }

    private async Task<List<ModelInfo>> FetchOpenAIModelsAsync(CancellationToken cancellationToken)
    {
        // 从 Endpoint 推导 /models 路径：
        // - "http://localhost:1234/v1/chat/completions" → "http://localhost:1234/v1/models"
        // - "http://localhost:1234/v1"                 → "http://localhost:1234/v1/models"
        // - "https://api.openai.com/v1"               → "https://api.openai.com/v1/models"
        // - 已经是 .../models                          → 原样使用
        var baseUrl = _config.Endpoint.TrimEnd('/');
        var modelsUrl = DeriveModelsUrl(baseUrl);

        XTrace.Log.Info($"[{ProviderName}] 拉取模型列表: Endpoint={_config.Endpoint}, ModelsUrl={modelsUrl}");

        var req = new HttpRequestMessage(HttpMethod.Get, modelsUrl);
        req.Headers.Add("Authorization", $"Bearer {_config.ApiKey}");

        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(
            Math.Min(_config.TimeoutSeconds > 0 ? _config.TimeoutSeconds : 120, 30)));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

        XTrace.Log.Info($"[{ProviderName}] 发送 GET {modelsUrl}");
        var resp = await _httpClient.SendAsync(req, linked.Token);

        XTrace.Log.Info($"[{ProviderName}] 响应状态码: {resp.StatusCode} (代码 {(int)resp.StatusCode})");
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync(linked.Token);
        XTrace.Log.Info($"[{ProviderName}] 响应体长度: {json.Length} 字符");

        var doc = JsonSerializer.Deserialize<OpenAIModelsResponse>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (doc?.Data == null)
        {
            XTrace.Log.Warn($"[{ProviderName}] 上游 /models 返回 data 为空");
            return new List<ModelInfo>();
        }

        XTrace.Log.Info($"[{ProviderName}] 拉取到 {doc.Data.Count} 个模型");
        return doc.Data.Select(d => new ModelInfo
        {
            Id = d.Id,
            Name = d.Id,
            Owner = d.OwnedBy ?? ProviderName,
            Created = d.Created,
            ProviderName = ProviderName,
            SupportsStreaming = true
        }).ToList();
    }

    /// <summary>从 Endpoint 推导 /models 接口 URL</summary>
    private static string DeriveModelsUrl(string endpoint)
    {
        // 去掉末尾 /
        var url = endpoint.TrimEnd('/');

        // 如果已经是 .../models，直接返回
        if (url.EndsWith("/models", StringComparison.OrdinalIgnoreCase))
            return url;

        // 去掉 /chat/completions 尾缀（常见写法 http://x/v1/chat/completions）
        if (url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            url = url[..^"/chat/completions".Length];

        // 去掉 /v1 尾缀（统一续加 /v1/models）
        if (url.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            return url + "/models";
        // 没有 /v1 尾缀，直接追加 /v1/models（兼容上游差异）
        return url.EndsWith("/v1/models", StringComparison.OrdinalIgnoreCase)
            ? url
            : $"{url}/v1/models";
    }

    private async Task<List<ModelInfo>> FetchAnthropicModelsAsync(CancellationToken cancellationToken)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/v1/models");
        req.Headers.Add("x-api-key", _config.ApiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");

        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(
            Math.Min(_config.TimeoutSeconds > 0 ? _config.TimeoutSeconds : 120, 30)));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

        var resp = await _httpClient.SendAsync(req, linked.Token);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync(linked.Token);
        var doc = JsonSerializer.Deserialize<AnthropicModelsResponse>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (doc?.Data == null) return new List<ModelInfo>();

        return doc.Data.Select(d => new ModelInfo
        {
            Id = d.Id,
            Name = d.Id,
            Owner = d.OwnedBy ?? ProviderName,
            Created = 0,
            ProviderName = ProviderName,
            SupportsStreaming = true
        }).ToList();
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
            Stream = request.Stream,
            // 流式场景强制请求 usage，确保网关始终能回填「已用 token」字段；
            // 非流式沿用客户端透传的 stream_options。
            StreamOptions = request.Stream
                ? new OpenAIStreamOptions { IncludeUsage = true }
                : (request.StreamOptions == null
                    ? null
                    : new OpenAIStreamOptions { IncludeUsage = request.StreamOptions.IncludeUsage })
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
                    { "image_url", new Dictionary<string, object> { ["url"] = block.ImageUrl } }
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

/// <summary>OpenAI 兼容 /models 接口响应</summary>
public class OpenAIModelsResponse
{
    [JsonPropertyName("object")]
    public string? Object { get; set; }

    [JsonPropertyName("data")]
    public List<OpenAIModelItem>? Data { get; set; }
}

public class OpenAIModelItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string? Object { get; set; }

    [JsonPropertyName("owned_by")]
    public string? OwnedBy { get; set; }

    [JsonPropertyName("created")]
    public long Created { get; set; }
}

/// <summary>Anthropic /v1/models 接口响应</summary>
public class AnthropicModelsResponse
{
    [JsonPropertyName("data")]
    public List<AnthropicModelItem>? Data { get; set; }
}

public class AnthropicModelItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("owned_by")]
    public string? OwnedBy { get; set; }
}

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

    [JsonPropertyName("stream_options")]
    public OpenAIStreamOptions? StreamOptions { get; set; }

    [JsonPropertyName("tools")]
    public List<OpenAIToolDefinition>? Tools { get; set; }

    [JsonPropertyName("tool_choice")]
    public string? ToolChoice { get; set; }
}

public class OpenAIStreamOptions
{
    [JsonPropertyName("include_usage")]
    public bool IncludeUsage { get; set; }
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

    /// <summary>
    /// 流式用量（已用 token）。OpenAI 在 stream_options.include_usage=true 时，于末尾发送一个 choices 为空、携带 usage 的哨兵分片。
    /// </summary>
    [JsonPropertyName("usage")]
    public OpenAIUsage? Usage { get; set; }
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

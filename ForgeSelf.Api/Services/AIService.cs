using System.Runtime.CompilerServices;
using ForgeSelf.Abstractions;
using System.Text;
using System.Text.Json;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Services.AI;
using ForgeSelf.Api.Services.AI.Models;
// 宿主旧版 AI 消息模型与 Abstractions.AIChatMessage 同名，用别名消除 CS0104 歧义。
using LegacyAIChatMessage = ForgeSelf.Api.Models.AIChatMessage;

namespace ForgeSelf.Api.Services;

/// <summary>
/// AI代理服务接口
/// </summary>
public interface IAIService
{
    /// <summary>
    /// 发送聊天消息并获取响应
    /// </summary>
    Task<string> ChatAsync(List<LegacyAIChatMessage> messages);

    /// <summary>
    /// 发送聊天消息并获取响应（按 chatModelId 路由到对应提供方；未命中回退默认配置）
    /// </summary>
    Task<string> ChatAsync(List<LegacyAIChatMessage> messages, string? chatModelId);

    /// <summary>
    /// 发送聊天消息并获取流式响应
    /// </summary>
    IAsyncEnumerable<string> ChatStreamAsync(List<LegacyAIChatMessage> messages, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送聊天消息并获取流式响应（按 chatModelId 路由到对应提供方；未命中回退默认配置）
    /// </summary>
    IAsyncEnumerable<string> ChatStreamAsync(List<LegacyAIChatMessage> messages, string? chatModelId, CancellationToken cancellationToken = default);
}

/// <summary>
/// AI代理服务实现 - 使用HttpClient直接调用AI API
/// </summary>
public class AIService : IAIService
{
    private readonly IConfigurationService _configService;
    private readonly ILogService _logService;
    private readonly HttpClient _httpClient;
    private readonly AIConfig _aiConfig;
    private readonly AIProviderRegistry? _providerRegistry;

    /// <summary>
    /// 构造函数
    /// </summary>
    public AIService(IConfigurationService configService, ILogService logService, HttpClient httpClient, AIProviderRegistry? providerRegistry = null)
    {
        _configService = configService;
        _logService = logService;
        _httpClient = httpClient;
        _aiConfig = _configService.GetAIConfig();
        _providerRegistry = providerRegistry;

        // 配置HttpClient
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// 发送聊天消息并获取响应（按 chatModelId 路由）
    /// </summary>
    public async Task<string> ChatAsync(List<LegacyAIChatMessage> messages, string? chatModelId)
    {
        if (!string.IsNullOrWhiteSpace(chatModelId) && _providerRegistry != null)
        {
            var provider = _providerRegistry.GetProviderByChatModelId(chatModelId);
            if (provider == null)
            {
                _logService.Warn("chatModelId {0} 未匹配到可用提供方，回退默认 AI 配置", chatModelId);
            }
            else
            {
                var unified = ToUnifiedRequest(messages, chatModelId, stream: false);
                var response = await provider.ChatAsync(unified);
                var content = response.Choices.FirstOrDefault()?.Content;
                if (!string.IsNullOrEmpty(content))
                {
                    _logService.Info("AI响应成功（chatModelId 路由），长度: {0}", content.Length);
                    return content;
                }
                _logService.Warn("chatModelId {0} 路由响应为空，回退默认 AI 配置", chatModelId);
            }
        }

        return await ChatAsync(messages);
    }

    /// <summary>
    /// 发送聊天消息并获取流式响应（按 chatModelId 路由）
    /// </summary>
    public async IAsyncEnumerable<string> ChatStreamAsync(List<LegacyAIChatMessage> messages, string? chatModelId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(chatModelId) && _providerRegistry != null)
        {
            var provider = _providerRegistry.GetProviderByChatModelId(chatModelId);
            if (provider == null)
            {
                _logService.Warn("chatModelId {0} 未匹配到可用提供方，回退默认 AI 配置", chatModelId);
            }
            else
            {
                var unified = ToUnifiedRequest(messages, chatModelId, stream: true);
                await foreach (var chunk in provider.ChatStreamAsync(unified, cancellationToken))
                {
                    if (!string.IsNullOrEmpty(chunk.DeltaContent))
                        yield return chunk.DeltaContent;
                }
                yield break;
            }
        }

        await foreach (var chunk in ChatStreamAsync(messages, cancellationToken))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// 把宿主旧版消息与 chatModelId 转换为统一请求（剥掉 provider: 前缀取上游模型 id）
    /// </summary>
    private static UnifiedChatRequest ToUnifiedRequest(List<LegacyAIChatMessage> messages, string chatModelId, bool stream)
    {
        var idx = chatModelId.IndexOf(':');
        var upstreamModelId = idx > 0 ? chatModelId[(idx + 1)..] : chatModelId;

        return new UnifiedChatRequest
        {
            Model = upstreamModelId,
            Stream = stream,
            Messages = messages.Select(m => new UnifiedChatMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList()
        };
    }

    /// <summary>
    /// 发送聊天消息并获取响应
    /// </summary>
    public async Task<string> ChatAsync(List<LegacyAIChatMessage> messages)
    {
        try
        {
            var request = new AIChatRequest
            {
                Model = _aiConfig.ModelName,
                Messages = messages,
                Stream = false
            };

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var jsonContent = JsonSerializer.Serialize(request, jsonOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, _aiConfig.ApiEndpoint);
            httpRequest.Content = content;
            httpRequest.Headers.Add("Authorization", $"Bearer {_aiConfig.ApiKey}");

            _logService.Info("发送AI请求: {0}", _aiConfig.ApiEndpoint);
            _logService.Debug("请求体: {0}", jsonContent);

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response = await _httpClient.SendAsync(httpRequest, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logService.Error("AI请求失败，状态码: {0}, 响应内容: {1}", (int)response.StatusCode, errorContent);
                return GetFallbackResponse(messages);
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var aiResponse = JsonSerializer.Deserialize<AIChatResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (aiResponse?.Choices?.Count > 0)
            {
                var message = aiResponse.Choices[0].Message;
                var result = !string.IsNullOrEmpty(message?.Content)
                    ? message.Content
                    : !string.IsNullOrEmpty(message?.ReasoningContent)
                        ? message.ReasoningContent
                        : string.Empty;
                _logService.Info("AI响应成功，长度: {0}", result.Length);
                return result;
            }

            _logService.Warn("AI响应为空");
            return GetFallbackResponse(messages);
        }
        catch (Exception ex)
        {
            _logService.Error("AI请求失败，使用降级响应: {0}", ex.Message);
            return GetFallbackResponse(messages);
        }
    }

    private string GetFallbackResponse(List<LegacyAIChatMessage> messages)
    {
        var lastMessage = messages.LastOrDefault()?.Content ?? "你好";
        return $"这是一个模拟的AI回复。你说的是：「{lastMessage}」。当前AI服务未连接，这是降级响应。";
    }

    /// <summary>
    /// 发送聊天消息并获取流式响应
    /// </summary>
    public async IAsyncEnumerable<string> ChatStreamAsync(List<LegacyAIChatMessage> messages, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new AIChatRequest
        {
            Model = _aiConfig.ModelName,
            Messages = messages,
            Stream = true
        };

        var jsonContent = JsonSerializer.Serialize(request);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, _aiConfig.ApiEndpoint);
        httpRequest.Content = content;
        httpRequest.Headers.Add("Authorization", $"Bearer {_aiConfig.ApiKey}");

        _logService.Info("发送AI流式请求: {0}", _aiConfig.ApiEndpoint);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logService.Error("AI流式请求失败: {0}", ex.Message);
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
                    _logService.Info("AI流式响应完成");
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
                    _logService.Warn("解析流式响应失败: {0}", ex.Message);
                    continue;
                }

                if (chunk?.Choices?.Count > 0)
                {
                    var delta = chunk.Choices[0].Delta;
                    if (delta != null)
                    {
                        if (!string.IsNullOrEmpty(delta.Content))
                        {
                            yield return delta.Content;
                        }
                        else if (!string.IsNullOrEmpty(delta.ReasoningContent))
                        {
                            yield return delta.ReasoningContent;
                        }
                    }
                }
            }
        }
    }
}
using System.Runtime.CompilerServices;
using ForgeSelf.Abstractions;
using System.Text;
using System.Text.Json;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Services.AI;
using ForgeSelf.Api.Services.AI.Providers;
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
            // PILOT-074 FR-8（U-2 放行）：回退路径出站切 openai-dotnet SDK（与主链路同一映射层）；
            // reasoning_content 经 ModelReaderWriter 往返提取（探针实证往返保留未知字段）
            var config = new AIProviderConfig
            {
                Name = "AIService回退",
                ProviderType = AIProviderType.OpenAI,
                Endpoint = _aiConfig.ApiEndpoint,
                ApiKey = _aiConfig.ApiKey ?? string.Empty,
                TimeoutSeconds = 10,
            };
            var request = new UnifiedChatRequest
            {
                Model = _aiConfig.ModelName,
                Messages = messages.Select(m => new UnifiedChatMessage
                {
                    Role = m.Role,
                    Content = m.Content
                }).ToList()
            };

            _logService.Info("发送AI请求: {0}", _aiConfig.ApiEndpoint);

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var unified = await OpenAISdkAdapter.ChatAsync(
                config, request, _httpClient, config.Name, cts.Token, reasoningAsContentFallback: true);

            var choice = unified.Choices.FirstOrDefault();
            if (choice == null)
            {
                _logService.Warn("AI响应为空");
                return GetFallbackResponse(messages);
            }

            var result = !string.IsNullOrEmpty(choice.Content)
                ? choice.Content
                : !string.IsNullOrEmpty(choice.ReasoningContent)
                    ? choice.ReasoningContent
                    : string.Empty;
            _logService.Info("AI响应成功，长度: {0}", result.Length);
            return result;
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
        // PILOT-074 FR-8（U-2 放行）：流式回退出站切 SDK；reasoning_content 增量经往返提取并以正文回放（对齐旧回退语义）
        var config = new AIProviderConfig
        {
            Name = "AIService回退",
            ProviderType = AIProviderType.OpenAI,
            Endpoint = _aiConfig.ApiEndpoint,
            ApiKey = _aiConfig.ApiKey ?? string.Empty,
            TimeoutSeconds = 10,
        };
        var request = new UnifiedChatRequest
        {
            Model = _aiConfig.ModelName,
            Stream = true,
            Messages = messages.Select(m => new UnifiedChatMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList()
        };

        _logService.Info("发送AI流式请求: {0}", _aiConfig.ApiEndpoint);

        var source = OpenAISdkAdapter.ChatStreamAsync(
            config, request, _httpClient, config.Name, cancellationToken, reasoningAsContentFallback: true);
        var enumerator = source.GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = await enumerator.MoveNextAsync();
                }
                catch (Exception ex)
                {
                    // 旧语义：流式失败仅记录并静默结束（SSE 已出流，不能中断为 500）
                    _logService.Error("AI流式请求失败: {0}", ex.Message);
                    yield break;
                }

                if (!hasNext)
                    yield break;

                var chunk = enumerator.Current;
                if (!string.IsNullOrEmpty(chunk.DeltaContent))
                {
                    yield return chunk.DeltaContent;
                }

                if (chunk.IsDone)
                {
                    _logService.Info("AI流式响应完成");
                    yield break;
                }
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
        }
    }
}
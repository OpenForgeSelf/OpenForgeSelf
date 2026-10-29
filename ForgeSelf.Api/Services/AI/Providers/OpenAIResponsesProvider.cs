using System.Runtime.CompilerServices;
using System.Text.Json;
using ForgeSelf.Abstractions;
using NewLife.Log;

namespace ForgeSelf.Api.Services.AI.Providers;

/// <summary>
/// PILOT-074：OpenAI Responses 出站提供方（新格式，POST {base}/v1/responses）。
/// 协议细节经 <see cref="OpenAISdkAdapter"/> 交 openai-dotnet 官方 SDK（PC-008）；
/// 模型列表沿用 OpenAI 同源 GET /v1/models（FR-4 兜底，SDK models 面与第三方兼容性未定，见 U-4）。
/// </summary>
public class OpenAIResponsesProvider : IAIProvider
{
    private readonly AIProviderConfig _config;
    private readonly HttpClient _httpClient;

    public string ProviderName => _config.Name;
    public AIProviderType ProviderType => _config.ProviderType;
    public List<string> SupportedModels => _config.SupportedModels;
    public bool IsDefault => _config.IsDefault;

    /// <summary>暴露底层提供方配置（与 OpenAICompatibleProvider 一致）。</summary>
    public AIProviderConfig Config => _config;

    public OpenAIResponsesProvider(AIProviderConfig config, HttpClient httpClient)
    {
        _config = config;
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
    }

    public async Task<UnifiedChatResponse> ChatAsync(UnifiedChatRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await OpenAISdkAdapter.ResponsesChatAsync(_config, request, _httpClient, ProviderName, cancellationToken);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error($"[{ProviderName}] Responses Chat 请求失败: {ex.Message}");
            throw;
        }
    }

    public async IAsyncEnumerable<UnifiedStreamChunk> ChatStreamAsync(UnifiedChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var chunk in OpenAISdkAdapter.ResponsesStreamAsync(_config, request, _httpClient, ProviderName, cancellationToken))
        {
            yield return chunk;
        }
    }

    public async Task<List<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = _config.Endpoint.TrimEnd('/');
            var url = endpoint.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
                ? endpoint + "/models"
                : endpoint + "/v1/models";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("Authorization", $"Bearer {_config.ApiKey}");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_config.TimeoutSeconds > 0 ? _config.TimeoutSeconds : 120));

            var resp = await _httpClient.SendAsync(req, cts.Token);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync(cts.Token);
            var doc = JsonSerializer.Deserialize<OpenAIModelsResponse>(json,
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
        catch (Exception ex)
        {
            XTrace.Log.Error($"[{ProviderName}] 获取模型列表失败: {ex.Message}");
            throw;
        }
    }
}

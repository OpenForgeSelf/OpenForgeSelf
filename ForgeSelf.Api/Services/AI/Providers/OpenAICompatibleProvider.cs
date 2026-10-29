using System.Runtime.CompilerServices;
using ForgeSelf.Abstractions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NewLife.Log;

namespace ForgeSelf.Api.Services.AI.Providers;

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
        // PILOT-074 FR-5 + 输入6（2026-10-28 拍板撤销 BR-5）：全类型出站统一 openai-dotnet SDK；
        // Anthropic 类型经官方 OpenAI 兼容层（DeriveAnthropicCompatBaseUrl，Bearer/max_completion_tokens/stream/tools 均支持）
        return await OpenAISdkAdapter.ChatAsync(_config, request, _httpClient, ProviderName, cancellationToken);
    }

    public async IAsyncEnumerable<UnifiedStreamChunk> ChatStreamAsync(UnifiedChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // PILOT-074 FR-5 + 输入6：全类型流式出站统一 SDK（delta 逐块保真）
        await foreach (var chunk in OpenAISdkAdapter.ChatStreamAsync(_config, request, _httpClient, ProviderName, cancellationToken))
            yield return chunk;
    }
    /// <summary>
    /// 根据 ProviderType 和 Endpoint 推导出 Chat 接口的 URL。
    /// </summary>
    /// <param name="providerType">AI 服务提供商类型</param>
    /// <param name="endpoint">用户配置的基础终结点，可能是完整 URL、带路径的前缀，或仅域名</param>
    /// <returns>推导后的 Chat 接口完整 URL；若无法推导则返回 null</returns>
    internal static string? GetChatEndpointByProviderType(AIProviderType providerType, string endpoint)
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
    private static bool IsAlreadyChatEndpoint(AIProviderType providerType, string endpoint)
    {
        return providerType switch
        {
            AIProviderType.OpenAI => endpoint.Contains("/chat/completions", StringComparison.OrdinalIgnoreCase),
            AIProviderType.Anthropic => endpoint.Contains("/messages", StringComparison.OrdinalIgnoreCase),
            // BC-4（PILOT-074）：Custom 视同 OpenAI 兼容（既有实现对 Custom 恒返回 false，导致流式推导必抛）
            AIProviderType.Custom => endpoint.Contains("/chat/completions", StringComparison.OrdinalIgnoreCase),
            // 可扩展其他提供商（如 Ollama、Google 等）
            _ => false
        };
    }

    /// <summary>
    /// 获取提供商的路径前缀和接口后缀。
    /// </summary>
    /// <returns>成功获取返回 true，无法自动拼接（如缺少必要参数）返回 false</returns>
    private static bool TryGetProviderPathParts(AIProviderType providerType, out string prefix, out string suffix)
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

            case AIProviderType.Custom: // BC-4（PILOT-074）：Custom 视 OpenAI 兼容既有实现走 default 抛 NotImplementedException）
                prefix = "/v1";
                suffix = "/chat/completions";
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
#endregion

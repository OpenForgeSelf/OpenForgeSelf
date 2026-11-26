using System.Net.Http.Headers;
using System.Text;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Services.AI.Providers;

namespace ForgeSelf.Api.Services.AI.Proxy;

/// <summary>
/// 用提供方已经配好的 HttpClient 把 JSON 原文 POST 到上游。
/// 不走 <c>CompleteChatAsync</c>：那个封闭模型会丢掉未知字段。
/// </summary>
internal static class OpenAIRawForwarder
{
    private static readonly HashSet<string> ForbiddenHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization", "Cookie", "Set-Cookie", "Host", "Connection",
        "Content-Length", "Transfer-Encoding", "Keep-Alive", "Proxy-Authorization",
        "TE", "Trailer", "Upgrade", "Expect"
    };

    public static async Task<HttpResponseMessage> PostAsync(
        IAIProvider provider,
        LlmWireProtocol outboundProtocol,
        string json,
        bool stream,
        IReadOnlyDictionary<string, string>? extraHeaders,
        CancellationToken cancellationToken)
    {
        var (http, config) = Transport(provider);
        var uri = OpenAISdkAdapter.GetWireUri(config, outboundProtocol);
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", OpenAISdkAdapter.ResolveApiKey(config));
        if (stream)
            request.Headers.Accept.ParseAdd("text/event-stream");
        CopySafeHeaders(request, extraHeaders);

        return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    internal static string EndpointOf(IAIProvider provider) => provider switch
    {
        OpenAICompatibleProvider chat => chat.Config.Endpoint,
        OpenAIResponsesProvider responses => responses.Config.Endpoint,
        _ => string.Empty
    };

    private static void CopySafeHeaders(HttpRequestMessage request, IReadOnlyDictionary<string, string>? extraHeaders)
    {
        if (extraHeaders == null)
            return;
        foreach (var pair in extraHeaders)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || ForbiddenHeaders.Contains(pair.Key))
                continue;
            request.Headers.TryAddWithoutValidation(pair.Key, pair.Value);
        }
    }

    private static (HttpClient Http, AIProviderConfig Config) Transport(IAIProvider provider) => provider switch
    {
        OpenAICompatibleProvider chat => (chat.Http, chat.Config),
        OpenAIResponsesProvider responses => (responses.Http, responses.Config),
        _ => throw new InvalidOperationException($"提供方 {provider.ProviderName} 没有可转发的 HTTP 传输")
    };
}

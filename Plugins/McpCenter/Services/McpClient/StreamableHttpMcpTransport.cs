using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.McpCenter.Services.McpClient;

/// <summary>
/// 标准 Streamable HTTP 传输（协议版本 2025-03-26 起，2025-06-18 定为标准）：
/// 单 endpoint，POST /mcp 发 JSON-RPC（响应为 JSON 或 SSE 单条 message），GET /mcp 后台 SSE 流保持连接。
/// 实现策略（v2.5.0）：以 POST 请求-响应为主；initialize 后发 notifications/initialized 通知并尝试 GET 流，
/// GET 不被支持（405/404）则降级纯请求-响应；服务器返回 Mcp-Session-Id 时会话复用（后续 POST/GET 回传
/// 会话头与协商协议版本），无会话头的无状态服务器（如 DeepWiki）行为不变。
/// </summary>
public sealed class StreamableHttpMcpTransport : IMcpClientTransport
{
    /// <summary>无参构造兜底超时（v2.5.2）：真源是 McpGatewayConfig.DefaultExecutionTimeoutMs；生产链路由 McpClientManager 注入网关配置值。</summary>
    public const int DefaultRequestTimeoutMs = McpGatewayConfig.DefaultExecutionTimeoutMs;

    private static readonly string[] AllowedSchemes = { "http", "https" };

    private readonly HttpClient _http;
    private readonly string _url;
    private readonly int _requestTimeoutMs;
    private readonly Dictionary<string, string> _headers;
    private readonly CancellationTokenSource _runCts = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pending = new();
    private Task? _sseTask;
    private bool _getSupported = true;
    private bool _closed;
    private int _nextId;
    private string? _sessionId;
    private string _negotiatedVersion = string.Empty;

    public StreamableHttpMcpTransport(string url, Dictionary<string, string>? headers, HttpMessageHandler? handler = null, int requestTimeoutMs = DefaultRequestTimeoutMs)
    {
        var uri = ValidateUrl(url);
        _url = uri.AbsoluteUri;
        _headers = headers ?? new();
        _requestTimeoutMs = requestTimeoutMs > 0 ? requestTimeoutMs : DefaultRequestTimeoutMs;
        _http = handler != null
            ? new HttpClient(handler)
            : new HttpClient();
        // v2.5.2 修单位 bug：原 FromSeconds(毫秒值) 把 30000ms 当 30000 秒（默认提到 300000ms 后会变成 83 小时）；
        // HttpClient 超时与请求取消预算统一取毫秒。GET 长流仅影到响应头为止（ResponseHeadersRead），不伤后台 SSE。
        _http.Timeout = TimeSpan.FromMilliseconds(_requestTimeoutMs);
    }

    /// <summary>实际生效的执行超时与 HttpClient 超时（internal 供单测：断言配置贯穿与 FromMilliseconds 单位）。</summary>
    internal int EffectiveRequestTimeoutMs => _requestTimeoutMs;
    internal TimeSpan HttpTimeout => _http.Timeout;

    private static Uri ValidateUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new McpClientException($"外部 MCP 地址不合法: {url}");
        if (!AllowedSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase))
            throw new McpClientException($"外部 MCP 地址仅支持 http/https: {url}");
        return uri;
    }

    public string Describe() => $"streamable-http[{_url}]";

    public async Task<JsonElement> InitializeAsync(IReadOnlyList<string> protocolVersions, CancellationToken ct)
    {
        // clientInfo 为规范必填（缺失即 -32602，DeepWiki 实测）：载荷经 McpClientProtocol 单点构造
        var result = await RequestAsync("initialize",
            McpClientProtocol.BuildInitializeParams(protocolVersions[0]), ct);
        _negotiatedVersion = result.TryGetProperty("protocolVersion", out var pv) && pv.ValueKind == JsonValueKind.String
            ? pv.GetString() ?? string.Empty
            : protocolVersions[0];
        // 规范要求收到 initialize result 后客户端 MUST 发 notifications/initialized；
        // 尽力而为：失败记日志不阻断（宽松服务器忽略即可，严格服务器会在后续调用暴露问题）
        try
        {
            await NotifyAsync(McpClientProtocol.InitializedNotificationMethod, ct);
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[McpClient] notifications/initialized 发送失败（不阻断连接）: {0}", ex.Message);
        }
        // 握手成功后启动 GET SSE 流（保持连接 + 接收服务器通知；listChanged=false，消息仅记日志）
        StartSseStream(ct);
        return result;
    }

    public Task<JsonElement> ListToolsAsync(CancellationToken ct) =>
        RequestAsync("tools/list", null, ct);

    public Task<JsonElement> CallToolAsync(string name, string argumentsJson, CancellationToken ct)
    {
        object? args = null;
        if (!string.IsNullOrWhiteSpace(argumentsJson))
        {
            try { args = JsonDocument.Parse(argumentsJson).RootElement.Clone(); }
            catch (JsonException) { args = new { }; }
        }
        return RequestAsync("tools/call", new { name, arguments = args ?? new { } }, ct); // 空参数给 {} 而非 null（PC-010 同族）
    }

    public Task<JsonElement> PingAsync(CancellationToken ct) => RequestAsync("ping", null, ct);

    public async Task CloseAsync()
    {
        if (_closed) return;
        _closed = true;
        _runCts.Cancel();
        if (_sseTask != null)
        {
            try { await _sseTask.WaitAsync(TimeSpan.FromSeconds(3)); }
            catch (Exception) { }
        }
        foreach (var kv in _pending)
        {
            if (_pending.TryRemove(kv.Key, out var tcs)) tcs.TrySetException(new McpClientException("外部 MCP 连接已关闭"));
        }
        try { _http.Dispose(); } catch (Exception) { }
    }

    private async Task<JsonElement> RequestAsync(string method, object? prms, CancellationToken ct)
    {
        var id = Interlocked.Increment(ref _nextId);
        var idKey = id.ToString();
        var payload = McpClientProtocol.SerializeRequest(id, method, prms); // null params 省略键（PC-010 canonical 单点）
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[idKey] = tcs;

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, _url);
            req.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            req.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
            foreach (var kv in _headers) req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            ApplyProtocolHeaders(req);
            req.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _runCts.Token);
            linked.CancelAfter(_requestTimeoutMs);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, linked.Token);
            CaptureSessionId(resp);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(linked.Token);
                throw new McpClientException($"外部 MCP HTTP {(int)resp.StatusCode}: {Truncate(body)}");
            }

            var contentType = resp.Content.Headers.ContentType?.MediaType ?? "application/json";
            if (contentType.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase))
            {
                await using var stream = await resp.Content.ReadAsStreamAsync(linked.Token);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                while (true)
                {
                    var evt = await SseStreamReader.ReadNextEventAsync(reader, linked.Token);
                    if (evt == null) break;
                    if (evt.Value.Event == "message" && !string.IsNullOrWhiteSpace(evt.Value.Data))
                    {
                        var respJson = evt.Value.Data;
                        var idMatch = ExtractResponseId(respJson);
                        if (idMatch == idKey)
                        {
                            tcs.TrySetResult(respJson);
                            break;
                        }
                        // 其他 id（通知/过期响应）忽略
                    }
                }
            }
            else
            {
                var respJson = await resp.Content.ReadAsStringAsync(linked.Token);
                tcs.TrySetResult(respJson);
            }

            var resultJson = await tcs.Task.WaitAsync(linked.Token);
            return ExtractResult(resultJson, method);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new McpClientException($"外部 MCP 请求超时（{_requestTimeoutMs / 1000}s）：{method}");
        }
        finally
        {
            _pending.TryRemove(idKey, out _);
        }
    }

    /// <summary>JSON-RPC 通知（无 id、不等响应体，只看 HTTP 状态）。v2.5.0：initialized 通知用。</summary>
    private async Task NotifyAsync(string method, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, _url);
        req.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        foreach (var kv in _headers) req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
        ApplyProtocolHeaders(req);
        req.Content = new StringContent(JsonSerializer.Serialize(new { jsonrpc = "2.0", method }), Encoding.UTF8, "application/json");

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _runCts.Token);
        linked.CancelAfter(_requestTimeoutMs);
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, linked.Token);
        CaptureSessionId(resp);
        if (!resp.IsSuccessStatusCode)
            throw new McpClientException($"外部 MCP 通知 {method} HTTP {(int)resp.StatusCode}");
    }

    /// <summary>会话建立后（initialize 协商完成）后续请求携带协议版本头；有会话头则一并携带。</summary>
    private void ApplyProtocolHeaders(HttpRequestMessage req)
    {
        if (_sessionId != null) req.Headers.TryAddWithoutValidation(McpClientProtocol.SessionIdHeader, _sessionId);
        if (_negotiatedVersion.Length > 0)
            req.Headers.TryAddWithoutValidation(McpClientProtocol.ProtocolVersionHeader, _negotiatedVersion);
    }

    /// <summary>服务器返回 Mcp-Session-Id 响应头时记住会话（无状态服务器不返回，保持 null）。</summary>
    private void CaptureSessionId(HttpResponseMessage resp)
    {
        if (resp.Headers.TryGetValues(McpClientProtocol.SessionIdHeader, out var values))
            _sessionId = values.FirstOrDefault() ?? _sessionId;
    }

    private static string? ExtractResponseId(string respJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(respJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("id", out var idEl) && idEl.ValueKind != JsonValueKind.Null)
            {
                return idEl.ValueKind == JsonValueKind.String ? idEl.GetString() : idEl.GetRawText();
            }
        }
        catch (JsonException) { }
        return null;
    }

    private void StartSseStream(CancellationToken ct)
    {
        if (_sseTask != null) return;
        _sseTask = Task.Run(async () =>
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, _url);
                req.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
                foreach (var kv in _headers) req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                ApplyProtocolHeaders(req);
                using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, _runCts.Token);
                if (!resp.IsSuccessStatusCode)
                {
                    // GET 流不被支持（405/404 等）→ 降级纯 POST 请求-响应
                    _getSupported = false;
                    return;
                }
                await using var stream = await resp.Content.ReadAsStreamAsync(_runCts.Token);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                while (!_runCts.IsCancellationRequested)
                {
                    var evt = await SseStreamReader.ReadNextEventAsync(reader, _runCts.Token);
                    if (evt == null) break;
                    if (evt.Value.Event == "message" && !string.IsNullOrWhiteSpace(evt.Value.Data))
                    {
                        XTrace.Log.Debug("[McpClient] GET 流收到服务器消息: {0}", Truncate(evt.Value.Data, 200));
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                XTrace.Log.Debug("[McpClient] GET SSE 流退出（降级纯请求-响应）: {0}", ex.Message);
                _getSupported = false;
            }
        });
    }

    private static JsonElement ExtractResult(string respJson, string method)
    {
        using var doc = JsonDocument.Parse(respJson);
        var root = doc.RootElement;
        if (root.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.Object)
        {
            var code = err.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : 0;
            var msg = err.TryGetProperty("message", out var m) ? m.GetString() : "未知错误";
            throw new McpClientException($"外部 MCP 返回错误 [{code}]（{method}）: {msg}");
        }
        if (!root.TryGetProperty("result", out var result) || result.ValueKind == JsonValueKind.Undefined)
        {
            throw new McpClientException($"外部 MCP 响应缺少 result（{method}）");
        }
        return result.Clone();
    }

    private static string Truncate(string s, int max = 300) =>
        string.IsNullOrEmpty(s) ? s : s.Length <= max ? s : s[..max] + "...";
}

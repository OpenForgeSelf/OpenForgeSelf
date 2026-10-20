using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.McpCenter.Services.McpClient;

/// <summary>
/// 旧版 HTTP+SSE 传输（协议版本 2024-11-05，双端点）：
/// ① GET {sseUrl}（如 /sse）建立服务器事件流：`endpoint` 事件给出后续 JSON-RPC 的 POST 目标 URL；
/// ② POST {endpointUrl} 发送 JSON-RPC 请求；**响应经 SSE 流以 message 事件回传**（POST 通常 202 空体）。
/// 本实现：GET 流后台常驻，响应按 JSON-RPC id 匹配挂起请求；endpoint 发现超时兜底。
/// </summary>
public sealed class LegacySseMcpTransport : IMcpClientTransport
{
    /// <summary>endpoint 发现超时：连接建立阶段固定预算（PC-010 lens），不随执行超时配置变动。</summary>
    public const int EndpointDiscoverTimeoutMs = 15000;

    /// <summary>无参构造兜底超时（v2.5.2）：真源是 McpGatewayConfig.DefaultExecutionTimeoutMs；生产链路由 McpClientManager 注入网关配置值。</summary>
    public const int DefaultRequestTimeoutMs = McpGatewayConfig.DefaultExecutionTimeoutMs;

    private static readonly string[] AllowedSchemes = { "http", "https" };

    private readonly HttpClient _http;
    private readonly string _sseUrl;
    private readonly int _requestTimeoutMs;
    private readonly Dictionary<string, string> _headers;
    private readonly CancellationTokenSource _runCts = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pending = new();
    private readonly SemaphoreSlim _endpointLock = new(1, 1);
    private string? _endpointUrl;
    private Task? _streamTask;
    private bool _closed;
    private int _nextId;

    public LegacySseMcpTransport(string sseUrl, Dictionary<string, string>? headers, HttpMessageHandler? handler = null, int requestTimeoutMs = DefaultRequestTimeoutMs)
    {
        var uri = ValidateUrl(sseUrl);
        _sseUrl = uri.AbsoluteUri;
        _headers = headers ?? new();
        _requestTimeoutMs = requestTimeoutMs > 0 ? requestTimeoutMs : DefaultRequestTimeoutMs;
        _http = handler != null ? new HttpClient(handler) : new HttpClient();
        // v2.5.2 修单位 bug：同 StreamableHttp 传输，原 FromSeconds(毫秒值) 把毫秒当秒；改 FromMilliseconds。
        // GET SSE 长流用 ResponseHeadersRead，HttpClient 超时只约束到响应头，不伤后台长连接。
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

    public string Describe() => $"http-sse[{_sseUrl}]";

    public async Task<JsonElement> InitializeAsync(IReadOnlyList<string> protocolVersions, CancellationToken ct)
    {
        StartStream(ct);
        // clientInfo 为规范必填（缺失即 -32602，DeepWiki 实测）：载荷经 McpClientProtocol 单点构造
        var result = await RequestAsync("initialize", McpClientProtocol.BuildInitializeParams(protocolVersions[0]), ct);
        // 规范要求收到 initialize result 后客户端 MUST 发 notifications/initialized；尽力而为不阻断
        try
        {
            await NotifyAsync(McpClientProtocol.InitializedNotificationMethod, ct);
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[McpClient] SSE notifications/initialized 发送失败（不阻断连接）: {0}", ex.Message);
        }
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
        if (_streamTask != null)
        {
            try { await _streamTask.WaitAsync(TimeSpan.FromSeconds(3)); }
            catch (Exception) { }
        }
        foreach (var kv in _pending)
        {
            if (_pending.TryRemove(kv.Key, out var tcs)) tcs.TrySetException(new McpClientException("外部 MCP 连接已关闭"));
        }
        try { _http.Dispose(); } catch (Exception) { }
    }

    /// <summary>启动 GET SSE 流（发现 endpoint + 回传响应）。幂等。</summary>
    private void StartStream(CancellationToken ct)
    {
        if (_streamTask != null) return;
        _streamTask = Task.Run(async () =>
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, _sseUrl);
                req.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
                foreach (var kv in _headers) req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, _runCts.Token);
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync(_runCts.Token);
                    throw new McpClientException($"外部 MCP SSE 端点 HTTP {(int)resp.StatusCode}: {Truncate(body)}");
                }
                await using var stream = await resp.Content.ReadAsStreamAsync(_runCts.Token);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                while (!_runCts.IsCancellationRequested)
                {
                    var evt = await SseStreamReader.ReadNextEventAsync(reader, _runCts.Token);
                    if (evt == null) break;
                    var (evtName, data) = evt.Value;
                    if (evtName == "endpoint" && !string.IsNullOrWhiteSpace(data))
                    {
                        var target = data.Trim();
                        // 相对 URL 相对 sseUrl 解析
                        if (!Uri.TryCreate(target, UriKind.Absolute, out var targetUri))
                        {
                            if (Uri.TryCreate(new Uri(_sseUrl), target, out var resolved)) target = resolved.AbsoluteUri;
                        }
                        _endpointUrl = target;
                        XTrace.Log.Info("[McpClient] 旧版 SSE 发现 endpoint: {0}", target);
                    }
                    else if (evtName == "message" && !string.IsNullOrWhiteSpace(data))
                    {
                        DispatchFrame(data);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                XTrace.Log.Warn("[McpClient] SSE 流退出: {0}", ex.Message);
            }
            finally
            {
                FailAllPending(new McpClientException("外部 MCP SSE 流已关闭"));
                _runCts.Cancel();
            }
        });
    }

    private void DispatchFrame(string frame)
    {
        try
        {
            using var doc = JsonDocument.Parse(frame);
            var root = doc.RootElement;
            if (!root.TryGetProperty("id", out var idEl) || idEl.ValueKind == JsonValueKind.Null || idEl.ValueKind == JsonValueKind.Undefined)
                return;
            var idKey = idEl.ValueKind == JsonValueKind.String ? idEl.GetString()! : idEl.GetRawText();
            if (_pending.TryRemove(idKey, out var tcs)) tcs.TrySetResult(frame);
        }
        catch (JsonException) { }
    }

    /// <summary>JSON-RPC 通知（无 id、经 message endpoint POST，不等响应体，只看 HTTP 状态）。v2.5.0：initialized 通知用。</summary>
    private async Task NotifyAsync(string method, CancellationToken ct)
    {
        var endpoint = await ResolveEndpointAsync(ct);
        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
        req.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        foreach (var kv in _headers) req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
        req.Content = new StringContent(JsonSerializer.Serialize(new { jsonrpc = "2.0", method }), Encoding.UTF8, "application/json");

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _runCts.Token);
        linked.CancelAfter(_requestTimeoutMs);
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, linked.Token);
        if (!resp.IsSuccessStatusCode)
            throw new McpClientException($"外部 MCP 通知 {method} HTTP {(int)resp.StatusCode}");
    }

    private async Task<JsonElement> RequestAsync(string method, object? prms, CancellationToken ct)
    {
        // 等待 endpoint 发现
        var endpoint = await ResolveEndpointAsync(ct);
        var id = Interlocked.Increment(ref _nextId);
        var idKey = id.ToString();
        var payload = McpClientProtocol.SerializeRequest(id, method, prms); // null params 省略键（PC-010 canonical 单点）
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[idKey] = tcs;

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
            req.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            foreach (var kv in _headers) req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            req.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _runCts.Token);
            linked.CancelAfter(_requestTimeoutMs);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, linked.Token);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(linked.Token);
                throw new McpClientException($"外部 MCP POST HTTP {(int)resp.StatusCode}: {Truncate(body)}");
            }

            // 兼容两种旧版实现：响应经 SSE 流回传（标准，POST 202 空体）或直接回 SSE body。
            // 若 POST body 本身是 SSE，则读第一条 message 匹配 id。
            var contentType = resp.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (contentType.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase))
            {
                await using var bodyStream = await resp.Content.ReadAsStreamAsync(linked.Token);
                using var bodyReader = new StreamReader(bodyStream, Encoding.UTF8);
                while (true)
                {
                    var evt = await SseStreamReader.ReadNextEventAsync(bodyReader, linked.Token);
                    if (evt == null) break;
                    if (evt.Value.Event == "message" && !string.IsNullOrWhiteSpace(evt.Value.Data))
                    {
                        if (ExtractResponseId(evt.Value.Data) == idKey) tcs.TrySetResult(evt.Value.Data);
                        break;
                    }
                }
            }

            // 标准路径：响应从 SSE 流按 id 匹配（POST 202 空体场景）
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

    private async Task<string> ResolveEndpointAsync(CancellationToken ct)
    {
        if (_endpointUrl != null) return _endpointUrl;
        await _endpointLock.WaitAsync(ct);
        try
        {
            if (_endpointUrl != null) return _endpointUrl;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _runCts.Token);
            linked.CancelAfter(EndpointDiscoverTimeoutMs);
            while (_endpointUrl == null)
            {
                await Task.Delay(50, linked.Token);
            }
            return _endpointUrl!;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new McpClientException($"外部 MCP SSE endpoint 发现超时（{EndpointDiscoverTimeoutMs / 1000}s）");
        }
        finally
        {
            _endpointLock.Release();
        }
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

    private void FailAllPending(Exception ex)
    {
        foreach (var kv in _pending)
        {
            if (_pending.TryRemove(kv.Key, out var tcs)) tcs.TrySetException(ex);
        }
    }

    private static string Truncate(string s, int max = 300) =>
        string.IsNullOrEmpty(s) ? s : s.Length <= max ? s : s[..max] + "...";
}

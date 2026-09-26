using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.McpCenter.Services.McpClient;

/// <summary>
/// 标准 Streamable HTTP 传输（协议版本 2025-03-26 起，2025-06-18 定为标准）：
/// 单 endpoint，POST /mcp 发 JSON-RPC（响应为 JSON 或 SSE 单条 message），GET /mcp 后台 SSE 流保持连接。
/// 实现策略（v2.1.0）：以 POST 请求-响应为主；initialize 后尝试 GET 流，GET 不被支持（405/404）则降级纯请求-响应。
/// 每次请求独立会话（不做会话复用/续传，缓做项）。
/// </summary>
public sealed class StreamableHttpMcpTransport : IMcpClientTransport
{
    public const int RequestTimeoutMs = 30000;

    private static readonly string[] AllowedSchemes = { "http", "https" };

    private readonly HttpClient _http;
    private readonly string _url;
    private readonly Dictionary<string, string> _headers;
    private readonly CancellationTokenSource _runCts = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pending = new();
    private Task? _sseTask;
    private bool _getSupported = true;
    private bool _closed;
    private int _nextId;

    public StreamableHttpMcpTransport(string url, Dictionary<string, string>? headers, HttpMessageHandler? handler = null)
    {
        var uri = ValidateUrl(url);
        _url = uri.AbsoluteUri;
        _headers = headers ?? new();
        _http = handler != null
            ? new HttpClient(handler)
            : new HttpClient();
        _http.Timeout = TimeSpan.FromSeconds(RequestTimeoutMs);
    }

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
        var result = await RequestAsync("initialize",
            new { protocolVersion = protocolVersions[0], capabilities = new { } }, ct);
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
        return RequestAsync("tools/call", new { name, arguments = args }, ct);
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
        var payload = JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method, @params = prms });
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[idKey] = tcs;

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, _url);
            req.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            req.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
            foreach (var kv in _headers) req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            req.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _runCts.Token);
            linked.CancelAfter(RequestTimeoutMs);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, linked.Token);
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
            throw new McpClientException($"外部 MCP 请求超时（{RequestTimeoutMs / 1000}s）：{method}");
        }
        finally
        {
            _pending.TryRemove(idKey, out _);
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

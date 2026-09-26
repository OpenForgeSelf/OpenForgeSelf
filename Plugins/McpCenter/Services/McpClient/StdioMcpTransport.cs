using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ForgeSelf.Api.Plugins.McpCenter.Models;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.McpCenter.Services.McpClient;

/// <summary>SSE（Server-Sent Events）行流解析：读事件直到空行，返回 (事件名, data 全文)。</summary>
internal static class SseStreamReader
{
    /// <summary>
    /// 读取下一个 SSE 事件。事件以空行结束；data 多行以 \n 连接。
    /// 流结束返回 null；取消抛 OperationCanceledException。
    /// </summary>
    public static async Task<(string Event, string Data)?> ReadNextEventAsync(TextReader reader, CancellationToken ct)
    {
        var evt = "message";
        var data = new StringBuilder();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);
            if (line == null)
            {
                return data.Length > 0 ? (evt, data.ToString().TrimEnd('\n')) : null;
            }
            if (line.Length == 0)
            {
                return data.Length > 0 ? (evt, data.ToString().TrimEnd('\n')) : null;
            }
            if (line.StartsWith("event:", StringComparison.Ordinal))
            {
                evt = line["event:".Length..].Trim();
            }
            else if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                var d = line["data:".Length..];
                if (d.StartsWith(' ')) d = d[1..];
                data.Append(d).Append('\n');
            }
            // id: / retry: / 注释行忽略
        }
    }
}

/// <summary>
/// 标准 MCP stdio 传输（v2.1.0）：拉起子进程，JSON-RPC 走 stdin/stdout，Content-Length 帧（LSP 风格，UTF-8）。
/// 单会话串行写（信号量）；响应按 JSON-RPC id 匹配挂起请求；进程退出/超时兜底。
/// </summary>
public sealed class StdioMcpTransport : IMcpClientTransport
{
    /// <summary>stdio 可执行文件白名单（v2.1.0 安全边界）。</summary>
    public static readonly string[] AllowedCommands =
        { "npx", "node", "python", "python3", "uvx", "uv", "dotnet" };

    public const int RequestTimeoutMs = 30000;

    private readonly McpExternalServerConfig _config;
    private readonly Process _process;
    private readonly StreamWriter _stdinWriter;
    private readonly CancellationTokenSource _runCts = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pending = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly Task _readTask;
    private int _nextId;
    private bool _closed;

    public StdioMcpTransport(McpExternalServerConfig config)
    {
        Validate(config);
        _config = config;

        var psi = new ProcessStartInfo
        {
            FileName = config.Command,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var arg in config.Args) psi.ArgumentList.Add(arg);
        foreach (var kv in config.Env) psi.Environment[kv.Key] = kv.Value;

        try
        {
            _process = Process.Start(psi)
                ?? throw new McpClientException($"无法启动外部 MCP 进程: {config.Command}");
        }
        catch (McpClientException) { throw; }
        catch (Exception ex)
        {
            throw new McpClientException($"无法启动外部 MCP 进程 '{config.Command}': {ex.Message}", ex);
        }

        _stdinWriter = new StreamWriter(_process.StandardInput.BaseStream, new UTF8Encoding(false)) { AutoFlush = false };
        _readTask = Task.Run(ReadLoopAsync);
        _ = Task.Run(DrainStderrAsync);
    }

    private static void Validate(McpExternalServerConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Command))
            throw new McpClientException("stdio 传输必须配置 command");
        if (!AllowedCommands.Contains(config.Command, StringComparer.OrdinalIgnoreCase))
            throw new McpClientException($"stdio command 不在白名单: {config.Command}（允许: {string.Join("/", AllowedCommands)}）");
    }

    public string Describe() => $"stdio[{_config.Command} {string.Join(' ', _config.Args)}]";

    public Task<JsonElement> InitializeAsync(IReadOnlyList<string> protocolVersions, CancellationToken ct) =>
        RequestAsync("initialize", new { protocolVersion = protocolVersions[0], capabilities = new { } }, ct);

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
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[McpClient] 关闭 stdio 进程异常: {0}", ex.Message);
        }
        _process.Dispose();
        FailAllPending(new McpClientException("外部 MCP 连接已关闭"));
    }

    private async Task<JsonElement> RequestAsync(string method, object? prms, CancellationToken ct)
    {
        var id = Interlocked.Increment(ref _nextId);
        var idKey = id.ToString();
        // @params 序列化为 "params"（params 为 C# 关键字，用 @ 转义属性名）
        var payload = JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method, @params = prms });
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[idKey] = tcs;

        try
        {
            await _writeLock.WaitAsync(ct);
            try
            {
                await WriteFrameAsync(_stdinWriter, payload, ct);
            }
            finally
            {
                _writeLock.Release();
            }

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _runCts.Token);
            linked.CancelAfter(RequestTimeoutMs);
            var respJson = await tcs.Task.WaitAsync(linked.Token);
            return ExtractResult(respJson, method);
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

    private async Task ReadLoopAsync()
    {
        try
        {
            var stream = _process.StandardOutput.BaseStream;
            while (!_runCts.IsCancellationRequested)
            {
                var frame = await ReadFrameAsync(stream, _runCts.Token);
                if (frame == null) break;
                DispatchFrame(frame);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[McpClient] stdio 读循环退出: {0}", ex.Message);
        }
        finally
        {
            FailAllPending(new McpClientException("外部 MCP 进程已退出"));
            _runCts.Cancel();
        }
    }

    /// <summary>读取一个 Content-Length 帧（headers 到空行 + body）。流结束返回 null。internal 供单测帧编解码。</summary>
    internal static async Task<string?> ReadFrameAsync(Stream stream, CancellationToken ct)
    {
        int? contentLength = null;
        while (true)
        {
            var line = await ReadHeaderLineAsync(stream, ct);
            if (line == null) return null;
            if (line.Length == 0) break;
            var text = Encoding.ASCII.GetString(line);
            var idx = text.IndexOf(':');
            if (idx > 0 && text[..idx].Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(text[(idx + 1)..].Trim(), out var headerLen)) contentLength = headerLen;
            }
        }
        if (contentLength is not int len || len <= 0) return null;

        // 帧体按 UTF-8 字节数读（中文字符 1 字符 ≠ 1 字节，必须字节级读取再解码）
        var buf = new byte[len];
        var total = 0;
        while (total < len)
        {
            var n = await stream.ReadAsync(buf.AsMemory(total, len - total), ct);
            if (n <= 0) return null;
            total += n;
        }
        return Encoding.UTF8.GetString(buf, 0, len);
    }

    /// <summary>按字节读一行（到 \n，去掉 \r）用于帧头。</summary>
    private static async Task<byte[]?> ReadHeaderLineAsync(Stream stream, CancellationToken ct)
    {
        var list = new List<byte>(32);
        while (true)
        {
            var one = new byte[1];
            var n = await stream.ReadAsync(one.AsMemory(), ct);
            if (n <= 0) return list.Count == 0 ? null : list.ToArray();
            if (one[0] == (byte)'\n')
            {
                while (list.Count > 0 && list[^1] == (byte)'\r') list.RemoveAt(list.Count - 1);
                return list.ToArray();
            }
            list.Add(one[0]);
        }
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

    private async Task DrainStderrAsync()
    {
        try
        {
            var line = await _process.StandardError.ReadLineAsync();
            while (line != null)
            {
                XTrace.Log.Debug("[McpClient] {0} stderr: {1}", _config.Command, line);
                line = await _process.StandardError.ReadLineAsync();
            }
        }
        catch (Exception) { }
    }

    private void FailAllPending(Exception ex)
    {
        foreach (var kv in _pending)
        {
            if (_pending.TryRemove(kv.Key, out var tcs)) tcs.TrySetException(ex);
        }
    }

    /// <summary>写入一个 Content-Length 帧（UTF-8 无 BOM）。internal 供单测帧编解码。</summary>
    internal static async Task WriteFrameAsync(StreamWriter writer, string json, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var header = $"Content-Length: {bytes.Length}\r\n\r\n";
        await writer.WriteAsync(header);
        await writer.WriteAsync(json);
        await writer.FlushAsync(ct);
    }
}

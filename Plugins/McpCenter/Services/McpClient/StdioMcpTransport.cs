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

    /// <summary>无参构造兜底超时（v2.5.2）：真源是 McpGatewayConfig.DefaultExecutionTimeoutMs；生产链路由 McpClientManager 注入网关配置值，本常量仅测试/直接构造时使用。</summary>
    public const int DefaultRequestTimeoutMs = McpGatewayConfig.DefaultExecutionTimeoutMs;

    /// <summary>auto 帧协商的探测超时：首帧 NDJSON 发出后无响应即换 LSP 帧重发一次（PC-010）。连接建立阶段固定预算，不随执行超时配置变动。</summary>
    public const int FramingProbeTimeoutMs = 5000;

    /// <summary>stdio 消息帧型：Ndjson=换行分隔 JSON（官方 TS/Python SDK 实况）；Lsp=Content-Length 帧（规范 SHOULD 形态）。</summary>
    internal enum StdioFraming { Ndjson, Lsp }

    private readonly McpExternalServerConfig _config;
    private readonly int _requestTimeoutMs;
    private readonly Process _process;
    private readonly StreamWriter _stdinWriter;
    private readonly CancellationTokenSource _runCts = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pending = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly Task _readTask;
    private readonly StdioFraming? _configuredFraming;
    private StdioFraming? _observedFraming;
    private bool _probeResent;
    private int _nextId;
    private bool _closed;

    public StdioMcpTransport(McpExternalServerConfig config, int requestTimeoutMs = DefaultRequestTimeoutMs)
    {
        Validate(config);
        _config = config;
        _requestTimeoutMs = requestTimeoutMs > 0 ? requestTimeoutMs : DefaultRequestTimeoutMs;
        _configuredFraming = ParseFraming(config.Framing);

        // Windows 下 npx/npm/uvx 等常为批处理 shim（.cmd/.bat），CreateProcess 不按 PATHEXT 解析 →
        // 必须先探测实际形态：.exe 直启；shim 用 shell 包装器包起；找不到给可行动报错
        //（PC-010，2026-10-18 用户实例实证 400「系统找不到指定的文件」）
        var (fileName, wrapperArguments) = ResolveLaunch(config.Command, config.Args, ResolvePathDirs(config), OperatingSystem.IsWindows());

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        if (wrapperArguments != null)
        {
            // shim 分支：整条命令行作为单个参数交给包装器（/d /s /c 保留引号语义）
            psi.Arguments = wrapperArguments;
        }
        else
        {
            foreach (var arg in config.Args) psi.ArgumentList.Add(arg);
        }
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

    /// <summary>PATH 探测目录：宿主进程 PATH + 配置 env 里的 PATH（用户可指向自定义 node/uv 安装目录）。internal 供单测。</summary>
    internal static IReadOnlyList<string> ResolvePathDirs(McpExternalServerConfig config)
    {
        var raw = config.Env.TryGetValue("PATH", out var overridePath) && !string.IsNullOrWhiteSpace(overridePath)
            ? overridePath
            : Environment.GetEnvironmentVariable("PATH") ?? "";
        return raw.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// 启动命令解析（PC-010 canonical 单点）：返回 (FileName, WrapperArguments)；
    /// WrapperArguments==null 表示直启（ArgumentList 原样传参），非 null 表示需 shell 包装器拉起批处理 shim。
    /// Windows 按 PATHEXT 优先级 exe &gt; cmd &gt; bat 逐个 PATH 目录探测；非 Windows 原样透传（execvp 自会解析 shebang 脚本）。
    /// internal 供单测（注入 pathDirs/isWindows/fileExists 实现跨平台确定性）。
    /// </summary>
    internal static (string FileName, string? WrapperArguments) ResolveLaunch(
        string command, IReadOnlyList<string> args, IReadOnlyList<string> pathDirs, bool isWindows,
        Func<string, bool>? fileExists = null)
    {
        if (!isWindows) return (command, null);
        fileExists ??= File.Exists;

        // 带路径分隔符/盘符：直接对给定路径判定形态
        if (command.Contains('/') || command.Contains('\\') || Path.IsPathRooted(command))
        {
            var rooted = Path.GetFullPath(command);
            if (!fileExists(rooted))
                throw new McpClientException($"找不到 stdio 命令文件：{command}（请确认已安装且路径正确）");
            return IsBatchShim(rooted)
                ? (ShellWrapperPath(), BuildWrapperArguments(rooted, args))
                : (rooted, null);
        }

        foreach (var dir in pathDirs)
        {
            string exe = Path.Combine(dir, command + ".exe");
            if (fileExists(exe)) return (exe, null);
            string cmd = Path.Combine(dir, command + ".cmd");
            if (fileExists(cmd)) return (ShellWrapperPath(), BuildWrapperArguments(cmd, args));
            string bat = Path.Combine(dir, command + ".bat");
            if (fileExists(bat)) return (ShellWrapperPath(), BuildWrapperArguments(bat, args));
        }

        throw new McpClientException(
            $"找不到 stdio 命令 '{command}'：已在宿主进程 PATH 的全部目录中查找 {command}.exe/{command}.cmd/{command}.bat。" +
            "请确认该命令已安装，并把它所在目录加入宿主进程 PATH（也可在服务器配置的 env.PATH 里指定）；若宿主由快捷方式/计划任务拉起，可能不继承终端 PATH。");
    }

    private static bool IsBatchShim(string file) =>
        file.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);

    /// <summary>shim 包装器：Windows 批处理必须经命令解释器拉起（ComSpec，回退 System32）。</summary>
    private static string ShellWrapperPath()
    {
        var comSpec = Environment.GetEnvironmentVariable("ComSpec");
        if (!string.IsNullOrWhiteSpace(comSpec) && File.Exists(comSpec)) return comSpec;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
    }

    /// <summary>构造 /d /s /c 包起的单行命令：/s 保留首尾引号，内层含空格 token 各自加引号。</summary>
    internal static string BuildWrapperArguments(string commandFile, IReadOnlyList<string> args)
    {
        var sb = new StringBuilder();
        sb.Append(QuoteForShell(commandFile));
        foreach (var a in args) sb.Append(' ').Append(QuoteForShell(a));
        return $"/d /s /c \"{sb}\"";
    }

    private static string QuoteForShell(string token) =>
        token.Contains(' ', StringComparison.Ordinal) || token.Contains('"', StringComparison.Ordinal)
            ? "\"" + token + "\""
            : token;

    public string Describe() => $"stdio[{_config.Command} {string.Join(' ', _config.Args)}]";

    public async Task<JsonElement> InitializeAsync(IReadOnlyList<string> protocolVersions, CancellationToken ct)
    {
        // clientInfo 为规范必填（缺失即 -32602，DeepWiki 实测）：载荷经 McpClientProtocol 单点构造
        var result = await RequestAsync("initialize", McpClientProtocol.BuildInitializeParams(protocolVersions[0]), ct);
        // 规范要求收到 initialize result 后客户端 MUST 发 notifications/initialized；尽力而为不阻断
        try
        {
            await NotifyAsync(McpClientProtocol.InitializedNotificationMethod, ct);
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[McpClient] stdio notifications/initialized 发送失败（不阻断连接）: {0}", ex.Message);
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
        return RequestAsync("tools/call", new { name, arguments = args ?? new { } }, ct); // 空参数给 {} 而非 null（PC-010 同族：嵌套 null 也会让部分服务器沉默）
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
        // 请求序列化经 McpClientProtocol 单点（null params 省略键，PC-010）
        var payload = McpClientProtocol.SerializeRequest(id, method, prms);
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[idKey] = tcs;

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _runCts.Token);
            linked.CancelAfter(_requestTimeoutMs);

            await WriteMessageAsync(payload, EffectiveWriteFraming, ct);
            string respJson;
            try
            {
                // auto 模式且尚未从响应识别对方帧型：短等探测超时，无响应则换形重发
                if (_configuredFraming is null && _observedFraming is null && !_probeResent)
                    respJson = await tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(FramingProbeTimeoutMs), linked.Token);
                else
                    respJson = await tcs.Task.WaitAsync(linked.Token);
            }
            catch (TimeoutException)
            {
                // NDJSON 探测未获响应 → 对方很可能是严格 LSP 帧服务器：换 LSP 帧重发同 id 请求
                //（重复响应由 _pending.TryRemove 去重；对方解析不了的那帧会被静默丢弃）
                _probeResent = true;
                await WriteMessageAsync(payload, StdioFraming.Lsp, ct);
                respJson = await tcs.Task.WaitAsync(linked.Token);
            }
            return ExtractResult(respJson, method);
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

    /// <summary>写帧：已识别的对方帧型优先，其次配置强制，最后 auto 首选 NDJSON（生态多数实现实况）。</summary>
    private StdioFraming EffectiveWriteFraming => _configuredFraming ?? _observedFraming ?? StdioFraming.Ndjson;

    /// <summary>按帧型写入一条消息（带写锁）。internal 供单测。</summary>
    internal static async Task WriteMessageAsync(StreamWriter writer, string json, StdioFraming framing, CancellationToken ct)
    {
        if (framing == StdioFraming.Lsp)
        {
            await WriteFrameAsync(writer, json, ct);
            return;
        }
        await writer.WriteAsync(json + "\n");
        await writer.FlushAsync(ct);
    }

    private async Task WriteMessageAsync(string payload, StdioFraming framing, CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct);
        try
        {
            await WriteMessageAsync(_stdinWriter, payload, framing, ct);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    internal static StdioFraming? ParseFraming(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "ndjson" => StdioFraming.Ndjson,
        "lsp" => StdioFraming.Lsp,
        _ => null
    };

    /// <summary>写入 JSON-RPC 通知帧（无 id，不等响应）。v2.5.0：initialized 通知用。</summary>
    private async Task NotifyAsync(string method, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new { jsonrpc = "2.0", method });
        await WriteMessageAsync(payload, EffectiveWriteFraming, ct);
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
                var frame = await ReadMessageAsync(stream, _runCts.Token);
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

    /// <summary>
    /// 读一条消息，**双帧型自适应**（PC-010）：行首 '{' → NDJSON 整行即消息；否则按 LSP 头解析
    ///（Content-Length + 空行 + 定长字节体，未知头行容错跳过）。首次命中即锁定 _observedFraming，
    /// 后续写帧跟随对方帧型。流结束返回 null。
    /// </summary>
    private async Task<string?> ReadMessageAsync(Stream stream, CancellationToken ct)
    {
        while (true)
        {
            var lineBytes = await ReadHeaderLineAsync(stream, ct);
            if (lineBytes == null) return null;
            var line = Encoding.UTF8.GetString(lineBytes);
            if (line.Length == 0) continue;

            if (line[0] == '{')
            {
                _observedFraming ??= StdioFraming.Ndjson;
                return line;
            }

            // 非 NDJSON 行 → 按 LSP 头解析：本行可能是 Content-Length，也可能是不明头/杂音（容错跳行）
            int? contentLength = TryParseContentLength(line);
            while (true)
            {
                var h = await ReadHeaderLineAsync(stream, ct);
                if (h == null) return null;
                if (h.Length == 0) break; // 头部区结束
                var v = TryParseContentLength(Encoding.UTF8.GetString(h));
                if (v.HasValue) contentLength = v;
            }
            if (contentLength is not int len || len <= 0) continue;

            // 帧体按 UTF-8 字节数读（中文字符 1 字符 ≠ 1 字节，必须字节级读取再解码）
            var buf = new byte[len];
            var total = 0;
            while (total < len)
            {
                var n = await stream.ReadAsync(buf.AsMemory(total, len - total), ct);
                if (n <= 0) return null;
                total += n;
            }
            _observedFraming ??= StdioFraming.Lsp;
            return Encoding.UTF8.GetString(buf, 0, len);
        }
    }

    private static int? TryParseContentLength(string line)
    {
        var idx = line.IndexOf(':');
        if (idx > 0 && line[..idx].Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(line[(idx + 1)..].Trim(), out var len))
            return len;
        return null;
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

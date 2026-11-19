using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ForgeSelf.Abstractions;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AgentHub.Services;

/// <summary>
/// AgentHub ↔ RuntimeWorker 的 stdio JSON-RPC/JSONL 客户端（P1）。
/// 不实现 <see cref="IAgentRuntime"/>（那是 P3 Provider）；本类型只负责进程生命周期与协议面。
/// </summary>
public sealed class OarWorkerClient : IAsyncDisposable, IDisposable
{
    private readonly OarWorkerClientOptions _options;
    private readonly object _writeLock = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _pending = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _lifetime = new();

    private Process? _process;
    private StreamWriter? _stdin;
    private Task? _stdoutLoop;
    private Task? _stderrLoop;
    private int _nextId;
    private string? _unavailableReason;
    private bool _disposed;

    /// <summary>会话事件通知（method = session.event）。</summary>
    public event Action<JsonElement>? SessionEventReceived;

    /// <summary>当前是否已成功 hello。</summary>
    public bool IsAvailable { get; private set; }

    /// <summary>不可用原因（Available=false 时）。</summary>
    public string? UnavailableReason => _unavailableReason;

    /// <summary>最近一次 hello 结果（可空）。</summary>
    public OarWorkerHelloResult? LastHello { get; private set; }

    public OarWorkerClient(OarWorkerClientOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>探测并启动 Worker；已启动则复用。失败时 Available=false，不抛导致宿主崩溃。</summary>
    public async Task<bool> EnsureStartedAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsAvailable && _process is { HasExited: false })
            return true;

        try
        {
            ValidateOptionsOrThrow();
            await StartProcessAsync(ct).ConfigureAwait(false);
            LastHello = await HelloAsync(ct).ConfigureAwait(false);
            if (LastHello.ProtocolVersion < 1)
            {
                MarkUnavailable($"unsupported protocolVersion={LastHello.ProtocolVersion}");
                await DisposeAsync().ConfigureAwait(false);
                return false;
            }

            IsAvailable = true;
            _unavailableReason = null;
            return true;
        }
        catch (Exception ex)
        {
            MarkUnavailable(ex.Message);
            XTrace.Log.Warn("OarWorkerClient 启动失败: {0}", ex.Message);
            try { await DisposeCoreAsync(disposing: true).ConfigureAwait(false); } catch { /* ignore */ }
            return false;
        }
    }

    /// <summary><c>worker.hello</c></summary>
    public async Task<OarWorkerHelloResult> HelloAsync(CancellationToken ct = default)
    {
        var el = await RequestAsync("worker.hello", null, ct).ConfigureAwait(false);
        return new OarWorkerHelloResult
        {
            ProtocolVersion = el.TryGetProperty("protocolVersion", out var pv) && pv.TryGetInt32(out var v) ? v : 0,
            Oar = el.TryGetProperty("oar", out var oar) ? oar.GetString() : null,
            Node = el.TryGetProperty("node", out var node) ? node.GetString() : null,
            Pid = el.TryGetProperty("pid", out var pid) && pid.TryGetInt32(out var p) ? p : null,
            UptimeMs = el.TryGetProperty("uptimeMs", out var up) && up.TryGetInt64(out var u) ? u : null,
            Sessions = el.TryGetProperty("sessions", out var s) && s.TryGetInt32(out var n) ? n : null,
        };
    }

    /// <summary><c>runtime.list</c></summary>
    public async Task<IReadOnlyList<OarRuntimeSummary>> ListRuntimesAsync(CancellationToken ct = default)
    {
        var el = await RequestAsync("runtime.list", null, ct).ConfigureAwait(false);
        if (!el.TryGetProperty("runtimes", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return [];

        var list = new List<OarRuntimeSummary>();
        foreach (var item in arr.EnumerateArray())
        {
            string? brandName = null;
            if (item.TryGetProperty("brand", out var brand))
            {
                brandName = brand.ValueKind == JsonValueKind.Object && brand.TryGetProperty("name", out var bn)
                    ? bn.GetString()
                    : brand.GetString();
            }

            list.Add(new OarRuntimeSummary
            {
                Id = item.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                Name = brandName,
                Brand = brandName,
            });
        }

        return list;
    }

    /// <summary><c>session.open</c></summary>
    public async Task<OarSessionOpenResult> OpenSessionAsync(
        string runtimeId,
        string? cwd = null,
        string? model = null,
        CancellationToken ct = default)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["runtimeId"] = runtimeId,
            ["cwd"] = string.IsNullOrWhiteSpace(cwd) ? Directory.GetCurrentDirectory() : cwd,
        };
        if (!string.IsNullOrWhiteSpace(model))
            parameters["model"] = model;

        var el = await RequestAsync("session.open", parameters, ct).ConfigureAwait(false);
        return new OarSessionOpenResult
        {
            SessionKey = el.GetProperty("sessionKey").GetString() ?? "",
            OarSessionId = el.TryGetProperty("oarSessionId", out var oid) ? oid.GetString() : null,
            RuntimeId = el.TryGetProperty("runtimeId", out var rid) ? rid.GetString() : runtimeId,
            Generation = el.TryGetProperty("generation", out var g) && g.TryGetInt32(out var gen) ? gen : 1,
        };
    }

    /// <summary><c>session.prompt</c> → 映射为 <see cref="AgentControlResult"/>。</summary>
    public async Task<AgentControlResult> PromptAsync(string sessionKey, string input, CancellationToken ct = default)
    {
        var el = await RequestAsync(
            "session.prompt",
            new Dictionary<string, object?> { ["sessionKey"] = sessionKey, ["input"] = input },
            ct).ConfigureAwait(false);
        return MapControlOutcome(el.TryGetProperty("outcome", out var outcome) ? outcome : el);
    }

    /// <summary><c>session.control</c> abort/cancel。</summary>
    public async Task<AgentControlResult> ControlAsync(
        string sessionKey,
        string action = "abort",
        CancellationToken ct = default)
    {
        var el = await RequestAsync(
            "session.control",
            new Dictionary<string, object?> { ["sessionKey"] = sessionKey, ["action"] = action },
            ct).ConfigureAwait(false);
        return MapControlOutcome(el.TryGetProperty("outcome", out var outcome) ? outcome : el);
    }

    /// <summary><c>session.dispose</c></summary>
    public async Task DisposeSessionAsync(string sessionKey, CancellationToken ct = default)
    {
        await RequestAsync(
            "session.dispose",
            new Dictionary<string, object?> { ["sessionKey"] = sessionKey },
            ct).ConfigureAwait(false);
    }

    /// <summary><c>runtime.installation</c>（只读探测）。</summary>
    public async Task<JsonElement> InstallationAsync(string runtimeId, CancellationToken ct = default)
    {
        return await RequestAsync(
            "runtime.installation",
            new Dictionary<string, object?> { ["runtimeId"] = runtimeId },
            ct).ConfigureAwait(false);
    }

    /// <summary><c>runtime.auth</c>（只读；scripted 可能 not_supported）。</summary>
    public async Task<JsonElement> AuthAsync(string runtimeId, CancellationToken ct = default)
    {
        return await RequestAsync(
            "runtime.auth",
            new Dictionary<string, object?> { ["runtimeId"] = runtimeId },
            ct).ConfigureAwait(false);
    }

    /// <summary><c>runtime.models</c>（只读；scripted 可能 not_supported）。</summary>
    public async Task<JsonElement> ModelsAsync(string runtimeId, CancellationToken ct = default)
    {
        return await RequestAsync(
            "runtime.models",
            new Dictionary<string, object?> { ["runtimeId"] = runtimeId },
            ct).ConfigureAwait(false);
    }

    /// <summary><c>session.records</c>（Worker 侧原始审计；调用方应再脱敏落库）。</summary>
    public async Task<JsonElement> SessionRecordsAsync(string sessionKey, CancellationToken ct = default)
    {
        return await RequestAsync(
            "session.records",
            new Dictionary<string, object?> { ["sessionKey"] = sessionKey },
            ct).ConfigureAwait(false);
    }

    /// <summary>底层 JSON-RPC 请求。</summary>
    public async Task<JsonElement> RequestAsync(string method, object? parameters, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_process is null || _stdin is null || _process.HasExited)
            throw new InvalidOperationException(_unavailableReason ?? "OAR Worker 未启动");

        var id = Interlocked.Increment(ref _nextId).ToString();
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(id, tcs))
            throw new InvalidOperationException("duplicate rpc id");

        var payload = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
        };
        if (parameters != null)
            payload["params"] = parameters;

        var line = JsonSerializer.Serialize(payload, ForgeJson.Default);
        try
        {
            lock (_writeLock)
            {
                _stdin.WriteLine(line);
                _stdin.Flush();
            }
        }
        catch (Exception ex)
        {
            _pending.TryRemove(id, out _);
            throw new InvalidOperationException("写入 Worker stdin 失败: " + ex.Message, ex);
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
        linked.CancelAfter(_options.RequestTimeout);
        await using var reg = linked.Token.Register(() =>
            tcs.TrySetException(new TimeoutException($"OAR RPC timeout: {method} id={id}")));

        return await tcs.Task.ConfigureAwait(false);
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeCoreAsync(disposing: true).ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private async Task DisposeCoreAsync(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        IsAvailable = false;

        if (!disposing) return;

        try { _lifetime.Cancel(); } catch { /* ignore */ }

        foreach (var kv in _pending)
            kv.Value.TrySetCanceled();
        _pending.Clear();

        try
        {
            if (_stdin != null)
            {
                lock (_writeLock)
                {
                    try { _stdin.Dispose(); } catch { /* ignore */ }
                    _stdin = null;
                }
            }
        }
        catch { /* ignore */ }

        var proc = _process;
        _process = null;
        if (proc != null)
        {
            try
            {
                if (!proc.HasExited)
                {
                    // 先软关 stdin（已 Dispose），再 Kill 进程树
                    try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
                    try
                    {
                        using var waitCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        await proc.WaitForExitAsync(waitCts.Token).ConfigureAwait(false);
                    }
                    catch { /* ignore */ }
                }
            }
            finally
            {
                proc.Dispose();
            }
        }

        if (_stdoutLoop != null)
        {
            try { await Task.WhenAny(_stdoutLoop, Task.Delay(2000)).ConfigureAwait(false); } catch { /* ignore */ }
        }

        if (_stderrLoop != null)
        {
            try { await Task.WhenAny(_stderrLoop, Task.Delay(500)).ConfigureAwait(false); } catch { /* ignore */ }
        }

        try { _lifetime.Dispose(); } catch { /* ignore */ }
    }

    private void ValidateOptionsOrThrow()
    {
        if (string.IsNullOrWhiteSpace(_options.NodeExecutable))
            throw new InvalidOperationException("NodeExecutable 为空");
        if (string.IsNullOrWhiteSpace(_options.WorkerEntryPath))
            throw new InvalidOperationException("WorkerEntryPath 为空");
        if (!File.Exists(_options.WorkerEntryPath))
            throw new FileNotFoundException("Worker 入口不存在", _options.WorkerEntryPath);
    }

    private Task StartProcessAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var psi = new ProcessStartInfo
        {
            FileName = _options.NodeExecutable,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var arg in _options.NodeImportArgs)
            psi.ArgumentList.Add(arg);
        psi.ArgumentList.Add(_options.WorkerEntryPath);

        var cwd = _options.WorkingDirectory;
        if (string.IsNullOrWhiteSpace(cwd))
            cwd = Path.GetDirectoryName(_options.WorkerEntryPath);
        if (!string.IsNullOrWhiteSpace(cwd) && Directory.Exists(cwd))
            psi.WorkingDirectory = cwd;

        foreach (var (key, value) in _options.Environment)
            psi.Environment[key] = value;

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        if (!process.Start())
            throw new InvalidOperationException("无法启动 Node Worker 进程");

        _process = process;
        _stdin = new StreamWriter(process.StandardInput.BaseStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
            NewLine = "\n",
        };

        _stdoutLoop = Task.Run(() => ReadStdoutLoopAsync(_lifetime.Token), _lifetime.Token);
        _stderrLoop = Task.Run(() => ReadStderrLoopAsync(_lifetime.Token), _lifetime.Token);
        return Task.CompletedTask;
    }

    private async Task ReadStdoutLoopAsync(CancellationToken ct)
    {
        var proc = _process;
        if (proc == null) return;

        try
        {
            while (!ct.IsCancellationRequested && !proc.HasExited)
            {
                var line = await proc.StandardOutput.ReadLineAsync(ct).ConfigureAwait(false);
                if (line == null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;
                HandleStdoutLine(line);
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (Exception ex)
        {
            XTrace.Log.Warn("OarWorkerClient stdout 循环结束: {0}", ex.Message);
        }
        finally
        {
            foreach (var kv in _pending)
                kv.Value.TrySetException(new InvalidOperationException("Worker stdout 已关闭"));
            _pending.Clear();
            if (IsAvailable)
                MarkUnavailable("Worker 进程已退出");
            IsAvailable = false;
        }
    }

    private async Task ReadStderrLoopAsync(CancellationToken ct)
    {
        var proc = _process;
        if (proc == null) return;
        try
        {
            while (!ct.IsCancellationRequested && !proc.HasExited)
            {
                var line = await proc.StandardError.ReadLineAsync(ct).ConfigureAwait(false);
                if (line == null) break;
                if (!string.IsNullOrWhiteSpace(line))
                    XTrace.Log.Debug("[OAR Worker] {0}", line);
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch { /* ignore */ }
    }

    private void HandleStdoutLine(string line)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(line);
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("OarWorkerClient 无法解析协议行: {0}", ex.Message);
            return;
        }

        using (doc)
        {
            var root = doc.RootElement.Clone();
            if (root.TryGetProperty("id", out var idEl) && idEl.ValueKind != JsonValueKind.Null)
            {
                var id = idEl.ValueKind == JsonValueKind.Number
                    ? idEl.GetRawText()
                    : idEl.GetString() ?? idEl.GetRawText();

                if (_pending.TryRemove(id, out var tcs))
                {
                    if (root.TryGetProperty("error", out var err))
                    {
                        var code = err.TryGetProperty("code", out var c) && c.TryGetInt32(out var ci) ? ci : -32000;
                        var message = err.TryGetProperty("message", out var m) ? m.GetString() ?? "rpc error" : "rpc error";
                        tcs.TrySetException(new OarWorkerRpcException(code, message, err.Clone()));
                    }
                    else if (root.TryGetProperty("result", out var result))
                    {
                        tcs.TrySetResult(result.Clone());
                    }
                    else
                    {
                        tcs.TrySetException(new InvalidOperationException("RPC 响应缺少 result/error"));
                    }
                }

                return;
            }

            // Notification
            if (root.TryGetProperty("method", out var methodEl))
            {
                var method = methodEl.GetString();
                if (string.Equals(method, "session.event", StringComparison.Ordinal))
                {
                    var paramsEl = root.TryGetProperty("params", out var p) ? p.Clone() : root.Clone();
                    try { SessionEventReceived?.Invoke(paramsEl); } catch { /* swallow listener errors */ }
                }
            }
        }
    }

    private static AgentControlResult MapControlOutcome(JsonElement outcome)
    {
        // Worker controlOutcomeSafe：{ kind, seq, requestId, code?, reason? }
        if (outcome.ValueKind == JsonValueKind.True)
            return AgentControlResult.Ok();
        if (outcome.ValueKind == JsonValueKind.False)
            return AgentControlResult.Reject("rejected", "control rejected");

        var kind = outcome.TryGetProperty("kind", out var k) ? k.GetString() : null;
        var code = outcome.TryGetProperty("code", out var c) && c.ValueKind != JsonValueKind.Null
            ? c.GetString()
            : null;
        var reason = outcome.TryGetProperty("reason", out var r) && r.ValueKind != JsonValueKind.Null
            ? r.GetString()
            : outcome.TryGetProperty("message", out var msg) ? msg.GetString() : null;

        if (outcome.TryGetProperty("accepted", out var a) && a.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return a.GetBoolean()
                ? AgentControlResult.Ok(code ?? kind ?? "ok", reason)
                : AgentControlResult.Reject(code ?? kind ?? "rejected", reason ?? "control rejected");
        }

        // OAR ControlOutcome.kind：accepted / rejected / unsupported / …
        var rejected = kind is not null
            && (kind.Contains("reject", StringComparison.OrdinalIgnoreCase)
                || kind.Contains("unsupported", StringComparison.OrdinalIgnoreCase)
                || kind.Contains("error", StringComparison.OrdinalIgnoreCase)
                || kind.Contains("fail", StringComparison.OrdinalIgnoreCase));

        return rejected
            ? AgentControlResult.Reject(code ?? kind!, reason ?? kind!)
            : AgentControlResult.Ok(code ?? kind ?? "ok", reason);
    }

    private void MarkUnavailable(string reason)
    {
        IsAvailable = false;
        _unavailableReason = reason;
    }
}

/// <summary>JSON-RPC 业务/协议错误。</summary>
public sealed class OarWorkerRpcException : Exception
{
    public int Code { get; }
    public JsonElement? Data { get; }

    public OarWorkerRpcException(int code, string message, JsonElement? data = null)
        : base(message)
    {
        Code = code;
        Data = data;
    }
}

/// <summary><c>worker.hello</c> 结果。</summary>
public sealed class OarWorkerHelloResult
{
    public int ProtocolVersion { get; init; }
    public string? Oar { get; init; }
    public string? Node { get; init; }
    public int? Pid { get; init; }
    public long? UptimeMs { get; init; }
    public int? Sessions { get; init; }
}

/// <summary><c>runtime.list</c> 单项摘要。</summary>
public sealed class OarRuntimeSummary
{
    public string Id { get; init; } = "";
    public string? Name { get; init; }
    public string? Brand { get; init; }
}

/// <summary><c>session.open</c> 结果。</summary>
public sealed class OarSessionOpenResult
{
    public string SessionKey { get; init; } = "";
    public string? OarSessionId { get; init; }
    public string? RuntimeId { get; init; }
    public int Generation { get; init; }
}

using System.Runtime.CompilerServices;
using System.Text.Json;
using ForgeSelf.Api.Plugins.AgentHub.Models;
using NewLife;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AgentHub.Services;

/// <summary>
/// OAR RuntimeWorker 传输实现（P2）。经 <see cref="OarWorkerClient"/> 开会话/prompt/事件/取消。
/// Kind = <c>Oar</c>；AccessPoint.RuntimeId 必填。
/// </summary>
public sealed class OarTransport : IAgentTransport
{
    private readonly OarWorkerClientOptions _options;
    private readonly AgentSessionManager _sessions;
    private readonly object _clientLock = new();
    private OarWorkerClient? _client;

    /// <summary>运行中 taskId → worker sessionKey（供 Cancel）。</summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Int32, String> _taskSessions = new();

    public OarTransport(OarWorkerClientOptions options, AgentSessionManager sessions)
    {
        _options = options;
        _sessions = sessions;
    }

    /// <inheritdoc />
    public String Kind => "Oar";

    /// <inheritdoc />
    public async IAsyncEnumerable<AgentEvent> StreamAsync(
        AgentAccessPointDto ap,
        AgentRunRequest req,
        AgentSession session,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var runtimeId = ap.RuntimeId;
        if (runtimeId.IsNullOrEmpty())
        {
            yield return AgentEvent.FromError("Oar 交互口缺少 RuntimeId");
            yield break;
        }

        var client = await EnsureClientAsync(ct).ConfigureAwait(false);
        if (client == null || !client.IsAvailable)
        {
            yield return AgentEvent.FromError(
                "OAR Worker 不可用: " + (client?.UnavailableReason ?? "未启动"));
            yield break;
        }

        // yield 不能放在 try/catch 内——先开会话，失败用局部变量带回
        OarSessionOpenResult? open = null;
        String? openError = null;
        try
        {
            open = await client.OpenSessionAsync(runtimeId, req.Cwd, model: null, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            openError = "session.open 失败: " + ex.Message;
        }

        if (openError != null)
        {
            yield return AgentEvent.FromError(openError);
            yield break;
        }

        var opened = open!;
        session.SessionRef = opened.SessionKey;
        session.Kind = Kind;
        session.CancelSupported = true;
        _taskSessions[session.TaskId] = opened.SessionKey;

        // 持久化对外 SessionKey（与 Worker sessionKey 对齐；P3 Provider 可换独立 GUID 映射）
        try
        {
            _sessions.Open(
                agentId: ap.AgentId,
                accessPointId: ap.Id,
                transport: Kind,
                runtimeId: runtimeId,
                oarSessionId: opened.OarSessionId,
                cwd: req.Cwd,
                model: null,
                generation: opened.Generation,
                sessionKey: opened.SessionKey);
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[OarTransport] Session 落库失败（继续执行）: {0}", ex.Message);
        }

        yield return AgentEvent.FromText($"session={opened.SessionKey}", AgentEventTypes.Meta);

        // prompt + 事件：在无 yield 的 helper 里收集，再统一吐出
        var buffered = await RunPromptBufferedAsync(client, opened.SessionKey, req.Prompt ?? String.Empty, ct)
            .ConfigureAwait(false);
        foreach (var ev in buffered)
            yield return ev;

        _taskSessions.TryRemove(session.TaskId, out _);
        try
        {
            await client.DisposeSessionAsync(opened.SessionKey, CancellationToken.None)
                .ConfigureAwait(false);
            _sessions.Close(opened.SessionKey);
        }
        catch (Exception ex)
        {
            XTrace.Log.Debug("[OarTransport] dispose session: {0}", ex.Message);
        }
    }

    /// <summary>执行 prompt 并缓冲事件（避免 async iterator 内 try/catch+yield）。</summary>
    private static async Task<List<AgentEvent>> RunPromptBufferedAsync(
        OarWorkerClient client,
        String sessionKey,
        String prompt,
        CancellationToken ct)
    {
        var results = new List<AgentEvent>();
        var eventQueue = new System.Collections.Concurrent.ConcurrentQueue<AgentEvent>();
        void OnSessionEvent(JsonElement payload)
        {
            try
            {
                var type = payload.TryGetProperty("event", out var ev) && ev.TryGetProperty("type", out var t)
                    ? t.GetString()
                    : "oar_event";
                var text = payload.TryGetProperty("event", out var ev2) && ev2.TryGetProperty("text", out var tx)
                    ? tx.GetString()
                    : payload.GetRawText();
                eventQueue.Enqueue(AgentEvent.FromText(text ?? type ?? "event"));
            }
            catch
            {
                /* ignore malformed */
            }
        }

        client.SessionEventReceived += OnSessionEvent;
        try
        {
            var outcome = await client.PromptAsync(sessionKey, prompt, ct).ConfigureAwait(false);
            while (eventQueue.TryDequeue(out var queued))
                results.Add(queued);

            if (!outcome.Accepted)
            {
                results.Add(AgentEvent.FromError(
                    $"prompt 被拒: {outcome.Code} {outcome.Reason}".Trim()));
            }
            else if (!outcome.Reason.IsNullOrEmpty())
            {
                results.Add(AgentEvent.FromText(outcome.Reason!));
            }

            await Task.Delay(50, ct).ConfigureAwait(false);
            while (eventQueue.TryDequeue(out var late))
                results.Add(late);

            results.Add(new AgentEvent { Type = AgentEventTypes.Exit, ExitCode = 0 });
        }
        catch (OperationCanceledException)
        {
            results.Add(AgentEvent.FromError("cancelled"));
            results.Add(new AgentEvent { Type = AgentEventTypes.Exit, ExitCode = -1 });
        }
        catch (Exception ex)
        {
            results.Add(AgentEvent.FromError(ex.Message));
            results.Add(new AgentEvent { Type = AgentEventTypes.Exit, ExitCode = 1 });
        }
        finally
        {
            client.SessionEventReceived -= OnSessionEvent;
        }

        return results;
    }

    /// <inheritdoc />
    public async Task CancelAsync(AgentSession session)
    {
        if (!_taskSessions.TryGetValue(session.TaskId, out var key) || key.IsNullOrEmpty())
            key = session.SessionRef;
        if (key.IsNullOrEmpty()) return;

        OarWorkerClient? client;
        lock (_clientLock) client = _client;
        if (client == null || !client.IsAvailable) return;

        try
        {
            await client.ControlAsync(key, "abort").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[OarTransport] abort 失败: {0}", ex.Message);
        }
    }

    private async Task<OarWorkerClient?> EnsureClientAsync(CancellationToken ct)
    {
        lock (_clientLock)
        {
            if (_client != null && _client.IsAvailable)
                return _client;
        }

        // 选项未配置入口时标记不可用（P4 再固化发现策略）
        if (_options.WorkerEntryPath.IsNullOrEmpty() || !File.Exists(_options.WorkerEntryPath))
        {
            XTrace.Log.Warn("[OarTransport] WorkerEntryPath 未配置或不存在: {0}", _options.WorkerEntryPath);
            return null;
        }

        var client = new OarWorkerClient(_options);
        var ok = await client.EnsureStartedAsync(ct).ConfigureAwait(false);
        if (!ok)
        {
            await client.DisposeAsync().ConfigureAwait(false);
            return client; // IsAvailable=false，调用方读 UnavailableReason
        }

        lock (_clientLock)
        {
            _client?.Dispose();
            _client = client;
            return _client;
        }
    }
}

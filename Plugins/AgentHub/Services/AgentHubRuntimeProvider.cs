using System.Runtime.CompilerServices;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AgentHub.Entities;
using ForgeSelf.Api.Plugins.AgentHub.Models;
using NewLife;
using NewLife.Log;
using TaskStatus = ForgeSelf.Api.Plugins.AgentHub.Services.TaskStatus;
using IAgentRegistry = ForgeSelf.Api.Plugins.AgentHub.Services.IAgentRegistry;

namespace ForgeSelf.Api.Plugins.AgentHub.Services;

/// <summary>
/// <see cref="IAgentRuntime"/> 提供方（P3）：同实例进宿主 DI + <c>ctx.Register</c>。
/// 纯适配层——执行仍走 <see cref="DelegationRuntime"/> / Session / Worker；每次方法内 ResolveHost，不字段缓存依赖。
/// </summary>
public sealed class AgentHubRuntimeProvider : IAgentRuntime
{
    public const String DefaultCreatedBy = "agent-runtime";

    private readonly IServiceProvider? _services;

    /// <summary>构造。传入插件 <c>IContext</c>（与 <see cref="AgentDelegationProvider"/> 同源）。</summary>
    public AgentHubRuntimeProvider(IServiceProvider? services) => _services = services;

    /// <inheritdoc />
    public async Task<IReadOnlyList<AgentDescriptor>> ListAgentsAsync(CancellationToken ct = default)
    {
        var registry = AgentHubDi.ResolveHost<IAgentRegistry>(_services);
        if (registry == null) return [];

        var list = new List<AgentDescriptor>();
        OarWorkerClient? client = null;
        try
        {
            client = AgentHubDi.ResolveHost<OarWorkerClient>(_services);
            if (client != null && !client.IsAvailable)
                await client.EnsureStartedAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            XTrace.Log.Debug("[AgentHubRuntime] Worker 启动失败: {0}", ex.Message);
            client = null;
        }

        foreach (var a in registry.List(enabledOnly: false))
        {
            var ap = a.AccessPoints.FirstOrDefault(p => p.IsDefault) ?? a.AccessPoints.FirstOrDefault();
            var transport = ap?.Transport ?? "Cli";
            var runtimeId = ap?.RuntimeId ?? String.Empty;
            // scripted = Worker/协议测试专用，不进产品 Runtime/Agent 投影
            if (String.Equals(runtimeId, "scripted", StringComparison.OrdinalIgnoreCase))
                continue;

            String? kind = null;
            String? via = null;
            Boolean available;
            String? reason = null;
            if (client is { IsAvailable: true } && !runtimeId.IsNullOrEmpty()
                && String.Equals(transport, "Oar", StringComparison.OrdinalIgnoreCase))
            {
                (kind, via, available, reason) = await ProbeInstallationHonestyAsync(client, runtimeId, ct)
                    .ConfigureAwait(false);
                if (!a.Enabled)
                {
                    available = false;
                    reason = "disabled";
                }
            }
            else
            {
                available = a.Enabled && !String.Equals(a.Health, "Missing", StringComparison.OrdinalIgnoreCase);
                reason = available ? null : (a.Enabled ? $"health={a.Health}" : "disabled");
            }

            list.Add(new AgentDescriptor
            {
                AgentId = a.Id,
                Name = a.Name ?? String.Empty,
                Vendor = a.Vendor ?? String.Empty,
                RuntimeId = runtimeId,
                Transport = transport,
                Available = available,
                UnavailableReason = available ? null : reason,
                InstallationKind = kind,
                InstallationVia = via,
                Capabilities = BuildCapabilities(a),
            });
        }

        // 补充 Worker 侧 runtime.list（产品面剔除 scripted；安装态走真实 OAR snapshot，禁止假绿）
        try
        {
            if (client is { IsAvailable: true })
            {
                var known = new HashSet<String>(list.Select(x => x.RuntimeId), StringComparer.OrdinalIgnoreCase);
                foreach (var rt in await client.ListRuntimesAsync(ct).ConfigureAwait(false))
                {
                    if (rt.Id.IsNullOrEmpty() || known.Contains(rt.Id)) continue;
                    // scripted = Worker 烟雾测试专用，不进消费侧 Runtime 列表 / UI
                    if (String.Equals(rt.Id, "scripted", StringComparison.OrdinalIgnoreCase)) continue;

                    var (kind, via, available, reason) = await ProbeInstallationHonestyAsync(client, rt.Id, ct)
                        .ConfigureAwait(false);

                    list.Add(new AgentDescriptor
                    {
                        AgentId = 0,
                        Name = rt.Name.IsNullOrEmpty() ? rt.Id : rt.Name!,
                        Vendor = rt.Id,
                        RuntimeId = rt.Id,
                        Transport = "Oar",
                        Available = available,
                        UnavailableReason = available ? null : reason,
                        InstallationKind = kind,
                        InstallationVia = via,
                        Capabilities = ["prompt", "abort", "dispose"],
                    });
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Debug("[AgentHubRuntime] runtime.list 补充失败: {0}", ex.Message);
        }

        return list;
    }

    /// <summary>
    /// 读 OAR <c>runtime.installation</c>，仅 <c>kind=available &amp;&amp; via=executable</c> 标可用。
    /// bundled / not_found / unsupported 一律诚实返回，禁止假绿。
    /// </summary>
    private static async Task<(String? Kind, String? Via, Boolean Available, String? Reason)> ProbeInstallationHonestyAsync(
        OarWorkerClient client,
        String runtimeId,
        CancellationToken ct)
    {
        try
        {
            var el = await client.InstallationAsync(runtimeId, ct).ConfigureAwait(false);
            String? kind = null;
            String? via = null;
            String? reason = null;
            if (el.TryGetProperty("snapshot", out var snap) && snap.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                kind = snap.TryGetProperty("kind", out var k) ? k.GetString() : null;
                via = snap.TryGetProperty("via", out var v) ? v.GetString() : null;
                reason = snap.TryGetProperty("reason", out var r) ? r.GetString() : null;
            }
            else
            {
                kind = el.TryGetProperty("kind", out var k2) ? k2.GetString() : null;
                via = el.TryGetProperty("via", out var v2) ? v2.GetString() : null;
                reason = el.TryGetProperty("reason", out var r2) ? r2.GetString() : null;
            }

            if (String.Equals(kind, "available", StringComparison.OrdinalIgnoreCase)
                && String.Equals(via, "executable", StringComparison.OrdinalIgnoreCase))
            {
                return (kind, via, true, null);
            }

            if (String.Equals(kind, "available", StringComparison.OrdinalIgnoreCase)
                && String.Equals(via, "bundled", StringComparison.OrdinalIgnoreCase))
            {
                return (kind, via, false, "bundled（内置/非本机安装，不当作已装 Agent）");
            }

            if (String.Equals(kind, "not_found", StringComparison.OrdinalIgnoreCase))
                return (kind, via, false, "not_found（本机未安装）");

            if (String.Equals(kind, "unsupported", StringComparison.OrdinalIgnoreCase))
                return (kind, via, false, reason.IsNullOrEmpty() ? "unsupported" : $"unsupported: {reason}");

            return (kind, via, false, kind.IsNullOrEmpty() ? "installation 未返回 kind" : kind);
        }
        catch (Exception ex)
        {
            return (null, null, false, $"installation 探测失败: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<AgentTaskHandle> SubmitAsync(AgentTaskRequest request, CancellationToken ct = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.Prompt.IsNullOrWhiteSpace())
            return new AgentTaskHandle { Status = TaskStatus.Failed, TaskKey = String.Empty };

        var runtime = AgentHubDi.ResolveHost<DelegationRuntime>(_services);
        if (runtime == null)
            return FailHandle("AgentHub 运行时不可用（插件未正确加载）");

        if (AgentPolicy.IsForbiddenMode(request.PermissionMode))
            return FailHandle($"权限模式「{request.PermissionMode}」被禁止");

        var inner = new DelegationRequest
        {
            Prompt = request.Prompt,
            AgentId = request.AgentId is > 0 ? request.AgentId : null,
            Cwd = request.Cwd,
            PermissionMode = request.PermissionMode,
            SessionRef = request.SessionKey,
            SessionKey = request.SessionKey,
            Tag = request.Tag,
            CreatedBy = request.CreatedBy.IsNullOrEmpty() ? DefaultCreatedBy : request.CreatedBy,
        };

        // RuntimeId 选路：无 AgentId 时找带该 RuntimeId 的 Oar AccessPoint
        if (inner.AgentId is null && !request.RuntimeId.IsNullOrEmpty())
        {
            var registry = AgentHubDi.ResolveHost<IAgentRegistry>(_services);
            var hit = registry?.List(true).FirstOrDefault(a =>
                a.AccessPoints.Any(p =>
                    String.Equals(p.RuntimeId, request.RuntimeId, StringComparison.OrdinalIgnoreCase)));
            if (hit != null) inner.AgentId = hit.Id;
        }

        var dto = await runtime.CreateAsync(inner).ConfigureAwait(false);
        if (dto.Id <= 0 || dto.Status == TaskStatus.Failed)
            return FailHandle(dto.Message ?? "提交失败");

        var taskId = dto.Id;
        _ = Task.Run(async () =>
        {
            try { await runtime.RunAsync(taskId).ConfigureAwait(false); }
            catch (Exception ex)
            {
                XTrace.Log.Error("[AgentHubRuntime] 后台执行 {0}: {1}", taskId, ex.Message);
            }
        }, CancellationToken.None);

        return new AgentTaskHandle
        {
            TaskKey = dto.TaskKey,
            SessionKey = dto.SessionKey ?? dto.SessionRef ?? String.Empty,
            Status = dto.Status,
            RuntimeId = request.RuntimeId,
            AgentId = dto.AgentId,
        };
    }

    /// <inheritdoc />
    public Task<AgentTaskSnapshot?> GetTaskAsync(string taskKey, CancellationToken ct = default)
    {
        if (taskKey.IsNullOrEmpty()) return Task.FromResult<AgentTaskSnapshot?>(null);
        var runtime = AgentHubDi.ResolveHost<DelegationRuntime>(_services);
        var dto = runtime?.GetByKey(taskKey);
        return Task.FromResult(dto == null ? null : ToSnapshot(dto));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AgentTaskEvent> WatchTaskAsync(
        string taskKey,
        long afterSequence = 0,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var runtime = AgentHubDi.ResolveHost<DelegationRuntime>(_services);
        if (runtime == null || taskKey.IsNullOrEmpty()) yield break;

        var dto = runtime.GetByKey(taskKey);
        if (dto == null) yield break;

        var fromSeq = afterSequence > 0 ? (Int32)afterSequence : 0;
        var seen = new HashSet<Int32>();
        // 轮询事件直到终态或取消（最小面；SSE 仍由 tasks 控制器提供）
        for (var i = 0; i < 600 && !ct.IsCancellationRequested; i++)
        {
            dto = runtime.GetByKey(taskKey) ?? dto;
            var events = DelegationRuntime.ListEvents(dto.Id, fromSeq);
            foreach (var ev in events)
            {
                if (!seen.Add(ev.Seq)) continue;
                yield return new AgentTaskEvent
                {
                    AgentHubSequence = ev.Seq,
                    TaskKey = taskKey,
                    SessionKey = dto.SessionKey ?? dto.SessionRef,
                    Type = ev.Type ?? "event",
                    Timestamp = ev.Timestamp.Kind == DateTimeKind.Unspecified
                        ? new DateTimeOffset(ev.Timestamp, TimeSpan.Zero)
                        : ev.Timestamp,
                    PayloadJson = ev.PayloadJson,
                };
                fromSeq = Math.Max(fromSeq, ev.Seq + 1);
            }

            if (TaskStatus.IsTerminal(dto.Status)) yield break;
            try { await Task.Delay(200, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { yield break; }
        }
    }

    /// <inheritdoc />
    public async Task<AgentControlResult> SendInputAsync(string sessionKey, AgentInput input, CancellationToken ct = default)
    {
        if (sessionKey.IsNullOrEmpty())
            return AgentControlResult.Reject("session_required", "sessionKey 不能为空");
        if (input == null || input.Text.IsNullOrWhiteSpace())
            return AgentControlResult.Reject("input_required", "input.text 不能为空");

        var sessions = AgentHubDi.ResolveHost<AgentSessionManager>(_services);
        var session = sessions?.Find(sessionKey);
        var runtime = AgentHubDi.ResolveHost<DelegationRuntime>(_services);
        if (runtime == null)
            return AgentControlResult.Reject("runtime_unavailable", "AgentHub 运行时不可用");

        var agentId = session?.AgentId;
        if (agentId is null or <= 0)
        {
            // 允许用 SessionKey 直接当 SessionRef 续接（Cli 路径未必落 AgentHubSession）
            agentId = null;
        }

        var dto = await runtime.DelegateAsync(new DelegationRequest
        {
            Prompt = input.Text,
            AgentId = agentId is > 0 ? agentId : null,
            SessionRef = sessionKey,
            SessionKey = sessionKey,
            Cwd = session?.Cwd,
            CreatedBy = DefaultCreatedBy + ":input",
            Wait = true,
        }, ct).ConfigureAwait(false);

        if (dto.Status == TaskStatus.Succeeded)
            return AgentControlResult.Ok("ok", dto.ResultText);
        if (dto.Id <= 0)
            return AgentControlResult.Reject(dto.ErrorCode ?? "submit_failed", dto.Message ?? "投递失败");
        return AgentControlResult.Reject(dto.ErrorCode ?? dto.Status, dto.Message ?? dto.ResultText ?? dto.Status);
    }

    /// <inheritdoc />
    public async Task<AgentControlResult> CancelAsync(string taskKey, CancellationToken ct = default)
    {
        if (taskKey.IsNullOrEmpty())
            return AgentControlResult.Reject("task_required", "taskKey 不能为空");

        var runtime = AgentHubDi.ResolveHost<DelegationRuntime>(_services);
        var dto = runtime?.GetByKey(taskKey);
        if (runtime == null || dto == null)
            return AgentControlResult.Reject("not_found", $"任务 {taskKey} 不存在");
        if (TaskStatus.IsTerminal(dto.Status))
            return AgentControlResult.Ok("already_terminal", $"已是终态 {dto.Status}");

        var ok = await runtime.CancelAsync(dto.Id).ConfigureAwait(false);
        return ok
            ? AgentControlResult.Ok("cancelling", "已请求取消")
            : AgentControlResult.Reject("cancel_failed", "取消失败（可能已结束）");
    }

    /// <inheritdoc />
    public async Task CloseSessionAsync(string sessionKey, CancellationToken ct = default)
    {
        if (sessionKey.IsNullOrEmpty()) return;

        var sessions = AgentHubDi.ResolveHost<AgentSessionManager>(_services);
        var entity = sessions?.Find(sessionKey);

        var client = AgentHubDi.ResolveHost<OarWorkerClient>(_services);
        if (client != null && (client.IsAvailable || await client.EnsureStartedAsync(ct).ConfigureAwait(false)))
        {
            try { await client.DisposeSessionAsync(sessionKey, ct).ConfigureAwait(false); }
            catch (Exception ex)
            {
                XTrace.Log.Debug("[AgentHubRuntime] dispose session: {0}", ex.Message);
            }
        }

        sessions?.Close(sessionKey);
        if (entity != null)
            XTrace.Log.Info("[AgentHubRuntime] Session {0} 已关闭", sessionKey);
    }

    private static AgentTaskSnapshot ToSnapshot(DelegationTaskDto dto) => new()
    {
        TaskKey = dto.TaskKey,
        SessionKey = dto.SessionKey ?? dto.SessionRef ?? String.Empty,
        Status = dto.Status,
        Terminal = TaskStatus.IsTerminal(dto.Status),
        AgentHubSequence = dto.LastSeq,
        ErrorCode = dto.ErrorCode,
        ResultText = dto.ResultText,
    };

    private static AgentTaskHandle FailHandle(String message) => new()
    {
        TaskKey = String.Empty,
        SessionKey = String.Empty,
        Status = TaskStatus.Failed,
    };

    private static IReadOnlyList<String> BuildCapabilities(AgentDto a)
    {
        var caps = new List<String>();
        if (a.Capabilities.Supports(AgentFacets.Driving)) caps.Add("prompt");
        if (a.Capabilities.Supports(AgentFacets.Sessions)) caps.Add("session");
        if (a.AccessPoints.Any(p => p.CancelSupported)) caps.Add("abort");
        if (a.AccessPoints.Any(p => String.Equals(p.Transport, "Oar", StringComparison.OrdinalIgnoreCase)))
            caps.Add("oar");
        return caps;
    }
}

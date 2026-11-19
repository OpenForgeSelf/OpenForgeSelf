using System.Text;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AgentHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using XCode;

namespace ForgeSelf.Api.Plugins.AgentHub.Controllers;

/// <summary>
/// Session 管理面：列表 / 详情 / 输入 / abort / records / stream / 关闭。对外只暴露 SessionKey。
/// 路由前缀 <c>api/agent-hub/sessions</c>。
/// </summary>
[ApiController]
[Route("api/agent-hub/sessions")]
[Authorize("ApiKeyPolicy")]
public class AgentHubSessionsController : ControllerBase
{
    private readonly AgentSessionManager _sessions;
    private readonly IAgentRuntime _runtime;
    private readonly AgentRuntimeEventStore _eventStore;
    private readonly OarWorkerClient _worker;

    public AgentHubSessionsController(
        AgentSessionManager sessions,
        IAgentRuntime runtime,
        AgentRuntimeEventStore eventStore,
        OarWorkerClient worker)
    {
        _sessions = sessions;
        _runtime = runtime;
        _eventStore = eventStore;
        _worker = worker;
    }

    /// <summary>列出最近会话（默认最多 50；不含 OarSessionId）。</summary>
    [HttpGet]
    public ActionResult<ApiResponse<IReadOnlyList<SessionDto>>> List([FromQuery] Int32 limit = 50)
    {
        var take = Math.Clamp(limit, 1, 200);
        var entities = Entities.AgentHubSession.FindAll(
            null,
            Entities.AgentHubSession._.UpdateTime.Desc(),
            null, 0, take);
        var list = entities.Select(ToDto).ToList();
        return Ok(ApiResponse<IReadOnlyList<SessionDto>>.Ok(list, $"共 {list.Count} 条"));
    }

    /// <summary>按 SessionKey 取详情。</summary>
    [HttpGet("{sessionKey}")]
    public ActionResult<ApiResponse<SessionDto>> Get(String sessionKey)
    {
        var entity = _sessions.Find(sessionKey);
        return entity == null
            ? NotFound(ApiResponse<SessionDto>.Error($"会话 {sessionKey} 不存在", 404))
            : Ok(ApiResponse<SessionDto>.Ok(ToDto(entity)));
    }

    /// <summary>向会话投递输入（经 IAgentRuntime.SendInputAsync）。</summary>
    [HttpPost("{sessionKey}/input")]
    public async Task<ActionResult<ApiResponse<AgentControlResult>>> Input(
        String sessionKey,
        [FromBody] SessionInputRequest body,
        CancellationToken ct)
    {
        if (body == null || body.Text.IsNullOrWhiteSpace())
            return BadRequest(ApiResponse<AgentControlResult>.Error("text 不能为空", 400));

        var result = await _runtime.SendInputAsync(sessionKey, new AgentInput
        {
            Text = body.Text!,
            Mode = body.Mode,
        }, ct).ConfigureAwait(false);

        return result.Accepted
            ? Ok(ApiResponse<AgentControlResult>.Ok(result))
            : BadRequest(ApiResponse<AgentControlResult>.Error(result.Reason ?? result.Code ?? "rejected", 400));
    }

    /// <summary>中止会话侧当前活动（Worker session.control abort）。</summary>
    [HttpPost("{sessionKey}/abort")]
    public async Task<ActionResult<ApiResponse<AgentControlResult>>> Abort(String sessionKey, CancellationToken ct)
    {
        var entity = _sessions.Find(sessionKey);
        if (entity == null)
            return NotFound(ApiResponse<AgentControlResult>.Error($"会话 {sessionKey} 不存在", 404));

        // Worker 键：库内可能与 SessionKey 对齐；优先用对外 SessionKey 调 control
        var workerKey = entity.SessionKey;
        if (!_worker.IsAvailable && !await _worker.EnsureStartedAsync(ct).ConfigureAwait(false))
        {
            return StatusCode(503, ApiResponse<AgentControlResult>.Error(
                _worker.UnavailableReason ?? "OAR Worker 不可用", 503));
        }

        try
        {
            var result = await _worker.ControlAsync(workerKey, "abort", ct).ConfigureAwait(false);
            try
            {
                _eventStore.Append(sessionKey, "session.abort",
                    $"{{\"accepted\":{result.Accepted.ToString().ToLowerInvariant()},\"code\":\"{result.Code}\"}}");
            }
            catch { /* ignore persist */ }

            return result.Accepted
                ? Ok(ApiResponse<AgentControlResult>.Ok(result))
                : BadRequest(ApiResponse<AgentControlResult>.Error(result.Reason ?? result.Code ?? "rejected", 400));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<AgentControlResult>.Error(ex.Message, 400));
        }
    }

    /// <summary>列出会话脱敏 Record（本地 EventStore；可选合并 Worker session.records 摘要）。</summary>
    [HttpGet("{sessionKey}/records")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SessionRecordDto>>>> Records(
        String sessionKey,
        [FromQuery] Int64 afterSequence = 0,
        CancellationToken ct = default)
    {
        var entity = _sessions.Find(sessionKey);
        if (entity == null)
            return NotFound(ApiResponse<IReadOnlyList<SessionRecordDto>>.Error($"会话 {sessionKey} 不存在", 404));

        var local = _eventStore.List(sessionKey, afterSequence)
            .Select(r => new SessionRecordDto
            {
                AgentHubSequence = r.AgentHubSequence,
                OarSequence = r.OarSequence,
                Generation = r.Generation,
                Type = r.Type,
                PayloadJson = r.PayloadJson,
                Truncated = r.Truncated,
                Timestamp = r.Timestamp,
            })
            .ToList();

        // 若本地为空且 Worker 可用，尝试拉一次 Worker records 并落库（最小耐久补齐）
        if (local.Count == 0 && (_worker.IsAvailable || await _worker.EnsureStartedAsync(ct).ConfigureAwait(false)))
        {
            try
            {
                var el = await _worker.SessionRecordsAsync(sessionKey, ct).ConfigureAwait(false);
                _eventStore.Append(sessionKey, "session.records.snapshot", el.GetRawText());
                local = _eventStore.List(sessionKey, afterSequence)
                    .Select(r => new SessionRecordDto
                    {
                        AgentHubSequence = r.AgentHubSequence,
                        OarSequence = r.OarSequence,
                        Generation = r.Generation,
                        Type = r.Type,
                        PayloadJson = r.PayloadJson,
                        Truncated = r.Truncated,
                        Timestamp = r.Timestamp,
                    })
                    .ToList();
            }
            catch
            {
                /* Worker 无会话时忽略，返回本地空列表 */
            }
        }

        return Ok(ApiResponse<IReadOnlyList<SessionRecordDto>>.Ok(local, $"共 {local.Count} 条"));
    }

    /// <summary>
    /// 会话 Record SSE（最小面：轮询 EventStore 增量；非统一 Watch 重写）。
    /// </summary>
    [HttpGet("{sessionKey}/stream")]
    public async Task Stream(String sessionKey, [FromQuery] Int64 fromSeq = 0, CancellationToken ct = default)
    {
        var entity = _sessions.Find(sessionKey);
        if (entity == null)
        {
            Response.StatusCode = 404;
            return;
        }

        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";

        var sent = fromSeq;
        foreach (var rec in _eventStore.List(sessionKey, fromSeq))
        {
            await WriteRecordAsync(rec, ct).ConfigureAwait(false);
            sent = rec.AgentHubSequence;
        }

        var lastKeepAlive = DateTime.Now;
        while (!ct.IsCancellationRequested)
        {
            entity = _sessions.Find(sessionKey);
            if (entity == null) break;

            foreach (var rec in _eventStore.List(sessionKey, sent))
            {
                await WriteRecordAsync(rec, ct).ConfigureAwait(false);
                sent = rec.AgentHubSequence;
            }

            if (String.Equals(entity.Status, Entities.AgentHubSession.SessionStatus.Closed, StringComparison.OrdinalIgnoreCase)
                || String.Equals(entity.Status, Entities.AgentHubSession.SessionStatus.Interrupted, StringComparison.OrdinalIgnoreCase))
            {
                var done = JsonSerializer.Serialize(new { sessionKey, status = entity.Status, lastSeq = sent }, ForgeJson.Default);
                await WriteRawAsync("event: done\ndata: " + done + "\n\n", ct).ConfigureAwait(false);
                break;
            }

            if ((DateTime.Now - lastKeepAlive).TotalSeconds >= 15)
            {
                await WriteRawAsync(": keep-alive\n\n", ct).ConfigureAwait(false);
                lastKeepAlive = DateTime.Now;
            }

            try { await Task.Delay(500, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>关闭会话。</summary>
    [HttpPost("{sessionKey}/close")]
    public async Task<ActionResult<ApiResponse>> Close(String sessionKey, CancellationToken ct)
    {
        await _runtime.CloseSessionAsync(sessionKey, ct).ConfigureAwait(false);
        return Ok(ApiResponse.Ok($"会话 {sessionKey} 已关闭"));
    }

    private async Task WriteRecordAsync(Entities.AgentRuntimeRecord rec, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            agentHubSequence = rec.AgentHubSequence,
            oarSequence = rec.OarSequence,
            generation = rec.Generation,
            type = rec.Type,
            payloadJson = rec.PayloadJson,
            truncated = rec.Truncated,
            timestamp = rec.Timestamp,
        }, ForgeJson.Default);
        await WriteRawAsync($"id: {rec.AgentHubSequence}\nevent: record\ndata: {payload}\n\n", ct).ConfigureAwait(false);
    }

    private async Task WriteRawAsync(String chunk, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(chunk);
        await Response.Body.WriteAsync(bytes, ct).ConfigureAwait(false);
        await Response.Body.FlushAsync(ct).ConfigureAwait(false);
    }

    private static SessionDto ToDto(Entities.AgentHubSession e) => new()
    {
        SessionKey = e.SessionKey,
        AgentId = e.AgentId,
        AccessPointId = e.AccessPointId,
        Transport = e.Transport,
        RuntimeId = e.RuntimeId,
        Cwd = e.Cwd,
        Model = e.Model,
        Status = e.Status,
        Generation = e.Generation,
        LastTaskId = e.LastTaskId,
        AgentHubSequence = e.AgentHubSequence,
        OarSequence = e.OarSequence,
        CreateTime = e.CreateTime,
        UpdateTime = e.UpdateTime,
    };
}

/// <summary>会话对外 DTO（无 OarSessionId）。</summary>
public class SessionDto
{
    public String SessionKey { get; set; } = String.Empty;
    public Int32 AgentId { get; set; }
    public Int32 AccessPointId { get; set; }
    public String? Transport { get; set; }
    public String? RuntimeId { get; set; }
    public String? Cwd { get; set; }
    public String? Model { get; set; }
    public String? Status { get; set; }
    public Int32 Generation { get; set; }
    public Int32 LastTaskId { get; set; }
    public Int64 AgentHubSequence { get; set; }
    public Int64 OarSequence { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
}

/// <summary>会话输入请求体。</summary>
public class SessionInputRequest
{
    public String? Text { get; set; }
    public String? Mode { get; set; }
}

/// <summary>会话 Record DTO。</summary>
public class SessionRecordDto
{
    public Int64 AgentHubSequence { get; set; }
    public Int64 OarSequence { get; set; }
    public Int32 Generation { get; set; }
    public String Type { get; set; } = String.Empty;
    public String? PayloadJson { get; set; }
    public Boolean Truncated { get; set; }
    public DateTime Timestamp { get; set; }
}

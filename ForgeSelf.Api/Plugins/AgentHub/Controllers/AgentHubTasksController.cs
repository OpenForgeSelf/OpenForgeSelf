using System.Text;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AgentHub.Services;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;
using TaskStatus = ForgeSelf.Api.Plugins.AgentHub.Services.TaskStatus;

namespace ForgeSelf.Api.Plugins.AgentHub.Controllers;

/// <summary>
/// 委派任务接口：创建、查看、取消、事件流（SSE）、审批。
/// 路由前缀 <c>api/agent-hub/tasks</c>。
/// </summary>
[ApiController]
[Route("api/agent-hub/tasks")]
public class AgentHubTasksController : ControllerBase
{
    private readonly DelegationRuntime _runtime;
    private readonly PermissionBroker _permissions;

    public AgentHubTasksController(DelegationRuntime runtime, PermissionBroker permissions)
    {
        _runtime = runtime;
        _permissions = permissions;
    }

    /// <summary>任务列表</summary>
    /// <param name="status">按状态过滤（不传返回全部最近任务）</param>
    /// <param name="limit">条数上限</param>
    /// <returns>任务列表</returns>
    [HttpGet]
    public ActionResult<ApiResponse<IReadOnlyList<DelegationTaskDto>>> List(
        [FromQuery] String? status,
        [FromQuery] Int32 limit = 50)
    {
        var list = _runtime.List(status, limit);
        return Ok(ApiResponse<IReadOnlyList<DelegationTaskDto>>.Ok(list, $"共 {list.Count} 条"));
    }

    /// <summary>概览统计</summary>
    /// <returns>状态计数</returns>
    [HttpGet("stats")]
    public ActionResult<ApiResponse<Dictionary<String, Int32>>> Stats()
    {
        var stats = DelegationRuntime.Stats();
        stats["PendingApprovals"] = _permissions.PendingCount;
        return Ok(ApiResponse<Dictionary<String, Int32>>.Ok(stats));
    }

    /// <summary>取任务详情</summary>
    /// <param name="id">任务主键</param>
    /// <returns>任务</returns>
    [HttpGet("{id:int}")]
    public ActionResult<ApiResponse<DelegationTaskDto>> Get(Int32 id)
    {
        var task = _runtime.Get(id);
        return task == null
            ? NotFound(ApiResponse<DelegationTaskDto>.Error($"任务 #{id} 不存在", 404))
            : Ok(ApiResponse<DelegationTaskDto>.Ok(task));
    }

    /// <summary>按任务标识取详情</summary>
    /// <param name="taskKey">任务标识</param>
    /// <returns>任务</returns>
    [HttpGet("by-key/{taskKey}")]
    public ActionResult<ApiResponse<DelegationTaskDto>> GetByKey(String taskKey)
    {
        var task = _runtime.GetByKey(taskKey);
        return task == null
            ? NotFound(ApiResponse<DelegationTaskDto>.Error($"任务 {taskKey} 不存在", 404))
            : Ok(ApiResponse<DelegationTaskDto>.Ok(task));
    }

    /// <summary>创建并执行委派任务（异步；用 SSE 或轮询看进展）</summary>
    /// <param name="request">委派请求</param>
    /// <returns>任务（已入队）</returns>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<DelegationTaskDto>>> Create([FromBody] DelegationRequest request)
    {
        var task = await _runtime.CreateAsync(request);
        if (task.Status == TaskStatus.Failed)
            return BadRequest(ApiResponse<DelegationTaskDto>.Error(task.Message ?? "创建失败", 400));

        // 后台执行（不阻塞请求，UI 通过 SSE 观察）
        var taskId = task.Id;
        _ = Task.Run(async () =>
        {
            try
            {
                await _runtime.RunAsync(taskId);
            }
            catch (Exception ex)
            {
                XTrace.Log.Error("[AgentHub] 后台执行任务 {0} 异常: {1}", taskId, ex.Message);
            }
        });

        return Ok(ApiResponse<DelegationTaskDto>.Ok(task, $"任务 {task.TaskKey} 已入队"));
    }

    /// <summary>同步执行委派任务（等待完成，供调试与工具复用）</summary>
    /// <param name="request">委派请求</param>
    /// <returns>任务终态</returns>
    [HttpPost("run-sync")]
    public async Task<ActionResult<ApiResponse<DelegationTaskDto>>> RunSync([FromBody] DelegationRequest request)
    {
        var task = await _runtime.DelegateAsync(request);
        return Ok(ApiResponse<DelegationTaskDto>.Ok(task, $"任务 {task.Status}"));
    }

    /// <summary>取消任务（终止外部 agent 进程树）</summary>
    /// <param name="id">任务主键</param>
    /// <returns>操作结果</returns>
    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<ApiResponse>> Cancel(Int32 id)
    {
        var ok = await _runtime.CancelAsync(id);
        return ok
            ? Ok(ApiResponse.Ok("已请求取消"))
            : BadRequest(ApiResponse.Error("取消失败：任务不存在或已结束", 400));
    }

    /// <summary>取任务事件流（历史全量）</summary>
    /// <param name="id">任务主键</param>
    /// <param name="fromSeq">起始序号（含；用于断线续读 G1）</param>
    /// <returns>事件列表</returns>
    [HttpGet("{id:int}/events")]
    public ActionResult<ApiResponse<Object>> Events(Int32 id, [FromQuery] Int32 fromSeq = 0)
    {
        var events = DelegationRuntime.ListEvents(id, fromSeq)
            .Select(e => new { e.Seq, e.Type, e.PayloadJson, e.Truncated, e.Timestamp })
            .ToList();

        return Ok(ApiResponse<Object>.Ok(new { taskId = id, fromSeq, events }, $"共 {events.Count} 条事件"));
    }

    /// <summary>
    /// 任务事件流（SSE）。
    /// 支持 <c>?fromSeq=N</c> 断线续读（G1）：先补历史，再持续推送新增。
    /// </summary>
    /// <param name="id">任务主键</param>
    /// <param name="fromSeq">起始序号（含）</param>
    /// <param name="ct">取消令牌（客户端断开时触发）</param>
    /// <returns>SSE 流</returns>
    [HttpGet("{id:int}/stream")]
    public async Task Stream(Int32 id, [FromQuery] Int32 fromSeq = 0, CancellationToken ct = default)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";

        // 1) 先补历史（G1：断线重连不丢事件）
        var sent = fromSeq;
        foreach (var evt in DelegationRuntime.ListEvents(id, fromSeq))
        {
            await WriteEventAsync(evt.Seq, evt.Type, evt.PayloadJson, evt.Truncated, ct);
            sent = evt.Seq;
        }

        // 2) 轮询增量直到任务终态
        var lastKeepAlive = DateTime.Now;
        while (!ct.IsCancellationRequested)
        {
            var task = _runtime.Get(id);
            if (task == null) break;

            var fresh = DelegationRuntime.ListEvents(id, sent);
            foreach (var evt in fresh)
            {
                await WriteEventAsync(evt.Seq, evt.Type, evt.PayloadJson, evt.Truncated, ct);
                sent = evt.Seq;
            }

            if (TaskStatus.IsTerminal(task.Status))
            {
                // 终态：发一条 done 后收流，客户端据此关闭
                await WriteRawAsync("event: done\ndata: " + JsonSerializer.Serialize(new { taskId = id, status = task.Status, lastSeq = sent }) + "\n\n", ct);
                break;
            }

            // 心跳（防代理超时断流）
            if ((DateTime.Now - lastKeepAlive).TotalSeconds >= 15)
            {
                await WriteRawAsync(": keep-alive\n\n", ct);
                lastKeepAlive = DateTime.Now;
            }

            try
            {
                await Task.Delay(500, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>取任务当前待审批的权限申请</summary>
    /// <param name="id">任务主键</param>
    /// <returns>待审批列表</returns>
    [HttpGet("{id:int}/permissions")]
    public ActionResult<ApiResponse<Object>> PendingPermissions(Int32 id)
    {
        var pending = _permissions.ListPending(id)
            .Select(p => new { p.RequestId, p.Kind, p.Detail, p.RequestTime })
            .ToList();

        return Ok(ApiResponse<Object>.Ok(pending, $"共 {pending.Count} 项待审批"));
    }

    /// <summary>答复一个待审批的权限申请</summary>
    /// <param name="id">任务主键</param>
    /// <param name="requestId">申请 id</param>
    /// <param name="request">裁决请求</param>
    /// <returns>操作结果</returns>
    [HttpPost("{id:int}/permissions/{requestId}/resolve")]
    public ActionResult<ApiResponse> ResolvePermission(Int32 id, String requestId, [FromBody] PermissionResolveRequest request)
    {
        var ok = _permissions.Resolve(id, requestId, request.Allowed, request.Note);
        return ok
            ? Ok(ApiResponse.Ok(request.Allowed ? "已允许（仅本次）" : "已拒绝"))
            : NotFound(ApiResponse.Error("该权限申请不存在或已处理", 404));
    }

    /// <summary>写一条 SSE 事件</summary>
    private async Task WriteEventAsync(Int32 seq, String type, String? payload, Boolean truncated, CancellationToken ct)
    {
        var data = JsonSerializer.Serialize(new { seq, type, payload, truncated });
        await WriteRawAsync($"id: {seq}\nevent: {type}\ndata: {data}\n\n", ct);
    }

    /// <summary>写原始 SSE 文本</summary>
    private async Task WriteRawAsync(String text, CancellationToken ct)
    {
        try
        {
            await Response.WriteAsync(text, Encoding.UTF8, ct);
            await Response.Body.FlushAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // 客户端断开，正常结束
        }
        catch (Exception ex)
        {
            XTrace.Log.Debug("[AgentHub] SSE 写入中断: {0}", ex.Message);
        }
    }
}

/// <summary>权限裁决请求体。</summary>
public class PermissionResolveRequest
{
    /// <summary>是否允许</summary>
    public Boolean Allowed { get; set; }

    /// <summary>备注</summary>
    public String? Note { get; set; }
}

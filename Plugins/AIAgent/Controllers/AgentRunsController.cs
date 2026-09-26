using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Models;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AIAgent.Controllers;

/// <summary>
/// 计划驱动执行记录控制器（029，tasks.md T019，contracts/agent-runs-api.md）
/// </summary>
/// <remarks>
/// 提供计划驱动执行（Plan-Driven）的完整操作面：创建执行（SSE 流）、Run 列表/详情、
/// 恢复（resume）/重开（restart）/取消（cancel）/人工介入（skip/override）。
/// SSE 采用 text/event-stream + camelCase 序列化，对齐 AIChatController.WriteEventAsync 模式。
/// </remarks>
[ApiController]
[Route("api/ai-agent/runs")]
public class AgentRunsController : ControllerBase
{
    private readonly IRunOrchestratorService _orchestrator;

    /// <summary>SSE 事件序列化选项：camelCase（对齐前端事件/usage 字段）。</summary>
    private static readonly JsonSerializerOptions SseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AgentRunsController(IRunOrchestratorService orchestrator)
    {
        _orchestrator = orchestrator;
    }

    /// <summary>
    /// 创建计划驱动执行（SSE 流）：建 Run → 规划 → 逐步执行 → 终局合成。
    /// </summary>
    /// <param name="request">创建请求（taskInput 必填）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <response code="200">SSE 事件流（plan_created/step_started/step_completed/run_stuck/done/error）</response>
    /// <response code="400">taskInput 为空</response>
    [HttpPost]
    public async Task CreateRun([FromBody] RunRequest request, CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        async Task WriteEventAsync(object payload)
        {
            var sseData = JsonSerializer.Serialize(payload, SseJsonOptions);
            await Response.WriteAsync($"data: {sseData}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        try
        {
            if (string.IsNullOrWhiteSpace(request.TaskInput))
            {
                await WriteEventAsync(new { type = "error", content = "任务原文（taskInput）不能为空" });
                return;
            }

            XTrace.Log.Info("[AIAgentPlugin] 创建计划驱动执行，Agent: {0}, 任务长度: {1}, 工作流: {2}",
                request.AgentId, request.TaskInput.Length, request.WorkflowId);

            await foreach (var ev in _orchestrator.RunAsync(request, cancellationToken))
                await WriteSseEventAsync(ev, WriteEventAsync);
        }
        catch (OperationCanceledException)
        {
            XTrace.Log.Info("[AIAgentPlugin] 计划驱动执行请求被取消");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] 计划驱动执行失败: {0}", ex.Message);
            try
            {
                await WriteEventAsync(new { type = "error", content = ex.Message });
            }
            catch
            {
                // 响应可能已断开，忽略二次写入失败
            }
        }
    }

    /// <summary>
    /// 从当前步骤继续执行（重试语义，SSE 流）；仅 Status ∈ {Stuck, Failed} 允许。
    /// </summary>
    /// <param name="id">Run id</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <response code="200">SSE 事件流（同 POST /runs）</response>
    /// <response code="400">Run 不存在或状态不允许继续</response>
    [HttpPost("{id}/resume")]
    public async Task ResumeRun(long id, CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        async Task WriteEventAsync(object payload)
        {
            var sseData = JsonSerializer.Serialize(payload, SseJsonOptions);
            await Response.WriteAsync($"data: {sseData}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        try
        {
            var run = await _orchestrator.GetRunAsync(id);
            if (run == null)
            {
                await WriteEventAsync(new { type = "error", content = $"执行记录 {id} 不存在" });
                return;
            }
            if (run.Status is not (AgentRunStatus.Stuck or AgentRunStatus.Failed))
            {
                await WriteEventAsync(new { type = "error", content = $"当前状态 {run.Status} 不可恢复（仅 Stuck/Failed 可 resume）" });
                return;
            }

            XTrace.Log.Info("[AIAgentPlugin] 恢复计划驱动执行，Run: {0}, 当前状态: {1}", id, run.Status);

            await foreach (var ev in _orchestrator.ResumeAsync(id, cancellationToken))
                await WriteSseEventAsync(ev, WriteEventAsync);
        }
        catch (OperationCanceledException)
        {
            XTrace.Log.Info("[AIAgentPlugin] 恢复执行请求被取消，Run: {0}", id);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] 恢复执行失败（Run {0}）: {1}", id, ex.Message);
            try
            {
                await WriteEventAsync(new { type = "error", content = ex.Message });
            }
            catch
            {
                // 响应可能已断开，忽略二次写入失败
            }
        }
    }

    /// <summary>
    /// Run 列表（分页 + 状态过滤）。
    /// </summary>
    /// <param name="sessionId">关联会话 id（可选）</param>
    /// <param name="status">状态过滤（可选，camelCase 枚举字符串）</param>
    /// <param name="page">页码（从 1 开始）</param>
    /// <param name="pageSize">每页条数（1-100）</param>
    /// <response code="200">Run 分页列表（RetrieveTotalCount=true）</response>
    [HttpGet]
    public async Task<ActionResult<AgentRunListResponse>> ListRuns(
        [FromQuery] string? sessionId = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            AgentRunStatus? statusFilter = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<AgentRunStatus>(status, true, out var parsed))
                    return BadRequest(new { error = $"状态 {status} 非法（可选: pending/planning/running/completed/stuck/failed/cancelled）" });
                statusFilter = parsed;
            }

            XTrace.Log.Info("[AIAgentPlugin] 查询执行记录列表，SessionId: {0}, 状态: {1}, 页: {2}/{3}",
                sessionId ?? "(全部)", status ?? "(全部)", page, pageSize);

            var result = await _orchestrator.ListRunsAsync(sessionId, statusFilter, page, pageSize);
            return Ok(result);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] 查询执行记录列表失败: {0}", ex.Message);
            return StatusCode(500, new { error = "查询执行记录列表时发生错误", details = ex.Message });
        }
    }

    /// <summary>
    /// Run 详情（含步骤列表）。
    /// </summary>
    /// <param name="id">Run id</param>
    /// <response code="200">Run + 步骤列表</response>
    /// <response code="404">Run 不存在</response>
    [HttpGet("{id}")]
    public async Task<ActionResult<AgentRunDetailResponse>> GetRunDetail(long id)
    {
        try
        {
            var result = await _orchestrator.GetRunDetailAsync(id);
            if (result == null)
                return NotFound(new { error = $"执行记录 {id} 不存在" });

            return Ok(result);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] 查询执行记录详情失败（Run {0}）: {1}", id, ex.Message);
            return StatusCode(500, new { error = "查询执行记录详情时发生错误", details = ex.Message });
        }
    }

    /// <summary>
    /// 以同 Plan 新建 Run 从头执行。
    /// </summary>
    /// <param name="id">原 Run id</param>
    /// <response code="200">{ success, newRunId }</response>
    /// <response code="404">Run 不存在</response>
    /// <response code="400">原记录无 Plan，无法重开</response>
    [HttpPost("{id}/restart")]
    public async Task<ActionResult> RestartRun(long id)
    {
        try
        {
            XTrace.Log.Info("[AIAgentPlugin] 重开计划驱动执行，原 Run: {0}", id);

            var newRun = await _orchestrator.RestartAsync(id);
            if (newRun == null)
                return NotFound(new { error = $"执行记录 {id} 不存在" });

            return Ok(new { success = true, newRunId = newRun.Id });
        }
        catch (InvalidOperationException ex)
        {
            XTrace.Log.Warn("[AIAgentPlugin] 重开失败（Run {0}）: {1}", id, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] 重开失败（Run {0}）: {1}", id, ex.Message);
            return StatusCode(500, new { error = "重开执行记录时发生错误", details = ex.Message });
        }
    }

    /// <summary>
    /// 取消（将 Run 置 Cancelled，停止步骤推进）。
    /// </summary>
    /// <param name="id">Run id</param>
    /// <response code="200">{ success }</response>
    /// <response code="404">Run 不存在</response>
    /// <response code="400">Run 已到终态，不可取消</response>
    [HttpPost("{id}/cancel")]
    public async Task<ActionResult> CancelRun(long id)
    {
        try
        {
            XTrace.Log.Info("[AIAgentPlugin] 取消计划驱动执行，Run: {0}", id);

            var run = await _orchestrator.CancelAsync(id);
            if (run == null)
                return NotFound(new { error = $"执行记录 {id} 不存在" });

            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            XTrace.Log.Warn("[AIAgentPlugin] 取消失败（Run {0}）: {1}", id, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] 取消失败（Run {0}）: {1}", id, ex.Message);
            return StatusCode(500, new { error = "取消执行记录时发生错误", details = ex.Message });
        }
    }

    /// <summary>
    /// 人工介入某个卡住/待执行的步骤（skip 跳过 / override 人工补位）。
    /// </summary>
    /// <param name="id">Run id</param>
    /// <param name="index">步骤下标（从 0 开始）</param>
    /// <param name="request">介入请求体</param>
    /// <response code="200">{ success, stepIndex, status }</response>
    /// <response code="400">action 非法 / 步骤非 Pending|Stuck / Run 终态 / 步骤不存在</response>
    [HttpPatch("{id}/steps/{index}")]
    public async Task<ActionResult> Intervene(long id, int index, [FromBody] InterveneRequest request)
    {
        try
        {
            if (request.Action is not ("skip" or "override"))
                return BadRequest(new { error = $"介入动作 {request.Action} 非法（仅 skip/override）" });

            XTrace.Log.Info("[AIAgentPlugin] 人工介入执行记录，Run: {0}, 步骤: {1}, 动作: {2}", id, index, request.Action);

            var run = await _orchestrator.InterveneAsync(id, index, request);
            if (run == null)
                return NotFound(new { error = $"执行记录 {id} 不存在" });

            return Ok(new { success = true, stepIndex = index, status = run.Status.ToString().ToLowerInvariant() });
        }
        catch (InvalidOperationException ex)
        {
            XTrace.Log.Warn("[AIAgentPlugin] 人工介入失败（Run {0} 步骤 {1}）: {2}", id, index, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIAgentPlugin] 人工介入失败（Run {0} 步骤 {1}）: {2}", id, index, ex.Message);
            return StatusCode(500, new { error = "人工介入时发生错误", details = ex.Message });
        }
    }

    /// <summary>统一 SSE 事件透传：完整载荷（type/content）+ runId 便于前端关联。</summary>
    private static async Task WriteSseEventAsync(AgentLoopEvent ev, Func<object, Task> writeEventAsync)
    {
        switch (ev.Type)
        {
            case "plan_created":
            case "step_started":
            case "step_completed":
            case "run_stuck":
            case "done":
                await writeEventAsync(new { type = ev.Type, payload = ev.Content });
                break;
            case "content":
                await writeEventAsync(new { type = "content", content = ev.Content });
                break;
            case "tool_call":
                await writeEventAsync(new { type = "tool_call", name = ev.Name, arguments = ev.Arguments });
                break;
            case "tool_result":
                await writeEventAsync(new { type = "tool_result", name = ev.Name, result = ev.Result, success = ev.Success });
                break;
            case "usage":
                await writeEventAsync(new { type = "usage", usage = ev.Usage });
                break;
            default:
                await writeEventAsync(new { type = ev.Type, content = ev.Content });
                break;
        }
    }
}

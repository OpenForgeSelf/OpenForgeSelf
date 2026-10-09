using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.TodoTracker.Models;
using ForgeSelf.Api.Plugins.TodoTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;
// ApiResponse 来自 Abstractions，那里另有一个 PagedResult<T>；本插件分页壳一直是 Models.PagedResult<T>，
// 用命名空间别名钉死（开放泛型 using 别名在本工程语言版本下不可用，实测 CS1002）。
using TodoModels = ForgeSelf.Api.Plugins.TodoTracker.Models;

namespace ForgeSelf.Api.Plugins.TodoTracker.Controllers;

/// <summary>
/// 执行记录接口（PILOT-054 · FR-4/FR-5）：<c>api/todos/{id}/records</c> 与 agent 面
/// <c>api/todos/by-key/{taskKey}/records</c>。
///
/// 记录是 <b>append-only</b>：只有 POST/GET，没有 PUT/DELETE —— 「做了什么、结果如何」不允许事后被改写，
/// 否则台账就失去了作为验收证据的资格（AGENTS §10：Evidence 只记实际发生的事）。
/// </summary>
[ApiController]
[Authorize("ApiKeyPolicy")]
[Route("api/todos")]
public class TaskExecutionsController : ControllerBase
{
    private readonly ITodoService _todos;
    private readonly ITaskExecutionService _records;
    private readonly ITodoDispatchService _dispatch;

    public TaskExecutionsController(ITodoService todos, ITaskExecutionService records, ITodoDispatchService dispatch)
    {
        _todos = todos;
        _records = records;
        _dispatch = dispatch;
    }

    /// <summary>读某任务的执行记录（按 Seq 升序分页）。</summary>
    [HttpGet("{id:int}/records")]
    public async Task<ActionResult<ApiResponse<TodoModels.PagedResult<TaskExecutionDto>>>> List(int id,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        try
        {
            if (id <= 0) return BadRequest(ApiResponse<TodoModels.PagedResult<TaskExecutionDto>>.Error("id 必须为正整数", 400));

            var result = await _records.ListAsync(id, page, pageSize);
            return Ok(ApiResponse<TodoModels.PagedResult<TaskExecutionDto>>.Ok(result, "获取执行记录成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取执行记录失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<TodoModels.PagedResult<TaskExecutionDto>>.Error("获取执行记录失败: " + ex.Message));
        }
    }

    /// <summary>追加一条执行记录（可同批流转状态）。</summary>
    [HttpPost("{id:int}/records")]
    public Task<ActionResult<ApiResponse<TodoDto>>> Append(int id, [FromBody] CreateTaskExecutionRequest request) =>
        AppendInternal(id, request);

    /// <summary>追加执行记录（按外部键，agent 回报面）。</summary>
    [HttpPost("by-key/{taskKey}/records")]
    public async Task<ActionResult<ApiResponse<TodoDto>>> AppendByKey(string taskKey, [FromBody] CreateTaskExecutionRequest request)
    {
        var todo = await _todos.GetTodoByKeyAsync(taskKey);
        return todo == null
            ? NotFound(ApiResponse<TodoDto>.Error($"任务不存在：{taskKey}", 404))
            : await AppendInternal(todo.Id, request);
    }

    /// <summary>读自己被派的任务全文（含下发提示词），agent 领完家用一次调用拿齐。</summary>
    [HttpGet("by-key/{taskKey}/dispatch")]
    public async Task<ActionResult<ApiResponse<DispatchPreviewDto>>> PreviewByKey(string taskKey)
    {
        try
        {
            var preview = await _dispatch.PreviewByKeyAsync(taskKey, $"{Request.Scheme}://{Request.Host}");
            return preview == null
                ? NotFound(ApiResponse<DispatchPreviewDto>.Error($"任务不存在：{taskKey}", 404))
                : Ok(ApiResponse<DispatchPreviewDto>.Ok(preview, "下发内容已读取"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("读取任务下发内容失败 [{0}]: {1}", taskKey, ex.Message);
            return StatusCode(500, ApiResponse<DispatchPreviewDto>.Error("读取任务下发内容失败: " + ex.Message));
        }
    }

    private async Task<ActionResult<ApiResponse<TodoDto>>> AppendInternal(int id, CreateTaskExecutionRequest request)
    {
        try
        {
            if (request == null) return BadRequest(ApiResponse<TodoDto>.Error("请求体不能为空", 400));
            if (string.IsNullOrWhiteSpace(request.Action))
                return BadRequest(ApiResponse<TodoDto>.Error("action（做了什么操作）不能为空", 400));

            // PILOT-057B 编码止损：回报体出现 U+FFFD（替换符）说明源头编码已坏（GBK 字节被当 UTF-8 解），
            // 落库前记 warning 供排查，不阻断写入（历史数据不修，新数据靠 ASCII 模板规避）。
            if (HasReplacementChar(request))
            {
                XTrace.Log.Warn("执行记录回报含 U+FFFD 替换符（编码已损坏）：todo={0} actor={1}", id, request.Actor);
            }

            var result = await _records.AppendAsync(id, request, ActorHint());

            // PILOT-057B 回报优先：agent 回报「已进待验收」即视为委派完成，回调 AgentHub 标记完成
            // （否则 AgentHub 等进程退出，opencode 这类进程不退出时会被 600s 超时打成 Failed · exit -1 · timeout）。
            // 标记失败只记日志，不阻断回报响应；下次轮询仍可看到真实状态。
            if (result.Ok && result.Data != null && IsReviewTransition(request))
            {
                var marked = await _dispatch.MarkDelegationCompleteAsync(id);
                if (!marked.Success)
                {
                    XTrace.Log.Warn("[todo-tracker] 委派完成标记未生效（todo={0}）：{1}", id, marked.Error);
                }
            }

            if (result.Ok && result.Data != null)
                return Ok(ApiResponse<TodoDto>.Ok(result.Data, "执行记录已写入"));

            var reason = result.Error ?? "写入执行记录失败";
            return result.StatusCode switch
            {
                404 => NotFound(ApiResponse<TodoDto>.Error(reason, 404)),
                409 => Conflict(ApiResponse<TodoDto>.Error(reason, 409)),
                _ => BadRequest(ApiResponse<TodoDto>.Error(reason, 400))
            };
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("写入执行记录失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<TodoDto>.Error("写入执行记录失败: " + ex.Message));
        }
    }

    /// <summary>本次回报是否把任务推进到「待验收」（Review）—— 只有判成功才回调完成标记。</summary>
    private static bool IsReviewTransition(CreateTaskExecutionRequest request)
    {
        var stage = request.StageTo?.Trim();
        if (string.IsNullOrEmpty(stage)) return false;
        if (stage.Equals("Review", StringComparison.OrdinalIgnoreCase)) return true;
        return int.TryParse(stage, out var n) && n == TodoStage.Review;
    }

    /// <summary>回报体是否含 U+FFFD 替换符（编码损坏信号）。</summary>
    private static bool HasReplacementChar(CreateTaskExecutionRequest request)
    {
        const char Fffd = '\uFFFD';
        foreach (var value in new[] { request.Action, request.Detail, request.Result, request.Verification,
                                       request.Risks, request.Residuals, request.Evidence, request.Actor })
        {
            if (!string.IsNullOrEmpty(value) && value!.IndexOf(Fffd) >= 0) return true;
        }
        return false;
    }

    /// <summary>调用方标识：优先取请求头 <c>X-Actor</c>（agent 回报时写自己的名字），缺省记 <c>rest</c>。</summary>
    private string ActorHint()
    {
        var header = Request.Headers["X-Actor"].ToString();
        return string.IsNullOrWhiteSpace(header) ? "rest" : header.Trim();
    }
}

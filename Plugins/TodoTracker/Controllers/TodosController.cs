using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.TodoTracker.Models;
using ForgeSelf.Api.Plugins.TodoTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;
// ApiResponse 来自 Abstractions，那里另有一个 PagedResult<T>（UsageStats 用）；本插件的分页壳一直是
// Models.PagedResult<T>（Home 面板按它的形状解析 items/total）。用命名空间别名把两者钉死，
// 免得二义性悄悄换掉出参形状（开放泛型 using 别名在本工程语言版本下不可用，实测 CS1002）。
using TodoModels = ForgeSelf.Api.Plugins.TodoTracker.Models;

namespace ForgeSelf.Api.Plugins.TodoTracker.Controllers;

/// <summary>
/// 任务（待办）主接口 <c>api/todos</c>。
///
/// 兼容承诺（PILOT-054）：既有 7 个端点的路径、方法、封套与错误文案<b>全部保持</b>
/// （<c>Plugins/Home/web/src/homeStore.ts</c> 与 <c>e2e/todo.spec.ts</c> 在直接消费）。
/// 新增能力一律挂在 <c>{id:int}/…</c> 与 <c>by-key/{taskKey}/…</c> 子模板上，用 <c>:int</c> 约束避免与
/// <c>projects</c>/<c>artifact-sets</c> 这类字面段抢路由。
///
/// 鉴权（铁律 17）：宿主没有全局鉴权中间件，管理面必须<b>逐控制器</b>声明
/// <c>[Authorize("ApiKeyPolicy")]</c>；本插件全部控制器都带，守卫用例见
/// <c>ForgeSelf.Api.Tests/Plugins/TodoTracker/TodoTrackerAuthTests.cs</c>。
/// </summary>
[ApiController]
[Authorize("ApiKeyPolicy")]
[Route("api/todos")]
public class TodosController : ControllerBase
{
    private readonly ITodoService _todoService;
    private readonly ITodoDispatchService _dispatch;

    public TodosController(ITodoService todoService, ITodoDispatchService dispatch)
    {
        _todoService = todoService;
        _dispatch = dispatch;
    }

    /// <summary>列表（旧三参形状继续可用，新增 stage/projectId/q 过滤）。</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<TodoModels.PagedResult<TodoDto>>>> GetTodos(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? stage = null,
        [FromQuery] int projectId = 0,
        [FromQuery] string? q = null)
    {
        try
        {
            XTrace.Log.Info("获取待办列表，status={0}, stage={1}, projectId={2}, page={3}", status, stage, projectId, page);

            if (!string.IsNullOrEmpty(status) && status != TodoStatus.PendingName && status != TodoStatus.CompletedName)
                return BadRequest(ApiResponse<TodoModels.PagedResult<TodoDto>>.Error(
                    $"status 参数无效：{status}，仅支持 {TodoStatus.PendingName} 或 {TodoStatus.CompletedName}", 400));

            int? stageValue = null;
            if (!string.IsNullOrEmpty(stage))
            {
                if (!TodoStage.TryParse(stage, out var parsed))
                    return BadRequest(ApiResponse<TodoModels.PagedResult<TodoDto>>.Error(
                        $"stage 参数无效：{stage}，可用值 {string.Join("/", TodoStage.All.Select(TodoStage.ToName))}", 400));
                stageValue = parsed;
            }

            if (projectId < 0)
                return BadRequest(ApiResponse<TodoModels.PagedResult<TodoDto>>.Error("projectId 不能为负数", 400));

            var result = await _todoService.GetTodosAsync(new TaskQuery
            {
                Status = status,
                Stage = stageValue,
                ProjectId = projectId,
                Keyword = q,
                Page = page,
                PageSize = pageSize
            });

            return Ok(ApiResponse<TodoModels.PagedResult<TodoDto>>.Ok(result, "获取待办列表成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取待办列表失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<TodoModels.PagedResult<TodoDto>>.Error("获取待办列表失败: " + ex.Message));
        }
    }

    /// <summary>详情。</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<TodoDto>>> GetTodoById(int id)
    {
        try
        {
            if (id <= 0) return BadRequest(ApiResponse<TodoDto>.Error("id 必须为正整数", 400));

            var todo = await _todoService.GetTodoByIdAsync(id);
            return todo == null
                ? NotFound(ApiResponse<TodoDto>.Error("待办不存在", 404))
                : Ok(ApiResponse<TodoDto>.Ok(todo, "获取待办详情成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取待办详情失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<TodoDto>.Error("获取待办详情失败: " + ex.Message));
        }
    }

    /// <summary>按外部键取详情（agent 侧引用的是 taskKey，不是自增 id）。</summary>
    [HttpGet("by-key/{taskKey}")]
    public async Task<ActionResult<ApiResponse<TodoDto>>> GetTodoByKey(string taskKey)
    {
        try
        {
            var todo = await _todoService.GetTodoByKeyAsync(taskKey);
            return todo == null
                ? NotFound(ApiResponse<TodoDto>.Error($"任务不存在：{taskKey}", 404))
                : Ok(ApiResponse<TodoDto>.Ok(todo, "获取待办详情成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("按 key 获取待办失败 [{0}]: {1}", taskKey, ex.Message);
            return StatusCode(500, ApiResponse<TodoDto>.Error("获取待办详情失败: " + ex.Message));
        }
    }

    /// <summary>创建。</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<TodoDto>>> CreateTodo([FromBody] CreateTodoRequest request)
    {
        try
        {
            if (request == null) return BadRequest(ApiResponse<TodoDto>.Error("请求体不能为空", 400));
            if (string.IsNullOrWhiteSpace(request.Title)) return BadRequest(ApiResponse<TodoDto>.Error("标题不能为空", 400));
            if (request.Title.Length > 200) return BadRequest(ApiResponse<TodoDto>.Error("标题长度不能超过 200 字符", 400));
            if (request.Remark != null && request.Remark.Length > 1000)
                return BadRequest(ApiResponse<TodoDto>.Error("备注长度不能超过 1000 字符", 400));
            if (!string.IsNullOrWhiteSpace(request.Stage) && !TodoStage.TryParse(request.Stage, out _))
                return BadRequest(ApiResponse<TodoDto>.Error(
                    $"stage 参数无效：{request.Stage}，可用值 {string.Join("/", TodoStage.All.Select(TodoStage.ToName))}", 400));
            if (!string.IsNullOrWhiteSpace(request.ProjectPath) && !ProjectPathCanonicalizer.Normalize(request.ProjectPath).Ok)
            {
                var error = ProjectPathCanonicalizer.Normalize(request.ProjectPath).Error;
                return BadRequest(ApiResponse<TodoDto>.Error($"项目路径无法解析：{error}", 400));
            }

            var todo = await _todoService.CreateTodoAsync(request);
            return StatusCode(201, ApiResponse<TodoDto>.Ok(todo, "创建待办成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("创建待办参数错误: {0}", ex.Message);
            return BadRequest(ApiResponse<TodoDto>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("创建待办失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<TodoDto>.Error("创建待办失败: " + ex.Message));
        }
    }

    /// <summary>部分更新（null=不改，空串=清空）。状态流转请走 <c>/{id}/stage</c>，这里改不了阶段。</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<TodoDto>>> UpdateTodo(int id, [FromBody] UpdateTodoRequest request)
    {
        try
        {
            if (id <= 0) return BadRequest(ApiResponse<TodoDto>.Error("id 必须为正整数", 400));
            if (request == null) return BadRequest(ApiResponse<TodoDto>.Error("请求体不能为空", 400));
            if (request.Title != null)
            {
                if (string.IsNullOrWhiteSpace(request.Title)) return BadRequest(ApiResponse<TodoDto>.Error("标题不能为空", 400));
                if (request.Title.Length > 200) return BadRequest(ApiResponse<TodoDto>.Error("标题长度不能超过 200 字符", 400));
            }
            if (request.Remark != null && request.Remark.Length > 1000)
                return BadRequest(ApiResponse<TodoDto>.Error("备注长度不能超过 1000 字符", 400));

            var todo = await _todoService.UpdateTodoAsync(id, request);
            return todo == null
                ? NotFound(ApiResponse<TodoDto>.Error("待办不存在", 404))
                : Ok(ApiResponse<TodoDto>.Ok(todo, "更新待办成功"));
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn("更新待办参数错误 [{0}]: {1}", id, ex.Message);
            return BadRequest(ApiResponse<TodoDto>.Error(ex.Message, 400));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("更新待办失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<TodoDto>.Error("更新待办失败: " + ex.Message));
        }
    }

    /// <summary>删除。</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTodo(int id)
    {
        try
        {
            if (id <= 0) return BadRequest(ApiResponse.Error("id 必须为正整数", 400));

            var success = await _todoService.DeleteTodoAsync(id);
            return success ? NoContent() : NotFound(ApiResponse.Error("待办不存在", 404));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("删除待办失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse.Error("删除待办失败: " + ex.Message));
        }
    }

    /// <summary>标记完成（旧端点，内部同步 Stage=Done）。</summary>
    [HttpPost("{id:int}/complete")]
    public async Task<ActionResult<ApiResponse<TodoDto>>> CompleteTodo(int id)
    {
        try
        {
            if (id <= 0) return BadRequest(ApiResponse<TodoDto>.Error("id 必须为正整数", 400));

            var todo = await _todoService.CompleteTodoAsync(id, ActorHint());
            return todo == null
                ? NotFound(ApiResponse<TodoDto>.Error("待办不存在", 404))
                : Ok(ApiResponse<TodoDto>.Ok(todo, "标记完成成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("标记待办完成失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<TodoDto>.Error("标记完成失败: " + ex.Message));
        }
    }

    /// <summary>重新打开（旧端点，内部回到 Draft 草稿态）。</summary>
    [HttpPost("{id:int}/reopen")]
    public async Task<ActionResult<ApiResponse<TodoDto>>> ReopenTodo(int id)
    {
        try
        {
            if (id <= 0) return BadRequest(ApiResponse<TodoDto>.Error("id 必须为正整数", 400));

            var todo = await _todoService.ReopenTodoAsync(id, ActorHint());
            return todo == null
                ? NotFound(ApiResponse<TodoDto>.Error("待办不存在", 404))
                : Ok(ApiResponse<TodoDto>.Ok(todo, "重新打开成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("重新打开待办失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<TodoDto>.Error("重新打开失败: " + ex.Message));
        }
    }

    /// <summary>状态流转（合法边见 <see cref="TodoStage"/>；非法 409 并列出可达目标）。</summary>
    [HttpPost("{id:int}/stage")]
    public async Task<ActionResult<ApiResponse<TodoDto>>> ChangeStage(int id, [FromBody] StageChangeRequest request) =>
        MapTodoResult(await _todoService.ChangeStageAsync(id, request, ActorHint()));

    /// <summary>状态流转（按外部键，agent 回报面）。</summary>
    [HttpPost("by-key/{taskKey}/stage")]
    public async Task<ActionResult<ApiResponse<TodoDto>>> ChangeStageByKey(string taskKey, [FromBody] StageChangeRequest request) =>
        MapTodoResult(await _todoService.ChangeStageByKeyAsync(taskKey, request, ActorHint()));

    /// <summary>关联/换关联项目（路径任何写法皆可，服务端归一后匹配宿主档案）。</summary>
    [HttpPost("{id:int}/project")]
    public async Task<ActionResult<ApiResponse<TodoDto>>> LinkProject(int id, [FromBody] LinkProjectRequest request)
    {
        var result = await _todoService.LinkProjectAsync(id, request);
        return MapTodoResult(result);
    }

    /// <summary>解除项目关联（只清任务上的投影字段，<b>不删</b>宿主项目档案）。</summary>
    [HttpDelete("{id:int}/project")]
    public async Task<ActionResult<ApiResponse<TodoDto>>> UnlinkProject(int id)
    {
        var result = await _todoService.UnlinkProjectAsync(id);
        return MapTodoResult(result);
    }

    /// <summary>下发预览（提示词 + 载荷 + 缺口 + 委派可用性）。</summary>
    [HttpGet("{id:int}/dispatch")]
    public async Task<ActionResult<ApiResponse<DispatchPreviewDto>>> PreviewDispatch(int id)
    {
        try
        {
            var preview = await _dispatch.PreviewAsync(id, BaseUrl());
            return preview == null
                ? NotFound(ApiResponse<DispatchPreviewDto>.Error("待办不存在", 404))
                : Ok(ApiResponse<DispatchPreviewDto>.Ok(preview, "下发预览已生成"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("生成下发预览失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<DispatchPreviewDto>.Error("生成下发预览失败: " + ex.Message));
        }
    }

    private ActionResult<ApiResponse<TodoDto>> MapTodoResult(TodoOpResult result)
    {
        if (result.Ok)
        {
            if (result.Data == null) return NoContent();
            return Ok(ApiResponse<TodoDto>.Ok(result.Data, result.Warning ?? "操作成功"));
        }

        var envelope = ApiResponse<TodoDto>.Error(result.Error ?? "操作失败", result.StatusCode);
        return result.StatusCode switch
        {
            404 => NotFound(envelope),
            409 => Conflict(envelope),
            503 => StatusCode(StatusCodes.Status503ServiceUnavailable, envelope),
            _ => BadRequest(envelope)
        };
    }

    /// <summary>调用方标识（进执行记录的 actor）：带 Bearer 的调用无法在此解析出用户名，
    /// 先取请求头 <c>X-Actor</c>，缺省记 <c>rest</c>；工具函数面会显式传 <c>agent:&lt;name&gt;</c>。</summary>
    private string ActorHint()
    {
        var header = Request.Headers["X-Actor"].ToString();
        return string.IsNullOrWhiteSpace(header) ? "rest" : header.Trim();
    }

    /// <summary>当前请求的基址，供提示词里的回报契约拼出真实可访问地址。</summary>
    private string? BaseUrl() => $"{Request.Scheme}://{Request.Host}";
}

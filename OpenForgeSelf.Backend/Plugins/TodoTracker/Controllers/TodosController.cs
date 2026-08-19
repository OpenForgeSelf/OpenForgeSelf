using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Backend.Plugins.TodoTracker.Models;
using OpenForgeSelf.Backend.Plugins.TodoTracker.Services;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.TodoTracker.Controllers;

[ApiController]
[Route("api/todos")]
public class TodosController : ControllerBase
{
    private readonly ITodoService _todoService;

    public TodosController(ITodoService todoService)
    {
        _todoService = todoService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<OpenForgeSelf.Backend.Plugins.TodoTracker.Models.PagedResult<TodoDto>>>> GetTodos(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            XTrace.Log.Info("获取待办列表，status={0}, page={1}, pageSize={2}", status, page, pageSize);

            if (!string.IsNullOrEmpty(status) && status != "Pending" && status != "Completed")
            {
                return BadRequest(ApiResponse<OpenForgeSelf.Backend.Plugins.TodoTracker.Models.PagedResult<TodoDto>>.Error(
                    "status 参数无效，仅支持 Pending 或 Completed", 400));
            }

            var result = await _todoService.GetTodosAsync(status, page, pageSize);
            return Ok(ApiResponse<OpenForgeSelf.Backend.Plugins.TodoTracker.Models.PagedResult<TodoDto>>.Ok(result, "获取待办列表成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取待办列表失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<OpenForgeSelf.Backend.Plugins.TodoTracker.Models.PagedResult<TodoDto>>.Error("获取待办列表失败: " + ex.Message));
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<TodoDto>>> GetTodoById(int id)
    {
        try
        {
            XTrace.Log.Info("获取待办详情，id={0}", id);

            if (id <= 0)
            {
                return BadRequest(ApiResponse<TodoDto>.Error("id 必须为正整数", 400));
            }

            var todo = await _todoService.GetTodoByIdAsync(id);
            if (todo == null)
            {
                return NotFound(ApiResponse<TodoDto>.Error("待办不存在", 404));
            }

            return Ok(ApiResponse<TodoDto>.Ok(todo, "获取待办详情成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取待办详情失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<TodoDto>.Error("获取待办详情失败: " + ex.Message));
        }
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<TodoDto>>> CreateTodo([FromBody] CreateTodoRequest request)
    {
        try
        {
            XTrace.Log.Info("创建待办，Title={0}", request?.Title);

            if (request == null)
            {
                return BadRequest(ApiResponse<TodoDto>.Error("请求体不能为空", 400));
            }

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest(ApiResponse<TodoDto>.Error("标题不能为空", 400));
            }

            if (request.Title.Length > 200)
            {
                return BadRequest(ApiResponse<TodoDto>.Error("标题长度不能超过 200 字符", 400));
            }

            if (request.Remark != null && request.Remark.Length > 1000)
            {
                return BadRequest(ApiResponse<TodoDto>.Error("备注长度不能超过 1000 字符", 400));
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

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<TodoDto>>> UpdateTodo(int id, [FromBody] UpdateTodoRequest request)
    {
        try
        {
            XTrace.Log.Info("更新待办，id={0}", id);

            if (id <= 0)
            {
                return BadRequest(ApiResponse<TodoDto>.Error("id 必须为正整数", 400));
            }

            if (request == null)
            {
                return BadRequest(ApiResponse<TodoDto>.Error("请求体不能为空", 400));
            }

            if (request.Title != null)
            {
                if (string.IsNullOrWhiteSpace(request.Title))
                {
                    return BadRequest(ApiResponse<TodoDto>.Error("标题不能为空", 400));
                }
                if (request.Title.Length > 200)
                {
                    return BadRequest(ApiResponse<TodoDto>.Error("标题长度不能超过 200 字符", 400));
                }
            }

            if (request.Remark != null && request.Remark.Length > 1000)
            {
                return BadRequest(ApiResponse<TodoDto>.Error("备注长度不能超过 1000 字符", 400));
            }

            var todo = await _todoService.UpdateTodoAsync(id, request);
            if (todo == null)
            {
                return NotFound(ApiResponse<TodoDto>.Error("待办不存在", 404));
            }

            return Ok(ApiResponse<TodoDto>.Ok(todo, "更新待办成功"));
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

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTodo(int id)
    {
        try
        {
            XTrace.Log.Info("删除待办，id={0}", id);

            if (id <= 0)
            {
                return BadRequest(ApiResponse.Error("id 必须为正整数", 400));
            }

            var success = await _todoService.DeleteTodoAsync(id);
            if (!success)
            {
                return NotFound(ApiResponse.Error("待办不存在", 404));
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("删除待办失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse.Error("删除待办失败: " + ex.Message));
        }
    }

    [HttpPost("{id}/complete")]
    public async Task<ActionResult<ApiResponse<TodoDto>>> CompleteTodo(int id)
    {
        try
        {
            XTrace.Log.Info("标记待办完成，id={0}", id);

            if (id <= 0)
            {
                return BadRequest(ApiResponse<TodoDto>.Error("id 必须为正整数", 400));
            }

            var todo = await _todoService.CompleteTodoAsync(id);
            if (todo == null)
            {
                return NotFound(ApiResponse<TodoDto>.Error("待办不存在", 404));
            }

            return Ok(ApiResponse<TodoDto>.Ok(todo, "标记完成成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("标记待办完成失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<TodoDto>.Error("标记完成失败: " + ex.Message));
        }
    }

    [HttpPost("{id}/reopen")]
    public async Task<ActionResult<ApiResponse<TodoDto>>> ReopenTodo(int id)
    {
        try
        {
            XTrace.Log.Info("重新打开待办，id={0}", id);

            if (id <= 0)
            {
                return BadRequest(ApiResponse<TodoDto>.Error("id 必须为正整数", 400));
            }

            var todo = await _todoService.ReopenTodoAsync(id);
            if (todo == null)
            {
                return NotFound(ApiResponse<TodoDto>.Error("待办不存在", 404));
            }

            return Ok(ApiResponse<TodoDto>.Ok(todo, "重新打开成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("重新打开待办失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<TodoDto>.Error("重新打开失败: " + ex.Message));
        }
    }
}

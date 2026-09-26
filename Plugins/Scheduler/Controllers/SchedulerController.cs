using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.Scheduler.Models;
using ForgeSelf.Api.Plugins.Scheduler.Services;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.Scheduler.Controllers;

[ApiController]
[Route("api/scheduler")]
public class SchedulerController : ControllerBase
{
    private readonly ISchedulerService _schedulerService;

    public SchedulerController(ISchedulerService schedulerService)
    {
        _schedulerService = schedulerService;
    }

    [HttpGet("tasks")]
    public async Task<ActionResult<ApiResponse<ForgeSelf.Api.Plugins.Scheduler.Models.PagedResult<ScheduledTaskDto>>>> GetTasks(
        [FromQuery] string? keyword = null,
        [FromQuery] ScheduledTaskStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            XTrace.Log.Info("[Scheduler] 获取任务列表，keyword={0}, status={1}, page={2}, pageSize={3}",
                keyword, status, page, pageSize);

            var result = await _schedulerService.ListTasksAsync(keyword, status, page, pageSize);
            return Ok(ApiResponse<ForgeSelf.Api.Plugins.Scheduler.Models.PagedResult<ScheduledTaskDto>>.Ok(result, "获取任务列表成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[Scheduler] 获取任务列表失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<ForgeSelf.Api.Plugins.Scheduler.Models.PagedResult<ScheduledTaskDto>>.Error("获取任务列表失败: " + ex.Message));
        }
    }

    [HttpGet("tasks/{id}")]
    public async Task<ActionResult<ApiResponse<ScheduledTaskDto>>> GetTask(long id)
    {
        try
        {
            XTrace.Log.Info("[Scheduler] 获取任务详情，id={0}", id);

            var task = await _schedulerService.GetTaskAsync(id);
            if (task == null)
            {
                return NotFound(ApiResponse<ScheduledTaskDto>.Error("任务不存在", 404));
            }

            return Ok(ApiResponse<ScheduledTaskDto>.Ok(task, "获取任务详情成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[Scheduler] 获取任务详情失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<ScheduledTaskDto>.Error("获取任务详情失败: " + ex.Message));
        }
    }

    [HttpPost("tasks")]
    public async Task<ActionResult<ApiResponse<ScheduledTaskDto>>> CreateTask([FromBody] CreateScheduledTaskRequest request)
    {
        try
        {
            XTrace.Log.Info("[Scheduler] 创建任务，Name={0}, Type={1}", request.Name, request.TaskType);

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(ApiResponse<ScheduledTaskDto>.Error("任务名称不能为空", 400));
            }

            if (string.IsNullOrWhiteSpace(request.TargetId))
            {
                return BadRequest(ApiResponse<ScheduledTaskDto>.Error("任务目标ID不能为空", 400));
            }

            var task = await _schedulerService.CreateTaskAsync(request);
            return Ok(ApiResponse<ScheduledTaskDto>.Ok(task, "创建任务成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[Scheduler] 创建任务失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<ScheduledTaskDto>.Error("创建任务失败: " + ex.Message));
        }
    }

    [HttpPut("tasks/{id}")]
    public async Task<ActionResult<ApiResponse<ScheduledTaskDto>>> UpdateTask(long id, [FromBody] UpdateScheduledTaskRequest request)
    {
        try
        {
            XTrace.Log.Info("[Scheduler] 更新任务，id={0}", id);

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(ApiResponse<ScheduledTaskDto>.Error("任务名称不能为空", 400));
            }

            var task = await _schedulerService.UpdateTaskAsync(id, request);
            if (task == null)
            {
                return NotFound(ApiResponse<ScheduledTaskDto>.Error("任务不存在", 404));
            }

            return Ok(ApiResponse<ScheduledTaskDto>.Ok(task, "更新任务成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[Scheduler] 更新任务失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<ScheduledTaskDto>.Error("更新任务失败: " + ex.Message));
        }
    }

    [HttpDelete("tasks/{id}")]
    public async Task<ActionResult<ApiResponse>> DeleteTask(long id)
    {
        try
        {
            XTrace.Log.Info("[Scheduler] 删除任务，id={0}", id);

            var success = await _schedulerService.DeleteTaskAsync(id);
            if (!success)
            {
                return NotFound(ApiResponse.Error("任务不存在", 404));
            }

            return Ok(ApiResponse.Ok("删除任务成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[Scheduler] 删除任务失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse.Error("删除任务失败: " + ex.Message));
        }
    }

    [HttpPost("tasks/{id}/toggle")]
    public async Task<ActionResult<ApiResponse<ScheduledTaskDto>>> ToggleTask(long id, [FromBody] ToggleTaskStatusRequest request)
    {
        try
        {
            XTrace.Log.Info("[Scheduler] 切换任务状态，id={0}, enabled={1}", id, request.Enabled);

            var task = await _schedulerService.ToggleTaskStatusAsync(id, request.Enabled);
            if (task == null)
            {
                return NotFound(ApiResponse<ScheduledTaskDto>.Error("任务不存在", 404));
            }

            var message = request.Enabled ? "任务已启用" : "任务已禁用";
            return Ok(ApiResponse<ScheduledTaskDto>.Ok(task, message));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[Scheduler] 切换任务状态失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<ScheduledTaskDto>.Error("切换任务状态失败: " + ex.Message));
        }
    }

    [HttpPost("tasks/{id}/run-now")]
    public async Task<ActionResult<ApiResponse>> RunNow(long id)
    {
        try
        {
            XTrace.Log.Info("[Scheduler] 立即执行任务，id={0}", id);

            var success = await _schedulerService.RunNowAsync(id);
            if (!success)
            {
                return NotFound(ApiResponse.Error("任务不存在", 404));
            }

            return Ok(ApiResponse.Ok("任务已开始执行"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[Scheduler] 立即执行任务失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse.Error("立即执行任务失败: " + ex.Message));
        }
    }

    [HttpGet("tasks/{id}/logs")]
    public async Task<ActionResult<ApiResponse<ForgeSelf.Api.Plugins.Scheduler.Models.PagedResult<ScheduledTaskLogDto>>>> GetTaskLogs(
        long id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            XTrace.Log.Info("[Scheduler] 获取任务执行日志，taskId={0}, page={1}, pageSize={2}", id, page, pageSize);

            var result = await _schedulerService.GetTaskLogsAsync(id, page, pageSize);
            return Ok(ApiResponse<ForgeSelf.Api.Plugins.Scheduler.Models.PagedResult<ScheduledTaskLogDto>>.Ok(result, "获取执行日志成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[Scheduler] 获取任务执行日志失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<ForgeSelf.Api.Plugins.Scheduler.Models.PagedResult<ScheduledTaskLogDto>>.Error("获取执行日志失败: " + ex.Message));
        }
    }

    [HttpPost("parse-cron")]
    public ActionResult<ApiResponse<CronParseResult>> ParseCron([FromBody] CronParseRequest request)
    {
        try
        {
            XTrace.Log.Info("[Scheduler] 解析Cron表达式: {0}", request.CronExpression);

            if (string.IsNullOrWhiteSpace(request.CronExpression))
            {
                return BadRequest(ApiResponse<CronParseResult>.Error("Cron表达式不能为空", 400));
            }

            var result = _schedulerService.ParseCron(request.CronExpression, request.Count, request.TimeZone);
            return Ok(ApiResponse<CronParseResult>.Ok(result, "解析成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[Scheduler] 解析Cron表达式失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<CronParseResult>.Error("解析失败: " + ex.Message));
        }
    }
}

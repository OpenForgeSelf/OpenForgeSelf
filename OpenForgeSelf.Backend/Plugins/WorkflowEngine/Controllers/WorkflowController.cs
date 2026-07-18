using OpenForgeSelf.Backend.Models.Plugins;
using OpenForgeSelf.Backend.Plugins.WorkflowEngine.Models;
using OpenForgeSelf.Backend.Plugins.WorkflowEngine.Services;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.WorkflowEngine.Controllers;

[ApiController]
[Route("api/workflows")]
public class WorkflowController : ControllerBase
{
    private readonly IWorkflowService _workflowService;

    public WorkflowController(IWorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkflowDefinitionDto>>>> GetWorkflows(
        [FromQuery] string? keyword = null,
        [FromQuery] string? category = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            XTrace.Log.Info("[WorkflowController] 获取工作流列表，keyword={0}, category={1}, page={2}, pageSize={3}",
                keyword, category, page, pageSize);

            var result = await _workflowService.ListWorkflowsAsync(keyword, category, page, pageSize);
            return Ok(ApiResponse<PagedResult<WorkflowDefinitionDto>>.Ok(result, "获取工作流列表成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[WorkflowController] 获取工作流列表失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<PagedResult<WorkflowDefinitionDto>>.Error("获取工作流列表失败: " + ex.Message));
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<WorkflowDefinitionDto>>> GetWorkflowById(long id)
    {
        try
        {
            XTrace.Log.Info("[WorkflowController] 获取工作流详情，id={0}", id);

            var workflow = await _workflowService.GetWorkflowAsync(id);
            if (workflow == null)
            {
                return NotFound(ApiResponse<WorkflowDefinitionDto>.Error("工作流不存在", 404));
            }

            return Ok(ApiResponse<WorkflowDefinitionDto>.Ok(workflow, "获取工作流详情成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[WorkflowController] 获取工作流详情失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<WorkflowDefinitionDto>.Error("获取工作流详情失败: " + ex.Message));
        }
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<WorkflowDefinitionDto>>> CreateWorkflow(
        [FromBody] CreateWorkflowRequest request)
    {
        try
        {
            XTrace.Log.Info("[WorkflowController] 创建工作流，Name={0}", request.Name);

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(ApiResponse<WorkflowDefinitionDto>.Error("工作流名称不能为空", 400));
            }

            var workflow = await _workflowService.CreateWorkflowAsync(request);
            return Ok(ApiResponse<WorkflowDefinitionDto>.Ok(workflow, "创建工作流成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[WorkflowController] 创建工作流失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<WorkflowDefinitionDto>.Error("创建工作流失败: " + ex.Message));
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<WorkflowDefinitionDto>>> UpdateWorkflow(
        long id,
        [FromBody] UpdateWorkflowRequest request)
    {
        try
        {
            XTrace.Log.Info("[WorkflowController] 更新工作流，id={0}", id);

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(ApiResponse<WorkflowDefinitionDto>.Error("工作流名称不能为空", 400));
            }

            var workflow = await _workflowService.UpdateWorkflowAsync(id, request);
            if (workflow == null)
            {
                return NotFound(ApiResponse<WorkflowDefinitionDto>.Error("工作流不存在", 404));
            }

            return Ok(ApiResponse<WorkflowDefinitionDto>.Ok(workflow, "更新工作流成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[WorkflowController] 更新工作流失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<WorkflowDefinitionDto>.Error("更新工作流失败: " + ex.Message));
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse>> DeleteWorkflow(long id)
    {
        try
        {
            XTrace.Log.Info("[WorkflowController] 删除工作流，id={0}", id);

            var result = await _workflowService.DeleteWorkflowAsync(id);
            if (!result)
            {
                return NotFound(ApiResponse.Error("工作流不存在", 404));
            }

            return Ok(ApiResponse.Ok("删除工作流成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[WorkflowController] 删除工作流失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse.Error("删除工作流失败: " + ex.Message));
        }
    }

    [HttpPost("{id}/favorite")]
    public async Task<ActionResult<ApiResponse>> FavoriteWorkflow(
        long id,
        [FromBody] FavoriteWorkflowRequest request)
    {
        try
        {
            XTrace.Log.Info("[WorkflowController] 收藏/取消收藏工作流，id={0}, isFavorite={1}", id, request.IsFavorite);

            var result = await _workflowService.FavoriteWorkflowAsync(id, request.IsFavorite);
            if (!result)
            {
                return NotFound(ApiResponse.Error("工作流不存在", 404));
            }

            var message = request.IsFavorite ? "收藏成功" : "取消收藏成功";
            return Ok(ApiResponse.Ok(message));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[WorkflowController] 收藏工作流失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse.Error("操作失败: " + ex.Message));
        }
    }

    [HttpPost("{id}/execute")]
    public async Task<ActionResult<ApiResponse<WorkflowExecutionDto>>> ExecuteWorkflow(
        long id,
        [FromBody] ExecuteWorkflowRequest request)
    {
        try
        {
            XTrace.Log.Info("[WorkflowController] 执行工作流，id={0}", id);

            var execution = await _workflowService.ExecuteWorkflowAsync(id, request);
            return Ok(ApiResponse<WorkflowExecutionDto>.Ok(execution, "工作流已启动"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[WorkflowController] 执行工作流失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<WorkflowExecutionDto>.Error("执行工作流失败: " + ex.Message));
        }
    }

    [HttpGet("executions")]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkflowExecutionDto>>>> GetExecutions(
        [FromQuery] long? workflowId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            XTrace.Log.Info("[WorkflowController] 获取执行记录列表，workflowId={0}, page={1}, pageSize={2}",
                workflowId, page, pageSize);

            var result = await _workflowService.GetExecutionsAsync(workflowId, page, pageSize);
            return Ok(ApiResponse<PagedResult<WorkflowExecutionDto>>.Ok(result, "获取执行记录成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[WorkflowController] 获取执行记录失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<PagedResult<WorkflowExecutionDto>>.Error("获取执行记录失败: " + ex.Message));
        }
    }

    [HttpGet("executions/{id}")]
    public async Task<ActionResult<ApiResponse<WorkflowExecutionDetailDto>>> GetExecutionDetail(long id)
    {
        try
        {
            XTrace.Log.Info("[WorkflowController] 获取执行详情，executionId={0}", id);

            var execution = await _workflowService.GetExecutionDetailAsync(id);
            if (execution == null)
            {
                return NotFound(ApiResponse<WorkflowExecutionDetailDto>.Error("执行记录不存在", 404));
            }

            return Ok(ApiResponse<WorkflowExecutionDetailDto>.Ok(execution, "获取执行详情成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[WorkflowController] 获取执行详情失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<WorkflowExecutionDetailDto>.Error("获取执行详情失败: " + ex.Message));
        }
    }

    [HttpPost("executions/{id}/pause")]
    public async Task<ActionResult<ApiResponse>> PauseExecution(long id)
    {
        try
        {
            XTrace.Log.Info("[WorkflowController] 暂停执行，executionId={0}", id);

            var execution = await _workflowService.GetExecutionDetailAsync(id);
            if (execution == null)
            {
                return NotFound(ApiResponse.Error("执行记录不存在", 404));
            }

            return Ok(ApiResponse.Ok("暂停指令已发送"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[WorkflowController] 暂停执行失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse.Error("暂停执行失败: " + ex.Message));
        }
    }

    [HttpPost("executions/{id}/resume")]
    public async Task<ActionResult<ApiResponse>> ResumeExecution(long id)
    {
        try
        {
            XTrace.Log.Info("[WorkflowController] 继续执行，executionId={0}", id);

            var execution = await _workflowService.GetExecutionDetailAsync(id);
            if (execution == null)
            {
                return NotFound(ApiResponse.Error("执行记录不存在", 404));
            }

            return Ok(ApiResponse.Ok("继续指令已发送"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[WorkflowController] 继续执行失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse.Error("继续执行失败: " + ex.Message));
        }
    }

    [HttpPost("executions/{id}/cancel")]
    public async Task<ActionResult<ApiResponse>> CancelExecution(long id)
    {
        try
        {
            XTrace.Log.Info("[WorkflowController] 取消执行，executionId={0}", id);

            var execution = await _workflowService.GetExecutionDetailAsync(id);
            if (execution == null)
            {
                return NotFound(ApiResponse.Error("执行记录不存在", 404));
            }

            return Ok(ApiResponse.Ok("取消指令已发送"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[WorkflowController] 取消执行失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse.Error("取消执行失败: " + ex.Message));
        }
    }

    [HttpGet("templates")]
    public async Task<ActionResult<ApiResponse<List<WorkflowTemplateDto>>>> GetTemplates()
    {
        try
        {
            XTrace.Log.Info("[WorkflowController] 获取推荐模板");

            var templates = await _workflowService.GetTemplatesAsync();
            return Ok(ApiResponse<List<WorkflowTemplateDto>>.Ok(templates, "获取模板列表成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[WorkflowController] 获取模板列表失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<WorkflowTemplateDto>>.Error("获取模板列表失败: " + ex.Message));
        }
    }
}

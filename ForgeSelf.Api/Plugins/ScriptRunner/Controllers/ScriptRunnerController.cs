using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.ScriptRunner.Models;
using ForgeSelf.Api.Plugins.ScriptRunner.Services;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.ScriptRunner.Controllers;

[ApiController]
[Route("api/scripts")]
public class ScriptRunnerController : ControllerBase
{
    private readonly IScriptService _scriptService;
    private readonly IScriptExecutor _scriptExecutor;
    private readonly IRuntimeDetector _runtimeDetector;
    private readonly IScriptTemplateService _templateService;

    public ScriptRunnerController(
        IScriptService scriptService,
        IScriptExecutor scriptExecutor,
        IRuntimeDetector runtimeDetector,
        IScriptTemplateService templateService)
    {
        _scriptService = scriptService;
        _scriptExecutor = scriptExecutor;
        _runtimeDetector = runtimeDetector;
        _templateService = templateService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<ScriptListResponse>>> GetScripts(
        [FromQuery] string? keyword = null,
        [FromQuery] string? category = null,
        [FromQuery] ScriptLanguage? language = null,
        [FromQuery] bool? isFavorite = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            XTrace.Log.Info("[ScriptRunnerController] 获取脚本列表，keyword={0}, category={1}, language={2}, isFavorite={3}, page={4}, pageSize={5}",
                keyword, category, language, isFavorite, page, pageSize);

            var result = await _scriptService.ListScriptsAsync(keyword, category, language, isFavorite, page, pageSize);
            return Ok(ApiResponse<ScriptListResponse>.Ok(result, "获取脚本列表成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptRunnerController] 获取脚本列表失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<ScriptListResponse>.Error("获取脚本列表失败: " + ex.Message));
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<Script>>> GetScriptById(long id)
    {
        try
        {
            XTrace.Log.Info("[ScriptRunnerController] 获取脚本详情，id={0}", id);

            var script = await _scriptService.GetScriptAsync(id);
            if (script == null)
            {
                return NotFound(ApiResponse<Script>.Error("脚本不存在", 404));
            }

            return Ok(ApiResponse<Script>.Ok(script, "获取脚本详情成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptRunnerController] 获取脚本详情失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<Script>.Error("获取脚本详情失败: " + ex.Message));
        }
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Script>>> CreateScript([FromBody] CreateScriptRequest request)
    {
        try
        {
            XTrace.Log.Info("[ScriptRunnerController] 创建脚本，Name={0}, Language={1}", request.Name, request.Language);

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(ApiResponse<Script>.Error("脚本名称不能为空", 400));
            }

            var script = await _scriptService.CreateScriptAsync(request);
            return Ok(ApiResponse<Script>.Ok(script, "创建脚本成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptRunnerController] 创建脚本失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<Script>.Error("创建脚本失败: " + ex.Message));
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<Script>>> UpdateScript(long id, [FromBody] UpdateScriptRequest request)
    {
        try
        {
            XTrace.Log.Info("[ScriptRunnerController] 更新脚本，id={0}", id);

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(ApiResponse<Script>.Error("脚本名称不能为空", 400));
            }

            var script = await _scriptService.UpdateScriptAsync(id, request);
            if (script == null)
            {
                return NotFound(ApiResponse<Script>.Error("脚本不存在", 404));
            }

            return Ok(ApiResponse<Script>.Ok(script, "更新脚本成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptRunnerController] 更新脚本失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<Script>.Error("更新脚本失败: " + ex.Message));
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse>> DeleteScript(long id)
    {
        try
        {
            XTrace.Log.Info("[ScriptRunnerController] 删除脚本，id={0}", id);

            var result = await _scriptService.DeleteScriptAsync(id);
            if (!result)
            {
                return NotFound(ApiResponse.Error("脚本不存在", 404));
            }

            return Ok(ApiResponse.Ok("删除脚本成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptRunnerController] 删除脚本失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse.Error("删除脚本失败: " + ex.Message));
        }
    }

    [HttpPost("{id}/favorite")]
    public async Task<ActionResult<ApiResponse>> ToggleFavorite(long id, [FromBody] FavoriteRequest request)
    {
        try
        {
            XTrace.Log.Info("[ScriptRunnerController] 切换脚本收藏状态，id={0}, isFavorite={1}", id, request.IsFavorite);

            var result = await _scriptService.FavoriteScriptAsync(id, request.IsFavorite);
            if (!result)
            {
                return NotFound(ApiResponse.Error("脚本不存在", 404));
            }

            return Ok(ApiResponse.Ok(request.IsFavorite ? "已收藏" : "已取消收藏"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptRunnerController] 切换收藏状态失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse.Error("切换收藏状态失败: " + ex.Message));
        }
    }

    [HttpGet("categories")]
    public async Task<ActionResult<ApiResponse<List<string>>>> GetCategories()
    {
        try
        {
            XTrace.Log.Info("[ScriptRunnerController] 获取脚本分类列表");

            var categories = await _scriptService.GetCategoriesAsync();
            return Ok(ApiResponse<List<string>>.Ok(categories, "获取分类列表成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptRunnerController] 获取分类列表失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<string>>.Error("获取分类列表失败: " + ex.Message));
        }
    }

    [HttpGet("tags")]
    public async Task<ActionResult<ApiResponse<List<string>>>> GetTags()
    {
        try
        {
            XTrace.Log.Info("[ScriptRunnerController] 获取脚本标签列表");

            var tags = await _scriptService.GetTagsAsync();
            return Ok(ApiResponse<List<string>>.Ok(tags, "获取标签列表成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptRunnerController] 获取标签列表失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<string>>.Error("获取标签列表失败: " + ex.Message));
        }
    }

    [HttpGet("runtimes")]
    public async Task<ActionResult<ApiResponse<List<RuntimeEnvironment>>>> GetRuntimes()
    {
        try
        {
            XTrace.Log.Info("[ScriptRunnerController] 获取可用运行环境");

            var runtimes = await _runtimeDetector.DetectAllAsync();
            return Ok(ApiResponse<List<RuntimeEnvironment>>.Ok(runtimes, "获取运行环境成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptRunnerController] 获取运行环境失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<RuntimeEnvironment>>.Error("获取运行环境失败: " + ex.Message));
        }
    }

    [HttpPost("{id}/execute")]
    public async Task<ActionResult<ApiResponse<ScriptExecution>>> ExecuteScript(
        long id,
        [FromBody] ExecuteScriptRequest? request = null)
    {
        try
        {
            XTrace.Log.Info("[ScriptRunnerController] 执行脚本，id={0}", id);

            var script = await _scriptService.GetScriptAsync(id);
            if (script == null)
            {
                return NotFound(ApiResponse<ScriptExecution>.Error("脚本不存在", 404));
            }

            var execution = await _scriptExecutor.ExecuteAsync(id, request?.Parameters);
            await _scriptService.IncrementUsageAsync(id);

            return Ok(ApiResponse<ScriptExecution>.Ok(execution, "脚本已开始执行"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptRunnerController] 执行脚本失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<ScriptExecution>.Error("执行脚本失败: " + ex.Message));
        }
    }

    [HttpPost("execute-code")]
    public async Task<ActionResult<ApiResponse<ScriptExecution>>> ExecuteCode([FromBody] ExecuteCodeRequest request)
    {
        try
        {
            XTrace.Log.Info("[ScriptRunnerController] 执行代码，Language={0}", request.Language);

            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest(ApiResponse<ScriptExecution>.Error("代码不能为空", 400));
            }

            var execution = await _scriptExecutor.ExecuteCodeAsync(
                request.Code,
                request.Language,
                request.Parameters,
                request.WorkingDirectory);

            return Ok(ApiResponse<ScriptExecution>.Ok(execution, "代码已开始执行"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptRunnerController] 执行代码失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<ScriptExecution>.Error("执行代码失败: " + ex.Message));
        }
    }

    [HttpGet("executions/{id}")]
    public async Task<ActionResult<ApiResponse<ScriptExecution>>> GetExecution(long id)
    {
        try
        {
            XTrace.Log.Info("[ScriptRunnerController] 获取执行详情，id={0}", id);

            var execution = await _scriptExecutor.GetExecutionAsync(id);
            if (execution == null)
            {
                return NotFound(ApiResponse<ScriptExecution>.Error("执行记录不存在", 404));
            }

            return Ok(ApiResponse<ScriptExecution>.Ok(execution, "获取执行详情成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptRunnerController] 获取执行详情失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<ScriptExecution>.Error("获取执行详情失败: " + ex.Message));
        }
    }

    [HttpPost("executions/{id}/cancel")]
    public async Task<ActionResult<ApiResponse>> CancelExecution(long id)
    {
        try
        {
            XTrace.Log.Info("[ScriptRunnerController] 取消执行，id={0}", id);

            var result = await _scriptExecutor.CancelAsync(id);
            if (!result)
            {
                return NotFound(ApiResponse.Error("执行记录不存在或无法取消", 404));
            }

            return Ok(ApiResponse.Ok("已取消执行"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptRunnerController] 取消执行失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse.Error("取消执行失败: " + ex.Message));
        }
    }

    [HttpGet("executions")]
    public async Task<ActionResult<ApiResponse<ExecutionListResponse>>> GetExecutions(
        [FromQuery] long? scriptId = null,
        [FromQuery] ScriptExecutionStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            XTrace.Log.Info("[ScriptRunnerController] 获取执行记录列表，scriptId={0}, status={1}, page={2}, pageSize={3}",
                scriptId, status, page, pageSize);

            var result = await _scriptService.ListExecutionsAsync(scriptId, status, page, pageSize);
            return Ok(ApiResponse<ExecutionListResponse>.Ok(result, "获取执行记录成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptRunnerController] 获取执行记录失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<ExecutionListResponse>.Error("获取执行记录失败: " + ex.Message));
        }
    }

    [HttpGet("templates")]
    public async Task<ActionResult<ApiResponse<List<ScriptTemplate>>>> GetTemplates(
        [FromQuery] string? category = null,
        [FromQuery] string? keyword = null,
        [FromQuery] ScriptLanguage? language = null)
    {
        try
        {
            XTrace.Log.Info("[ScriptRunnerController] 获取脚本模板列表，category={0}, keyword={1}, language={2}",
                category, keyword, language);

            var templates = await _templateService.GetTemplatesAsync(category, keyword, language);
            return Ok(ApiResponse<List<ScriptTemplate>>.Ok(templates, "获取模板列表成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptRunnerController] 获取模板列表失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<ScriptTemplate>>.Error("获取模板列表失败: " + ex.Message));
        }
    }

    [HttpGet("templates/categories")]
    public async Task<ActionResult<ApiResponse<List<ScriptTemplateCategory>>>> GetTemplateCategories()
    {
        try
        {
            XTrace.Log.Info("[ScriptRunnerController] 获取模板分类列表");

            var categories = await _templateService.GetCategoriesAsync();
            return Ok(ApiResponse<List<ScriptTemplateCategory>>.Ok(categories, "获取模板分类成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptRunnerController] 获取模板分类失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<ScriptTemplateCategory>>.Error("获取模板分类失败: " + ex.Message));
        }
    }
}

public class FavoriteRequest
{
    public bool IsFavorite { get; set; }
}

using OpenForgeSelf.Backend.Models.Plugins;
using OpenForgeSelf.Backend.Plugins.AIAgent.Services;
using OpenForgeSelf.Backend.Plugins.ScriptRunner.Models;
using OpenForgeSelf.Backend.Plugins.ScriptRunner.Services;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.AIAgent.Controllers;

[ApiController]
[Route("api/ai-agent/script")]
public class AIScriptController : ControllerBase
{
    private readonly IAIAgentService _aiAgentService;
    private readonly IScriptTemplateService? _scriptTemplateService;

    public AIScriptController(IAIAgentService aiAgentService, IScriptTemplateService? scriptTemplateService = null)
    {
        _aiAgentService = aiAgentService;
        _scriptTemplateService = scriptTemplateService;
    }

    [HttpPost("generate")]
    public async Task<ActionResult<ApiResponse<GenerateScriptResponse>>> GenerateScript([FromBody] GenerateScriptRequest request)
    {
        try
        {
            XTrace.Log.Info("[AIScriptController] 生成脚本，语言: {0}", request.Language);

            if (string.IsNullOrWhiteSpace(request.Language))
            {
                return BadRequest(ApiResponse<GenerateScriptResponse>.Error("语言不能为空", 400));
            }

            if (string.IsNullOrWhiteSpace(request.Description))
            {
                return BadRequest(ApiResponse<GenerateScriptResponse>.Error("脚本描述不能为空", 400));
            }

            var result = await _aiAgentService.GenerateScriptAsync(
                request.Language,
                request.Description,
                request.Requirements);

            if (string.IsNullOrEmpty(result.Code))
            {
                return StatusCode(500, ApiResponse<GenerateScriptResponse>.Error("脚本生成失败"));
            }

            return Ok(ApiResponse<GenerateScriptResponse>.Ok(result, "脚本生成成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIScriptController] 生成脚本失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<GenerateScriptResponse>.Error("生成脚本失败: " + ex.Message));
        }
    }

    [HttpPost("analyze-error")]
    public async Task<ActionResult<ApiResponse<AnalyzeScriptErrorResponse>>> AnalyzeError([FromBody] AnalyzeScriptErrorRequest request)
    {
        try
        {
            XTrace.Log.Info("[AIScriptController] 分析脚本错误，语言: {0}", request.Language);

            if (string.IsNullOrWhiteSpace(request.Language))
            {
                return BadRequest(ApiResponse<AnalyzeScriptErrorResponse>.Error("语言不能为空", 400));
            }

            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest(ApiResponse<AnalyzeScriptErrorResponse>.Error("代码不能为空", 400));
            }

            if (string.IsNullOrWhiteSpace(request.ErrorMessage))
            {
                return BadRequest(ApiResponse<AnalyzeScriptErrorResponse>.Error("错误信息不能为空", 400));
            }

            var result = await _aiAgentService.AnalyzeScriptErrorAsync(
                request.Language,
                request.Code,
                request.ErrorMessage);

            return Ok(ApiResponse<AnalyzeScriptErrorResponse>.Ok(result, "错误分析完成"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIScriptController] 分析脚本错误失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<AnalyzeScriptErrorResponse>.Error("分析错误失败: " + ex.Message));
        }
    }

    [HttpPost("suggest-fix")]
    public async Task<ActionResult<ApiResponse<SuggestScriptFixResponse>>> SuggestFix([FromBody] SuggestScriptFixRequest request)
    {
        try
        {
            XTrace.Log.Info("[AIScriptController] 建议脚本修复，语言: {0}", request.Language);

            if (string.IsNullOrWhiteSpace(request.Language))
            {
                return BadRequest(ApiResponse<SuggestScriptFixResponse>.Error("语言不能为空", 400));
            }

            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest(ApiResponse<SuggestScriptFixResponse>.Error("代码不能为空", 400));
            }

            if (string.IsNullOrWhiteSpace(request.ErrorMessage))
            {
                return BadRequest(ApiResponse<SuggestScriptFixResponse>.Error("错误信息不能为空", 400));
            }

            var result = await _aiAgentService.SuggestScriptFixAsync(
                request.Language,
                request.Code,
                request.ErrorMessage);

            return Ok(ApiResponse<SuggestScriptFixResponse>.Ok(result, "修复建议生成成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIScriptController] 建议修复失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<SuggestScriptFixResponse>.Error("生成修复建议失败: " + ex.Message));
        }
    }

    [HttpGet("templates")]
    public async Task<ActionResult<ApiResponse<List<ScriptTemplate>>>> GetTemplates([FromQuery] string? category = null)
    {
        try
        {
            XTrace.Log.Info("[AIScriptController] 获取脚本模板，分类: {0}", category ?? "全部");

            List<ScriptTemplate> templates;
            if (_scriptTemplateService != null)
            {
                templates = await _scriptTemplateService.GetTemplatesAsync(category);
            }
            else
            {
                templates = await _aiAgentService.GetScriptTemplatesAsync(category);
            }

            return Ok(ApiResponse<List<ScriptTemplate>>.Ok(templates, $"获取到 {templates.Count} 个模板"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIScriptController] 获取脚本模板失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<ScriptTemplate>>.Error("获取模板失败: " + ex.Message));
        }
    }

    [HttpGet("templates/categories")]
    public async Task<ActionResult<ApiResponse<List<ScriptTemplateCategory>>>> GetTemplateCategories()
    {
        try
        {
            XTrace.Log.Info("[AIScriptController] 获取模板分类列表");

            if (_scriptTemplateService == null)
            {
                return Ok(ApiResponse<List<ScriptTemplateCategory>>.Ok([], "模板服务未注册"));
            }

            var categories = await _scriptTemplateService.GetCategoriesAsync();
            return Ok(ApiResponse<List<ScriptTemplateCategory>>.Ok(categories, "获取分类列表成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIScriptController] 获取模板分类失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<ScriptTemplateCategory>>.Error("获取分类失败: " + ex.Message));
        }
    }

    [HttpGet("templates/{templateId}")]
    public async Task<ActionResult<ApiResponse<ScriptTemplate>>> GetTemplateById(string templateId)
    {
        try
        {
            XTrace.Log.Info("[AIScriptController] 获取模板详情，templateId: {0}", templateId);

            if (string.IsNullOrWhiteSpace(templateId))
            {
                return BadRequest(ApiResponse<ScriptTemplate>.Error("模板ID不能为空", 400));
            }

            if (_scriptTemplateService == null)
            {
                return NotFound(ApiResponse<ScriptTemplate>.Error("模板服务未注册", 404));
            }

            var template = await _scriptTemplateService.GetTemplateByIdAsync(templateId);
            if (template == null)
            {
                return NotFound(ApiResponse<ScriptTemplate>.Error("模板不存在", 404));
            }

            return Ok(ApiResponse<ScriptTemplate>.Ok(template, "获取模板详情成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[AIScriptController] 获取模板详情失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<ScriptTemplate>.Error("获取模板详情失败: " + ex.Message));
        }
    }
}

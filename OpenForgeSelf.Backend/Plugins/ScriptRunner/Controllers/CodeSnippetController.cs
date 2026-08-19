using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Backend.Plugins.ScriptRunner.Models;
using OpenForgeSelf.Backend.Plugins.ScriptRunner.Services;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.ScriptRunner.Controllers;

[ApiController]
[Route("api/codesnippets")]
public class CodeSnippetController : ControllerBase
{
    private readonly ICodeSnippetService _codeSnippetService;

    public CodeSnippetController(ICodeSnippetService codeSnippetService)
    {
        _codeSnippetService = codeSnippetService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<CodeSnippetListResponse>>> GetSnippets(
        [FromQuery] string? keyword = null,
        [FromQuery] string? language = null,
        [FromQuery] string? category = null,
        [FromQuery] bool? isFavorite = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            XTrace.Log.Info("[CodeSnippetController] 获取代码片段列表，keyword={0}, language={1}, category={2}, isFavorite={3}, page={4}, pageSize={5}",
                keyword, language, category, isFavorite, page, pageSize);

            var result = await _codeSnippetService.ListSnippetsAsync(keyword, language, category, isFavorite, page, pageSize);
            return Ok(ApiResponse<CodeSnippetListResponse>.Ok(result, "获取代码片段列表成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[CodeSnippetController] 获取代码片段列表失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<CodeSnippetListResponse>.Error("获取代码片段列表失败: " + ex.Message));
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<CodeSnippet>>> GetSnippetById(long id)
    {
        try
        {
            XTrace.Log.Info("[CodeSnippetController] 获取代码片段详情，id={0}", id);

            var snippet = await _codeSnippetService.GetSnippetAsync(id);
            if (snippet == null)
            {
                return NotFound(ApiResponse<CodeSnippet>.Error("代码片段不存在", 404));
            }

            return Ok(ApiResponse<CodeSnippet>.Ok(snippet, "获取代码片段详情成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[CodeSnippetController] 获取代码片段详情失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<CodeSnippet>.Error("获取代码片段详情失败: " + ex.Message));
        }
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<CodeSnippet>>> CreateSnippet([FromBody] CreateCodeSnippetRequest request)
    {
        try
        {
            XTrace.Log.Info("[CodeSnippetController] 创建代码片段，Title={0}, Language={1}", request.Title, request.Language);

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest(ApiResponse<CodeSnippet>.Error("代码片段标题不能为空", 400));
            }

            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest(ApiResponse<CodeSnippet>.Error("代码内容不能为空", 400));
            }

            var snippet = await _codeSnippetService.CreateSnippetAsync(request);
            return Ok(ApiResponse<CodeSnippet>.Ok(snippet, "创建代码片段成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[CodeSnippetController] 创建代码片段失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<CodeSnippet>.Error("创建代码片段失败: " + ex.Message));
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<CodeSnippet>>> UpdateSnippet(long id, [FromBody] UpdateCodeSnippetRequest request)
    {
        try
        {
            XTrace.Log.Info("[CodeSnippetController] 更新代码片段，id={0}", id);

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest(ApiResponse<CodeSnippet>.Error("代码片段标题不能为空", 400));
            }

            var snippet = await _codeSnippetService.UpdateSnippetAsync(id, request);
            if (snippet == null)
            {
                return NotFound(ApiResponse<CodeSnippet>.Error("代码片段不存在", 404));
            }

            return Ok(ApiResponse<CodeSnippet>.Ok(snippet, "更新代码片段成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[CodeSnippetController] 更新代码片段失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<CodeSnippet>.Error("更新代码片段失败: " + ex.Message));
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse>> DeleteSnippet(long id)
    {
        try
        {
            XTrace.Log.Info("[CodeSnippetController] 删除代码片段，id={0}", id);

            var result = await _codeSnippetService.DeleteSnippetAsync(id);
            if (!result)
            {
                return NotFound(ApiResponse.Error("代码片段不存在", 404));
            }

            return Ok(ApiResponse.Ok("删除代码片段成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[CodeSnippetController] 删除代码片段失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse.Error("删除代码片段失败: " + ex.Message));
        }
    }

    [HttpPost("{id}/favorite")]
    public async Task<ActionResult<ApiResponse>> ToggleFavorite(long id, [FromBody] FavoriteRequest request)
    {
        try
        {
            XTrace.Log.Info("[CodeSnippetController] 切换代码片段收藏状态，id={0}, isFavorite={1}", id, request.IsFavorite);

            var result = await _codeSnippetService.FavoriteSnippetAsync(id, request.IsFavorite);
            if (!result)
            {
                return NotFound(ApiResponse.Error("代码片段不存在", 404));
            }

            return Ok(ApiResponse.Ok(request.IsFavorite ? "已收藏" : "已取消收藏"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[CodeSnippetController] 切换收藏状态失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse.Error("切换收藏状态失败: " + ex.Message));
        }
    }

    [HttpPost("from-script/{scriptId}")]
    public async Task<ActionResult<ApiResponse<CodeSnippet>>> CreateFromScript(long scriptId, [FromBody] CreateFromScriptRequest? request = null)
    {
        try
        {
            XTrace.Log.Info("[CodeSnippetController] 从脚本创建代码片段，scriptId={0}, title={1}", scriptId, request?.Title);

            var snippet = await _codeSnippetService.CreateFromScriptAsync(scriptId, request?.Title);
            if (snippet == null)
            {
                return NotFound(ApiResponse<CodeSnippet>.Error("脚本不存在", 404));
            }

            return Ok(ApiResponse<CodeSnippet>.Ok(snippet, "从脚本创建代码片段成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[CodeSnippetController] 从脚本创建代码片段失败 [{0}]: {1}", scriptId, ex.Message);
            return StatusCode(500, ApiResponse<CodeSnippet>.Error("从脚本创建代码片段失败: " + ex.Message));
        }
    }

    [HttpGet("languages")]
    public async Task<ActionResult<ApiResponse<List<string>>>> GetLanguages()
    {
        try
        {
            XTrace.Log.Info("[CodeSnippetController] 获取所有语言");

            var languages = await _codeSnippetService.GetLanguagesAsync();
            return Ok(ApiResponse<List<string>>.Ok(languages, "获取语言列表成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[CodeSnippetController] 获取语言列表失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<string>>.Error("获取语言列表失败: " + ex.Message));
        }
    }

    [HttpGet("categories")]
    public async Task<ActionResult<ApiResponse<List<string>>>> GetCategories()
    {
        try
        {
            XTrace.Log.Info("[CodeSnippetController] 获取所有分类");

            var categories = await _codeSnippetService.GetCategoriesAsync();
            return Ok(ApiResponse<List<string>>.Ok(categories, "获取分类列表成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[CodeSnippetController] 获取分类列表失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<string>>.Error("获取分类列表失败: " + ex.Message));
        }
    }
}

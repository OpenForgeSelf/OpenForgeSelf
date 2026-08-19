using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Backend.Plugins.QuickLinks.Models;
using OpenForgeSelf.Backend.Plugins.QuickLinks.Services;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.QuickLinks.Controllers;

[ApiController]
[Route("api/quicklinks")]
public class QuickLinksController : ControllerBase
{
    private readonly IQuickLinkService _quickLinkService;

    public QuickLinksController(IQuickLinkService quickLinkService)
    {
        _quickLinkService = quickLinkService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<OpenForgeSelf.Backend.Plugins.QuickLinks.Models.PagedResult<QuickLinkDto>>>> GetLinks(
        [FromQuery] long? categoryId = null,
        [FromQuery] string? keyword = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            XTrace.Log.Info("获取快捷链接列表，categoryId={0}, keyword={1}, page={2}, pageSize={3}", categoryId, keyword, page, pageSize);

            var result = await _quickLinkService.GetLinksAsync(categoryId, keyword, page, pageSize);
            return Ok(ApiResponse<OpenForgeSelf.Backend.Plugins.QuickLinks.Models.PagedResult<QuickLinkDto>>.Ok(result, "获取快捷链接列表成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取快捷链接列表失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<OpenForgeSelf.Backend.Plugins.QuickLinks.Models.PagedResult<QuickLinkDto>>.Error("获取快捷链接列表失败: " + ex.Message));
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<QuickLinkDto>>> GetLinkById(long id)
    {
        try
        {
            XTrace.Log.Info("获取快捷链接详情，id={0}", id);

            var link = await _quickLinkService.GetLinkByIdAsync(id);
            if (link == null)
            {
                return NotFound(ApiResponse<QuickLinkDto>.Error("快捷链接不存在", 404));
            }

            return Ok(ApiResponse<QuickLinkDto>.Ok(link, "获取快捷链接详情成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取快捷链接详情失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<QuickLinkDto>.Error("获取快捷链接详情失败: " + ex.Message));
        }
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<QuickLinkDto>>> CreateLink([FromBody] CreateQuickLinkRequest request)
    {
        try
        {
            XTrace.Log.Info("创建快捷链接，Name={0}", request.Name);

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(ApiResponse<QuickLinkDto>.Error("链接名称不能为空", 400));
            }

            if (string.IsNullOrWhiteSpace(request.Url))
            {
                return BadRequest(ApiResponse<QuickLinkDto>.Error("链接地址不能为空", 400));
            }

            var link = await _quickLinkService.CreateLinkAsync(request);
            return Ok(ApiResponse<QuickLinkDto>.Ok(link, "创建快捷链接成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("创建快捷链接失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<QuickLinkDto>.Error("创建快捷链接失败: " + ex.Message));
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<QuickLinkDto>>> UpdateLink(long id, [FromBody] UpdateQuickLinkRequest request)
    {
        try
        {
            XTrace.Log.Info("更新快捷链接，id={0}", id);

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(ApiResponse<QuickLinkDto>.Error("链接名称不能为空", 400));
            }

            if (string.IsNullOrWhiteSpace(request.Url))
            {
                return BadRequest(ApiResponse<QuickLinkDto>.Error("链接地址不能为空", 400));
            }

            var link = await _quickLinkService.UpdateLinkAsync(id, request);
            if (link == null)
            {
                return NotFound(ApiResponse<QuickLinkDto>.Error("快捷链接不存在", 404));
            }

            return Ok(ApiResponse<QuickLinkDto>.Ok(link, "更新快捷链接成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("更新快捷链接失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<QuickLinkDto>.Error("更新快捷链接失败: " + ex.Message));
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse>> DeleteLink(long id)
    {
        try
        {
            XTrace.Log.Info("删除快捷链接，id={0}", id);

            var success = await _quickLinkService.DeleteLinkAsync(id);
            if (!success)
            {
                return NotFound(ApiResponse.Error("快捷链接不存在", 404));
            }

            return Ok(ApiResponse.Ok("删除快捷链接成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("删除快捷链接失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse.Error("删除快捷链接失败: " + ex.Message));
        }
    }

    [HttpPost("{id}/click")]
    public async Task<ActionResult<ApiResponse>> IncrementClickCount(long id)
    {
        try
        {
            XTrace.Log.Info("记录快捷链接点击，id={0}", id);

            var success = await _quickLinkService.IncrementClickCountAsync(id);
            if (!success)
            {
                return NotFound(ApiResponse.Error("快捷链接不存在", 404));
            }

            return Ok(ApiResponse.Ok("记录点击成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("记录点击失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse.Error("记录点击失败: " + ex.Message));
        }
    }

    [HttpPost("reorder")]
    public async Task<ActionResult<ApiResponse>> ReorderLinks([FromBody] ReorderLinksRequest request)
    {
        try
        {
            XTrace.Log.Info("重新排序快捷链接，数量={0}", request.OrderedIds.Count);

            var success = await _quickLinkService.ReorderLinksAsync(request.OrderedIds);
            if (!success)
            {
                return BadRequest(ApiResponse.Error("重新排序失败", 400));
            }

            return Ok(ApiResponse.Ok("重新排序成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("重新排序快捷链接失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse.Error("重新排序失败: " + ex.Message));
        }
    }

    [HttpGet("categories")]
    public async Task<ActionResult<ApiResponse<List<QuickLinkCategoryDto>>>> GetCategories()
    {
        try
        {
            XTrace.Log.Info("获取快捷链接分类列表");

            var categories = await _quickLinkService.GetCategoriesAsync();
            return Ok(ApiResponse<List<QuickLinkCategoryDto>>.Ok(categories, "获取分类列表成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取快捷链接分类列表失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<QuickLinkCategoryDto>>.Error("获取分类列表失败: " + ex.Message));
        }
    }

    [HttpPost("categories")]
    public async Task<ActionResult<ApiResponse<QuickLinkCategoryDto>>> CreateCategory([FromBody] CreateCategoryRequest request)
    {
        try
        {
            XTrace.Log.Info("创建快捷链接分类，Name={0}", request.Name);

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(ApiResponse<QuickLinkCategoryDto>.Error("分类名称不能为空", 400));
            }

            var category = await _quickLinkService.CreateCategoryAsync(request);
            return Ok(ApiResponse<QuickLinkCategoryDto>.Ok(category, "创建分类成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("创建快捷链接分类失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<QuickLinkCategoryDto>.Error("创建分类失败: " + ex.Message));
        }
    }

    [HttpPut("categories/{id}")]
    public async Task<ActionResult<ApiResponse<QuickLinkCategoryDto>>> UpdateCategory(long id, [FromBody] UpdateCategoryRequest request)
    {
        try
        {
            XTrace.Log.Info("更新快捷链接分类，id={0}", id);

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(ApiResponse<QuickLinkCategoryDto>.Error("分类名称不能为空", 400));
            }

            var category = await _quickLinkService.UpdateCategoryAsync(id, request);
            if (category == null)
            {
                return NotFound(ApiResponse<QuickLinkCategoryDto>.Error("分类不存在", 404));
            }

            return Ok(ApiResponse<QuickLinkCategoryDto>.Ok(category, "更新分类成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("更新快捷链接分类失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<QuickLinkCategoryDto>.Error("更新分类失败: " + ex.Message));
        }
    }

    [HttpDelete("categories/{id}")]
    public async Task<ActionResult<ApiResponse>> DeleteCategory(long id)
    {
        try
        {
            XTrace.Log.Info("删除快捷链接分类，id={0}", id);

            var success = await _quickLinkService.DeleteCategoryAsync(id);
            if (!success)
            {
                return NotFound(ApiResponse.Error("分类不存在", 404));
            }

            return Ok(ApiResponse.Ok("删除分类成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("删除快捷链接分类失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse.Error("删除分类失败: " + ex.Message));
        }
    }

    [HttpPost("import")]
    public async Task<ActionResult<ApiResponse<int>>> ImportLinks([FromBody] ImportLinksRequest request)
    {
        try
        {
            XTrace.Log.Info("导入快捷链接，数量={0}, 模式={1}", request.Links.Count, request.Mode);

            if (request.Links == null || request.Links.Count == 0)
            {
                return BadRequest(ApiResponse<int>.Error("导入数据不能为空", 400));
            }

            var count = await _quickLinkService.ImportLinksAsync(request);
            return Ok(ApiResponse<int>.Ok(count, $"成功导入 {count} 条链接"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("导入快捷链接失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<int>.Error("导入失败: " + ex.Message));
        }
    }

    [HttpGet("export")]
    public async Task<ActionResult<ApiResponse<List<QuickLinkDto>>>> ExportLinks(
        [FromQuery] long? categoryId = null)
    {
        try
        {
            XTrace.Log.Info("导出快捷链接，categoryId={0}", categoryId);

            var links = await _quickLinkService.ExportLinksAsync(categoryId);
            return Ok(ApiResponse<List<QuickLinkDto>>.Ok(links, "导出成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("导出快捷链接失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<QuickLinkDto>>.Error("导出失败: " + ex.Message));
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using ForgeSelf.Api.Plugins.MemorySystem.Models;
using ForgeSelf.Api.Plugins.MemorySystem.Services;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.MemorySystem.Controllers;

[ApiController]
[Route("api/memory")]
public class MemoryController : ControllerBase
{
    private readonly IMemoryService _memoryService;

    public MemoryController(IMemoryService memoryService)
    {
        _memoryService = memoryService;
    }

    [HttpPost("search")]
    public async Task<ActionResult<MemorySearchResult>> Search([FromBody] SearchMemoryRequest request)
    {
        try
        {
            XTrace.Log.Debug("[MemorySystem] 搜索记忆，关键词: {0}", request.Keyword);
            var result = await _memoryService.SearchAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[MemorySystem] 搜索记忆失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<MemoryDto>> GetById(long id)
    {
        try
        {
            var memory = await _memoryService.GetByIdAsync(id);
            if (memory == null)
                return NotFound(new { success = false, error = "记忆不存在" });

            await _memoryService.IncrementAccessAsync(id);
            return Ok(memory);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[MemorySystem] 获取记忆失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<MemoryDto>> Create([FromBody] CreateMemoryRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return BadRequest(new { success = false, error = "标题不能为空" });
            if (string.IsNullOrWhiteSpace(request.Content))
                return BadRequest(new { success = false, error = "内容不能为空" });

            XTrace.Log.Debug("[MemorySystem] 创建记忆: {0}", request.Title);
            var memory = await _memoryService.CreateAsync(request);
            return Ok(memory);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[MemorySystem] 创建记忆失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<MemoryDto>> Update(long id, [FromBody] UpdateMemoryRequest request)
    {
        try
        {
            var memory = await _memoryService.UpdateAsync(id, request);
            if (memory == null)
                return NotFound(new { success = false, error = "记忆不存在" });

            return Ok(memory);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[MemorySystem] 更新记忆失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
    {
        try
        {
            var result = await _memoryService.DeleteAsync(id);
            if (!result)
                return NotFound(new { success = false, error = "记忆不存在" });

            return Ok(new { success = true, message = "删除成功" });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[MemorySystem] 删除记忆失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpGet("categories")]
    public async Task<ActionResult<List<MemoryCategoryDto>>> GetCategories()
    {
        try
        {
            var categories = await _memoryService.GetCategoriesAsync();
            return Ok(categories);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[MemorySystem] 获取分类失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpPost("categories")]
    public async Task<ActionResult<MemoryCategoryDto>> CreateCategory([FromBody] CreateMemoryCategoryRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { success = false, error = "分类名称不能为空" });

            var category = await _memoryService.CreateCategoryAsync(request);
            return Ok(category);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[MemorySystem] 创建分类失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpPut("categories/{id}")]
    public async Task<ActionResult<MemoryCategoryDto>> UpdateCategory(long id, [FromBody] UpdateMemoryCategoryRequest request)
    {
        try
        {
            var category = await _memoryService.UpdateCategoryAsync(id, request);
            if (category == null)
                return NotFound(new { success = false, error = "分类不存在" });

            return Ok(category);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[MemorySystem] 更新分类失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpDelete("categories/{id}")]
    public async Task<IActionResult> DeleteCategory(long id)
    {
        try
        {
            var result = await _memoryService.DeleteCategoryAsync(id);
            if (!result)
                return NotFound(new { success = false, error = "分类不存在" });

            return Ok(new { success = true, message = "删除成功" });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[MemorySystem] 删除分类失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpGet("stats")]
    public async Task<ActionResult<MemoryStatsDto>> GetStats()
    {
        try
        {
            var stats = await _memoryService.GetStatsAsync();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[MemorySystem] 获取统计失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpPost("relevant")]
    public async Task<ActionResult<List<MemoryDto>>> GetRelevantMemories([FromBody] SearchMemoryRequest request)
    {
        try
        {
            var query = request.Keyword ?? string.Empty;
            var memories = await _memoryService.GetRelevantMemoriesAsync(query, request.PageSize);
            return Ok(memories);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[MemorySystem] 获取相关记忆失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpPost("import")]
    public async Task<IActionResult> ImportMemories([FromBody] ImportMemoryRequest request)
    {
        try
        {
            var count = await _memoryService.ImportMemoriesAsync(request);
            return Ok(new { success = true, importedCount = count, message = $"成功导入 {count} 条记忆" });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[MemorySystem] 导入记忆失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    [HttpGet("export")]
    public async Task<ActionResult<List<MemoryDto>>> ExportMemories([FromQuery] long? categoryId, [FromQuery] int? type)
    {
        try
        {
            Data.MemoryType? typeEnum = type.HasValue ? (Data.MemoryType)type.Value : null;
            var memories = await _memoryService.ExportMemoriesAsync(categoryId, typeEnum);
            return Ok(memories);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[MemorySystem] 导出记忆失败: {0}", ex.Message);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
}

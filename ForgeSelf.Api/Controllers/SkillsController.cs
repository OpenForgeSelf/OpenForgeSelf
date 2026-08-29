using ForgeSelf.Abstractions;
using ForgeSelf.Api.Models.Skills;
using ForgeSelf.Api.Services.Skills;
using Microsoft.AspNetCore.Mvc;
using NewLife.Log;

namespace ForgeSelf.Api.Controllers;

[ApiController]
[Route("api/skills")]
public class SkillsController : ControllerBase
{
    private readonly ISkillsService _skillsService;

    public SkillsController(ISkillsService skillsService)
    {
        _skillsService = skillsService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<SkillItemDto>>>> GetSkills(
        [FromQuery] string? keyword = null,
        [FromQuery] string? category = null,
        [FromQuery] bool? isEnabled = null)
    {
        try
        {
            XTrace.Log.Info("获取技能列表: keyword={0}, category={1}, isEnabled={2}", keyword, category, isEnabled);

            var skills = await _skillsService.GetAllAsync(keyword, category, isEnabled);
            return Ok(ApiResponse<List<SkillItemDto>>.Ok(skills, "获取技能列表成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取技能列表失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<List<SkillItemDto>>.Error("获取技能列表失败: " + ex.Message));
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<SkillDetailDto>>> GetSkill(string id)
    {
        try
        {
            XTrace.Log.Info("获取技能详情: {0}", id);

            var skill = await _skillsService.GetByIdAsync(id);
            if (skill == null)
            {
                return NotFound(ApiResponse<SkillDetailDto>.Error("技能不存在", 404));
            }

            return Ok(ApiResponse<SkillDetailDto>.Ok(skill, "获取技能详情成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取技能详情失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<SkillDetailDto>.Error("获取技能详情失败: " + ex.Message));
        }
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<SkillDetailDto>>> CreateSkill([FromBody] CreateSkillDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return BadRequest(ApiResponse<SkillDetailDto>.Error("技能名称不能为空", 400));
            }

            XTrace.Log.Info("创建技能: {0}", dto.Name);

            var skill = await _skillsService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetSkill), new { id = skill.Id },
                ApiResponse<SkillDetailDto>.Ok(skill, "创建技能成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("创建技能失败: {0}", ex.Message);
            return StatusCode(500, ApiResponse<SkillDetailDto>.Error("创建技能失败: " + ex.Message));
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<SkillDetailDto>>> UpdateSkill(string id, [FromBody] UpdateSkillDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return BadRequest(ApiResponse<SkillDetailDto>.Error("技能名称不能为空", 400));
            }

            XTrace.Log.Info("更新技能: {0}", id);

            var skill = await _skillsService.UpdateAsync(id, dto);
            if (skill == null)
            {
                return NotFound(ApiResponse<SkillDetailDto>.Error("技能不存在", 404));
            }

            return Ok(ApiResponse<SkillDetailDto>.Ok(skill, "更新技能成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("更新技能失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<SkillDetailDto>.Error("更新技能失败: " + ex.Message));
        }
    }

    [HttpPost("{id}/toggle")]
    public async Task<ActionResult<ApiResponse<SkillItemDto>>> ToggleSkill(string id)
    {
        try
        {
            XTrace.Log.Info("切换技能启用状态: {0}", id);

            var success = await _skillsService.ToggleAsync(id);
            if (!success)
            {
                return NotFound(ApiResponse<SkillItemDto>.Error("技能不存在", 404));
            }

            var skill = await _skillsService.GetByIdAsync(id);
            var item = new SkillItemDto
            {
                Id = skill!.Id,
                Name = skill.Name,
                Description = skill.Description,
                Category = skill.Category,
                IsEnabled = skill.IsEnabled,
                ToolCount = skill.ToolIds.Count,
                UsageCount = skill.UsageCount,
                CreatedAt = skill.CreatedAt,
                UpdatedAt = skill.UpdatedAt
            };

            return Ok(ApiResponse<SkillItemDto>.Ok(item, "切换状态成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("切换技能状态失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<SkillItemDto>.Error("切换技能状态失败: " + ex.Message));
        }
    }

    [HttpGet("{id}/settings")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, string>>>> GetSkillSettings(string id)
    {
        try
        {
            XTrace.Log.Info("获取技能配置: {0}", id);

            var settings = await _skillsService.GetSettingsAsync(id);
            if (settings == null)
            {
                return NotFound(ApiResponse<Dictionary<string, string>>.Error("技能不存在", 404));
            }

            return Ok(ApiResponse<Dictionary<string, string>>.Ok(settings, "获取技能配置成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("获取技能配置失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse<Dictionary<string, string>>.Error("获取技能配置失败: " + ex.Message));
        }
    }

    [HttpPut("{id}/settings")]
    public async Task<ActionResult<ApiResponse>> UpdateSkillSettings(string id, [FromBody] Dictionary<string, string> settings)
    {
        try
        {
            XTrace.Log.Info("更新技能配置: {0}", id);

            var success = await _skillsService.UpdateSettingsAsync(id, settings);
            if (!success)
            {
                return NotFound(ApiResponse.Error("技能不存在", 404));
            }

            return Ok(ApiResponse.Ok("更新技能配置成功"));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("更新技能配置失败 [{0}]: {1}", id, ex.Message);
            return StatusCode(500, ApiResponse.Error("更新技能配置失败: " + ex.Message));
        }
    }
}
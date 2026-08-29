using ForgeSelf.Api.Models.Skills;

namespace ForgeSelf.Api.Services.Skills;

public interface ISkillsService
{
    Task<List<SkillItemDto>> GetAllAsync(string? keyword = null, string? category = null, bool? isEnabled = null);
    Task<SkillDetailDto?> GetByIdAsync(string id);
    Task<SkillDetailDto> CreateAsync(CreateSkillDto dto);
    Task<SkillDetailDto?> UpdateAsync(string id, UpdateSkillDto dto);
    Task<bool> ToggleAsync(string id);
    Task<Dictionary<string, string>?> GetSettingsAsync(string id);
    Task<bool> UpdateSettingsAsync(string id, Dictionary<string, string> settings);
    Task<bool> DeleteAsync(string id);
}
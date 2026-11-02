namespace ForgeSelf.Api.Services.Skills;

/// <summary>
/// 技能内部存储实体
/// </summary>
public class SkillEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string SystemPrompt { get; set; } = string.Empty;
    public List<string> ToolIds { get; set; } = new();
    public int UsageCount { get; set; }
    public Dictionary<string, string>? Settings { get; set; }
    /// <summary>完整 SKILL.md。不进入 CRUD DTO，只供个人技能目录注入上下文。</summary>
    public string SkillMarkdown { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
namespace OpenForgeSelf.Backend.Models.Skills;

public class SkillDetailDto
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
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
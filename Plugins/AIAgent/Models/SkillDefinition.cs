namespace ForgeSelf.Api.Plugins.AIAgent.Models;

public class SkillDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int UsageCount { get; set; }
    public List<string> AssociatedTools { get; set; } = new();
    public string? ConfigJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

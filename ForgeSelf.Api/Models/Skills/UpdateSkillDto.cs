namespace ForgeSelf.Api.Models.Skills;

public class UpdateSkillDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public List<string> ToolIds { get; set; } = new();
}
namespace ForgeSelf.Api.Plugins.McpCenter.Models;

public class McpServerDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// connected / disconnected / error
    /// </summary>
    public string Status { get; set; } = "disconnected";

    public int ToolCount { get; set; }
}
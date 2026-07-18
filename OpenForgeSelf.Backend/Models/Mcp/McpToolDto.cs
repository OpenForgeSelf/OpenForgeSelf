namespace OpenForgeSelf.Backend.Models.Mcp;

public class McpToolDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string ServerId { get; set; } = string.Empty;

    public string ServerName { get; set; } = string.Empty;

    /// <summary>
    /// system / file / network / data / dev
    /// </summary>
    public string Category { get; set; } = "system";

    public bool IsEnabled { get; set; } = true;
}
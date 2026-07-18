namespace OpenForgeSelf.Backend.Models.Mcp;

public class McpTestResultDto
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public long DurationMs { get; set; }
}
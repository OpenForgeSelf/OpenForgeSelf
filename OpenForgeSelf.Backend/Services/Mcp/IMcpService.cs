using OpenForgeSelf.Backend.Models.Mcp;

namespace OpenForgeSelf.Backend.Services.Mcp;

public interface IMcpService
{
    Task<List<McpServerDto>> GetServersAsync();

    Task<List<McpToolDto>> GetToolsAsync(string serverId, string? keyword = null, string? category = null);

    Task<McpToolDto?> ToggleToolAsync(string toolId);

    Task<McpTestResultDto> TestToolAsync(string toolId);

    Task<McpTestResultDto> TestServerAsync(string serverId);
}
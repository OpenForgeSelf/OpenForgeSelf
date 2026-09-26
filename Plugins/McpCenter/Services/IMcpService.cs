using ForgeSelf.Api.Plugins.McpCenter.Models;

namespace ForgeSelf.Api.Plugins.McpCenter.Services;

public interface IMcpService
{
    Task<List<McpServerDto>> GetServersAsync();

    Task<List<McpToolDto>> GetToolsAsync(string serverId, string? keyword = null, string? category = null);

    Task<McpToolDto?> ToggleToolAsync(string toolId);

    Task<McpTestResultDto> TestToolAsync(string toolId);

    Task<McpTestResultDto> TestServerAsync(string serverId);
}
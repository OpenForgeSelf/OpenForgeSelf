using OpenForgeSelf.Backend.Plugins.Abstractions;
using OpenForgeSelf.Backend.Plugins.AIAgent.Models;

namespace OpenForgeSelf.Backend.Plugins.AIAgent.Services;

public interface IToolRegistry
{
    void RegisterTool(IToolFunctionExtension tool);
    void UnregisterTool(string toolId);
    IToolFunctionExtension? GetTool(string toolName);
    IEnumerable<IToolFunctionExtension> GetAllTools();
    List<AIToolDefinition> GetToolDefinitions();
    Task<string> ExecuteToolAsync(string toolName, string parameters);
    ToolValidationResult ValidateParameters(string toolName, string parameters);
    Task<ToolExecutionResult> ExecuteToolWithResultAsync(string toolName, string parameters, CancellationToken cancellationToken = default);
    Task<ToolExecutionResult> ExecuteToolWithTimeoutAsync(string toolName, string parameters, int timeoutSeconds = 30, CancellationToken cancellationToken = default);
}

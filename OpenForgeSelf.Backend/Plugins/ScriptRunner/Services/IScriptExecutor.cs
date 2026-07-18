using OpenForgeSelf.Backend.Plugins.ScriptRunner.Models;

namespace OpenForgeSelf.Backend.Plugins.ScriptRunner.Services;

public interface IScriptExecutor
{
    Task<ScriptExecution> ExecuteAsync(long scriptId, Dictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default);
    Task<ScriptExecution> ExecuteCodeAsync(string code, ScriptLanguage language, Dictionary<string, object?>? parameters = null, string? workingDirectory = null, CancellationToken cancellationToken = default);
    Task<bool> CancelAsync(long executionId);
    Task<ScriptExecution?> GetExecutionAsync(long executionId);
    Task<List<ScriptExecutionLog>> GetOutputAsync(long executionId, DateTime? sinceTimestamp = null);
}

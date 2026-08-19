namespace OpenForgeSelf.Abstractions;

/// <summary>
/// 脚本执行器契约（实现：OpenForgeSelf.Backend.Plugins.ScriptRunner.Services.ScriptExecutor）。
/// 供 WorkflowEngine 等兄弟插件按接口消费，避免直接依赖 ScriptRunner 插件程序集。
/// </summary>
public interface IScriptExecutor
{
    Task<ScriptExecution> ExecuteAsync(long scriptId, Dictionary<string, object?>? parameters = null, CancellationToken cancellationToken = default);
    Task<ScriptExecution> ExecuteCodeAsync(string code, ScriptLanguage language, Dictionary<string, object?>? parameters = null, string? workingDirectory = null, CancellationToken cancellationToken = default);
    Task<bool> CancelAsync(long executionId);
    Task<ScriptExecution?> GetExecutionAsync(long executionId);
    Task<List<ScriptExecutionLog>> GetOutputAsync(long executionId, DateTime? sinceTimestamp = null);
}

/// <summary>
/// 脚本管理服务契约（实现：OpenForgeSelf.Backend.Plugins.ScriptRunner.Services.ScriptService）。
/// </summary>
public interface IScriptService
{
    Task<Script> CreateScriptAsync(CreateScriptRequest request);
    Task<Script?> UpdateScriptAsync(long id, UpdateScriptRequest request);
    Task<bool> DeleteScriptAsync(long id);
    Task<Script?> GetScriptAsync(long id);
    Task<ScriptListResponse> ListScriptsAsync(string? keyword = null, string? category = null, ScriptLanguage? language = null, bool? isFavorite = null, int page = 1, int pageSize = 20);
    Task<bool> FavoriteScriptAsync(long id, bool isFavorite);
    Task<bool> IncrementUsageAsync(long id);
    Task<List<string>> GetCategoriesAsync();
    Task<List<string>> GetTagsAsync();
    Task<ExecutionListResponse> ListExecutionsAsync(long? scriptId = null, ScriptExecutionStatus? status = null, int page = 1, int pageSize = 20);
}

using OpenForgeSelf.Backend.Plugins.ScriptRunner.Models;

namespace OpenForgeSelf.Backend.Plugins.ScriptRunner.Services;

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

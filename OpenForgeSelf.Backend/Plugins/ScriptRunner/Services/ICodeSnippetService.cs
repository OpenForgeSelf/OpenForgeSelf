using OpenForgeSelf.Backend.Plugins.ScriptRunner.Models;

namespace OpenForgeSelf.Backend.Plugins.ScriptRunner.Services;

public interface ICodeSnippetService
{
    Task<CodeSnippet> CreateSnippetAsync(CreateCodeSnippetRequest request);
    Task<CodeSnippet?> UpdateSnippetAsync(long id, UpdateCodeSnippetRequest request);
    Task<bool> DeleteSnippetAsync(long id);
    Task<CodeSnippet?> GetSnippetAsync(long id);
    Task<CodeSnippetListResponse> ListSnippetsAsync(
        string? keyword = null,
        string? language = null,
        string? category = null,
        bool? isFavorite = null,
        int page = 1,
        int pageSize = 20);
    Task<bool> FavoriteSnippetAsync(long id, bool isFavorite);
    Task<bool> IncrementUsageAsync(long id);
    Task<List<string>> GetLanguagesAsync();
    Task<List<string>> GetCategoriesAsync();
    Task<CodeSnippet?> CreateFromScriptAsync(long scriptId, string? title = null);
}

using ForgeSelf.Api.Plugins.MemorySystem.Data;
using ForgeSelf.Api.Plugins.MemorySystem.Models;

namespace ForgeSelf.Api.Plugins.MemorySystem.Services;

public interface IMemoryService
{
    Task<MemorySearchResult> SearchAsync(SearchMemoryRequest request);
    Task<MemoryDto?> GetByIdAsync(long id);
    Task<MemoryDto> CreateAsync(CreateMemoryRequest request);
    Task<MemoryDto?> UpdateAsync(long id, UpdateMemoryRequest request);
    Task<bool> DeleteAsync(long id);
    Task<bool> IncrementAccessAsync(long id);
    Task<List<MemoryCategoryDto>> GetCategoriesAsync();
    Task<MemoryCategoryDto> CreateCategoryAsync(CreateMemoryCategoryRequest request);
    Task<MemoryCategoryDto?> UpdateCategoryAsync(long id, UpdateMemoryCategoryRequest request);
    Task<bool> DeleteCategoryAsync(long id);
    Task<MemoryStatsDto> GetStatsAsync();
    Task<List<MemoryDto>> GetRelevantMemoriesAsync(string query, int limit = 10, double minScore = 0.1);
    Task<int> ImportMemoriesAsync(ImportMemoryRequest request);
    Task<List<MemoryDto>> ExportMemoriesAsync(long? categoryId = null, MemoryType? type = null);

    void SetShortTermMemory(string sessionId, string key, string value, TimeSpan? ttl = null);
    string? GetShortTermMemory(string sessionId, string key);
    Dictionary<string, string> GetAllShortTermMemories(string sessionId);
    void RemoveShortTermMemory(string sessionId, string key);
    void ClearShortTermMemory(string sessionId);
    void CleanupExpiredShortTerm();
}

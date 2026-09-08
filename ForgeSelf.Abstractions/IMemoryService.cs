namespace ForgeSelf.Abstractions;

/// <summary>
/// 长期记忆服务契约（实现：ForgeSelf.Api.Plugins.MemorySystem.Services.MemoryServiceXCode）。
/// 由 MemorySystem 插件在 <c>Apply</c> 中 eager 构造并 <c>ctx.Register&lt;IMemoryService&gt;(instance)</c>
/// 写入共享服务表；兄弟插件（如 AIAgent）经 <c>ctx.Get&lt;IMemoryService&gt;()</c> 消费，
/// 契约与 DTO 均在 Abstractions，避免插件直接依赖 MemorySystem 程序集。
/// </summary>
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

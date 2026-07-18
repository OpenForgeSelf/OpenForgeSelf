using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OpenForgeSelf.Backend.Plugins.MemorySystem.Data;
using OpenForgeSelf.Backend.Plugins.MemorySystem.Models;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.MemorySystem.Services;

public class MemoryService : IMemoryService
{
    private readonly MemoryDbContext _dbContext;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ShortTermMemoryItem>> _shortTermMemory = new();

    public MemoryService(MemoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MemorySearchResult> SearchAsync(SearchMemoryRequest request)
    {
        var query = _dbContext.Memories.AsQueryable();

        if (request.CategoryId.HasValue)
            query = query.Where(m => m.CategoryId == request.CategoryId.Value);

        if (request.Type.HasValue)
            query = query.Where(m => m.Type == request.Type.Value);

        if (request.MinImportance.HasValue)
            query = query.Where(m => m.Importance >= request.MinImportance.Value);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.Trim().ToLower();
            query = query.Where(m =>
                m.Title.ToLower().Contains(kw) ||
                m.Content.ToLower().Contains(kw) ||
                (m.Tags != null && m.Tags.ToLower().Contains(kw)));
        }

        if (!string.IsNullOrWhiteSpace(request.Tag))
        {
            var tag = request.Tag.Trim().ToLower();
            query = query.Where(m => m.Tags != null && m.Tags.ToLower().Contains(tag));
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(m => m.Importance)
            .ThenByDescending(m => m.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var dtos = items.Select(ToDto).ToList();

        return new MemorySearchResult
        {
            Items = dtos,
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    public async Task<MemoryDto?> GetByIdAsync(long id)
    {
        var entity = await _dbContext.Memories
            .Include(m => m.Category)
            .FirstOrDefaultAsync(m => m.Id == id);
        return entity == null ? null : ToDto(entity);
    }

    public async Task<MemoryDto> CreateAsync(CreateMemoryRequest request)
    {
        var entity = new MemoryEntity
        {
            Title = request.Title,
            Content = request.Content,
            Type = request.Type,
            Importance = request.Importance,
            Tags = request.Tags != null ? string.Join(",", request.Tags) : null,
            Source = request.Source,
            CategoryId = request.CategoryId,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _dbContext.Memories.Add(entity);
        await _dbContext.SaveChangesAsync();

        if (request.CategoryId.HasValue)
        {
            var category = await _dbContext.MemoryCategories.FindAsync(request.CategoryId.Value);
            if (category != null)
            {
                category.MemoryCount++;
                category.UpdatedAt = DateTime.Now;
                await _dbContext.SaveChangesAsync();
            }
        }

        return ToDto(entity);
    }

    public async Task<MemoryDto?> UpdateAsync(long id, UpdateMemoryRequest request)
    {
        var entity = await _dbContext.Memories.FindAsync(id);
        if (entity == null) return null;

        var oldCategoryId = entity.CategoryId;

        if (request.Title != null) entity.Title = request.Title;
        if (request.Content != null) entity.Content = request.Content;
        if (request.Type.HasValue) entity.Type = request.Type.Value;
        if (request.Importance.HasValue) entity.Importance = request.Importance.Value;
        if (request.Tags != null) entity.Tags = string.Join(",", request.Tags);
        if (request.Source != null) entity.Source = request.Source;
        if (request.CategoryId.HasValue) entity.CategoryId = request.CategoryId.Value;

        entity.UpdatedAt = DateTime.Now;

        if (oldCategoryId != entity.CategoryId)
        {
            if (oldCategoryId.HasValue)
            {
                var oldCat = await _dbContext.MemoryCategories.FindAsync(oldCategoryId.Value);
                if (oldCat != null && oldCat.MemoryCount > 0)
                    oldCat.MemoryCount--;
            }
            if (entity.CategoryId.HasValue)
            {
                var newCat = await _dbContext.MemoryCategories.FindAsync(entity.CategoryId.Value);
                if (newCat != null)
                    newCat.MemoryCount++;
            }
        }

        await _dbContext.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var entity = await _dbContext.Memories.FindAsync(id);
        if (entity == null) return false;

        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.Now;

        if (entity.CategoryId.HasValue)
        {
            var category = await _dbContext.MemoryCategories.FindAsync(entity.CategoryId.Value);
            if (category != null && category.MemoryCount > 0)
                category.MemoryCount--;
        }

        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IncrementAccessAsync(long id)
    {
        var entity = await _dbContext.Memories.FindAsync(id);
        if (entity == null) return false;

        entity.AccessCount++;
        entity.LastAccessedAt = DateTime.Now;
        entity.UpdatedAt = DateTime.Now;

        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<List<MemoryCategoryDto>> GetCategoriesAsync()
    {
        var categories = await _dbContext.MemoryCategories
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();

        return categories.Select(c => new MemoryCategoryDto
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
            Icon = c.Icon,
            SortOrder = c.SortOrder,
            MemoryCount = c.MemoryCount,
            CreatedAt = c.CreatedAt
        }).ToList();
    }

    public async Task<MemoryCategoryDto> CreateCategoryAsync(CreateMemoryCategoryRequest request)
    {
        var maxSortOrder = await _dbContext.MemoryCategories.MaxAsync(c => (int?)c.SortOrder) ?? 0;

        var entity = new MemoryCategoryEntity
        {
            Name = request.Name,
            Description = request.Description,
            Icon = request.Icon,
            SortOrder = maxSortOrder + 1,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _dbContext.MemoryCategories.Add(entity);
        await _dbContext.SaveChangesAsync();

        return new MemoryCategoryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Icon = entity.Icon,
            SortOrder = entity.SortOrder,
            MemoryCount = 0,
            CreatedAt = entity.CreatedAt
        };
    }

    public async Task<MemoryCategoryDto?> UpdateCategoryAsync(long id, UpdateMemoryCategoryRequest request)
    {
        var entity = await _dbContext.MemoryCategories.FindAsync(id);
        if (entity == null) return null;

        if (request.Name != null) entity.Name = request.Name;
        if (request.Description != null) entity.Description = request.Description;
        if (request.Icon != null) entity.Icon = request.Icon;
        if (request.SortOrder.HasValue) entity.SortOrder = request.SortOrder.Value;

        entity.UpdatedAt = DateTime.Now;
        await _dbContext.SaveChangesAsync();

        return new MemoryCategoryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Icon = entity.Icon,
            SortOrder = entity.SortOrder,
            MemoryCount = entity.MemoryCount,
            CreatedAt = entity.CreatedAt
        };
    }

    public async Task<bool> DeleteCategoryAsync(long id)
    {
        var entity = await _dbContext.MemoryCategories.FindAsync(id);
        if (entity == null) return false;

        var memoriesInCategory = await _dbContext.Memories
            .Where(m => m.CategoryId == id)
            .ToListAsync();

        foreach (var mem in memoriesInCategory)
            mem.CategoryId = null;

        _dbContext.MemoryCategories.Remove(entity);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<MemoryStatsDto> GetStatsAsync()
    {
        var totalMemories = await _dbContext.Memories.CountAsync();
        var totalCategories = await _dbContext.MemoryCategories.CountAsync();

        var today = DateTime.Today;
        var weekAgo = today.AddDays(-7);

        var todayAccessed = await _dbContext.Memories
            .CountAsync(m => m.LastAccessedAt >= today);
        var weekAccessed = await _dbContext.Memories
            .CountAsync(m => m.LastAccessedAt >= weekAgo);

        var byType = await _dbContext.Memories
            .GroupBy(m => m.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Type, g => g.Count);

        var byImportance = await _dbContext.Memories
            .GroupBy(m => m.Importance)
            .Select(g => new { Imp = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Imp, g => g.Count);

        var recent = await _dbContext.Memories
            .OrderByDescending(m => m.CreatedAt)
            .Take(5)
            .ToListAsync();

        var frequent = await _dbContext.Memories
            .OrderByDescending(m => m.AccessCount)
            .ThenByDescending(m => m.LastAccessedAt)
            .Take(5)
            .ToListAsync();

        return new MemoryStatsDto
        {
            TotalMemories = totalMemories,
            TotalCategories = totalCategories,
            TodayAccessed = todayAccessed,
            WeekAccessed = weekAccessed,
            ByType = byType,
            ByImportance = byImportance,
            RecentMemories = recent.Select(ToDto).ToList(),
            FrequentlyAccessed = frequent.Select(ToDto).ToList()
        };
    }

    public async Task<List<MemoryDto>> GetRelevantMemoriesAsync(string query, int limit = 10, double minScore = 0.1)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<MemoryDto>();

        var queryWords = ExtractKeywords(query).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (queryWords.Count == 0)
            return new List<MemoryDto>();

        var allMemories = await _dbContext.Memories
            .Include(m => m.Category)
            .Where(m => m.Importance >= MemoryImportance.Low)
            .OrderByDescending(m => m.Importance)
            .ThenByDescending(m => m.AccessCount)
            .Take(200)
            .ToListAsync();

        var scored = new List<(MemoryEntity Memory, double Score)>();

        foreach (var memory in allMemories)
        {
            var score = CalculateRelevanceScore(memory, queryWords, query);
            if (score >= minScore)
                scored.Add((memory, score));
        }

        var results = scored
            .OrderByDescending(s => s.Score)
            .ThenByDescending(s => s.Memory.Importance)
            .ThenByDescending(s => s.Memory.AccessCount)
            .Take(limit)
            .Select(s =>
            {
                var dto = ToDto(s.Memory);
                dto.RelevanceScore = Math.Round(s.Score, 4);
                return dto;
            })
            .ToList();

        return results;
    }

    public async Task<int> ImportMemoriesAsync(ImportMemoryRequest request)
    {
        int imported = 0;

        foreach (var item in request.Items)
        {
            var categoryId = await GetOrCreateCategoryAsync(item.CategoryName);

            var entity = new MemoryEntity
            {
                Title = item.Title,
                Content = item.Content,
                Type = item.Type,
                Importance = item.Importance,
                Tags = item.Tags != null ? string.Join(",", item.Tags) : null,
                Source = item.Source,
                CategoryId = categoryId,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _dbContext.Memories.Add(entity);
            imported++;
        }

        await _dbContext.SaveChangesAsync();

        await UpdateCategoryCountsAsync();

        return imported;
    }

    public async Task<List<MemoryDto>> ExportMemoriesAsync(long? categoryId = null, MemoryType? type = null)
    {
        var query = _dbContext.Memories.AsQueryable();

        if (categoryId.HasValue)
            query = query.Where(m => m.CategoryId == categoryId.Value);

        if (type.HasValue)
            query = query.Where(m => m.Type == type.Value);

        var memories = await query
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        return memories.Select(ToDto).ToList();
    }

    public void SetShortTermMemory(string sessionId, string key, string value, TimeSpan? ttl = null)
    {
        var sessionMem = _shortTermMemory.GetOrAdd(sessionId, _ => new ConcurrentDictionary<string, ShortTermMemoryItem>());
        var item = new ShortTermMemoryItem
        {
            SessionId = sessionId,
            Key = key,
            Value = value,
            ExpiresAt = DateTime.Now.Add(ttl ?? TimeSpan.FromHours(1))
        };
        sessionMem.AddOrUpdate(key, item, (_, _) => item);
    }

    public string? GetShortTermMemory(string sessionId, string key)
    {
        if (_shortTermMemory.TryGetValue(sessionId, out var sessionMem))
        {
            if (sessionMem.TryGetValue(key, out var item))
            {
                if (item.ExpiresAt > DateTime.Now)
                    return item.Value;
                else
                    sessionMem.TryRemove(key, out _);
            }
        }
        return null;
    }

    public Dictionary<string, string> GetAllShortTermMemories(string sessionId)
    {
        CleanupExpiredShortTerm();
        if (_shortTermMemory.TryGetValue(sessionId, out var sessionMem))
        {
            return sessionMem
                .Where(kvp => kvp.Value.ExpiresAt > DateTime.Now)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value);
        }
        return new Dictionary<string, string>();
    }

    public void RemoveShortTermMemory(string sessionId, string key)
    {
        if (_shortTermMemory.TryGetValue(sessionId, out var sessionMem))
        {
            sessionMem.TryRemove(key, out _);
        }
    }

    public void ClearShortTermMemory(string sessionId)
    {
        _shortTermMemory.TryRemove(sessionId, out _);
    }

    public void CleanupExpiredShortTerm()
    {
        var now = DateTime.Now;
        foreach (var sessionKvp in _shortTermMemory)
        {
            var expiredKeys = sessionKvp.Value
                .Where(kvp => kvp.Value.ExpiresAt <= now)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
                sessionKvp.Value.TryRemove(key, out _);
        }
    }

    private MemoryDto ToDto(MemoryEntity entity)
    {
        return new MemoryDto
        {
            Id = entity.Id,
            Title = entity.Title,
            Content = entity.Content,
            Type = entity.Type,
            Importance = entity.Importance,
            Tags = entity.Tags?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
            Source = entity.Source,
            CategoryId = entity.CategoryId,
            CategoryName = entity.Category?.Name,
            AccessCount = entity.AccessCount,
            LastAccessedAt = entity.LastAccessedAt,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static HashSet<string> ExtractKeywords(string text)
    {
        var words = Regex.Matches(text.ToLower(), @"[\w\u4e00-\u9fa5]{2,}")
            .Cast<Match>()
            .Select(m => m.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var stopWords = new HashSet<string> { "the", "a", "an", "是", "的", "了", "在", "有", "和", "与", "我", "你", "他", "她", "它", "这", "那" };
        words.RemoveWhere(w => stopWords.Contains(w));
        return words;
    }

    private static double CalculateRelevanceScore(MemoryEntity memory, HashSet<string> queryWords, string originalQuery)
    {
        double score = 0;
        var memoryText = (memory.Title + " " + memory.Content + " " + memory.Tags).ToLower();
        var memoryWords = ExtractKeywords(memoryText);

        if (queryWords.Count == 0) return 0;

        int matchedWords = queryWords.Count(w => memoryWords.Contains(w));
        double wordMatchScore = (double)matchedWords / queryWords.Count;
        score += wordMatchScore * 0.5;

        double titleMatchBonus = 0;
        foreach (var word in queryWords)
        {
            if (memory.Title.ToLower().Contains(word))
                titleMatchBonus += 0.1;
        }
        score += Math.Min(titleMatchBonus, 0.3);

        double importanceBonus = memory.Importance switch
        {
            MemoryImportance.Critical => 0.2,
            MemoryImportance.High => 0.1,
            MemoryImportance.Medium => 0.05,
            _ => 0
        };
        score += importanceBonus;

        if (memory.AccessCount > 0)
        {
            double accessBonus = Math.Min(0.1, Math.Log10(memory.AccessCount + 1) * 0.05);
            score += accessBonus;
        }

        if (memory.LastAccessedAt.HasValue)
        {
            var daysSinceAccess = (DateTime.Now - memory.LastAccessedAt.Value).TotalDays;
            double recencyBonus = Math.Max(0, 0.1 - daysSinceAccess * 0.01);
            score += recencyBonus;
        }

        return Math.Min(1.0, score);
    }

    private async Task<long?> GetOrCreateCategoryAsync(string? categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
            return null;

        var category = await _dbContext.MemoryCategories
            .FirstOrDefaultAsync(c => c.Name == categoryName);

        if (category != null)
            return category.Id;

        var newCategory = new MemoryCategoryEntity
        {
            Name = categoryName,
            SortOrder = 0,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _dbContext.MemoryCategories.Add(newCategory);
        await _dbContext.SaveChangesAsync();
        return newCategory.Id;
    }

    private async Task UpdateCategoryCountsAsync()
    {
        var categories = await _dbContext.MemoryCategories.ToListAsync();
        foreach (var cat in categories)
        {
            cat.MemoryCount = await _dbContext.Memories.CountAsync(m => m.CategoryId == cat.Id);
            cat.UpdatedAt = DateTime.Now;
        }
        await _dbContext.SaveChangesAsync();
    }
}

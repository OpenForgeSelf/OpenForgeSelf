using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using ForgeSelf.Api.Plugins.MemorySystem.Data;
using ForgeSelf.Api.Plugins.MemorySystem.Entities;
using ForgeSelf.Api.Plugins.MemorySystem.Models;
using NewLife;
using NewLife.Data;
using XCode;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Plugins.MemorySystem.Services;

/// <summary>
/// 基于 XCode ORM 的记忆服务实现
/// </summary>
public class MemoryServiceXCode : IMemoryService
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ShortTermMemoryItem>> _shortTermMemory = new();

    #region 基础 CRUD

    public Task<MemorySearchResult> SearchAsync(SearchMemoryRequest request)
    {
        var exp = new WhereExpression();

        if (request.CategoryId.HasValue)
            exp &= Memory._.CategoryId == request.CategoryId.Value;

        if (request.Type.HasValue)
            exp &= Memory._.Type == (int)request.Type.Value;

        if (request.MinImportance.HasValue)
            exp &= Memory._.Importance >= (int)request.MinImportance.Value;

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.Trim();
            exp &= (Memory._.Title.Contains(kw) | Memory._.Content.Contains(kw) | Memory._.Tags.Contains(kw));
        }

        if (!string.IsNullOrWhiteSpace(request.Tag))
        {
            exp &= Memory._.Tags.Contains(request.Tag.Trim());
        }

        // 排除已删除
        exp &= Memory._.IsDeleted == false;

        var page = new PageParameter
        {
            PageIndex = request.Page - 1,
            PageSize = request.PageSize,
            RetrieveTotalCount = true
        };

        var list = Memory.FindAll(exp, page);
        var total = (int)page.TotalCount;

        return Task.FromResult(new MemorySearchResult
        {
            Items = list.Select(ToDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }

    public Task<MemoryDto?> GetByIdAsync(long id)
    {
        // 删除为软删除（IsDeleted=true），查询必须过滤，否则已删除记忆仍可被 GET 命中
        var entity = Memory.Find(Memory._.Id == id & Memory._.IsDeleted == false);
        return Task.FromResult(entity == null ? null : ToDto(entity));
    }

    public Task<MemoryDto> CreateAsync(CreateMemoryRequest request)
    {
        var entity = new Memory
        {
            Title = request.Title,
            Content = request.Content,
            Type = (int)request.Type,
            Importance = (int)request.Importance,
            Tags = request.Tags != null ? string.Join(",", request.Tags) : null,
            Source = request.Source,
            CategoryId = request.CategoryId ?? 0,
            AccessCount = 0,
            DecayScore = 1.0,
            IsDeleted = false
        };

        entity.Insert();

        // 更新分类计数
        if (request.CategoryId.HasValue)
        {
            UpdateCategoryCount(request.CategoryId.Value, 1);
        }

        return Task.FromResult(ToDto(entity));
    }

    public Task<MemoryDto?> UpdateAsync(long id, UpdateMemoryRequest request)
    {
        var entity = Memory.FindByKey(id);
        if (entity == null) return Task.FromResult<MemoryDto?>(null);

        var oldCategoryId = entity.CategoryId;

        if (request.Title != null) entity.Title = request.Title;
        if (request.Content != null) entity.Content = request.Content;
        if (request.Type.HasValue) entity.Type = (int)request.Type.Value;
        if (request.Importance.HasValue) entity.Importance = (int)request.Importance.Value;
        if (request.Tags != null) entity.Tags = string.Join(",", request.Tags);
        if (request.Source != null) entity.Source = request.Source;
        if (request.CategoryId.HasValue) entity.CategoryId = request.CategoryId.Value;

        entity.Update();

        // 更新分类计数
        if (oldCategoryId != entity.CategoryId)
        {
            if (oldCategoryId > 0) UpdateCategoryCount(oldCategoryId, -1);
            if (entity.CategoryId > 0) UpdateCategoryCount(entity.CategoryId, 1);
        }

        return Task.FromResult<MemoryDto?>(ToDto(entity));
    }

    public Task<bool> DeleteAsync(long id)
    {
        var entity = Memory.FindByKey(id);
        if (entity == null) return Task.FromResult(false);

        entity.IsDeleted = true;
        entity.Update();

        // 更新分类计数
        if (entity.CategoryId > 0)
        {
            UpdateCategoryCount(entity.CategoryId, -1);
        }

        return Task.FromResult(true);
    }

    public Task<bool> IncrementAccessAsync(long id)
    {
        var entity = Memory.FindByKey(id);
        if (entity == null) return Task.FromResult(false);

        entity.AccessCount++;
        entity.LastAccessedAt = DateTime.Now;
        entity.Update();

        return Task.FromResult(true);
    }

    #endregion

    #region 分类管理

    public Task<List<MemoryCategoryDto>> GetCategoriesAsync()
    {
        var list = MemoryCategory.FindAll();
        var ordered = list.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToList();
        
        return Task.FromResult(ordered.Select(c => new MemoryCategoryDto
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
            Icon = c.Icon,
            SortOrder = c.SortOrder,
            MemoryCount = c.MemoryCount,
            CreatedAt = c.CreatedAt
        }).ToList());
    }

    public Task<MemoryCategoryDto> CreateCategoryAsync(CreateMemoryCategoryRequest request)
    {
        var maxSort = MemoryCategory.FindAll()
            .Select(c => c.SortOrder)
            .DefaultIfEmpty(0)
            .Max();

        var entity = new MemoryCategory
        {
            Name = request.Name,
            Description = request.Description,
            Icon = request.Icon,
            SortOrder = maxSort + 1,
            MemoryCount = 0
        };

        entity.Insert();

        return Task.FromResult(new MemoryCategoryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Icon = entity.Icon,
            SortOrder = entity.SortOrder,
            MemoryCount = 0,
            CreatedAt = entity.CreatedAt
        });
    }

    public Task<MemoryCategoryDto?> UpdateCategoryAsync(long id, UpdateMemoryCategoryRequest request)
    {
        var entity = MemoryCategory.FindByKey(id);
        if (entity == null) return Task.FromResult<MemoryCategoryDto?>(null);

        if (request.Name != null) entity.Name = request.Name;
        if (request.Description != null) entity.Description = request.Description;
        if (request.Icon != null) entity.Icon = request.Icon;
        if (request.SortOrder.HasValue) entity.SortOrder = request.SortOrder.Value;

        entity.Update();

        return Task.FromResult<MemoryCategoryDto?>(new MemoryCategoryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Icon = entity.Icon,
            SortOrder = entity.SortOrder,
            MemoryCount = entity.MemoryCount,
            CreatedAt = entity.CreatedAt
        });
    }

    public Task<bool> DeleteCategoryAsync(long id)
    {
        var entity = MemoryCategory.FindByKey(id);
        if (entity == null) return Task.FromResult(false);

        // 清除该分类下所有记忆的分类ID
        Memory.Update(Memory._.CategoryId == 0, Memory._.CategoryId == id);

        entity.Delete();
        return Task.FromResult(true);
    }

    #endregion

    #region 统计

    public Task<MemoryStatsDto> GetStatsAsync()
    {
        var totalMemories = (int)Memory.FindCount(Memory._.IsDeleted == false);
        var totalCategories = (int)MemoryCategory.FindCount();

        var today = DateTime.Today;
        var weekAgo = today.AddDays(-7);

        var todayAccessed = (int)Memory.FindCount(Memory._.LastAccessedAt >= today & Memory._.IsDeleted == false);
        var weekAccessed = (int)Memory.FindCount(Memory._.LastAccessedAt >= weekAgo & Memory._.IsDeleted == false);

        // 按类型统计
        var byType = new Dictionary<MemoryType, int>();
        for (int i = 0; i <= 5; i++)
        {
            var count = (int)Memory.FindCount(Memory._.Type == i & Memory._.IsDeleted == false);
            byType[(MemoryType)i] = count;
        }
        byType[MemoryType.Other] = (int)Memory.FindCount(Memory._.Type == 99 & Memory._.IsDeleted == false);

        // 按重要度统计
        var byImportance = new Dictionary<MemoryImportance, int>();
        for (int i = 0; i <= 3; i++)
        {
            var count = (int)Memory.FindCount(Memory._.Importance == i & Memory._.IsDeleted == false);
            byImportance[(MemoryImportance)i] = count;
        }

        // 最近和最常访问
        var recentMemories = Memory.FindAll(Memory._.IsDeleted == false)
            .OrderByDescending(m => m.CreatedAt)
            .Take(5)
            .ToList();

        var frequentMemories = Memory.FindAll(Memory._.IsDeleted == false & Memory._.AccessCount > 0)
            .OrderByDescending(m => m.AccessCount)
            .Take(5)
            .ToList();

        return Task.FromResult(new MemoryStatsDto
        {
            TotalMemories = totalMemories,
            TotalCategories = totalCategories,
            TodayAccessed = todayAccessed,
            WeekAccessed = weekAccessed,
            ByType = byType,
            ByImportance = byImportance,
            RecentMemories = recentMemories.Select(ToDto).ToList(),
            FrequentlyAccessed = frequentMemories.Select(ToDto).ToList()
        });
    }

    #endregion

    #region 智能检索

    public Task<List<MemoryDto>> GetRelevantMemoriesAsync(string query, int limit = 10, double minScore = 0.1)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Task.FromResult(new List<MemoryDto>());

        var queryWords = ExtractKeywords(query).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (queryWords.Count == 0)
            return Task.FromResult(new List<MemoryDto>());

        // 获取所有非删除的记忆
        var allMemories = Memory.FindAll(Memory._.IsDeleted == false)
            .OrderByDescending(m => m.Importance)
            .OrderByDescending(m => m.AccessCount)
            .Take(200)
            .ToList();

        var scored = new List<(Memory Memory, double Score)>();

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

        return Task.FromResult(results);
    }

    #endregion

    #region 导入导出

    public Task<int> ImportMemoriesAsync(ImportMemoryRequest request)
    {
        int imported = 0;

        foreach (var item in request.Items)
        {
            long? categoryId = null;
            if (!string.IsNullOrWhiteSpace(item.CategoryName))
            {
                categoryId = GetOrCreateCategory(item.CategoryName);
            }

            var entity = new Memory
            {
                Title = item.Title,
                Content = item.Content,
                Type = (int)item.Type,
                Importance = (int)item.Importance,
                Tags = item.Tags != null ? string.Join(",", item.Tags) : null,
                Source = item.Source,
                CategoryId = categoryId ?? 0,
                AccessCount = 0,
                DecayScore = 1.0,
                IsDeleted = false
            };

            entity.Insert();
            imported++;

            if (categoryId.HasValue)
            {
                UpdateCategoryCount(categoryId.Value, 1);
            }
        }

        return Task.FromResult(imported);
    }

    public Task<List<MemoryDto>> ExportMemoriesAsync(long? categoryId = null, MemoryType? type = null)
    {
        var exp = Memory._.IsDeleted == false;

        if (categoryId.HasValue)
            exp &= Memory._.CategoryId == categoryId.Value;

        if (type.HasValue)
            exp &= Memory._.Type == (int)type.Value;

        var list = Memory.FindAll(exp).OrderBy(m => m.CreatedAt).ToList();
        return Task.FromResult(list.Select(ToDto).ToList());
    }

    #endregion

    #region 短期记忆

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

    #endregion

    #region 私有方法

    private MemoryDto ToDto(Memory entity)
    {
        var categoryName = string.Empty;
        if (entity.CategoryId > 0)
        {
            var cat = MemoryCategory.FindByKey(entity.CategoryId);
            categoryName = cat?.Name ?? string.Empty;
        }

        return new MemoryDto
        {
            Id = entity.Id,
            Title = entity.Title,
            Content = entity.Content,
            Type = (MemoryType)entity.Type,
            Importance = (MemoryImportance)entity.Importance,
            Tags = entity.Tags?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
            Source = entity.Source,
            CategoryId = entity.CategoryId > 0 ? entity.CategoryId : null,
            CategoryName = categoryName,
            AccessCount = entity.AccessCount,
            LastAccessedAt = entity.LastAccessedAt == DateTime.MinValue ? null : entity.LastAccessedAt,
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

    private static double CalculateRelevanceScore(Memory memory, HashSet<string> queryWords, string originalQuery)
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
            3 => 0.2,  // Critical
            2 => 0.1,  // High
            1 => 0.05, // Medium
            _ => 0
        };
        score += importanceBonus;

        if (memory.AccessCount > 0)
        {
            double accessBonus = Math.Min(0.1, Math.Log10(memory.AccessCount + 1) * 0.05);
            score += accessBonus;
        }

        if (memory.LastAccessedAt > DateTime.MinValue)
        {
            var daysSinceAccess = (DateTime.Now - memory.LastAccessedAt).TotalDays;
            double recencyBonus = Math.Max(0, 0.1 - daysSinceAccess * 0.01);
            score += recencyBonus;
        }

        return Math.Min(1.0, score);
    }

    private long GetOrCreateCategory(string categoryName)
    {
        var category = MemoryCategory.FindAll(MemoryCategory._.Name == categoryName).FirstOrDefault();

        if (category != null)
            return category.Id;

        var maxSort = MemoryCategory.FindAll()
            .Select(c => c.SortOrder)
            .DefaultIfEmpty(0)
            .Max();

        var newCategory = new MemoryCategory
        {
            Name = categoryName,
            SortOrder = maxSort + 1,
            MemoryCount = 0
        };

        newCategory.Insert();
        return newCategory.Id;
    }

    private static void UpdateCategoryCount(long categoryId, int delta)
    {
        var category = MemoryCategory.FindByKey(categoryId);
        if (category != null)
        {
            category.MemoryCount = Math.Max(0, category.MemoryCount + delta);
            category.Update();
        }
    }

    #endregion
}

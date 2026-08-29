using System.Collections.Concurrent;
using ForgeSelf.Api.Models.Skills;

namespace ForgeSelf.Api.Services.Skills;

public class SkillsService : ISkillsService
{
    private readonly ConcurrentDictionary<string, SkillEntity> _skills = new();
    private int _nextId = 1;

    public SkillsService()
    {
        SeedData();
    }

    private void SeedData()
    {
        var now = DateTime.UtcNow;

        var sampleSkills = new[]
        {
            new SkillEntity
            {
                Id = GetNextId(),
                Name = "代码审查",
                Description = "自动化代码审查和最佳实践检查，支持多种编程语言",
                Category = "开发",
                IsEnabled = true,
                SystemPrompt = "你是一个经验丰富的代码审查专家，能够检查代码质量、安全性和最佳实践。",
                ToolIds = new List<string> { "code-reader", "code-search", "diff-viewer" },
                UsageCount = 128,
                Settings = new Dictionary<string, string> { { "autoReview", "true" }, { "strictMode", "false" } },
                CreatedAt = now.AddDays(-30),
                UpdatedAt = now.AddDays(-1)
            },
            new SkillEntity
            {
                Id = GetNextId(),
                Name = "文档生成",
                Description = "根据代码自动生成项目文档和API参考",
                Category = "文档",
                IsEnabled = true,
                SystemPrompt = "你是一个专业的技术文档写手，能够从代码中提取信息并生成清晰的结构化文档。",
                ToolIds = new List<string> { "code-reader", "markdown-writer" },
                UsageCount = 95,
                Settings = new Dictionary<string, string> { { "outputFormat", "markdown" }, { "includeExamples", "true" } },
                CreatedAt = now.AddDays(-28),
                UpdatedAt = now.AddDays(-2)
            },
            new SkillEntity
            {
                Id = GetNextId(),
                Name = "数据分析",
                Description = "对数据集进行探索性分析和可视化",
                Category = "数据",
                IsEnabled = true,
                SystemPrompt = "你是一个数据分析专家，擅长数据清洗、统计分析和可视化。",
                ToolIds = new List<string> { "csv-reader", "chart-generator", "statistics" },
                UsageCount = 67,
                Settings = new Dictionary<string, string> { { "defaultChartType", "bar" } },
                CreatedAt = now.AddDays(-25),
                UpdatedAt = now.AddDays(-3)
            },
            new SkillEntity
            {
                Id = GetNextId(),
                Name = "单元测试生成",
                Description = "自动生成单元测试代码，支持多种测试框架",
                Category = "测试",
                IsEnabled = false,
                SystemPrompt = "你是一个测试专家，能够为代码自动生成全面可靠的单元测试。",
                ToolIds = new List<string> { "code-reader", "test-runner" },
                UsageCount = 42,
                Settings = new Dictionary<string, string> { { "framework", "xunit" }, { "coverage", "80" } },
                CreatedAt = now.AddDays(-20),
                UpdatedAt = now.AddDays(-5)
            },
            new SkillEntity
            {
                Id = GetNextId(),
                Name = "数据库查询优化",
                Description = "分析和优化数据库查询性能",
                Category = "数据库",
                IsEnabled = true,
                SystemPrompt = "你是一个数据库优化专家，擅长SQL分析和索引优化。",
                ToolIds = new List<string> { "sql-analyzer", "index-advisor", "query-profiler" },
                UsageCount = 34,
                Settings = new Dictionary<string, string> { { "dbType", "postgresql" } },
                CreatedAt = now.AddDays(-15),
                UpdatedAt = now.AddDays(-1)
            }
        };

        foreach (var skill in sampleSkills)
        {
            _skills.TryAdd(skill.Id, skill);
        }
    }

    private string GetNextId()
    {
        return $"skill_{Interlocked.Increment(ref _nextId)}";
    }

    public Task<List<SkillItemDto>> GetAllAsync(string? keyword = null, string? category = null, bool? isEnabled = null)
    {
        var query = _skills.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.ToLower();
            query = query.Where(s =>
                s.Name.ToLower().Contains(kw) ||
                s.Description.ToLower().Contains(kw) ||
                s.Category.ToLower().Contains(kw));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(s =>
                s.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        if (isEnabled.HasValue)
        {
            query = query.Where(s => s.IsEnabled == isEnabled.Value);
        }

        var result = query.OrderByDescending(s => s.UpdatedAt)
            .Select(s => new SkillItemDto
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                Category = s.Category,
                IsEnabled = s.IsEnabled,
                ToolCount = s.ToolIds.Count,
                UsageCount = s.UsageCount,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt
            })
            .ToList();

        return Task.FromResult(result);
    }

    public Task<SkillDetailDto?> GetByIdAsync(string id)
    {
        if (_skills.TryGetValue(id, out var entity))
        {
            return Task.FromResult<SkillDetailDto?>(MapToDetail(entity));
        }
        return Task.FromResult<SkillDetailDto?>(null);
    }

    public Task<SkillDetailDto> CreateAsync(CreateSkillDto dto)
    {
        var now = DateTime.UtcNow;
        var entity = new SkillEntity
        {
            Id = GetNextId(),
            Name = dto.Name,
            Description = dto.Description,
            Category = dto.Category,
            SystemPrompt = dto.SystemPrompt,
            ToolIds = dto.ToolIds.ToList(),
            IsEnabled = true,
            UsageCount = 0,
            Settings = new Dictionary<string, string>(),
            CreatedAt = now,
            UpdatedAt = now
        };

        _skills.TryAdd(entity.Id, entity);
        return Task.FromResult(MapToDetail(entity));
    }

    public Task<SkillDetailDto?> UpdateAsync(string id, UpdateSkillDto dto)
    {
        if (!_skills.TryGetValue(id, out var entity))
        {
            return Task.FromResult<SkillDetailDto?>(null);
        }

        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.Category = dto.Category;
        entity.SystemPrompt = dto.SystemPrompt;
        entity.ToolIds = dto.ToolIds.ToList();
        entity.UpdatedAt = DateTime.UtcNow;

        return Task.FromResult<SkillDetailDto?>(MapToDetail(entity));
    }

    public Task<bool> ToggleAsync(string id)
    {
        if (!_skills.TryGetValue(id, out var entity))
        {
            return Task.FromResult(false);
        }

        entity.IsEnabled = !entity.IsEnabled;
        entity.UpdatedAt = DateTime.UtcNow;
        return Task.FromResult(true);
    }

    public Task<Dictionary<string, string>?> GetSettingsAsync(string id)
    {
        if (_skills.TryGetValue(id, out var entity))
        {
            return Task.FromResult<Dictionary<string, string>?>(entity.Settings ?? new Dictionary<string, string>());
        }
        return Task.FromResult<Dictionary<string, string>?>(null);
    }

    public Task<bool> UpdateSettingsAsync(string id, Dictionary<string, string> settings)
    {
        if (!_skills.TryGetValue(id, out var entity))
        {
            return Task.FromResult(false);
        }

        entity.Settings = new Dictionary<string, string>(settings);
        entity.UpdatedAt = DateTime.UtcNow;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(string id)
    {
        return Task.FromResult(_skills.TryRemove(id, out _));
    }

    private static SkillDetailDto MapToDetail(SkillEntity entity)
    {
        return new SkillDetailDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Category = entity.Category,
            IsEnabled = entity.IsEnabled,
            SystemPrompt = entity.SystemPrompt,
            ToolIds = entity.ToolIds.ToList(),
            UsageCount = entity.UsageCount,
            Settings = entity.Settings?.ToDictionary(kv => kv.Key, kv => kv.Value),
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
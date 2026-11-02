using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Entities;
using ForgeSelf.Api.Models.Skills;

namespace ForgeSelf.Api.Services.Skills;

/// <summary>
/// 宿主个人技能库。行落在 ForgeSelf SQLite（XCode <see cref="PersonalSkill"/>），重启不丢。
/// CRUD 字段与原先内存实现一致；技能正文同时写成 SKILL.md 存在 <see cref="PersonalSkill.SkillMarkdown"/>。
/// </summary>
public class SkillsService : ISkillsService, IPersonalSkillCatalog
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly object _gate = new();
    private bool _ready;

    public Task<List<SkillItemDto>> GetAllAsync(string? keyword = null, string? category = null, bool? isEnabled = null)
    {
        var entities = LoadAll();
        IEnumerable<SkillEntity> query = entities;

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
        var entity = Find(id);
        return Task.FromResult(entity == null ? null : MapToDetail(entity));
    }

    public Task<SkillDetailDto> CreateAsync(CreateSkillDto dto)
    {
        lock (_gate)
        {
            EnsureReady();
            var now = DateTime.UtcNow;
            var entity = new SkillEntity
            {
                Id = AllocateId(),
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
            Insert(entity);
            return Task.FromResult(MapToDetail(entity));
        }
    }

    public Task<SkillDetailDto?> UpdateAsync(string id, UpdateSkillDto dto)
    {
        lock (_gate)
        {
            EnsureReady();
            var row = PersonalSkill.FindBySkillId(id);
            if (row == null)
                return Task.FromResult<SkillDetailDto?>(null);

            row.Name = dto.Name ?? string.Empty;
            row.Description = dto.Description ?? string.Empty;
            row.Category = dto.Category ?? string.Empty;
            row.SystemPrompt = dto.SystemPrompt ?? string.Empty;
            row.ToolIdsJson = SerializeList(dto.ToolIds);
            row.SkillMarkdown = SkillMarkdown.Compose(row.Name, row.Description, row.SystemPrompt);
            row.UpdatedAt = DateTime.UtcNow;
            row.Update();
            return Task.FromResult<SkillDetailDto?>(MapToDetail(ToEntity(row)));
        }
    }

    public Task<bool> ToggleAsync(string id)
    {
        lock (_gate)
        {
            EnsureReady();
            var row = PersonalSkill.FindBySkillId(id);
            if (row == null)
                return Task.FromResult(false);

            row.IsEnabled = !row.IsEnabled;
            row.UpdatedAt = DateTime.UtcNow;
            row.Update();
            return Task.FromResult(true);
        }
    }

    public Task<Dictionary<string, string>?> GetSettingsAsync(string id)
    {
        var entity = Find(id);
        if (entity == null)
            return Task.FromResult<Dictionary<string, string>?>(null);
        return Task.FromResult<Dictionary<string, string>?>(entity.Settings ?? new Dictionary<string, string>());
    }

    public Task<bool> UpdateSettingsAsync(string id, Dictionary<string, string> settings)
    {
        lock (_gate)
        {
            EnsureReady();
            var row = PersonalSkill.FindBySkillId(id);
            if (row == null)
                return Task.FromResult(false);

            row.SettingsJson = JsonSerializer.Serialize(settings, JsonOpts);
            row.UpdatedAt = DateTime.UtcNow;
            row.Update();
            return Task.FromResult(true);
        }
    }

    public Task<bool> DeleteAsync(string id)
    {
        lock (_gate)
        {
            EnsureReady();
            var row = PersonalSkill.FindBySkillId(id);
            if (row == null)
                return Task.FromResult(false);
            row.Delete();
            return Task.FromResult(true);
        }
    }

    public IReadOnlyList<PersonalSkillSnapshot> ListEnabled()
    {
        return LoadAll()
            .Where(s => s.IsEnabled)
            .Select(s => new PersonalSkillSnapshot
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                SkillMarkdown = s.SkillMarkdown
            })
            .ToList();
    }

    private SkillEntity? Find(string id)
    {
        lock (_gate)
        {
            EnsureReady();
            var row = PersonalSkill.FindBySkillId(id);
            return row == null ? null : ToEntity(row);
        }
    }

    private List<SkillEntity> LoadAll()
    {
        lock (_gate)
        {
            EnsureReady();
            return PersonalSkill.FindAllSkills().Select(ToEntity).ToList();
        }
    }

    private void EnsureReady()
    {
        if (_ready) return;
        if (!PersonalSkill.EnsureCreated())
            throw new InvalidOperationException("个人技能库数据库未就绪");
        if (PersonalSkill.FindCount() == 0)
            SeedData();
        _ready = true;
    }

    private void SeedData()
    {
        var now = DateTime.UtcNow;
        var samples = new[]
        {
            Sample("skill_2", "代码审查", "自动化代码审查和最佳实践检查，支持多种编程语言", "开发", true,
                "你是一个经验丰富的代码审查专家，能够检查代码质量、安全性和最佳实践。",
                ["code-reader", "code-search", "diff-viewer"], 128,
                new Dictionary<string, string> { ["autoReview"] = "true", ["strictMode"] = "false" },
                now.AddDays(-30), now.AddDays(-1)),
            Sample("skill_3", "文档生成", "根据代码自动生成项目文档和API参考", "文档", true,
                "你是一个专业的技术文档写手，能够从代码中提取信息并生成清晰的结构化文档。",
                ["code-reader", "markdown-writer"], 95,
                new Dictionary<string, string> { ["outputFormat"] = "markdown", ["includeExamples"] = "true" },
                now.AddDays(-28), now.AddDays(-2)),
            Sample("skill_4", "数据分析", "对数据集进行探索性分析和可视化", "数据", true,
                "你是一个数据分析专家，擅长数据清洗、统计分析和可视化。",
                ["csv-reader", "chart-generator", "statistics"], 67,
                new Dictionary<string, string> { ["defaultChartType"] = "bar" },
                now.AddDays(-25), now.AddDays(-3)),
            Sample("skill_5", "单元测试生成", "自动生成单元测试代码，支持多种测试框架", "测试", false,
                "你是一个测试专家，能够为代码自动生成全面可靠的单元测试。",
                ["code-reader", "test-runner"], 42,
                new Dictionary<string, string> { ["framework"] = "xunit", ["coverage"] = "80" },
                now.AddDays(-20), now.AddDays(-5)),
            Sample("skill_6", "数据库查询优化", "分析和优化数据库查询性能", "数据库", true,
                "你是一个数据库优化专家，擅长SQL分析和索引优化。",
                ["sql-analyzer", "index-advisor", "query-profiler"], 34,
                new Dictionary<string, string> { ["dbType"] = "postgresql" },
                now.AddDays(-15), now.AddDays(-1))
        };

        foreach (var entity in samples)
            Insert(entity);
    }

    private static SkillEntity Sample(
        string id, string name, string description, string category, bool enabled,
        string systemPrompt, string[] tools, int usage, Dictionary<string, string> settings,
        DateTime createdAt, DateTime updatedAt)
        => new()
        {
            Id = id,
            Name = name,
            Description = description,
            Category = category,
            IsEnabled = enabled,
            SystemPrompt = systemPrompt,
            ToolIds = tools.ToList(),
            UsageCount = usage,
            Settings = settings,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };

    /// <summary>与旧内存实现一致：空库第一条是 skill_2。</summary>
    private static string AllocateId()
    {
        var max = 1;
        foreach (var row in PersonalSkill.FindAllSkills())
        {
            var skillId = row.SkillId ?? string.Empty;
            if (skillId.StartsWith("skill_", StringComparison.Ordinal)
                && int.TryParse(skillId.AsSpan("skill_".Length), out var n)
                && n > max)
            {
                max = n;
            }
        }
        return $"skill_{max + 1}";
    }

    private static void Insert(SkillEntity entity)
    {
        var row = new PersonalSkill
        {
            SkillId = entity.Id,
            Name = entity.Name ?? string.Empty,
            Description = entity.Description ?? string.Empty,
            Category = entity.Category ?? string.Empty,
            IsEnabled = entity.IsEnabled,
            SystemPrompt = entity.SystemPrompt ?? string.Empty,
            ToolIdsJson = SerializeList(entity.ToolIds),
            SettingsJson = JsonSerializer.Serialize(entity.Settings ?? new Dictionary<string, string>(), JsonOpts),
            SkillMarkdown = SkillMarkdown.Compose(entity.Name, entity.Description, entity.SystemPrompt),
            UsageCount = entity.UsageCount,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
        row.Insert();
        entity.SkillMarkdown = row.SkillMarkdown;
    }

    private static SkillEntity ToEntity(PersonalSkill row)
    {
        return new SkillEntity
        {
            Id = row.SkillId ?? string.Empty,
            Name = row.Name ?? string.Empty,
            Description = row.Description ?? string.Empty,
            Category = row.Category ?? string.Empty,
            IsEnabled = row.IsEnabled,
            SystemPrompt = row.SystemPrompt ?? string.Empty,
            ToolIds = DeserializeList(row.ToolIdsJson),
            UsageCount = row.UsageCount,
            Settings = DeserializeSettings(row.SettingsJson),
            SkillMarkdown = row.SkillMarkdown ?? string.Empty,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt
        };
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

    private static string SerializeList(IEnumerable<string>? toolIds)
        => JsonSerializer.Serialize(toolIds?.ToList() ?? [], JsonOpts);

    private static List<string> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static Dictionary<string, string> DeserializeSettings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, string>();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }
}

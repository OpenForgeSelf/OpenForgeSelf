using System.Diagnostics;
using System.Text.Json;
using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Core;
using OpenForgeSelf.Backend.Plugins.MemorySystem.Data;
using OpenForgeSelf.Backend.Plugins.MemorySystem.Models;
using OpenForgeSelf.Backend.Plugins.MemorySystem.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.MemorySystem;

public class MemorySystemPlugin : IPlugin
{
    public List<IMenuExtension> MenuExtensions { get; private set; } = new();
    public List<IToolFunctionExtension> ToolExtensions { get; private set; } = new();

    public void Apply(IContext ctx)
    {
        var pluginId = ctx.Get<PluginMetadata>()?.Id ?? "";
        XTrace.Log.Info("[MemorySystem] 初始化记忆系统插件");

        var services = ctx.Get<IServiceCollection>();
        services?.AddScoped<IMemoryService, MemoryServiceXCode>();
        services?.AddScoped<IMemoryIntegrationService>(sp =>
            new MemoryIntegrationService(sp.GetRequiredService<IMemoryService>(), ctx.GetPluginDataDirectory()));

        RegisterMenuExtensions(pluginId);
        RegisterToolExtensions(pluginId, ctx);
        EnsureDatabaseCreated();

        XTrace.Log.Info("[MemorySystem] 记忆系统插件初始化完成");
    }

    private void RegisterMenuExtensions(string pluginId)
    {
        MenuExtensions.Add(new MemorySystemMenuExtension
        {
            Id = "memorysystem.menu.main",
            Name = "记忆管理",
            PluginId = pluginId,
            Icon = "fa-brain",
            Path = "/memory",
            Order = 50,
            ParentId = null
        });

        XTrace.Log.Debug("[MemorySystem] 菜单扩展点注册完成，共 {0} 个菜单项", MenuExtensions.Count);
    }

    private void RegisterToolExtensions(string pluginId, IServiceProvider services)
    {
        ToolExtensions.Add(new GetRelevantMemoriesToolFunction(services)
        {
            Id = "memorysystem.tool.get_relevant_memories",
            Name = "get_relevant_memories",
            PluginId = pluginId,
            Description = "从长期记忆中检索与当前问题相关的记忆，支持语义相似度搜索",
            ParametersJsonSchema = @"
{
    ""type"": ""object"",
    ""properties"": {
        ""query"": {
            ""type"": ""string"",
            ""description"": ""查询关键词或问题描述，用于搜索相关记忆""
        },
        ""limit"": {
            ""type"": ""integer"",
            ""description"": ""返回结果最大数量，默认5条"",
            ""default"": 5
        },
        ""minScore"": {
            ""type"": ""number"",
            ""description"": ""最低相关度阈值，0-1之间，默认0.1"",
            ""default"": 0.1
        }
    },
    ""required"": [""query""]
}"
        });

        ToolExtensions.Add(new AddMemoryToolFunction(services)
        {
            Id = "memorysystem.tool.add_memory",
            Name = "add_memory",
            PluginId = pluginId,
            Description = "添加一条新的长期记忆，保存重要信息供以后使用",
            ParametersJsonSchema = @"
{
    ""type"": ""object"",
    ""properties"": {
        ""title"": {
            ""type"": ""string"",
            ""description"": ""记忆标题，简洁概括记忆内容""
        },
        ""content"": {
            ""type"": ""string"",
            ""description"": ""记忆的详细内容""
        },
        ""type"": {
            ""type"": ""string"",
            ""description"": ""记忆类型：fact/preference/project/personal/workflow/skill/other"",
            ""default"": ""fact""
        },
        ""importance"": {
            ""type"": ""string"",
            ""description"": ""重要程度：low/medium/high/critical"",
            ""default"": ""medium""
        },
        ""tags"": {
            ""type"": ""array"",
            ""items"": { ""type"": ""string"" },
            ""description"": ""标签列表，便于分类和搜索""
        },
        ""source"": {
            ""type"": ""string"",
            ""description"": ""记忆来源，如对话ID、用户输入等""
        }
    },
    ""required"": [""title"", ""content""]
}"
        });

        ToolExtensions.Add(new SearchMemoriesToolFunction(services)
        {
            Id = "memorysystem.tool.search_memories",
            Name = "search_memories",
            PluginId = pluginId,
            Description = "搜索记忆，支持按关键词、类型、分类、标签等条件筛选",
            ParametersJsonSchema = @"
{
    ""type"": ""object"",
    ""properties"": {
        ""keyword"": {
            ""type"": ""string"",
            ""description"": ""搜索关键词""
        },
        ""type"": {
            ""type"": ""string"",
            ""description"": ""记忆类型筛选：fact/preference/project/personal/workflow/skill/other""
        },
        ""tag"": {
            ""type"": ""string"",
            ""description"": ""标签筛选""
        },
        ""page"": {
            ""type"": ""integer"",
            ""description"": ""页码，默认1"",
            ""default"": 1
        },
        ""pageSize"": {
            ""type"": ""integer"",
            ""description"": ""每页数量，默认20"",
            ""default"": 20
        }
    },
    ""required"": []
}"
        });

        ToolExtensions.Add(new UpdateMemoryToolFunction(services)
        {
            Id = "memorysystem.tool.update_memory",
            Name = "update_memory",
            PluginId = pluginId,
            Description = "更新已有的记忆条目",
            ParametersJsonSchema = @"
{
    ""type"": ""object"",
    ""properties"": {
        ""memoryId"": {
            ""type"": ""integer"",
            ""description"": ""要更新的记忆ID""
        },
        ""title"": {
            ""type"": ""string"",
            ""description"": ""新的标题（可选）""
        },
        ""content"": {
            ""type"": ""string"",
            ""description"": ""新的内容（可选）""
        },
        ""importance"": {
            ""type"": ""string"",
            ""description"": ""新的重要程度（可选）：low/medium/high/critical""
        },
        ""tags"": {
            ""type"": ""array"",
            ""items"": { ""type"": ""string"" },
            ""description"": ""新的标签列表（可选）""
        }
    },
    ""required"": [""memoryId""]
}"
        });

        ToolExtensions.Add(new GetMemoryStatsToolFunction(services)
        {
            Id = "memorysystem.tool.get_memory_stats",
            Name = "get_memory_stats",
            PluginId = pluginId,
            Description = "获取记忆系统的统计信息，包括记忆总数、分类统计、最近记忆等",
            ParametersJsonSchema = @"
{
    ""type"": ""object"",
    ""properties"": {},
    ""required"": []
}"
        });

        XTrace.Log.Debug("[MemorySystem] AI工具函数扩展点注册完成，共 {0} 个工具函数", ToolExtensions.Count);
    }

    private void EnsureDatabaseCreated()
    {
        try
        {
            var dbPath = Path.Combine(AppContext.BaseDirectory, "memory.db");
            var optionsBuilder = new DbContextOptionsBuilder<MemoryDbContext>();
            optionsBuilder.UseSqlite($"Data Source={dbPath}");

            using var dbContext = new MemoryDbContext(optionsBuilder.Options);
            dbContext.Database.EnsureCreated();

            SeedDefaultCategories(dbContext);

            XTrace.Log.Info("[MemorySystem] 数据库初始化完成，路径: {0}", dbPath);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[MemorySystem] 数据库初始化失败: {0}", ex.Message);
        }
    }

    private static void SeedDefaultCategories(MemoryDbContext dbContext)
    {
        if (dbContext.MemoryCategories.Any()) return;

        var defaultCategories = new List<MemoryCategoryEntity>
        {
            new() { Name = "个人信息", Icon = "fa-user", SortOrder = 1, Description = "个人基本信息和偏好" },
            new() { Name = "工作项目", Icon = "fa-briefcase", SortOrder = 2, Description = "工作相关的记忆和项目信息" },
            new() { Name = "技能知识", Icon = "fa-lightbulb", SortOrder = 3, Description = "学到的技能和知识" },
            new() { Name = "常用工具", Icon = "fa-tools", SortOrder = 4, Description = "常用工具和工作流" },
            new() { Name = "其他", Icon = "fa-folder", SortOrder = 100, Description = "其他分类的记忆" }
        };

        dbContext.MemoryCategories.AddRange(defaultCategories);
        dbContext.SaveChanges();
    }
}

public class MemorySystemMenuExtension : IMenuExtension
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PluginId { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int Order { get; set; }
    public string? ParentId { get; set; }
    public IReadOnlyList<IMenuExtension>? Children { get; set; }
}

public abstract class MemoryToolFunctionBase : IToolFunctionExtension
{
    protected readonly IServiceProvider? _serviceProvider;

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PluginId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ParametersJsonSchema { get; set; } = string.Empty;

    protected MemoryToolFunctionBase(IServiceProvider? serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public abstract Task<string> ExecuteAsync(string parameters);

    protected async Task RecordUsageAsync(string actionType, long durationMs, Dictionary<string, object>? metadata = null)
    {
        try
        {
            if (_serviceProvider == null) return;

            using var scope = _serviceProvider.CreateScope();
            var usageStatsService = scope.ServiceProvider.GetService<IUsageStatsService>();
            if (usageStatsService != null)
            {
                await usageStatsService.RecordUsageAsync(
                    PluginId,
                    Id,
                    actionType,
                    durationMs,
                    metadata);
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[MemorySystem] 记录使用统计失败: {0}", ex.Message);
        }
    }

    protected static MemoryService CreateMemoryService()
    {
        var dbPath = Path.Combine(AppContext.BaseDirectory, "memory.db");
        var optionsBuilder = new DbContextOptionsBuilder<MemoryDbContext>();
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
        var dbContext = new MemoryDbContext(optionsBuilder.Options);
        return new MemoryService(dbContext);
    }

    protected static MemoryType ParseMemoryType(string? typeStr)
    {
        if (string.IsNullOrWhiteSpace(typeStr)) return MemoryType.Fact;
        return typeStr.ToLower() switch
        {
            "fact" => MemoryType.Fact,
            "preference" => MemoryType.Preference,
            "project" => MemoryType.Project,
            "personal" => MemoryType.Personal,
            "workflow" => MemoryType.Workflow,
            "skill" => MemoryType.Skill,
            _ => MemoryType.Other
        };
    }

    protected static MemoryImportance ParseImportance(string? impStr)
    {
        if (string.IsNullOrWhiteSpace(impStr)) return MemoryImportance.Medium;
        return impStr.ToLower() switch
        {
            "low" => MemoryImportance.Low,
            "medium" => MemoryImportance.Medium,
            "high" => MemoryImportance.High,
            "critical" => MemoryImportance.Critical,
            _ => MemoryImportance.Medium
        };
    }
}

public class GetRelevantMemoriesToolFunction : MemoryToolFunctionBase
{
    public GetRelevantMemoriesToolFunction(IServiceProvider? serviceProvider) : base(serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[MemorySystem] 执行 get_relevant_memories 工具函数，参数: {0}", parameters);

            using var doc = JsonDocument.Parse(parameters);
            var root = doc.RootElement;
            var query = root.GetProperty("query").GetString() ?? string.Empty;

            int limit = 5;
            if (root.TryGetProperty("limit", out var limitProp))
                limit = limitProp.GetInt32();

            double minScore = 0.1;
            if (root.TryGetProperty("minScore", out var minScoreProp))
                minScore = minScoreProp.GetDouble();

            var service = CreateMemoryService();
            var memories = await service.GetRelevantMemoriesAsync(query, limit, minScore);

            var response = new
            {
                success = true,
                query = query,
                total = memories.Count,
                limit = limit,
                memories = memories.Select(m => new
                {
                    id = m.Id,
                    title = m.Title,
                    content = m.Content,
                    type = m.Type.ToString(),
                    importance = m.Importance.ToString(),
                    tags = m.Tags,
                    relevanceScore = m.RelevanceScore,
                    source = m.Source,
                    categoryName = m.CategoryName
                }).ToList()
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("get_relevant", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["queryLength"] = query.Length,
                ["resultCount"] = memories.Count
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[MemorySystem] get_relevant_memories 执行失败: {0}", ex.Message);
            stopwatch.Stop();
            await RecordUsageAsync("get_relevant", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }
}

public class AddMemoryToolFunction : MemoryToolFunctionBase
{
    public AddMemoryToolFunction(IServiceProvider? serviceProvider) : base(serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[MemorySystem] 执行 add_memory 工具函数，参数: {0}", parameters);

            using var doc = JsonDocument.Parse(parameters);
            var root = doc.RootElement;

            var title = root.GetProperty("title").GetString() ?? string.Empty;
            var content = root.GetProperty("content").GetString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
                return JsonSerializer.Serialize(new { success = false, error = "标题和内容不能为空" });

            var typeStr = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;
            var impStr = root.TryGetProperty("importance", out var impProp) ? impProp.GetString() : null;
            var source = root.TryGetProperty("source", out var sourceProp) ? sourceProp.GetString() : null;

            List<string>? tags = null;
            if (root.TryGetProperty("tags", out var tagsProp))
            {
                tags = new List<string>();
                foreach (var tag in tagsProp.EnumerateArray())
                {
                    var tagStr = tag.GetString();
                    if (!string.IsNullOrEmpty(tagStr))
                        tags.Add(tagStr);
                }
            }

            var service = CreateMemoryService();

            var request = new CreateMemoryRequest
            {
                Title = title,
                Content = content,
                Type = ParseMemoryType(typeStr),
                Importance = ParseImportance(impStr),
                Tags = tags,
                Source = source
            };

            var memory = await service.CreateAsync(request);

            var response = new
            {
                success = true,
                message = "记忆添加成功",
                memory = new
                {
                    id = memory.Id,
                    title = memory.Title,
                    content = memory.Content,
                    type = memory.Type.ToString(),
                    importance = memory.Importance.ToString(),
                    tags = memory.Tags,
                    createdAt = memory.CreatedAt
                }
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("add_memory", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["memoryId"] = memory.Id,
                ["memoryType"] = memory.Type.ToString()
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[MemorySystem] add_memory 执行失败: {0}", ex.Message);
            stopwatch.Stop();
            await RecordUsageAsync("add_memory", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }
}

public class SearchMemoriesToolFunction : MemoryToolFunctionBase
{
    public SearchMemoriesToolFunction(IServiceProvider? serviceProvider) : base(serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[MemorySystem] 执行 search_memories 工具函数，参数: {0}", parameters);

            using var doc = JsonDocument.Parse(parameters);
            var root = doc.RootElement;

            string? keyword = null;
            if (root.TryGetProperty("keyword", out var kwProp))
                keyword = kwProp.GetString();

            string? typeStr = null;
            MemoryType? type = null;
            if (root.TryGetProperty("type", out var typeProp))
            {
                typeStr = typeProp.GetString();
                type = ParseMemoryType(typeStr);
            }

            string? tag = null;
            if (root.TryGetProperty("tag", out var tagProp))
                tag = tagProp.GetString();

            int page = 1;
            if (root.TryGetProperty("page", out var pageProp))
                page = pageProp.GetInt32();

            int pageSize = 20;
            if (root.TryGetProperty("pageSize", out var pageSizeProp))
                pageSize = pageSizeProp.GetInt32();

            var service = CreateMemoryService();

            var request = new SearchMemoryRequest
            {
                Keyword = keyword,
                Type = type,
                Tag = tag,
                Page = page,
                PageSize = pageSize
            };

            var result = await service.SearchAsync(request);

            var response = new
            {
                success = true,
                total = result.Total,
                page = result.Page,
                pageSize = result.PageSize,
                memories = result.Items.Select(m => new
                {
                    id = m.Id,
                    title = m.Title,
                    content = m.Content,
                    type = m.Type.ToString(),
                    importance = m.Importance.ToString(),
                    tags = m.Tags,
                    categoryName = m.CategoryName,
                    accessCount = m.AccessCount
                }).ToList()
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("search_memories", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["keyword"] = keyword ?? string.Empty,
                ["total"] = result.Total
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[MemorySystem] search_memories 执行失败: {0}", ex.Message);
            stopwatch.Stop();
            await RecordUsageAsync("search_memories", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }
}

public class UpdateMemoryToolFunction : MemoryToolFunctionBase
{
    public UpdateMemoryToolFunction(IServiceProvider? serviceProvider) : base(serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[MemorySystem] 执行 update_memory 工具函数，参数: {0}", parameters);

            using var doc = JsonDocument.Parse(parameters);
            var root = doc.RootElement;

            var memoryId = root.GetProperty("memoryId").GetInt64();

            var service = CreateMemoryService();

            var request = new UpdateMemoryRequest();

            if (root.TryGetProperty("title", out var titleProp))
                request.Title = titleProp.GetString();

            if (root.TryGetProperty("content", out var contentProp))
                request.Content = contentProp.GetString();

            if (root.TryGetProperty("importance", out var impProp))
                request.Importance = ParseImportance(impProp.GetString());

            if (root.TryGetProperty("tags", out var tagsProp))
            {
                var tags = new List<string>();
                foreach (var tag in tagsProp.EnumerateArray())
                {
                    var tagStr = tag.GetString();
                    if (!string.IsNullOrEmpty(tagStr))
                        tags.Add(tagStr);
                }
                request.Tags = tags;
            }

            var memory = await service.UpdateAsync(memoryId, request);
            if (memory == null)
                return JsonSerializer.Serialize(new { success = false, error = "记忆不存在" });

            var response = new
            {
                success = true,
                message = "记忆更新成功",
                memory = new
                {
                    id = memory.Id,
                    title = memory.Title,
                    content = memory.Content,
                    type = memory.Type.ToString(),
                    importance = memory.Importance.ToString(),
                    tags = memory.Tags
                }
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("update_memory", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["memoryId"] = memoryId
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[MemorySystem] update_memory 执行失败: {0}", ex.Message);
            stopwatch.Stop();
            await RecordUsageAsync("update_memory", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }
}

public class GetMemoryStatsToolFunction : MemoryToolFunctionBase
{
    public GetMemoryStatsToolFunction(IServiceProvider? serviceProvider) : base(serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[MemorySystem] 执行 get_memory_stats 工具函数");

            var service = CreateMemoryService();
            var stats = await service.GetStatsAsync();

            var response = new
            {
                success = true,
                stats = new
                {
                    totalMemories = stats.TotalMemories,
                    totalCategories = stats.TotalCategories,
                    todayAccessed = stats.TodayAccessed,
                    weekAccessed = stats.WeekAccessed,
                    byType = stats.ByType.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
                    byImportance = stats.ByImportance.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
                    recentMemories = stats.RecentMemories.Select(m => new
                    {
                        id = m.Id,
                        title = m.Title,
                        type = m.Type.ToString()
                    }).ToList(),
                    frequentlyAccessed = stats.FrequentlyAccessed.Select(m => new
                    {
                        id = m.Id,
                        title = m.Title,
                        accessCount = m.AccessCount
                    }).ToList()
                }
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("get_stats", stopwatch.ElapsedMilliseconds);

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[MemorySystem] get_memory_stats 执行失败: {0}", ex.Message);
            stopwatch.Stop();
            await RecordUsageAsync("get_stats", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }
}

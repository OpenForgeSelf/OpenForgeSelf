using System.Diagnostics;
using System.Text.Json;
using OpenForgeSelf.Backend.Plugins.Abstractions;
using OpenForgeSelf.Backend.Plugins.QuickLinks.Entities;
using OpenForgeSelf.Backend.Plugins.QuickLinks.Models;
using OpenForgeSelf.Backend.Plugins.QuickLinks.Services;
using OpenForgeSelf.Backend.Services.UsageStats;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;
using XCode.DataAccessLayer;

namespace OpenForgeSelf.Backend.Plugins.QuickLinks;

public class QuickLinksPlugin : IPlugin
{
    public string Id => "quicklinks.plugin";
    public string Name => "快捷链接插件";
    public string Version => "1.0.0";
    public string Author => "OpenForgeSelf Team";
    public string Description => "快捷链接管理插件，支持链接分类、排序、导入导出等功能。";
    public string IconUrl => "https://example.com/quicklinks-icon.png";

    public List<IMenuExtension> MenuExtensions { get; private set; } = new();
    public List<IToolFunctionExtension> ToolExtensions { get; private set; } = new();

    private IServiceProvider? _serviceProvider;

    public void Initialize(IServiceProvider services)
    {
        try
        {
            XTrace.Log.Info("初始化快捷链接插件");
            _serviceProvider = services;

            RegisterMenuExtensions();
            RegisterToolExtensions();

            XTrace.Log.Info("快捷链接插件初始化完成");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("快捷链接插件初始化失败: {0}", ex.Message);
            throw;
        }
    }

    public void Start()
    {
        try
        {
            XTrace.Log.Info("启动快捷链接插件");

            EnsureDatabaseCreated();

            XTrace.Log.Info("快捷链接插件启动完成");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("快捷链接插件启动失败: {0}", ex.Message);
            throw;
        }
    }

    public void Stop()
    {
        try
        {
            XTrace.Log.Info("停止快捷链接插件");
            XTrace.Log.Info("快捷链接插件已停止");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("快捷链接插件停止失败: {0}", ex.Message);
            throw;
        }
    }

    public void Destroy()
    {
        try
        {
            XTrace.Log.Info("销毁快捷链接插件");
            MenuExtensions.Clear();
            ToolExtensions.Clear();
            XTrace.Log.Info("快捷链接插件已销毁");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("快捷链接插件销毁失败: {0}", ex.Message);
            throw;
        }
    }

    private void RegisterMenuExtensions()
    {
        MenuExtensions.Add(new QuickLinksMenuExtension
        {
            Id = "quicklinks.menu.main",
            Name = "快捷链接",
            PluginId = Id,
            Icon = "fa-link",
            Path = "/quicklinks",
            Order = 100,
            ParentId = null
        });

        XTrace.Log.Debug("快捷链接插件已注册菜单扩展点");
    }

    private void RegisterToolExtensions()
    {
        ToolExtensions.Add(new SearchLinksToolFunction(_serviceProvider)
        {
            Id = "quicklinks.tool.search",
            Name = "search_links",
            PluginId = Id,
            Description = "搜索快捷链接，根据关键词查找相关的链接，支持按分类筛选和限制返回数量",
            ParametersJsonSchema = @"
{
    ""type"": ""object"",
    ""properties"": {
        ""keyword"": {
            ""type"": ""string"",
            ""description"": ""搜索关键词，用于匹配链接名称、URL和描述""
        },
        ""categoryId"": {
            ""type"": ""integer"",
            ""description"": ""分类ID，用于限定搜索范围到指定分类""
        },
        ""limit"": {
            ""type"": ""integer"",
            ""description"": ""返回结果的最大数量，默认10条"",
            ""default"": 10
        }
    },
    ""required"": [""keyword""]
}"
        });

        ToolExtensions.Add(new OpenLinkToolFunction(_serviceProvider)
        {
            Id = "quicklinks.tool.open",
            Name = "open_link",
            PluginId = Id,
            Description = "打开指定的快捷链接，并记录点击次数",
            ParametersJsonSchema = @"
{
    ""type"": ""object"",
    ""properties"": {
        ""linkId"": {
            ""type"": ""integer"",
            ""description"": ""链接的唯一标识符ID""
        }
    },
    ""required"": [""linkId""]
}"
        });

        ToolExtensions.Add(new GetCategoriesToolFunction(_serviceProvider)
        {
            Id = "quicklinks.tool.get_categories",
            Name = "get_categories",
            PluginId = Id,
            Description = "获取所有快捷链接分类列表",
            ParametersJsonSchema = @"
{
    ""type"": ""object"",
    ""properties"": {},
    ""required"": []
}"
        });

        ToolExtensions.Add(new CreateLinkToolFunction(_serviceProvider)
        {
            Id = "quicklinks.tool.create",
            Name = "create_link",
            PluginId = Id,
            Description = "创建一个新的快捷链接",
            ParametersJsonSchema = @"
{
    ""type"": ""object"",
    ""properties"": {
        ""name"": {
            ""type"": ""string"",
            ""description"": ""链接名称，用于显示""
        },
        ""url"": {
            ""type"": ""string"",
            ""description"": ""链接的URL地址""
        },
        ""icon"": {
            ""type"": ""string"",
            ""description"": ""链接图标，可以是图标类名或图片URL""
        },
        ""description"": {
            ""type"": ""string"",
            ""description"": ""链接的描述信息""
        },
        ""categoryId"": {
            ""type"": ""integer"",
            ""description"": ""所属分类的ID""
        }
    },
    ""required"": [""name"", ""url""]
}"
        });

        XTrace.Log.Debug("快捷链接插件已注册AI工具函数扩展点，共 {0} 个工具", ToolExtensions.Count);
    }

    private void EnsureDatabaseCreated()
    {
        try
        {
            var dbPath = Path.Combine(AppContext.BaseDirectory, "quicklinks.db");
            var connStr = $"Data Source={dbPath}";

            DAL.AddConnStr("QuickLinks", connStr, null, "SQLite");

            XTrace.Log.Info("快捷链接插件数据库初始化完成，数据库路径: {0}", dbPath);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("快捷链接插件数据库初始化失败: {0}", ex.Message);
        }
    }
}

public class QuickLinksMenuExtension : IMenuExtension
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

public class SearchLinksToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PluginId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ParametersJsonSchema { get; set; } = string.Empty;

    public SearchLinksToolFunction(IServiceProvider? serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[QuickLinks] 执行 search_links 工具函数，参数: {0}", parameters);

            using var doc = JsonDocument.Parse(parameters);
            var root = doc.RootElement;
            var keyword = root.GetProperty("keyword").GetString() ?? string.Empty;

            long? categoryId = null;
            if (root.TryGetProperty("categoryId", out var categoryIdProp))
            {
                categoryId = categoryIdProp.GetInt64();
            }

            int limit = 10;
            if (root.TryGetProperty("limit", out var limitProp))
            {
                limit = limitProp.GetInt32();
                limit = Math.Clamp(limit, 1, 100);
            }

            var service = new QuickLinkService();

            var result = await service.GetLinksAsync(categoryId, keyword, 1, limit);

            var response = new
            {
                success = true,
                total = result.Total,
                limit = limit,
                categoryId = categoryId,
                links = result.Items.Select(l => new
                {
                    id = l.Id,
                    name = l.Name,
                    url = l.Url,
                    icon = l.Icon,
                    description = l.Description,
                    categoryId = l.CategoryId,
                    categoryName = l.CategoryName,
                    clickCount = l.ClickCount
                }).ToList()
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("search", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["keyword"] = keyword,
                ["categoryId"] = categoryId ?? 0,
                ["limit"] = limit,
                ["resultCount"] = result.Total
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[QuickLinks] 执行 search_links 工具函数失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("search", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });

            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    private async Task RecordUsageAsync(string actionType, long durationMs, Dictionary<string, object>? metadata = null)
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
            XTrace.Log.Warn("[QuickLinks] 记录使用统计失败: {0}", ex.Message);
        }
    }
}

public class OpenLinkToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PluginId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ParametersJsonSchema { get; set; } = string.Empty;

    public OpenLinkToolFunction(IServiceProvider? serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("执行 open_link 工具函数，参数: {0}", parameters);

            using var doc = JsonDocument.Parse(parameters);
            var root = doc.RootElement;
            var linkId = root.GetProperty("linkId").GetInt64();

            var service = new QuickLinkService();

            var link = await service.GetLinkByIdAsync(linkId);
            if (link == null)
            {
                stopwatch.Stop();
                await RecordUsageAsync("open", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
                {
                    ["linkId"] = linkId,
                    ["error"] = "链接不存在"
                });

                return JsonSerializer.Serialize(new { success = false, error = "链接不存在" });
            }

            await service.IncrementClickCountAsync(linkId);

            var response = new
            {
                success = true,
                link = new
                {
                    id = link.Id,
                    name = link.Name,
                    url = link.Url,
                    description = link.Description
                },
                message = "链接已打开，点击次数已记录"
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("open", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["linkId"] = linkId,
                ["linkName"] = link.Name
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("执行 open_link 工具函数失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("open", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });

            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    private async Task RecordUsageAsync(string actionType, long durationMs, Dictionary<string, object>? metadata = null)
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
            XTrace.Log.Warn("[QuickLinks] 记录使用统计失败: {0}", ex.Message);
        }
    }
}

public class GetCategoriesToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PluginId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ParametersJsonSchema { get; set; } = string.Empty;

    public GetCategoriesToolFunction(IServiceProvider? serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[QuickLinks] 执行 get_categories 工具函数");

            var service = new QuickLinkService();

            var categories = await service.GetCategoriesAsync();

            var response = new
            {
                success = true,
                total = categories.Count,
                categories = categories.Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    icon = c.Icon,
                    sortOrder = c.SortOrder,
                    linkCount = c.LinkCount
                }).ToList()
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("get_categories", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["categoryCount"] = categories.Count
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[QuickLinks] 执行 get_categories 工具函数失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("get_categories", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });

            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    private async Task RecordUsageAsync(string actionType, long durationMs, Dictionary<string, object>? metadata = null)
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
            XTrace.Log.Warn("[QuickLinks] 记录使用统计失败: {0}", ex.Message);
        }
    }
}

public class CreateLinkToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PluginId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ParametersJsonSchema { get; set; } = string.Empty;

    public CreateLinkToolFunction(IServiceProvider? serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[QuickLinks] 执行 create_link 工具函数，参数: {0}", parameters);

            using var doc = JsonDocument.Parse(parameters);
            var root = doc.RootElement;

            var name = root.GetProperty("name").GetString() ?? string.Empty;
            var url = root.GetProperty("url").GetString() ?? string.Empty;

            string? icon = null;
            if (root.TryGetProperty("icon", out var iconProp))
            {
                icon = iconProp.GetString();
            }

            string? description = null;
            if (root.TryGetProperty("description", out var descProp))
            {
                description = descProp.GetString();
            }

            long? categoryId = null;
            if (root.TryGetProperty("categoryId", out var catIdProp))
            {
                categoryId = catIdProp.GetInt64();
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return JsonSerializer.Serialize(new { success = false, error = "链接名称不能为空" });
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                return JsonSerializer.Serialize(new { success = false, error = "链接URL不能为空" });
            }

            var service = new QuickLinkService();

            var allLinks = QuickLink.FindAll();
            var maxSortOrder = allLinks.Any() ? allLinks.Max(l => l.SortOrder) : 0;

            var request = new CreateQuickLinkRequest
            {
                Name = name,
                Url = url,
                Icon = icon,
                Description = description,
                CategoryId = categoryId,
                SortOrder = maxSortOrder + 1
            };

            var link = await service.CreateLinkAsync(request);

            var response = new
            {
                success = true,
                message = "链接创建成功",
                link = new
                {
                    id = link.Id,
                    name = link.Name,
                    url = link.Url,
                    icon = link.Icon,
                    description = link.Description,
                    categoryId = link.CategoryId,
                    categoryName = link.CategoryName,
                    sortOrder = link.SortOrder,
                    clickCount = link.ClickCount,
                    createdAt = link.CreatedAt
                }
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("create_link", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["linkId"] = link.Id,
                ["linkName"] = link.Name,
                ["categoryId"] = categoryId ?? 0
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[QuickLinks] 执行 create_link 工具函数失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("create_link", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });

            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    private async Task RecordUsageAsync(string actionType, long durationMs, Dictionary<string, object>? metadata = null)
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
            XTrace.Log.Warn("[QuickLinks] 记录使用统计失败: {0}", ex.Message);
        }
    }
}

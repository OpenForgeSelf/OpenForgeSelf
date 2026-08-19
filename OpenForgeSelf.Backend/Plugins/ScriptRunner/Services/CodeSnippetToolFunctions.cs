using System.Diagnostics;
using System.Text.Json;
using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Backend.Plugins.ScriptRunner.Models;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.ScriptRunner.Services;

public class ListCodeSnippetsToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;
    private readonly ICodeSnippetService _codeSnippetService;

    public string Id => "scriptrunner.list_code_snippets";
    public string Name => "list_code_snippets";
    public string PluginId { get; }
    public string Description => "列出代码片段库中的代码片段，支持按关键词、语言、分类、收藏状态筛选，可查看片段详情和使用次数。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""keyword"": {
            ""type"": ""string"",
            ""description"": ""搜索关键词，匹配片段标题、描述和标签""
        },
        ""language"": {
            ""type"": ""string"",
            ""description"": ""按编程语言筛选""
        },
        ""category"": {
            ""type"": ""string"",
            ""description"": ""按分类筛选""
        },
        ""isFavorite"": {
            ""type"": ""boolean"",
            ""description"": ""是否只显示收藏的片段""
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
}";

    public ListCodeSnippetsToolFunction(string pluginId, IServiceProvider? serviceProvider, ICodeSnippetService codeSnippetService)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
        _codeSnippetService = codeSnippetService;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[ScriptRunnerPlugin] 执行 list_code_snippets 工具函数");

            using var doc = JsonDocument.Parse(parameters);
            var root = doc.RootElement;

            string? keyword = null;
            if (root.TryGetProperty("keyword", out var keywordProp))
                keyword = keywordProp.GetString();

            string? language = null;
            if (root.TryGetProperty("language", out var langProp))
                language = langProp.GetString();

            string? category = null;
            if (root.TryGetProperty("category", out var catProp))
                category = catProp.GetString();

            bool? isFavorite = null;
            if (root.TryGetProperty("isFavorite", out var favProp))
                isFavorite = favProp.GetBoolean();

            int page = 1;
            if (root.TryGetProperty("page", out var pageProp))
                page = pageProp.GetInt32();

            int pageSize = 20;
            if (root.TryGetProperty("pageSize", out var pageSizeProp))
                pageSize = pageSizeProp.GetInt32();

            var result = await _codeSnippetService.ListSnippetsAsync(keyword, language, category, isFavorite, page, pageSize);

            var response = new
            {
                success = true,
                total = result.Total,
                page = result.Page,
                pageSize = result.PageSize,
                snippets = result.Items.Select(s => new
                {
                    id = s.Id,
                    title = s.Title,
                    description = s.Description,
                    language = s.Language,
                    category = s.Category,
                    tags = s.Tags,
                    isFavorite = s.IsFavorite,
                    usageCount = s.UsageCount,
                    lastUsedAt = s.LastUsedAt,
                    source = s.Source.ToString(),
                    updatedAt = s.UpdatedAt
                }).ToList()
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "list_code_snippets", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["keyword"] = keyword ?? string.Empty,
                ["total"] = result.Total
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptRunnerPlugin] list_code_snippets 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "list_code_snippets", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });

            var errorResponse = new
            {
                success = false,
                error = ex.Message
            };
            return JsonSerializer.Serialize(errorResponse);
        }
    }

}

public class CreateCodeSnippetToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;
    private readonly ICodeSnippetService _codeSnippetService;

    public string Id => "scriptrunner.create_code_snippet";
    public string Name => "create_code_snippet";
    public string PluginId { get; }
    public string Description => "创建新的代码片段，保存到代码片段库中，支持标题、描述、代码、语言、分类、标签等属性。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""title"": {
            ""type"": ""string"",
            ""description"": ""代码片段标题""
        },
        ""description"": {
            ""type"": ""string"",
            ""description"": ""代码片段描述说明""
        },
        ""code"": {
            ""type"": ""string"",
            ""description"": ""代码内容""
        },
        ""language"": {
            ""type"": ""string"",
            ""description"": ""编程语言，如 csharp、python、javascript、powershell 等""
        },
        ""category"": {
            ""type"": ""string"",
            ""description"": ""分类名称""
        },
        ""tags"": {
            ""type"": ""array"",
            ""items"": {
                ""type"": ""string""
            },
            ""description"": ""标签列表""
        }
    },
    ""required"": [""title"", ""code"", ""language""]
}";

    public CreateCodeSnippetToolFunction(string pluginId, IServiceProvider? serviceProvider, ICodeSnippetService codeSnippetService)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
        _codeSnippetService = codeSnippetService;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[ScriptRunnerPlugin] 执行 create_code_snippet 工具函数");

            using var doc = JsonDocument.Parse(parameters);
            var root = doc.RootElement;

            var title = root.GetProperty("title").GetString() ?? string.Empty;
            var code = root.GetProperty("code").GetString() ?? string.Empty;
            var language = root.GetProperty("language").GetString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(title))
            {
                return JsonSerializer.Serialize(new { success = false, error = "代码片段标题不能为空" });
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                return JsonSerializer.Serialize(new { success = false, error = "代码内容不能为空" });
            }

            if (string.IsNullOrWhiteSpace(language))
            {
                return JsonSerializer.Serialize(new { success = false, error = "编程语言不能为空" });
            }

            string? description = null;
            if (root.TryGetProperty("description", out var descProp))
                description = descProp.GetString();

            string? category = null;
            if (root.TryGetProperty("category", out var catProp))
                category = catProp.GetString();

            List<string> tags = [];
            if (root.TryGetProperty("tags", out var tagsProp))
            {
                foreach (var tag in tagsProp.EnumerateArray())
                {
                    var tagStr = tag.GetString();
                    if (!string.IsNullOrWhiteSpace(tagStr))
                        tags.Add(tagStr);
                }
            }

            var request = new CreateCodeSnippetRequest
            {
                Title = title,
                Description = description ?? string.Empty,
                Code = code,
                Language = language,
                Category = category ?? string.Empty,
                Tags = tags,
                Source = CodeSnippetSource.AIGenerated
            };

            var snippet = await _codeSnippetService.CreateSnippetAsync(request);

            var response = new
            {
                success = true,
                id = snippet.Id,
                title = snippet.Title,
                description = snippet.Description,
                code = snippet.Code,
                language = snippet.Language,
                category = snippet.Category,
                tags = snippet.Tags,
                createdAt = snippet.CreatedAt,
                message = "代码片段创建成功"
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "create_code_snippet", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["snippetId"] = snippet.Id,
                ["language"] = language,
                ["codeLength"] = code.Length
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptRunnerPlugin] create_code_snippet 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "create_code_snippet", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });

            var errorResponse = new
            {
                success = false,
                error = ex.Message
            };
            return JsonSerializer.Serialize(errorResponse);
        }
    }

}

public class GetPersonalStatsToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id => "scriptrunner.get_personal_stats";
    public string Name => "get_personal_stats";
    public string PluginId { get; }
    public string Description => "获取个人工具库统计数据，包括脚本数量、代码片段数量、工作流数量、使用次数、常用工具排行等。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""timeRange"": {
            ""type"": ""string"",
            ""enum"": [""7d"", ""30d"", ""90d"", ""all""],
            ""description"": ""统计时间范围，默认30d"",
            ""default"": ""30d""
        }
    },
    ""required"": []
}";

    public GetPersonalStatsToolFunction(string pluginId, IServiceProvider serviceProvider)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[ScriptRunnerPlugin] 执行 get_personal_stats 工具函数");

            if (_serviceProvider == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "服务提供者未初始化" });
            }

            using var doc = JsonDocument.Parse(parameters);
            var root = doc.RootElement;

            var timeRange = "30d";
            if (root.TryGetProperty("timeRange", out var rangeProp))
                timeRange = rangeProp.GetString() ?? "30d";

            using var scope = _serviceProvider.CreateScope();
            var usageStatsService = scope.ServiceProvider.GetRequiredService<IUsageStatsService>();

            var stats = await usageStatsService.GetPersonalLibraryStatsAsync(timeRange);

            var response = new
            {
                success = true,
                scriptCount = stats.ScriptCount,
                codeSnippetCount = stats.CodeSnippetCount,
                workflowCount = stats.WorkflowCount,
                favoriteCount = stats.FavoriteCount,
                totalUsageCount = stats.TotalUsageCount,
                totalUsageDurationSeconds = stats.TotalUsageDurationSeconds,
                topTools = stats.TopTools.Select(t => new
                {
                    pluginId = t.PluginId,
                    toolId = t.ToolId,
                    useCount = t.UseCount,
                    totalDurationMs = t.TotalDurationMs
                }).ToList(),
                topScripts = stats.TopScripts.Select(s => new
                {
                    scriptId = s.ScriptId,
                    scriptName = s.ScriptName,
                    language = s.Language,
                    usageCount = s.UsageCount
                }).ToList(),
                topWorkflows = stats.TopWorkflows.Select(w => new
                {
                    workflowId = w.WorkflowId,
                    workflowName = w.WorkflowName,
                    executionCount = w.ExecutionCount,
                    successRate = w.SuccessRate
                }).ToList(),
                message = "获取个人统计数据成功"
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "get_personal_stats", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["timeRange"] = timeRange
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptRunnerPlugin] get_personal_stats 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "get_personal_stats", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });

            var errorResponse = new
            {
                success = false,
                error = ex.Message
            };
            return JsonSerializer.Serialize(errorResponse);
        }
    }

}

public class GetRecommendationsToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id => "scriptrunner.get_recommendations";
    public string Name => "get_recommendations";
    public string PluginId { get; }
    public string Description => "获取基于上下文的智能推荐，包括推荐的工具、脚本、代码片段和工作流。可传入当前页面和操作来获取更精准的推荐。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""currentPage"": {
            ""type"": ""string"",
            ""description"": ""当前页面，用于上下文推荐""
        },
        ""currentAction"": {
            ""type"": ""string"",
            ""description"": ""当前正在进行的操作""
        },
        ""limit"": {
            ""type"": ""integer"",
            ""description"": ""每个分类的推荐数量限制，默认5"",
            ""default"": 5
        }
    },
    ""required"": []
}";

    public GetRecommendationsToolFunction(string pluginId, IServiceProvider serviceProvider)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[ScriptRunnerPlugin] 执行 get_recommendations 工具函数");

            if (_serviceProvider == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "服务提供者未初始化" });
            }

            using var doc = JsonDocument.Parse(parameters);
            var root = doc.RootElement;

            string? currentPage = null;
            if (root.TryGetProperty("currentPage", out var pageProp))
                currentPage = pageProp.GetString();

            string? currentAction = null;
            if (root.TryGetProperty("currentAction", out var actionProp))
                currentAction = actionProp.GetString();

            int limit = 5;
            if (root.TryGetProperty("limit", out var limitProp))
                limit = limitProp.GetInt32();

            using var scope = _serviceProvider.CreateScope();
            var recommendationService = scope.ServiceProvider.GetRequiredService<IWorkflowRecommendationService>();

            var request = new ContextualRecommendationRequest
            {
                CurrentPage = currentPage,
                CurrentAction = currentAction,
                Limit = limit
            };

            var recommendations = await recommendationService.GetContextualRecommendationsAsync(request);

            var response = new
            {
                success = true,
                recommendedTools = recommendations.RecommendedTools.Select(t => new
                {
                    pluginId = t.PluginId,
                    toolId = t.ToolId,
                    toolName = t.ToolName,
                    matchScore = t.MatchScore,
                    matchReason = t.MatchReason,
                    usageCount = t.UsageCount
                }).ToList(),
                recommendedScripts = recommendations.RecommendedScripts.Select(s => new
                {
                    scriptId = s.ScriptId,
                    scriptName = s.ScriptName,
                    language = s.Language,
                    matchScore = s.MatchScore,
                    matchReason = s.MatchReason,
                    usageCount = s.UsageCount
                }).ToList(),
                recommendedSnippets = recommendations.RecommendedSnippets.Select(s => new
                {
                    snippetId = s.SnippetId,
                    title = s.Title,
                    language = s.Language,
                    matchScore = s.MatchScore,
                    matchReason = s.MatchReason,
                    usageCount = s.UsageCount
                }).ToList(),
                recommendedWorkflows = recommendations.RecommendedWorkflows.Select(w => new
                {
                    workflowId = w.WorkflowId,
                    workflowName = w.WorkflowName,
                    matchScore = w.MatchScore,
                    matchReason = w.MatchReason,
                    usageCount = w.UsageCount
                }).ToList(),
                message = "获取推荐成功"
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "get_recommendations", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["currentPage"] = currentPage ?? string.Empty,
                ["limit"] = limit
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptRunnerPlugin] get_recommendations 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "get_recommendations", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["error"] = ex.Message
            });

            var errorResponse = new
            {
                success = false,
                error = ex.Message
            };
            return JsonSerializer.Serialize(errorResponse);
        }
    }

}

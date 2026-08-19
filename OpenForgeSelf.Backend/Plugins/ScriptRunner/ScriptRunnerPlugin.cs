using System.Diagnostics;
using System.Text.Json;
using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Core;
using OpenForgeSelf.Backend.Plugins.ScriptRunner.Hubs;
using OpenForgeSelf.Backend.Plugins.ScriptRunner.Services;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;
using XCode.DataAccessLayer;

namespace OpenForgeSelf.Backend.Plugins.ScriptRunner;

public class ScriptRunnerPlugin : IPlugin
{
    public List<IMenuExtension> MenuExtensions { get; private set; } = [];
    public List<IToolFunctionExtension> ToolExtensions { get; private set; } = [];

    private IScriptTemplateService? _scriptTemplateService;
    private ICodeSnippetService? _codeSnippetService;

    public void Apply(IContext ctx)
    {
        var pluginId = ctx.Get<PluginMetadata>()?.Id ?? "";
        XTrace.Log.Info("[ScriptRunnerPlugin] 初始化脚本运行器插件");

        var services = ctx.Get<IServiceCollection>();
        services?.AddScoped<IScriptService, ScriptService>();
        services?.AddScoped<IScriptExecutor, ScriptExecutor>();
        services?.AddScoped<ICodeSnippetService, CodeSnippetService>();
        services?.AddScoped<IScriptTemplateService, ScriptTemplateService>();
        services?.AddSingleton<ScriptExecutionBroadcaster>();
        // 宿主 UsageStats / Recommendation 服务跨程序集消费脚本库统计（ADR D2）。
        services?.AddScoped<IScriptLibraryStatsProvider, ScriptLibraryStatsProvider>();

        RegisterServices();
        RegisterMenuExtensions(pluginId);
        RegisterToolFunctionExtensions(pluginId, ctx);

        EnsureDatabaseCreated();

        XTrace.Log.Info("[ScriptRunnerPlugin] 脚本运行器插件初始化完成");
    }

    private void RegisterServices()
    {
        // IRuntimeDetector 由宿主 DI 提供（AppBuilder 注册单例），插件不直接构造宿主实现类；
        // ScriptExecutor/ScriptService 由 DI 按需解析（插件自注册 + 宿主服务回落）。
        _scriptTemplateService = new ScriptTemplateService();
        _codeSnippetService = new CodeSnippetService();

        XTrace.Log.Debug("[ScriptRunnerPlugin] 服务已注册");
    }

    private void RegisterMenuExtensions(string pluginId)
    {
        XTrace.Log.Debug("[ScriptRunnerPlugin] 注册菜单扩展点");

        var scriptRunnerMenu = new ScriptRunnerMenuExtension
        {
            Id = "scriptrunner.menu.main",
            Name = "脚本运行器",
            PluginId = pluginId,
            Icon = "fa-solid fa-terminal",
            Path = "/script-runner",
            Order = 50,
            ParentId = null,
            Children = new List<IMenuExtension>
            {
                new ScriptRunnerMenuExtension
                {
                    Id = "scriptrunner.menu.library",
                    Name = "脚本库",
                    PluginId = pluginId,
                    Icon = "fa-solid fa-book",
                    Path = "/script-runner/library",
                    Order = 1,
                    ParentId = "scriptrunner.menu.main"
                },
                new ScriptRunnerMenuExtension
                {
                    Id = "scriptrunner.menu.codesnippets",
                    Name = "代码片段",
                    PluginId = pluginId,
                    Icon = "fa-solid fa-code",
                    Path = "/script-runner/code-snippets",
                    Order = 2,
                    ParentId = "scriptrunner.menu.main"
                }
            }
        };

        MenuExtensions.Add(scriptRunnerMenu);
        XTrace.Log.Debug("[ScriptRunnerPlugin] 菜单扩展点注册完成，共 {0} 个菜单项", MenuExtensions.Count);
    }

    private void RegisterToolFunctionExtensions(string pluginId, IServiceProvider services)
    {
        XTrace.Log.Debug("[ScriptRunnerPlugin] 注册AI工具函数扩展点");

        ToolExtensions.Add(new RunScriptToolFunction(pluginId, services));
        ToolExtensions.Add(new ListScriptsToolFunction(pluginId, services));
        ToolExtensions.Add(new ExecuteCodeToolFunction(pluginId, services));
        ToolExtensions.Add(new GenerateScriptToolFunction(pluginId, services, _scriptTemplateService!));
        ToolExtensions.Add(new AnalyzeScriptErrorToolFunction(pluginId, services));
        ToolExtensions.Add(new SuggestScriptFixToolFunction(pluginId, services));
        ToolExtensions.Add(new ListScriptTemplatesToolFunction(pluginId, services, _scriptTemplateService!));
        ToolExtensions.Add(new ListCodeSnippetsToolFunction(pluginId, services, _codeSnippetService!));
        ToolExtensions.Add(new CreateCodeSnippetToolFunction(pluginId, services, _codeSnippetService!));
        ToolExtensions.Add(new GetPersonalStatsToolFunction(pluginId, services));
        ToolExtensions.Add(new GetRecommendationsToolFunction(pluginId, services));

        XTrace.Log.Debug("[ScriptRunnerPlugin] AI工具函数扩展点注册完成，共 {0} 个工具函数", ToolExtensions.Count);
    }

    private void EnsureDatabaseCreated()
    {
        try
        {
            // 连接串由宿主统一注册（XCodeConfig.AddXCode：Data\ScriptRunner.db），此处仅校验连通性，
            // 不自注册 AddConnStr，避免与宿主注册的路径（AppContext.BaseDirectory/scriptrunner.db）不一致。
            var dal = DAL.Create("ScriptRunner");
            dal.Session.Query("SELECT 1");

            XTrace.Log.Info("[ScriptRunnerPlugin] 数据库初始化完成");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptRunnerPlugin] 数据库初始化失败: {0}", ex.Message);
        }
    }
}

public class ScriptRunnerMenuExtension : IMenuExtension
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

public class RunScriptToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id => "scriptrunner.run_script";
    public string Name => "run_script";
    public string PluginId { get; }
    public string Description => "执行指定的脚本，支持传入参数。脚本执行是异步的，返回执行ID用于查询状态和输出。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""scriptId"": {
            ""type"": ""integer"",
            ""description"": ""要执行的脚本ID""
        },
        ""parameters"": {
            ""type"": ""object"",
            ""description"": ""脚本参数字典，键为参数名，值为参数值""
        }
    },
    ""required"": [""scriptId""]
}";

    public RunScriptToolFunction(string pluginId, IServiceProvider? serviceProvider)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[ScriptRunnerPlugin] 执行 run_script 工具函数");

            if (_serviceProvider == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "服务提供者未初始化" });
            }

            using var scope = _serviceProvider.CreateScope();
            var scriptService = scope.ServiceProvider.GetRequiredService<IScriptService>();
            var scriptExecutor = scope.ServiceProvider.GetRequiredService<IScriptExecutor>();

            var paramsDoc = JsonDocument.Parse(parameters);
            var scriptId = paramsDoc.RootElement.GetProperty("scriptId").GetInt64();

            Dictionary<string, object?>? scriptParams = null;
            if (paramsDoc.RootElement.TryGetProperty("parameters", out var paramsProp))
            {
                scriptParams = JsonSerializer.Deserialize<Dictionary<string, object?>>(paramsProp.GetRawText());
            }

            var script = await scriptService.GetScriptAsync(scriptId);
            if (script == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "脚本不存在" });
            }

            var execution = await scriptExecutor.ExecuteAsync(scriptId, scriptParams);
            await scriptService.IncrementUsageAsync(scriptId);

            var response = new
            {
                success = true,
                executionId = execution.Id,
                scriptId = execution.ScriptId,
                scriptName = execution.ScriptName,
                status = execution.Status.ToString(),
                startTime = execution.StartTime,
                message = "脚本已开始执行"
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "run_script", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["scriptId"] = scriptId,
                ["scriptName"] = script.Name,
                ["executionId"] = execution.Id
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptRunnerPlugin] run_script 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "run_script", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class ListScriptsToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id => "scriptrunner.list_scripts";
    public string Name => "list_scripts";
    public string PluginId { get; }
    public string Description => "列出可用的脚本，支持按关键词、分类、语言筛选，可查看脚本详情和使用次数。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""keyword"": {
            ""type"": ""string"",
            ""description"": ""搜索关键词，匹配脚本名称、描述和标签""
        },
        ""category"": {
            ""type"": ""string"",
            ""description"": ""按分类筛选""
        },
        ""language"": {
            ""type"": ""string"",
            ""enum"": [""PowerShell"", ""Python"", ""NodeJs"", ""Shell"", ""Cmd""],
            ""description"": ""按脚本语言筛选""
        },
        ""isFavorite"": {
            ""type"": ""boolean"",
            ""description"": ""是否只显示收藏的脚本""
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

    public ListScriptsToolFunction(string pluginId, IServiceProvider? serviceProvider)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[ScriptRunnerPlugin] 执行 list_scripts 工具函数");

            if (_serviceProvider == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "服务提供者未初始化" });
            }

            using var scope = _serviceProvider.CreateScope();
            var scriptService = scope.ServiceProvider.GetRequiredService<IScriptService>();

            var paramsDoc = JsonDocument.Parse(parameters);
            var root = paramsDoc.RootElement;

            string? keyword = null;
            if (root.TryGetProperty("keyword", out var keywordProp))
                keyword = keywordProp.GetString();

            string? category = null;
            if (root.TryGetProperty("category", out var categoryProp))
                category = categoryProp.GetString();

            ScriptLanguage? language = null;
            if (root.TryGetProperty("language", out var langProp))
            {
                var langStr = langProp.GetString();
                if (Enum.TryParse<ScriptLanguage>(langStr, out var lang))
                {
                    language = lang;
                }
            }

            bool? isFavorite = null;
            if (root.TryGetProperty("isFavorite", out var favProp))
                isFavorite = favProp.GetBoolean();

            int page = 1;
            if (root.TryGetProperty("page", out var pageProp))
                page = pageProp.GetInt32();

            int pageSize = 20;
            if (root.TryGetProperty("pageSize", out var pageSizeProp))
                pageSize = pageSizeProp.GetInt32();

            var result = await scriptService.ListScriptsAsync(keyword, category, language, isFavorite, page, pageSize);

            var response = new
            {
                success = true,
                total = result.Total,
                page = result.Page,
                pageSize = result.PageSize,
                scripts = result.Items.Select(s => new
                {
                    id = s.Id,
                    name = s.Name,
                    description = s.Description,
                    language = s.Language.ToString(),
                    category = s.Category,
                    tags = s.Tags,
                    isFavorite = s.IsFavorite,
                    usageCount = s.UsageCount,
                    lastUsedAt = s.LastUsedAt,
                    timeoutSeconds = s.TimeoutSeconds,
                    updatedAt = s.UpdatedAt
                }).ToList()
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "list_scripts", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["keyword"] = keyword ?? string.Empty,
                ["category"] = category ?? string.Empty,
                ["total"] = result.Total
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptRunnerPlugin] list_scripts 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "list_scripts", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class ExecuteCodeToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id => "scriptrunner.execute_code";
    public string Name => "execute_code";
    public string PluginId { get; }
    public string Description => "直接执行代码片段，支持 PowerShell、Python、Node.js、Shell、Cmd 等语言。执行是异步的，返回执行ID用于查询状态。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""code"": {
            ""type"": ""string"",
            ""description"": ""要执行的代码内容""
        },
        ""language"": {
            ""type"": ""string"",
            ""enum"": [""PowerShell"", ""Python"", ""NodeJs"", ""Shell"", ""Cmd""],
            ""description"": ""代码语言""
        },
        ""parameters"": {
            ""type"": ""object"",
            ""description"": ""执行参数字典，通过环境变量传递给脚本""
        },
        ""workingDirectory"": {
            ""type"": ""string"",
            ""description"": ""工作目录路径""
        }
    },
    ""required"": [""code"", ""language""]
}";

    public ExecuteCodeToolFunction(string pluginId, IServiceProvider? serviceProvider)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[ScriptRunnerPlugin] 执行 execute_code 工具函数");

            if (_serviceProvider == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "服务提供者未初始化" });
            }

            using var scope = _serviceProvider.CreateScope();
            var scriptExecutor = scope.ServiceProvider.GetRequiredService<IScriptExecutor>();

            var paramsDoc = JsonDocument.Parse(parameters);
            var root = paramsDoc.RootElement;

            var code = root.GetProperty("code").GetString() ?? string.Empty;
            var languageStr = root.GetProperty("language").GetString() ?? string.Empty;

            if (!Enum.TryParse<ScriptLanguage>(languageStr, out var language))
            {
                return JsonSerializer.Serialize(new { success = false, error = "不支持的语言类型" });
            }

            Dictionary<string, object?>? scriptParams = null;
            if (root.TryGetProperty("parameters", out var paramsProp))
            {
                scriptParams = JsonSerializer.Deserialize<Dictionary<string, object?>>(paramsProp.GetRawText());
            }

            string? workingDirectory = null;
            if (root.TryGetProperty("workingDirectory", out var wdProp))
            {
                workingDirectory = wdProp.GetString();
            }

            var execution = await scriptExecutor.ExecuteCodeAsync(code, language, scriptParams, workingDirectory);

            var response = new
            {
                success = true,
                executionId = execution.Id,
                status = execution.Status.ToString(),
                startTime = execution.StartTime,
                message = "代码已开始执行"
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "execute_code", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["language"] = language.ToString(),
                ["executionId"] = execution.Id
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptRunnerPlugin] execute_code 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "execute_code", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

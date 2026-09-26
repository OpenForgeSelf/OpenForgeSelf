using System.Diagnostics;
using System.Text.Json;
using ForgeSelf.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.ScriptRunner.Services;

public class GenerateScriptToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;
    private readonly IScriptTemplateService _scriptTemplateService;

    public string Id => "scriptrunner.generate_script";
    public string Name => "generate_script";
    public string PluginId { get; }
    public string Description => "根据用户需求描述，使用AI智能生成指定语言的脚本代码，支持参数化和错误处理。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""language"": {
            ""type"": ""string"",
            ""enum"": [""PowerShell"", ""Python"", ""NodeJs"", ""Shell"", ""Cmd""],
            ""description"": ""脚本语言""
        },
        ""description"": {
            ""type"": ""string"",
            ""description"": ""脚本功能描述，说明脚本要做什么""
        },
        ""requirements"": {
            ""type"": ""string"",
            ""description"": ""附加要求（可选），如性能要求、特定实现方式等""
        }
    },
    ""required"": [""language"", ""description""]
}";

    public GenerateScriptToolFunction(string pluginId, IServiceProvider? serviceProvider, IScriptTemplateService scriptTemplateService)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
        _scriptTemplateService = scriptTemplateService;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[ScriptRunnerPlugin] 执行 generate_script 工具函数");

            using var doc = JsonDocument.Parse(parameters);
            var root = doc.RootElement;
            var language = root.GetProperty("language").GetString() ?? "Python";
            var description = root.GetProperty("description").GetString() ?? string.Empty;

            string? requirements = null;
            if (root.TryGetProperty("requirements", out var reqProp))
            {
                requirements = reqProp.GetString();
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                return JsonSerializer.Serialize(new { success = false, error = "脚本描述不能为空" });
            }

            if (_serviceProvider == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "服务提供者未初始化" });
            }

            using var scope = _serviceProvider.CreateScope();
            var aiAgentService = scope.ServiceProvider.GetRequiredService<IAIAgentService>();

            var result = await aiAgentService.GenerateScriptAsync(language, description, requirements);

            var response = new
            {
                success = true,
                code = result.Code,
                description = result.Description,
                language = result.Language,
                parameters = result.Parameters.Select(p => new
                {
                    name = p.Name,
                    type = p.Type.ToString(),
                    description = p.Description,
                    defaultValue = p.DefaultValue,
                    isRequired = p.IsRequired,
                    options = p.Options
                }).ToList(),
                message = "脚本生成成功"
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "generate_script", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["language"] = language,
                ["codeLength"] = result.Code.Length
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptRunnerPlugin] generate_script 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "generate_script", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class AnalyzeScriptErrorToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id => "scriptrunner.analyze_script_error";
    public string Name => "analyze_script_error";
    public string PluginId { get; }
    public string Description => "分析脚本错误原因，提供可能的原因分析和修复建议。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""language"": {
            ""type"": ""string"",
            ""enum"": [""PowerShell"", ""Python"", ""NodeJs"", ""Shell"", ""Cmd""],
            ""description"": ""脚本语言""
        },
        ""code"": {
            ""type"": ""string"",
            ""description"": ""出现错误的脚本代码""
        },
        ""errorMessage"": {
            ""type"": ""string"",
            ""description"": ""错误信息""
        }
    },
    ""required"": [""language"", ""code"", ""errorMessage""]
}";

    public AnalyzeScriptErrorToolFunction(string pluginId, IServiceProvider? serviceProvider)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[ScriptRunnerPlugin] 执行 analyze_script_error 工具函数");

            using var doc = JsonDocument.Parse(parameters);
            var root = doc.RootElement;
            var language = root.GetProperty("language").GetString() ?? "Python";
            var code = root.GetProperty("code").GetString() ?? string.Empty;
            var errorMessage = root.GetProperty("errorMessage").GetString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(code))
            {
                return JsonSerializer.Serialize(new { success = false, error = "代码不能为空" });
            }

            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                return JsonSerializer.Serialize(new { success = false, error = "错误信息不能为空" });
            }

            if (_serviceProvider == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "服务提供者未初始化" });
            }

            using var scope = _serviceProvider.CreateScope();
            var aiAgentService = scope.ServiceProvider.GetRequiredService<IAIAgentService>();

            var result = await aiAgentService.AnalyzeScriptErrorAsync(language, code, errorMessage);

            var response = new
            {
                success = true,
                errorAnalysis = result.ErrorAnalysis,
                possibleCauses = result.PossibleCauses,
                suggestion = result.Suggestion,
                message = "错误分析完成"
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "analyze_script_error", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["language"] = language
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptRunnerPlugin] analyze_script_error 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "analyze_script_error", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class SuggestScriptFixToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id => "scriptrunner.suggest_script_fix";
    public string Name => "suggest_script_fix";
    public string PluginId { get; }
    public string Description => "针对脚本错误提供修复建议，生成修复后的完整代码并解释修改原因。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""language"": {
            ""type"": ""string"",
            ""enum"": [""PowerShell"", ""Python"", ""NodeJs"", ""Shell"", ""Cmd""],
            ""description"": ""脚本语言""
        },
        ""code"": {
            ""type"": ""string"",
            ""description"": ""出现错误的脚本代码""
        },
        ""errorMessage"": {
            ""type"": ""string"",
            ""description"": ""错误信息""
        }
    },
    ""required"": [""language"", ""code"", ""errorMessage""]
}";

    public SuggestScriptFixToolFunction(string pluginId, IServiceProvider? serviceProvider)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[ScriptRunnerPlugin] 执行 suggest_script_fix 工具函数");

            using var doc = JsonDocument.Parse(parameters);
            var root = doc.RootElement;
            var language = root.GetProperty("language").GetString() ?? "Python";
            var code = root.GetProperty("code").GetString() ?? string.Empty;
            var errorMessage = root.GetProperty("errorMessage").GetString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(code))
            {
                return JsonSerializer.Serialize(new { success = false, error = "代码不能为空" });
            }

            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                return JsonSerializer.Serialize(new { success = false, error = "错误信息不能为空" });
            }

            if (_serviceProvider == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "服务提供者未初始化" });
            }

            using var scope = _serviceProvider.CreateScope();
            var aiAgentService = scope.ServiceProvider.GetRequiredService<IAIAgentService>();

            var result = await aiAgentService.SuggestScriptFixAsync(language, code, errorMessage);

            var response = new
            {
                success = true,
                errorAnalysis = result.ErrorAnalysis,
                fixedCode = result.FixedCode,
                changes = result.Changes,
                explanation = result.Explanation,
                message = "修复建议生成成功"
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "suggest_script_fix", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["language"] = language
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptRunnerPlugin] suggest_script_fix 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "suggest_script_fix", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class ListScriptTemplatesToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;
    private readonly IScriptTemplateService _scriptTemplateService;

    public string Id => "scriptrunner.list_script_templates";
    public string Name => "list_script_templates";
    public string PluginId { get; }
    public string Description => "列出可用的脚本模板，支持按分类、语言、关键词筛选，可查看模板详情。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""category"": {
            ""type"": ""string"",
            ""description"": ""按分类筛选""
        },
        ""keyword"": {
            ""type"": ""string"",
            ""description"": ""搜索关键词，匹配模板名称和描述""
        },
        ""language"": {
            ""type"": ""string"",
            ""enum"": [""PowerShell"", ""Python"", ""NodeJs"", ""Shell"", ""Cmd""],
            ""description"": ""按脚本语言筛选""
        }
    },
    ""required"": []
}";

    public ListScriptTemplatesToolFunction(string pluginId, IServiceProvider? serviceProvider, IScriptTemplateService scriptTemplateService)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
        _scriptTemplateService = scriptTemplateService;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[ScriptRunnerPlugin] 执行 list_script_templates 工具函数");

            using var doc = JsonDocument.Parse(parameters);
            var root = doc.RootElement;

            string? category = null;
            if (root.TryGetProperty("category", out var catProp))
                category = catProp.GetString();

            string? keyword = null;
            if (root.TryGetProperty("keyword", out var kwProp))
                keyword = kwProp.GetString();

            ScriptLanguage? language = null;
            if (root.TryGetProperty("language", out var langProp))
            {
                var langStr = langProp.GetString();
                if (Enum.TryParse<ScriptLanguage>(langStr, out var lang))
                {
                    language = lang;
                }
            }

            var templates = await _scriptTemplateService.GetTemplatesAsync(category, keyword, language);

            var response = new
            {
                success = true,
                total = templates.Count,
                templates = templates.Select(t => new
                {
                    id = t.Id,
                    name = t.Name,
                    description = t.Description,
                    language = t.Language.ToString(),
                    category = t.Category,
                    tags = t.Tags,
                    parameterCount = t.Parameters.Count,
                    version = t.Version,
                    author = t.Author
                }).ToList(),
                message = "获取到 " + templates.Count + " 个模板"
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "list_script_templates", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["total"] = templates.Count,
                ["category"] = category ?? string.Empty
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[ScriptRunnerPlugin] list_script_templates 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "list_script_templates", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

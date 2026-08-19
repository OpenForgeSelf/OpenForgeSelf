using System.Diagnostics;
using System.Text.Json;
using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Core;
using OpenForgeSelf.Backend.Plugins.TextTools.Models;
using OpenForgeSelf.Backend.Plugins.TextTools.Services;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.TextTools;

public class TextToolsPlugin : IPlugin
{
    public List<IMenuExtension> MenuExtensions { get; } = new();
    public List<IToolFunctionExtension> ToolExtensions { get; } = new();

    public void Apply(IContext ctx)
    {
        var pluginId = ctx.Get<PluginMetadata>()?.Id ?? "";
        XTrace.Log.Info("[TextToolsPlugin] 初始化文本工具插件");

        var services = ctx.Get<IServiceCollection>();
        services?.AddScoped<ITextStatsService, TextStatsService>();
        services?.AddScoped<ITextFormatterService, TextFormatterService>();
        services?.AddScoped<IEncodingService, EncodingService>();
        services?.AddScoped<IHashService, HashService>();

        RegisterMenuExtensions(pluginId);
        RegisterToolFunctionExtensions(pluginId, ctx);

        XTrace.Log.Info("[TextToolsPlugin] 文本工具插件初始化完成");
    }

    private void RegisterMenuExtensions(string pluginId)
    {
        XTrace.Log.Debug("[TextToolsPlugin] 注册菜单扩展点");

        var textToolsMenu = new TextToolsMenuExtension
        {
            Id = "texttools.menu.main",
            Name = "文本工具",
            PluginId = pluginId,
            Icon = "fa-solid fa-font",
            Path = "/text-tools",
            Order = 200,
            ParentId = null,
            Children = new List<IMenuExtension>
            {
                new TextToolsMenuExtension
                {
                    Id = "texttools.menu.formatter",
                    Name = "格式化工具",
                    PluginId = pluginId,
                    Icon = "fa-solid fa-indent",
                    Path = "/text-tools/formatter",
                    Order = 1,
                    ParentId = "texttools.menu.main"
                },
                new TextToolsMenuExtension
                {
                    Id = "texttools.menu.encoding",
                    Name = "编码转换",
                    PluginId = pluginId,
                    Icon = "fa-solid fa-code",
                    Path = "/text-tools/encoding",
                    Order = 2,
                    ParentId = "texttools.menu.main"
                },
                new TextToolsMenuExtension
                {
                    Id = "texttools.menu.hash",
                    Name = "哈希计算",
                    PluginId = pluginId,
                    Icon = "fa-solid fa-fingerprint",
                    Path = "/text-tools/hash",
                    Order = 3,
                    ParentId = "texttools.menu.main"
                },
                new TextToolsMenuExtension
                {
                    Id = "texttools.menu.stats",
                    Name = "文本统计",
                    PluginId = pluginId,
                    Icon = "fa-solid fa-chart-bar",
                    Path = "/text-tools/stats",
                    Order = 4,
                    ParentId = "texttools.menu.main"
                }
            }
        };

        MenuExtensions.Add(textToolsMenu);
        XTrace.Log.Debug("[TextToolsPlugin] 菜单扩展点注册完成，共 {0} 个菜单项", MenuExtensions.Count);
    }

    private void RegisterToolFunctionExtensions(string pluginId, IServiceProvider services)
    {
        XTrace.Log.Debug("[TextToolsPlugin] 注册AI工具函数扩展点");

        ToolExtensions.Add(new FormatJsonToolFunction(pluginId, services));
        ToolExtensions.Add(new EncodeBase64ToolFunction(pluginId, services));
        ToolExtensions.Add(new DecodeBase64ToolFunction(pluginId, services));
        ToolExtensions.Add(new ComputeHashToolFunction(pluginId, services));
        ToolExtensions.Add(new TextStatsToolFunction(pluginId, services));

        XTrace.Log.Debug("[TextToolsPlugin] AI工具函数扩展点注册完成，共 {0} 个工具函数", ToolExtensions.Count);
    }
}

public class TextToolsMenuExtension : IMenuExtension
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

public class FormatJsonToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id => "texttools.format_json";
    public string Name => "format_json";
    public string PluginId { get; }
    public string Description => "格式化JSON字符串，使其更易读。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""text"": {
            ""type"": ""string"",
            ""description"": ""需要格式化的JSON字符串""
        },
        ""indentSize"": {
            ""type"": ""integer"",
            ""description"": ""缩进大小，默认为2"",
            ""default"": 2
        }
    },
    ""required"": [""text""]
}";

    public FormatJsonToolFunction(string pluginId, IServiceProvider? serviceProvider)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Debug("[TextToolsPlugin] 执行 format_json 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var text = paramsDoc.RootElement.GetProperty("text").GetString() ?? string.Empty;
            var indentSize = 2;
            if (paramsDoc.RootElement.TryGetProperty("indentSize", out var indentProp))
            {
                indentSize = indentProp.GetInt32();
            }

            var service = new TextFormatterService();
            var result = await service.FormatJsonAsync(text, indentSize);

            var response = new
            {
                success = true,
                result = result
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "format_json", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["indentSize"] = indentSize,
                ["inputLength"] = text.Length
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[TextToolsPlugin] format_json 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "format_json", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class EncodeBase64ToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id => "texttools.encode_base64";
    public string Name => "encode_base64";
    public string PluginId { get; }
    public string Description => "对文本进行Base64编码，将普通文本转换为Base64格式。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""text"": {
            ""type"": ""string"",
            ""description"": ""需要进行Base64编码的原始文本""
        }
    },
    ""required"": [""text""]
}";

    public EncodeBase64ToolFunction(string pluginId, IServiceProvider? serviceProvider)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[TextToolsPlugin] 执行 encode_base64 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var text = paramsDoc.RootElement.GetProperty("text").GetString() ?? string.Empty;

            var service = new EncodingService();
            var result = await service.Base64EncodeAsync(text);

            var response = new
            {
                success = true,
                result = result,
                originalLength = text.Length,
                encodedLength = result.Length
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "encode_base64", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["inputLength"] = text.Length,
                ["outputLength"] = result.Length
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[TextToolsPlugin] encode_base64 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "encode_base64", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class DecodeBase64ToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id => "texttools.decode_base64";
    public string Name => "decode_base64";
    public string PluginId { get; }
    public string Description => "对Base64编码的文本进行解码，还原为原始文本。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""text"": {
            ""type"": ""string"",
            ""description"": ""需要解码的Base64编码字符串""
        }
    },
    ""required"": [""text""]
}";

    public DecodeBase64ToolFunction(string pluginId, IServiceProvider? serviceProvider)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[TextToolsPlugin] 执行 decode_base64 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var text = paramsDoc.RootElement.GetProperty("text").GetString() ?? string.Empty;

            var service = new EncodingService();
            var result = await service.Base64DecodeAsync(text);

            var response = new
            {
                success = true,
                result = result,
                encodedLength = text.Length,
                decodedLength = result.Length
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "decode_base64", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["inputLength"] = text.Length,
                ["outputLength"] = result.Length
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[TextToolsPlugin] decode_base64 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "decode_base64", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class ComputeHashToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id => "texttools.compute_hash";
    public string Name => "compute_hash";
    public string PluginId { get; }
    public string Description => "计算文本的哈希值，支持MD5、SHA1、SHA256、SHA512算法。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""text"": {
            ""type"": ""string"",
            ""description"": ""需要计算哈希的文本""
        },
        ""algorithm"": {
            ""type"": ""string"",
            ""description"": ""哈希算法：md5、sha1、sha256、sha512"",
            ""enum"": [""md5"", ""sha1"", ""sha256"", ""sha512""],
            ""default"": ""md5""
        }
    },
    ""required"": [""text""]
}";

    public ComputeHashToolFunction(string pluginId, IServiceProvider? serviceProvider)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Debug("[TextToolsPlugin] 执行 compute_hash 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var text = paramsDoc.RootElement.GetProperty("text").GetString() ?? string.Empty;
            var algorithm = "md5";
            if (paramsDoc.RootElement.TryGetProperty("algorithm", out var algoProp))
            {
                algorithm = algoProp.GetString() ?? "md5";
            }

            var service = new HashService();
            string result;

            switch (algorithm.ToLowerInvariant())
            {
                case "sha1":
                    result = await service.ComputeSHA1Async(text);
                    break;
                case "sha256":
                    result = await service.ComputeSHA256Async(text);
                    break;
                case "sha512":
                    result = await service.ComputeSHA512Async(text);
                    break;
                case "md5":
                default:
                    result = await service.ComputeMD5Async(text);
                    break;
            }

            var response = new
            {
                success = true,
                result = result,
                algorithm = algorithm
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "compute_hash", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["algorithm"] = algorithm,
                ["inputLength"] = text.Length
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[TextToolsPlugin] compute_hash 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "compute_hash", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class TextStatsToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id => "texttools.text_stats";
    public string Name => "text_stats";
    public string PluginId { get; }
    public string Description => "统计文本的字符数、字数、行数、字节数等信息。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""text"": {
            ""type"": ""string"",
            ""description"": ""需要统计的文本""
        }
    },
    ""required"": [""text""]
}";

    public TextStatsToolFunction(string pluginId, IServiceProvider? serviceProvider)
    {
        PluginId = pluginId;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Debug("[TextToolsPlugin] 执行 text_stats 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var text = paramsDoc.RootElement.GetProperty("text").GetString() ?? string.Empty;

            var service = new TextStatsService();
            var stats = await service.GetStatsAsync(text);

            var response = new
            {
                success = true,
                charCount = stats.CharCount,
                charCountNoSpaces = stats.CharCountNoSpaces,
                wordCount = stats.WordCount,
                lineCount = stats.LineCount,
                byteCount = stats.ByteCount
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "text_stats", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["charCount"] = stats.CharCount,
                ["wordCount"] = stats.WordCount,
                ["lineCount"] = stats.LineCount
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[TextToolsPlugin] text_stats 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "text_stats", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

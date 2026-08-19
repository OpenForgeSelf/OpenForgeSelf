using System.Diagnostics;
using System.Text.Json;
using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Core;
using OpenForgeSelf.Backend.Plugins.DevTools.Models;
using OpenForgeSelf.Backend.Plugins.DevTools.Services;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.DevTools;

public class DevToolsPlugin : IPlugin
{
    public List<IMenuExtension> MenuExtensions { get; } = new();
    public List<IToolFunctionExtension> ToolExtensions { get; } = new();

    public void Apply(IContext ctx)
    {
        var pluginId = ctx.Get<PluginMetadata>()?.Id ?? "";
        XTrace.Log.Info("[DevToolsPlugin] 初始化开发者工具插件");

        var services = ctx.Get<IServiceCollection>();
        services?.AddScoped<IJsonFormatterService, JsonFormatterService>();
        services?.AddScoped<IYamlFormatterService, YamlFormatterService>();
        services?.AddScoped<IXmlFormatterService, XmlFormatterService>();
        services?.AddScoped<IEncodingService, EncodingService>();
        services?.AddScoped<IHashService, HashService>();
        services?.AddScoped<IRegexService, RegexService>();
        services?.AddScoped<ITimestampService, TimestampService>();
        services?.AddScoped<IColorService, ColorService>();
        services?.AddScoped<IJwtService, JwtService>();
        services?.AddScoped<IUuidService, UuidService>();
        services?.AddScoped<IQrCodeService, QrCodeService>();

        RegisterMenuExtensions(pluginId);
        RegisterToolFunctionExtensions(pluginId, ctx);

        XTrace.Log.Info("[DevToolsPlugin] 开发者工具插件初始化完成");
    }

    private void RegisterMenuExtensions(string pluginId)
    {
        XTrace.Log.Debug("[DevToolsPlugin] 注册菜单扩展点");

        var devToolsMenu = new DevToolsMenuExtension
        {
            Id = "devtools.menu.main",
            Name = "开发者工具",
            PluginId = pluginId,
            Icon = "fa-solid fa-wrench",
            Path = "/dev-tools",
            Order = 250,
            ParentId = null,
            Children = new List<IMenuExtension>
            {
                new DevToolsMenuExtension
                {
                    Id = "devtools.menu.json",
                    Name = "JSON工具",
                    PluginId = pluginId,
                    Icon = "fa-solid fa-braille",
                    Path = "/dev-tools/json",
                    Order = 1,
                    ParentId = "devtools.menu.main"
                },
                new DevToolsMenuExtension
                {
                    Id = "devtools.menu.yaml",
                    Name = "YAML工具",
                    PluginId = pluginId,
                    Icon = "fa-solid fa-list-ul",
                    Path = "/dev-tools/yaml",
                    Order = 2,
                    ParentId = "devtools.menu.main"
                },
                new DevToolsMenuExtension
                {
                    Id = "devtools.menu.xml",
                    Name = "XML工具",
                    PluginId = pluginId,
                    Icon = "fa-solid fa-code",
                    Path = "/dev-tools/xml",
                    Order = 3,
                    ParentId = "devtools.menu.main"
                },
                new DevToolsMenuExtension
                {
                    Id = "devtools.menu.converter",
                    Name = "格式转换",
                    PluginId = pluginId,
                    Icon = "fa-solid fa-right-left",
                    Path = "/dev-tools/converter",
                    Order = 4,
                    ParentId = "devtools.menu.main"
                },
                new DevToolsMenuExtension
                {
                    Id = "devtools.menu.encoding",
                    Name = "编码转换",
                    PluginId = pluginId,
                    Icon = "fa-solid fa-keyboard",
                    Path = "/dev-tools/encoding",
                    Order = 5,
                    ParentId = "devtools.menu.main"
                },
                new DevToolsMenuExtension
                {
                    Id = "devtools.menu.hash",
                    Name = "哈希计算",
                    PluginId = pluginId,
                    Icon = "fa-solid fa-fingerprint",
                    Path = "/dev-tools/hash",
                    Order = 6,
                    ParentId = "devtools.menu.main"
                },
                new DevToolsMenuExtension
                {
                    Id = "devtools.menu.encrypt",
                    Name = "加密解密",
                    PluginId = pluginId,
                    Icon = "fa-solid fa-lock",
                    Path = "/dev-tools/encrypt",
                    Order = 7,
                    ParentId = "devtools.menu.main"
                },
                new DevToolsMenuExtension
                {
                    Id = "devtools.menu.regex",
                    Name = "正则测试",
                    PluginId = pluginId,
                    Icon = "fa-solid fa-code-branch",
                    Path = "/dev-tools/regex",
                    Order = 8,
                    ParentId = "devtools.menu.main"
                },
                new DevToolsMenuExtension
                {
                    Id = "devtools.menu.timestamp",
                    Name = "时间戳",
                    PluginId = pluginId,
                    Icon = "fa-solid fa-clock",
                    Path = "/dev-tools/timestamp",
                    Order = 9,
                    ParentId = "devtools.menu.main"
                },
                new DevToolsMenuExtension
                {
                    Id = "devtools.menu.color",
                    Name = "颜色工具",
                    PluginId = pluginId,
                    Icon = "fa-solid fa-palette",
                    Path = "/dev-tools/color",
                    Order = 10,
                    ParentId = "devtools.menu.main"
                },
                new DevToolsMenuExtension
                {
                    Id = "devtools.menu.jwt",
                    Name = "JWT工具",
                    PluginId = pluginId,
                    Icon = "fa-solid fa-key",
                    Path = "/dev-tools/jwt",
                    Order = 11,
                    ParentId = "devtools.menu.main"
                },
                new DevToolsMenuExtension
                {
                    Id = "devtools.menu.uuid",
                    Name = "UUID/ID生成",
                    PluginId = pluginId,
                    Icon = "fa-solid fa-fingerprint",
                    Path = "/dev-tools/uuid",
                    Order = 12,
                    ParentId = "devtools.menu.main"
                },
                new DevToolsMenuExtension
                {
                    Id = "devtools.menu.qrcode",
                    Name = "二维码工具",
                    PluginId = pluginId,
                    Icon = "fa-solid fa-qrcode",
                    Path = "/dev-tools/qrcode",
                    Order = 13,
                    ParentId = "devtools.menu.main"
                }
            }
        };

        MenuExtensions.Add(devToolsMenu);
        XTrace.Log.Debug("[DevToolsPlugin] 菜单扩展点注册完成，共 {0} 个菜单项", MenuExtensions.Count);
    }

    private void RegisterToolFunctionExtensions(string pluginId, IServiceProvider services)
    {
        XTrace.Log.Debug("[DevToolsPlugin] 注册AI工具函数扩展点");

        ToolExtensions.Add(new FormatJsonToolFunction(pluginId, services));
        ToolExtensions.Add(new ValidateJsonToolFunction(pluginId, services));
        ToolExtensions.Add(new ConvertJsonYamlToolFunction(pluginId, services));
        ToolExtensions.Add(new JsonPathQueryToolFunction(pluginId, services));
        ToolExtensions.Add(new FormatXmlToolFunction(pluginId, services));
        ToolExtensions.Add(new Base64EncodeToolFunction(pluginId, services));
        ToolExtensions.Add(new Base64DecodeToolFunction(pluginId, services));
        ToolExtensions.Add(new UrlEncodeToolFunction(pluginId, services));
        ToolExtensions.Add(new UrlDecodeToolFunction(pluginId, services));
        ToolExtensions.Add(new ComputeHashToolFunction(pluginId, services));
        ToolExtensions.Add(new ComputeHmacToolFunction(pluginId, services));
        ToolExtensions.Add(new AesEncryptToolFunction(pluginId, services));
        ToolExtensions.Add(new AesDecryptToolFunction(pluginId, services));
        ToolExtensions.Add(new TestRegexToolFunction(pluginId, services));
        ToolExtensions.Add(new GenerateRegexToolFunction(pluginId, services));
        ToolExtensions.Add(new RegexReplaceToolFunction(pluginId, services));
        ToolExtensions.Add(new ConvertTimestampToolFunction(pluginId, services));
        ToolExtensions.Add(new ConvertColorToolFunction(pluginId, services));
        ToolExtensions.Add(new GenerateColorPaletteToolFunction(pluginId, services));
        ToolExtensions.Add(new CheckColorContrastToolFunction(pluginId, services));
        ToolExtensions.Add(new DecodeJwtToolFunction(pluginId, services));
        ToolExtensions.Add(new ValidateJwtToolFunction(pluginId, services));
        ToolExtensions.Add(new GenerateJwtToolFunction(pluginId, services));
        ToolExtensions.Add(new GenerateUuidToolFunction(pluginId, services));
        ToolExtensions.Add(new GenerateSnowflakeIdToolFunction(pluginId, services));
        ToolExtensions.Add(new GenerateQrCodeToolFunction(pluginId, services));

        XTrace.Log.Debug("[DevToolsPlugin] AI工具函数扩展点注册完成，共 {0} 个工具函数", ToolExtensions.Count);
    }
}

public class DevToolsMenuExtension : IMenuExtension
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

public abstract class DevToolsToolFunctionBase : IToolFunctionExtension
{
    protected readonly IServiceProvider? _serviceProvider;

    public abstract string Id { get; }
    public abstract string Name { get; }
    public string PluginId { get; }
    public abstract string Description { get; }
    public abstract string ParametersJsonSchema { get; }

    protected DevToolsToolFunctionBase(string pluginId, IServiceProvider? serviceProvider)
    {
        PluginId = pluginId;
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
            XTrace.Log.Warn("[DevToolsPlugin] 记录使用统计失败: {0}", ex.Message);
        }
    }
}

public class FormatJsonToolFunction : DevToolsToolFunctionBase
{
    public override string Id => "devtools.format_json";
    public override string Name => "format_json";
    public override string Description => "格式化JSON字符串，使其更易读。也可以压缩JSON。";

    public override string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""text"": {
            ""type"": ""string"",
            ""description"": ""需要处理的JSON字符串""
        },
        ""indentSize"": {
            ""type"": ""integer"",
            ""description"": ""缩进大小，默认为2"",
            ""default"": 2
        },
        ""action"": {
            ""type"": ""string"",
            ""enum"": [""format"", ""minify""],
            ""description"": ""操作类型：format格式化，minify压缩"",
            ""default"": ""format""
        }
    },
    ""required"": [""text""]
}";

    public FormatJsonToolFunction(string pluginId, IServiceProvider? serviceProvider)
        : base(pluginId, serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Debug("[DevToolsPlugin] 执行 format_json 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var text = paramsDoc.RootElement.GetProperty("text").GetString() ?? string.Empty;
            var indentSize = 2;
            if (paramsDoc.RootElement.TryGetProperty("indentSize", out var indentProp))
            {
                indentSize = indentProp.GetInt32();
            }
            var action = "format";
            if (paramsDoc.RootElement.TryGetProperty("action", out var actionProp))
            {
                action = actionProp.GetString() ?? "format";
            }

            var service = new JsonFormatterService();
            string result;

            if (action == "minify")
            {
                result = await service.MinifyJsonAsync(text);
            }
            else
            {
                result = await service.FormatJsonAsync(text, indentSize);
            }

            var response = new
            {
                success = true,
                result = result,
                action = action
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("format_json", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["action"] = action,
                ["indentSize"] = indentSize,
                ["inputLength"] = text.Length
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevToolsPlugin] format_json 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("format_json", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class ValidateJsonToolFunction : DevToolsToolFunctionBase
{
    public override string Id => "devtools.validate_json";
    public override string Name => "validate_json";
    public override string Description => "校验JSON字符串是否合法，返回错误位置和信息。";

    public override string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""text"": {
            ""type"": ""string"",
            ""description"": ""需要校验的JSON字符串""
        }
    },
    ""required"": [""text""]
}";

    public ValidateJsonToolFunction(string pluginId, IServiceProvider? serviceProvider)
        : base(pluginId, serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Debug("[DevToolsPlugin] 执行 validate_json 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var text = paramsDoc.RootElement.GetProperty("text").GetString() ?? string.Empty;

            var service = new JsonFormatterService();
            var result = await service.ValidateJsonAsync(text);

            var response = new
            {
                success = true,
                isValid = result.IsValid,
                errorMessage = result.ErrorMessage,
                lineNumber = result.LineNumber,
                position = result.Position
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("validate_json", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["isValid"] = result.IsValid,
                ["inputLength"] = text.Length
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevToolsPlugin] validate_json 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("validate_json", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class ConvertJsonYamlToolFunction : DevToolsToolFunctionBase
{
    public override string Id => "devtools.convert_json_yaml";
    public override string Name => "convert_json_yaml";
    public override string Description => "JSON和YAML格式互相转换。";

    public override string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""text"": {
            ""type"": ""string"",
            ""description"": ""需要转换的文本""
        },
        ""direction"": {
            ""type"": ""string"",
            ""enum"": [""json-to-yaml"", ""yaml-to-json""],
            ""description"": ""转换方向：json-to-yaml或yaml-to-json"",
            ""default"": ""json-to-yaml""
        }
    },
    ""required"": [""text""]
}";

    public ConvertJsonYamlToolFunction(string pluginId, IServiceProvider? serviceProvider)
        : base(pluginId, serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Debug("[DevToolsPlugin] 执行 convert_json_yaml 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var text = paramsDoc.RootElement.GetProperty("text").GetString() ?? string.Empty;
            var direction = "json-to-yaml";
            if (paramsDoc.RootElement.TryGetProperty("direction", out var dirProp))
            {
                direction = dirProp.GetString() ?? "json-to-yaml";
            }

            string result;

            if (direction == "yaml-to-json")
            {
                var yamlService = new YamlFormatterService();
                result = await yamlService.ConvertYamlToJsonAsync(text);
            }
            else
            {
                var jsonService = new JsonFormatterService();
                result = await jsonService.ConvertJsonToYamlAsync(text);
            }

            var response = new
            {
                success = true,
                result = result,
                direction = direction
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("convert_json_yaml", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["direction"] = direction,
                ["inputLength"] = text.Length
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevToolsPlugin] convert_json_yaml 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("convert_json_yaml", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class JsonPathQueryToolFunction : DevToolsToolFunctionBase
{
    public override string Id => "devtools.jsonpath_query";
    public override string Name => "jsonpath_query";
    public override string Description => "使用JSONPath表达式查询JSON数据。";

    public override string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""text"": {
            ""type"": ""string"",
            ""description"": ""JSON数据字符串""
        },
        ""expression"": {
            ""type"": ""string"",
            ""description"": ""JSONPath表达式，如 $.store.book[0].title""
        }
    },
    ""required"": [""text"", ""expression""]
}";

    public JsonPathQueryToolFunction(string pluginId, IServiceProvider? serviceProvider)
        : base(pluginId, serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Debug("[DevToolsPlugin] 执行 jsonpath_query 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var text = paramsDoc.RootElement.GetProperty("text").GetString() ?? string.Empty;
            var expression = paramsDoc.RootElement.GetProperty("expression").GetString() ?? string.Empty;

            var service = new JsonFormatterService();
            var result = await service.JsonPathQueryAsync(text, expression);

            var response = new
            {
                success = true,
                result = result,
                expression = expression
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("jsonpath_query", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["expression"] = expression,
                ["inputLength"] = text.Length
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevToolsPlugin] jsonpath_query 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("jsonpath_query", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class FormatXmlToolFunction : DevToolsToolFunctionBase
{
    public override string Id => "devtools.format_xml";
    public override string Name => "format_xml";
    public override string Description => "格式化或压缩XML字符串。";

    public override string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""text"": {
            ""type"": ""string"",
            ""description"": ""需要处理的XML字符串""
        },
        ""indentSize"": {
            ""type"": ""integer"",
            ""description"": ""缩进大小，默认为2"",
            ""default"": 2
        },
        ""action"": {
            ""type"": ""string"",
            ""enum"": [""format"", ""minify""],
            ""description"": ""操作类型：format格式化，minify压缩"",
            ""default"": ""format""
        }
    },
    ""required"": [""text""]
}";

    public FormatXmlToolFunction(string pluginId, IServiceProvider? serviceProvider)
        : base(pluginId, serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Debug("[DevToolsPlugin] 执行 format_xml 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var text = paramsDoc.RootElement.GetProperty("text").GetString() ?? string.Empty;
            var indentSize = 2;
            if (paramsDoc.RootElement.TryGetProperty("indentSize", out var indentProp))
            {
                indentSize = indentProp.GetInt32();
            }
            var action = "format";
            if (paramsDoc.RootElement.TryGetProperty("action", out var actionProp))
            {
                action = actionProp.GetString() ?? "format";
            }

            var service = new XmlFormatterService();
            string result;

            if (action == "minify")
            {
                result = await service.MinifyXmlAsync(text);
            }
            else
            {
                result = await service.FormatXmlAsync(text, indentSize);
            }

            var response = new
            {
                success = true,
                result = result,
                action = action
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("format_xml", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["action"] = action,
                ["indentSize"] = indentSize,
                ["inputLength"] = text.Length
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevToolsPlugin] format_xml 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("format_xml", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class Base64EncodeToolFunction : DevToolsToolFunctionBase
{
    public override string Id => "devtools.base64_encode";
    public override string Name => "base64_encode";
    public override string Description => "对文本进行Base64编码。";

    public override string ParametersJsonSchema => @"
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

    public Base64EncodeToolFunction(string pluginId, IServiceProvider? serviceProvider)
        : base(pluginId, serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[DevToolsPlugin] 执行 base64_encode 工具函数");

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
            await RecordUsageAsync("base64_encode", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["inputLength"] = text.Length,
                ["outputLength"] = result.Length
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevToolsPlugin] base64_encode 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("base64_encode", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class Base64DecodeToolFunction : DevToolsToolFunctionBase
{
    public override string Id => "devtools.base64_decode";
    public override string Name => "base64_decode";
    public override string Description => "对Base64编码的文本进行解码。";

    public override string ParametersJsonSchema => @"
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

    public Base64DecodeToolFunction(string pluginId, IServiceProvider? serviceProvider)
        : base(pluginId, serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[DevToolsPlugin] 执行 base64_decode 工具函数");

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
            await RecordUsageAsync("base64_decode", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["inputLength"] = text.Length,
                ["outputLength"] = result.Length
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevToolsPlugin] base64_decode 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("base64_decode", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class UrlEncodeToolFunction : DevToolsToolFunctionBase
{
    public override string Id => "devtools.url_encode";
    public override string Name => "url_encode";
    public override string Description => "对文本进行URL编码。";

    public override string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""text"": {
            ""type"": ""string"",
            ""description"": ""需要进行URL编码的文本""
        }
    },
    ""required"": [""text""]
}";

    public UrlEncodeToolFunction(string pluginId, IServiceProvider? serviceProvider)
        : base(pluginId, serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[DevToolsPlugin] 执行 url_encode 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var text = paramsDoc.RootElement.GetProperty("text").GetString() ?? string.Empty;

            var service = new EncodingService();
            var result = await service.UrlEncodeAsync(text);

            var response = new
            {
                success = true,
                result = result
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("url_encode", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["inputLength"] = text.Length
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevToolsPlugin] url_encode 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("url_encode", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class UrlDecodeToolFunction : DevToolsToolFunctionBase
{
    public override string Id => "devtools.url_decode";
    public override string Name => "url_decode";
    public override string Description => "对URL编码的文本进行解码。";

    public override string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""text"": {
            ""type"": ""string"",
            ""description"": ""需要解码的URL编码字符串""
        }
    },
    ""required"": [""text""]
}";

    public UrlDecodeToolFunction(string pluginId, IServiceProvider? serviceProvider)
        : base(pluginId, serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Info("[DevToolsPlugin] 执行 url_decode 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var text = paramsDoc.RootElement.GetProperty("text").GetString() ?? string.Empty;

            var service = new EncodingService();
            var result = await service.UrlDecodeAsync(text);

            var response = new
            {
                success = true,
                result = result
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("url_decode", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["inputLength"] = text.Length
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevToolsPlugin] url_decode 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("url_decode", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class ComputeHashToolFunction : DevToolsToolFunctionBase
{
    public override string Id => "devtools.compute_hash";
    public override string Name => "compute_hash";
    public override string Description => "计算文本的哈希值，支持MD5、SHA1、SHA256、SHA512算法。";

    public override string ParametersJsonSchema => @"
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
            ""default"": ""sha256""
        }
    },
    ""required"": [""text""]
}";

    public ComputeHashToolFunction(string pluginId, IServiceProvider? serviceProvider)
        : base(pluginId, serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Debug("[DevToolsPlugin] 执行 compute_hash 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var text = paramsDoc.RootElement.GetProperty("text").GetString() ?? string.Empty;
            var algorithm = "sha256";
            if (paramsDoc.RootElement.TryGetProperty("algorithm", out var algoProp))
            {
                algorithm = algoProp.GetString() ?? "sha256";
            }

            var service = new HashService();
            string result;

            switch (algorithm.ToLowerInvariant())
            {
                case "md5":
                    result = await service.ComputeMd5Async(text);
                    break;
                case "sha1":
                    result = await service.ComputeSha1Async(text);
                    break;
                case "sha512":
                    result = await service.ComputeSha512Async(text);
                    break;
                case "sha256":
                default:
                    result = await service.ComputeSha256Async(text);
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
            await RecordUsageAsync("compute_hash", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["algorithm"] = algorithm,
                ["inputLength"] = text.Length
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevToolsPlugin] compute_hash 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("compute_hash", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class ComputeHmacToolFunction : DevToolsToolFunctionBase
{
    public override string Id => "devtools.compute_hmac";
    public override string Name => "compute_hmac";
    public override string Description => "计算HMAC哈希消息认证码，支持MD5、SHA1、SHA256、SHA512算法。";

    public override string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""text"": {
            ""type"": ""string"",
            ""description"": ""需要计算HMAC的文本""
        },
        ""key"": {
            ""type"": ""string"",
            ""description"": ""HMAC密钥""
        },
        ""algorithm"": {
            ""type"": ""string"",
            ""description"": ""哈希算法：md5、sha1、sha256、sha512"",
            ""enum"": [""md5"", ""sha1"", ""sha256"", ""sha512""],
            ""default"": ""sha256""
        }
    },
    ""required"": [""text"", ""key""]
}";

    public ComputeHmacToolFunction(string pluginId, IServiceProvider? serviceProvider)
        : base(pluginId, serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Debug("[DevToolsPlugin] 执行 compute_hmac 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var text = paramsDoc.RootElement.GetProperty("text").GetString() ?? string.Empty;
            var key = paramsDoc.RootElement.GetProperty("key").GetString() ?? string.Empty;
            var algorithm = "sha256";
            if (paramsDoc.RootElement.TryGetProperty("algorithm", out var algoProp))
            {
                algorithm = algoProp.GetString() ?? "sha256";
            }

            var service = new HashService();
            var result = await service.ComputeHmacAsync(text, key, algorithm);

            var response = new
            {
                success = true,
                result = result,
                algorithm = algorithm
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("compute_hmac", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["algorithm"] = algorithm,
                ["inputLength"] = text.Length
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevToolsPlugin] compute_hmac 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("compute_hmac", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class AesEncryptToolFunction : DevToolsToolFunctionBase
{
    public override string Id => "devtools.aes_encrypt";
    public override string Name => "aes_encrypt";
    public override string Description => "使用AES算法加密文本。";

    public override string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""text"": {
            ""type"": ""string"",
            ""description"": ""需要加密的明文文本""
        },
        ""key"": {
            ""type"": ""string"",
            ""description"": ""加密密钥""
        },
        ""iv"": {
            ""type"": ""string"",
            ""description"": ""初始向量（可选）""
        }
    },
    ""required"": [""text"", ""key""]
}";

    public AesEncryptToolFunction(string pluginId, IServiceProvider? serviceProvider)
        : base(pluginId, serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Debug("[DevToolsPlugin] 执行 aes_encrypt 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var text = paramsDoc.RootElement.GetProperty("text").GetString() ?? string.Empty;
            var key = paramsDoc.RootElement.GetProperty("key").GetString() ?? string.Empty;
            string? iv = null;
            if (paramsDoc.RootElement.TryGetProperty("iv", out var ivProp))
            {
                iv = ivProp.GetString();
            }

            var service = new HashService();
            var result = await service.AesEncryptAsync(text, key, iv);

            var response = new
            {
                success = true,
                result = result
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("aes_encrypt", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["inputLength"] = text.Length,
                ["hasIv"] = iv != null
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevToolsPlugin] aes_encrypt 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("aes_encrypt", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class AesDecryptToolFunction : DevToolsToolFunctionBase
{
    public override string Id => "devtools.aes_decrypt";
    public override string Name => "aes_decrypt";
    public override string Description => "使用AES算法解密密文。";

    public override string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""text"": {
            ""type"": ""string"",
            ""description"": ""需要解密的Base64编码密文""
        },
        ""key"": {
            ""type"": ""string"",
            ""description"": ""解密密钥""
        },
        ""iv"": {
            ""type"": ""string"",
            ""description"": ""初始向量（可选，如果密文中已包含则不需要）""
        }
    },
    ""required"": [""text"", ""key""]
}";

    public AesDecryptToolFunction(string pluginId, IServiceProvider? serviceProvider)
        : base(pluginId, serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Debug("[DevToolsPlugin] 执行 aes_decrypt 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var text = paramsDoc.RootElement.GetProperty("text").GetString() ?? string.Empty;
            var key = paramsDoc.RootElement.GetProperty("key").GetString() ?? string.Empty;
            string? iv = null;
            if (paramsDoc.RootElement.TryGetProperty("iv", out var ivProp))
            {
                iv = ivProp.GetString();
            }

            var service = new HashService();
            var result = await service.AesDecryptAsync(text, key, iv);

            var response = new
            {
                success = true,
                result = result
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("aes_decrypt", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["inputLength"] = text.Length,
                ["hasIv"] = iv != null
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevToolsPlugin] aes_decrypt 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("aes_decrypt", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class TestRegexToolFunction : DevToolsToolFunctionBase
{
    public override string Id => "devtools.test_regex";
    public override string Name => "test_regex";
    public override string Description => "测试正则表达式匹配，返回所有匹配结果和捕获组详情。";

    public override string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""pattern"": {
            ""type"": ""string"",
            ""description"": ""正则表达式模式""
        },
        ""input"": {
            ""type"": ""string"",
            ""description"": ""要测试的输入文本""
        },
        ""ignoreCase"": {
            ""type"": ""boolean"",
            ""description"": ""忽略大小写"",
            ""default"": false
        },
        ""multiline"": {
            ""type"": ""boolean"",
            ""description"": ""多行模式"",
            ""default"": false
        }
    },
    ""required"": [""pattern"", ""input""]
}";

    public TestRegexToolFunction(string pluginId, IServiceProvider? serviceProvider)
        : base(pluginId, serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Debug("[DevToolsPlugin] 执行 test_regex 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var pattern = paramsDoc.RootElement.GetProperty("pattern").GetString() ?? string.Empty;
            var input = paramsDoc.RootElement.GetProperty("input").GetString() ?? string.Empty;
            var ignoreCase = false;
            if (paramsDoc.RootElement.TryGetProperty("ignoreCase", out var icProp))
                ignoreCase = icProp.GetBoolean();
            var multiline = false;
            if (paramsDoc.RootElement.TryGetProperty("multiline", out var mProp))
                multiline = mProp.GetBoolean();

            var service = new RegexService();
            var result = await service.TestMatchAsync(pattern, input, ignoreCase, multiline);

            var response = new
            {
                success = true,
                matchCount = result.MatchCount,
                captureGroupCount = result.CaptureGroupCount,
                matches = result.Matches.Select(m => new
                {
                    index = m.Index,
                    length = m.Length,
                    value = m.Value,
                    groups = m.Groups.Select(g => new { name = g.Name, value = g.Value }).ToList()
                }).ToList()
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("test_regex", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["matchCount"] = result.MatchCount,
                ["inputLength"] = input.Length
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevToolsPlugin] test_regex 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("test_regex", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class GenerateRegexToolFunction : DevToolsToolFunctionBase
{
    public override string Id => "devtools.generate_regex";
    public override string Name => "generate_regex";
    public override string Description => "根据描述生成正则表达式（支持常用场景：邮箱、手机、URL、IP、日期、中文、数字等）。";

    public override string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""description"": {
            ""type"": ""string"",
            ""description"": ""正则表达式的功能描述，如：邮箱、手机号、URL等""
        },
        ""language"": {
            ""type"": ""string"",
            ""description"": ""描述语言，zh或en"",
            ""default"": ""zh""
        }
    },
    ""required"": [""description""]
}";

    public GenerateRegexToolFunction(string pluginId, IServiceProvider? serviceProvider)
        : base(pluginId, serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Debug("[DevToolsPlugin] 执行 generate_regex 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var description = paramsDoc.RootElement.GetProperty("description").GetString() ?? string.Empty;
            var language = "zh";
            if (paramsDoc.RootElement.TryGetProperty("language", out var langProp))
                language = langProp.GetString() ?? "zh";

            var service = new RegexService();
            var result = await service.GenerateRegexAsync(description, language);

            var response = new
            {
                success = true,
                pattern = result,
                description = description
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("generate_regex", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["description"] = description
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevToolsPlugin] generate_regex 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("generate_regex", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class RegexReplaceToolFunction : DevToolsToolFunctionBase
{
    public override string Id => "devtools.regex_replace";
    public override string Name => "regex_replace";
    public override string Description => "使用正则表达式进行文本替换。";

    public override string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""pattern"": {
            ""type"": ""string"",
            ""description"": ""正则表达式模式""
        },
        ""input"": {
            ""type"": ""string"",
            ""description"": ""输入文本""
        },
        ""replacement"": {
            ""type"": ""string"",
            ""description"": ""替换文本，支持$1、$2等捕获组引用""
        },
        ""ignoreCase"": {
            ""type"": ""boolean"",
            ""description"": ""忽略大小写"",
            ""default"": false
        }
    },
    ""required"": [""pattern"", ""input"", ""replacement""]
}";

    public RegexReplaceToolFunction(string pluginId, IServiceProvider? serviceProvider)
        : base(pluginId, serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Debug("[DevToolsPlugin] 执行 regex_replace 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var pattern = paramsDoc.RootElement.GetProperty("pattern").GetString() ?? string.Empty;
            var input = paramsDoc.RootElement.GetProperty("input").GetString() ?? string.Empty;
            var replacement = paramsDoc.RootElement.GetProperty("replacement").GetString() ?? string.Empty;
            var ignoreCase = false;
            if (paramsDoc.RootElement.TryGetProperty("ignoreCase", out var icProp))
                ignoreCase = icProp.GetBoolean();

            var service = new RegexService();
            var result = await service.ReplaceAsync(pattern, input, replacement, ignoreCase);

            var response = new
            {
                success = true,
                result = result.Result,
                replacementCount = result.ReplacementCount
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("regex_replace", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["replacementCount"] = result.ReplacementCount,
                ["inputLength"] = input.Length
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevToolsPlugin] regex_replace 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("regex_replace", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class ConvertTimestampToolFunction : DevToolsToolFunctionBase
{
    public override string Id => "devtools.convert_timestamp";
    public override string Name => "convert_timestamp";
    public override string Description => "时间戳与日期时间互相转换，支持秒/毫秒、多时区。";

    public override string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""timestamp"": {
            ""type"": ""integer"",
            ""description"": ""时间戳（秒或毫秒）""
        },
        ""dateTime"": {
            ""type"": ""string"",
            ""description"": ""日期时间字符串（与timestamp二选一）""
        },
        ""timeUnit"": {
            ""type"": ""string"",
            ""enum"": [""s"", ""ms""],
            ""description"": ""时间单位：s秒，ms毫秒"",
            ""default"": ""ms""
        },
        ""timezone"": {
            ""type"": ""string"",
            ""description"": ""时区ID，如 China Standard Time、Eastern Standard Time""
        }
    },
    ""required"": []
}";

    public ConvertTimestampToolFunction(string pluginId, IServiceProvider? serviceProvider)
        : base(pluginId, serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Debug("[DevToolsPlugin] 执行 convert_timestamp 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var service = new TimestampService();
            var timeUnit = "ms";
            if (paramsDoc.RootElement.TryGetProperty("timeUnit", out var tuProp))
                timeUnit = tuProp.GetString() ?? "ms";
            var timezone = (string?)null;
            if (paramsDoc.RootElement.TryGetProperty("timezone", out var tzProp))
                timezone = tzProp.GetString();

            if (paramsDoc.RootElement.TryGetProperty("timestamp", out var tsProp))
            {
                var timestamp = tsProp.GetInt64();
                var result = await service.TimestampToDateTimeAsync(timestamp, timeUnit, timezone);

                var response = new
                {
                    success = true,
                    timestampSeconds = result.TimestampSeconds,
                    timestampMilliseconds = result.TimestampMilliseconds,
                    dateTimeIso = result.DateTimeIso,
                    dateTimeLocal = result.DateTimeLocal,
                    formats = result.Formats,
                    timezone = result.Timezone
                };

                var json = JsonSerializer.Serialize(response);
                stopwatch.Stop();
                await RecordUsageAsync("convert_timestamp", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
                {
                    ["direction"] = "timestamp-to-datetime"
                });
                return json;
            }
            else if (paramsDoc.RootElement.TryGetProperty("dateTime", out var dtProp))
            {
                var dateTime = dtProp.GetString() ?? string.Empty;
                var result = await service.DateTimeToTimestampAsync(dateTime, timeUnit, timezone);

                var response = new
                {
                    success = true,
                    timestampSeconds = result.TimestampSeconds,
                    timestampMilliseconds = result.TimestampMilliseconds,
                    dateTimeIso = result.DateTimeIso,
                    dateTimeLocal = result.DateTimeLocal,
                    timezone = result.Timezone
                };

                var json = JsonSerializer.Serialize(response);
                stopwatch.Stop();
                await RecordUsageAsync("convert_timestamp", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
                {
                    ["direction"] = "datetime-to-timestamp"
                });
                return json;
            }
            else
            {
                var result = await service.GetCurrentTimestampAsync();
                var response = new
                {
                    success = true,
                    timestampSeconds = result.TimestampSeconds,
                    timestampMilliseconds = result.TimestampMilliseconds,
                    dateTimeIso = result.DateTimeIso,
                    dateTimeLocal = result.DateTimeLocal
                };
                var json = JsonSerializer.Serialize(response);
                stopwatch.Stop();
                await RecordUsageAsync("convert_timestamp", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
                {
                    ["direction"] = "current"
                });
                return json;
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevToolsPlugin] convert_timestamp 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("convert_timestamp", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class ConvertColorToolFunction : DevToolsToolFunctionBase
{
    public override string Id => "devtools.convert_color";
    public override string Name => "convert_color";
    public override string Description => "颜色格式互相转换，支持HEX、RGB、HSL格式。";

    public override string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""hex"": {
            ""type"": ""string"",
            ""description"": ""HEX颜色值，如 #FF5733""
        },
        ""r"": { ""type"": ""integer"", ""description"": ""红色通道 0-255"" },
        ""g"": { ""type"": ""integer"", ""description"": ""绿色通道 0-255"" },
        ""b"": { ""type"": ""integer"", ""description"": ""蓝色通道 0-255"" }
    },
    ""required"": []
}";

    public ConvertColorToolFunction(string pluginId, IServiceProvider? serviceProvider)
        : base(pluginId, serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Debug("[DevToolsPlugin] 执行 convert_color 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var request = new ColorConvertRequest();

            if (paramsDoc.RootElement.TryGetProperty("hex", out var hexProp))
                request.Hex = hexProp.GetString();
            if (paramsDoc.RootElement.TryGetProperty("r", out var rProp))
                request.R = rProp.GetInt32();
            if (paramsDoc.RootElement.TryGetProperty("g", out var gProp))
                request.G = gProp.GetInt32();
            if (paramsDoc.RootElement.TryGetProperty("b", out var bProp))
                request.B = bProp.GetInt32();

            var service = new ColorService();
            var result = await service.ConvertColorAsync(request);

            var response = new
            {
                success = true,
                hex = result.Hex,
                rgb = new { r = result.R, g = result.G, b = result.B },
                hsl = new { h = result.H, s = result.S, l = result.L },
                rgbString = result.RgbString,
                hslString = result.HslString
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("convert_color", stopwatch.ElapsedMilliseconds);

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevToolsPlugin] convert_color 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("convert_color", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class GenerateColorPaletteToolFunction : DevToolsToolFunctionBase
{
    public override string Id => "devtools.generate_color_palette";
    public override string Name => "generate_color_palette";
    public override string Description => "根据基色生成配色方案，支持类似色、互补色、三角色、分裂互补、单色等方案。";

    public override string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""baseColor"": {
            ""type"": ""string"",
            ""description"": ""基色HEX值，如 #FF5733""
        },
        ""count"": {
            ""type"": ""integer"",
            ""description"": ""颜色数量，2-12"",
            ""default"": 5
        },
        ""scheme"": {
            ""type"": ""string"",
            ""enum"": [""analogous"", ""complementary"", ""triadic"", ""split-complementary"", ""monochromatic""],
            ""description"": ""配色方案：analogous类似色，complementary互补色，triadic三角色，split-complementary分裂互补，monochromatic单色"",
            ""default"": ""analogous""
        }
    },
    ""required"": [""baseColor""]
}";

    public GenerateColorPaletteToolFunction(string pluginId, IServiceProvider? serviceProvider)
        : base(pluginId, serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Debug("[DevToolsPlugin] 执行 generate_color_palette 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var baseColor = paramsDoc.RootElement.GetProperty("baseColor").GetString() ?? "#3b82f6";
            var count = 5;
            if (paramsDoc.RootElement.TryGetProperty("count", out var countProp))
                count = countProp.GetInt32();
            var scheme = "analogous";
            if (paramsDoc.RootElement.TryGetProperty("scheme", out var schemeProp))
                scheme = schemeProp.GetString() ?? "analogous";

            var service = new ColorService();
            var result = await service.GeneratePaletteAsync(baseColor, count, scheme);

            var response = new
            {
                success = true,
                baseColor = result.BaseColor,
                scheme = result.Scheme,
                colors = result.Colors
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("generate_color_palette", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["scheme"] = scheme,
                ["count"] = count
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevToolsPlugin] generate_color_palette 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("generate_color_palette", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class CheckColorContrastToolFunction : DevToolsToolFunctionBase
{
    public override string Id => "devtools.check_color_contrast";
    public override string Name => "check_color_contrast";
    public override string Description => "检查前景色和背景色的对比度是否符合WCAG标准（AA/AAA）。";

    public override string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""foreground"": {
            ""type"": ""string"",
            ""description"": ""前景色HEX值，如 #FFFFFF""
        },
        ""background"": {
            ""type"": ""string"",
            ""description"": ""背景色HEX值，如 #000000""
        }
    },
    ""required"": [""foreground"", ""background""]
}";

    public CheckColorContrastToolFunction(string pluginId, IServiceProvider? serviceProvider)
        : base(pluginId, serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Debug("[DevToolsPlugin] 执行 check_color_contrast 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var foreground = paramsDoc.RootElement.GetProperty("foreground").GetString() ?? "#000000";
            var background = paramsDoc.RootElement.GetProperty("background").GetString() ?? "#ffffff";

            var service = new ColorService();
            var result = await service.CheckContrastAsync(foreground, background);

            var response = new
            {
                success = true,
                ratio = result.Ratio,
                level = result.Level,
                aaNormal = result.AANormal,
                aaLarge = result.AALarge,
                aaaNormal = result.AAANormal,
                aaaLarge = result.AAALarge
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("check_color_contrast", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["ratio"] = result.Ratio
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevToolsPlugin] check_color_contrast 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("check_color_contrast", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class DecodeJwtToolFunction : DevToolsToolFunctionBase
{
    public override string Id => "devtools.decode_jwt";
    public override string Name => "decode_jwt";
    public override string Description => "解析JWT Token，返回Header、Payload、签名、过期时间等信息。";

    public override string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""token"": {
            ""type"": ""string"",
            ""description"": ""JWT Token字符串""
        }
    },
    ""required"": [""token""]
}";

    public DecodeJwtToolFunction(string pluginId, IServiceProvider? serviceProvider)
        : base(pluginId, serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Debug("[DevToolsPlugin] 执行 decode_jwt 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var token = paramsDoc.RootElement.GetProperty("token").GetString() ?? string.Empty;

            var service = new JwtService();
            var result = await service.DecodeJwtAsync(token);

            var response = new
            {
                success = result.Success,
                header = result.Header,
                payload = result.Payload,
                signature = result.Signature,
                isExpired = result.IsExpired,
                issuedAt = result.IssuedAt,
                expiration = result.Expiration,
                timeRemaining = result.TimeRemaining,
                error = result.ErrorMessage
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("decode_jwt", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["isExpired"] = result.IsExpired
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevToolsPlugin] decode_jwt 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("decode_jwt", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class ValidateJwtToolFunction : DevToolsToolFunctionBase
{
    public override string Id => "devtools.validate_jwt";
    public override string Name => "validate_jwt";
    public override string Description => "验证JWT Token的签名是否有效。";

    public override string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""token"": {
            ""type"": ""string"",
            ""description"": ""JWT Token字符串""
        },
        ""secret"": {
            ""type"": ""string"",
            ""description"": ""签名密钥""
        },
        ""algorithm"": {
            ""type"": ""string"",
            ""enum"": [""HS256"", ""HS384"", ""HS512""],
            ""description"": ""签名算法"",
            ""default"": ""HS256""
        }
    },
    ""required"": [""token"", ""secret""]
}";

    public ValidateJwtToolFunction(string pluginId, IServiceProvider? serviceProvider)
        : base(pluginId, serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Debug("[DevToolsPlugin] 执行 validate_jwt 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var token = paramsDoc.RootElement.GetProperty("token").GetString() ?? string.Empty;
            var secret = paramsDoc.RootElement.GetProperty("secret").GetString() ?? string.Empty;
            var algorithm = "HS256";
            if (paramsDoc.RootElement.TryGetProperty("algorithm", out var algoProp))
                algorithm = algoProp.GetString() ?? "HS256";

            var service = new JwtService();
            var result = await service.ValidateSignatureAsync(token, secret, algorithm);

            var response = new
            {
                success = true,
                isValid = result.IsValid,
                errorMessage = result.ErrorMessage
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("validate_jwt", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["isValid"] = result.IsValid,
                ["algorithm"] = algorithm
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevToolsPlugin] validate_jwt 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("validate_jwt", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class GenerateJwtToolFunction : DevToolsToolFunctionBase
{
    public override string Id => "devtools.generate_jwt";
    public override string Name => "generate_jwt";
    public override string Description => "生成测试用的JWT Token。";

    public override string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""payload"": {
            ""type"": ""object"",
            ""description"": ""JWT Payload载荷（JSON对象）""
        },
        ""secret"": {
            ""type"": ""string"",
            ""description"": ""签名密钥""
        },
        ""algorithm"": {
            ""type"": ""string"",
            ""enum"": [""HS256"", ""HS384"", ""HS512""],
            ""description"": ""签名算法"",
            ""default"": ""HS256""
        },
        ""expiresInMinutes"": {
            ""type"": ""integer"",
            ""description"": ""过期时间（分钟），不设置则永不过期""
        }
    },
    ""required"": [""payload"", ""secret""]
}";

    public GenerateJwtToolFunction(string pluginId, IServiceProvider? serviceProvider)
        : base(pluginId, serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Debug("[DevToolsPlugin] 执行 generate_jwt 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var payloadElement = paramsDoc.RootElement.GetProperty("payload");
            var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(payloadElement.GetRawText())
                ?? new Dictionary<string, object>();
            var secret = paramsDoc.RootElement.GetProperty("secret").GetString() ?? string.Empty;
            var algorithm = "HS256";
            if (paramsDoc.RootElement.TryGetProperty("algorithm", out var algoProp))
                algorithm = algoProp.GetString() ?? "HS256";
            int? expiresInMinutes = null;
            if (paramsDoc.RootElement.TryGetProperty("expiresInMinutes", out var expProp))
                expiresInMinutes = expProp.GetInt32();

            var service = new JwtService();
            var result = await service.GenerateJwtAsync(payload, secret, algorithm, expiresInMinutes);

            var response = new
            {
                success = true,
                token = result.Token,
                issuedAt = result.IssuedAt,
                expiration = result.Expiration
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("generate_jwt", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["algorithm"] = algorithm,
                ["hasExpiration"] = expiresInMinutes.HasValue
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevToolsPlugin] generate_jwt 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("generate_jwt", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class GenerateUuidToolFunction : DevToolsToolFunctionBase
{
    public override string Id => "devtools.generate_uuid";
    public override string Name => "generate_uuid";
    public override string Description => "生成UUID，支持v1（时间戳）和v4（随机）版本，支持批量生成。";

    public override string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""version"": {
            ""type"": ""string"",
            ""enum"": [""v1"", ""v4""],
            ""description"": ""UUID版本：v1时间戳，v4随机"",
            ""default"": ""v4""
        },
        ""count"": {
            ""type"": ""integer"",
            ""description"": ""生成数量（1-100）"",
            ""default"": 1,
            ""minimum"": 1,
            ""maximum"": 100
        },
        ""uppercase"": {
            ""type"": ""boolean"",
            ""description"": ""是否大写"",
            ""default"": false
        },
        ""withHyphens"": {
            ""type"": ""boolean"",
            ""description"": ""是否带连字符"",
            ""default"": true
        }
    },
    ""required"": []
}";

    public GenerateUuidToolFunction(string pluginId, IServiceProvider? serviceProvider)
        : base(pluginId, serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Debug("[DevToolsPlugin] 执行 generate_uuid 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var version = "v4";
            if (paramsDoc.RootElement.TryGetProperty("version", out var verProp))
                version = verProp.GetString() ?? "v4";
            var count = 1;
            if (paramsDoc.RootElement.TryGetProperty("count", out var countProp))
                count = countProp.GetInt32();
            var uppercase = false;
            if (paramsDoc.RootElement.TryGetProperty("uppercase", out var ucProp))
                uppercase = ucProp.GetBoolean();
            var withHyphens = true;
            if (paramsDoc.RootElement.TryGetProperty("withHyphens", out var whProp))
                withHyphens = whProp.GetBoolean();

            var service = new UuidService();
            var result = await service.GenerateUuidAsync(version, count, uppercase, withHyphens);

            var response = new
            {
                success = true,
                ids = result.Ids,
                version = result.Version,
                count = result.Count
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("generate_uuid", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["version"] = version,
                ["count"] = count
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevToolsPlugin] generate_uuid 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("generate_uuid", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class GenerateSnowflakeIdToolFunction : DevToolsToolFunctionBase
{
    public override string Id => "devtools.generate_snowflake_id";
    public override string Name => "generate_snowflake_id";
    public override string Description => "生成雪花ID（Snowflake），支持批量生成，可解析时间戳、工作ID等信息。";

    public override string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""workerId"": {
            ""type"": ""integer"",
            ""description"": ""工作ID（0-31）"",
            ""default"": 1,
            ""minimum"": 0,
            ""maximum"": 31
        },
        ""datacenterId"": {
            ""type"": ""integer"",
            ""description"": ""数据中心ID（0-31）"",
            ""default"": 1,
            ""minimum"": 0,
            ""maximum"": 31
        },
        ""count"": {
            ""type"": ""integer"",
            ""description"": ""生成数量（1-100）"",
            ""default"": 1,
            ""minimum"": 1,
            ""maximum"": 100
        }
    },
    ""required"": []
}";

    public GenerateSnowflakeIdToolFunction(string pluginId, IServiceProvider? serviceProvider)
        : base(pluginId, serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Debug("[DevToolsPlugin] 执行 generate_snowflake_id 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var workerId = 1L;
            if (paramsDoc.RootElement.TryGetProperty("workerId", out var widProp))
                workerId = widProp.GetInt64();
            var datacenterId = 1L;
            if (paramsDoc.RootElement.TryGetProperty("datacenterId", out var didProp))
                datacenterId = didProp.GetInt64();
            var count = 1;
            if (paramsDoc.RootElement.TryGetProperty("count", out var countProp))
                count = countProp.GetInt32();

            var service = new UuidService();
            var result = await service.GenerateSnowflakeIdAsync(workerId, datacenterId, count);

            var response = new
            {
                success = true,
                ids = result.Ids.Select(id => new
                {
                    id = id.Id,
                    timestamp = id.Timestamp,
                    workerId = id.WorkerId,
                    datacenterId = id.DatacenterId,
                    sequence = id.Sequence
                }).ToList(),
                count = result.Count
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("generate_snowflake_id", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["count"] = count
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevToolsPlugin] generate_snowflake_id 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("generate_snowflake_id", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

public class GenerateQrCodeToolFunction : DevToolsToolFunctionBase
{
    public override string Id => "devtools.generate_qrcode";
    public override string Name => "generate_qrcode";
    public override string Description => "生成二维码图片，返回Base64编码的PNG图片。支持自定义大小、容错级别、颜色等。";

    public override string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""text"": {
            ""type"": ""string"",
            ""description"": ""二维码内容（文本、URL等）""
        },
        ""size"": {
            ""type"": ""integer"",
            ""description"": ""图片大小（像素），128-512"",
            ""default"": 256,
            ""minimum"": 128,
            ""maximum"": 512
        },
        ""level"": {
            ""type"": ""string"",
            ""enum"": [""L"", ""M"", ""Q"", ""H""],
            ""description"": ""容错级别：L低，M中，Q较高，H高"",
            ""default"": ""M""
        },
        ""margin"": {
            ""type"": ""integer"",
            ""description"": ""边距（模块数）"",
            ""default"": 4
        },
        ""foregroundColor"": {
            ""type"": ""string"",
            ""description"": ""前景色（HEX格式）"",
            ""default"": ""#000000""
        },
        ""backgroundColor"": {
            ""type"": ""string"",
            ""description"": ""背景色（HEX格式）"",
            ""default"": ""#FFFFFF""
        }
    },
    ""required"": [""text""]
}";

    public GenerateQrCodeToolFunction(string pluginId, IServiceProvider? serviceProvider)
        : base(pluginId, serviceProvider) { }

    public override async Task<string> ExecuteAsync(string parameters)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            XTrace.Log.Debug("[DevToolsPlugin] 执行 generate_qrcode 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var text = paramsDoc.RootElement.GetProperty("text").GetString() ?? string.Empty;
            var size = 256;
            if (paramsDoc.RootElement.TryGetProperty("size", out var sizeProp))
                size = sizeProp.GetInt32();
            var level = "M";
            if (paramsDoc.RootElement.TryGetProperty("level", out var levelProp))
                level = levelProp.GetString() ?? "M";
            var margin = 4;
            if (paramsDoc.RootElement.TryGetProperty("margin", out var marginProp))
                margin = marginProp.GetInt32();
            var foregroundColor = "#000000";
            if (paramsDoc.RootElement.TryGetProperty("foregroundColor", out var fgProp))
                foregroundColor = fgProp.GetString() ?? "#000000";
            var backgroundColor = "#FFFFFF";
            if (paramsDoc.RootElement.TryGetProperty("backgroundColor", out var bgProp))
                backgroundColor = bgProp.GetString() ?? "#FFFFFF";

            var service = new QrCodeService();
            var result = await service.GenerateCustomQrCodeAsync(text, size, level, margin, foregroundColor, backgroundColor);

            var response = new
            {
                success = true,
                imageBase64 = result.ImageBase64,
                size = result.Size,
                level = result.Level
            };

            var json = JsonSerializer.Serialize(response);

            stopwatch.Stop();
            await RecordUsageAsync("generate_qrcode", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["size"] = size,
                ["level"] = level,
                ["inputLength"] = text.Length
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevToolsPlugin] generate_qrcode 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await RecordUsageAsync("generate_qrcode", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

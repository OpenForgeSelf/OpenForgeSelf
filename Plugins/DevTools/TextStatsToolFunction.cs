using System.Diagnostics;
using System.Text.Json;
using ForgeSelf.Abstractions;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.DevTools;

/// <summary>
/// dev_text_stats（2026-10-28 自 text-tools 的 text_stats 并入 dev-tools，用户拍板 text-tools 全部合并；
/// 按命名空间规则改 dev_ 前缀——ADR-004；pilot 2026-10-28-plugin-design-boundary D-V1）。
/// 统计文本的字符数、字数、行数、字节数。
/// </summary>
public class TextStatsToolFunction : IToolFunctionExtension
{
    private readonly IServiceProvider? _serviceProvider;

    public string Id => "devtools.text_stats";
    public string Name => "dev_text_stats";
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
            XTrace.Log.Debug("[DevTools] 执行 dev_text_stats 工具函数");

            var paramsDoc = JsonDocument.Parse(parameters);
            var text = paramsDoc.RootElement.GetProperty("text").GetString() ?? string.Empty;

            var service = new Services.TextStatsService();
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
            await this.RecordUsageAsync(_serviceProvider, "dev_text_stats", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["charCount"] = stats.CharCount,
                ["wordCount"] = stats.WordCount,
                ["lineCount"] = stats.LineCount
            });

            return json;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[DevTools] dev_text_stats 执行失败: {0}", ex.Message);

            stopwatch.Stop();
            await this.RecordUsageAsync(_serviceProvider, "dev_text_stats", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
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

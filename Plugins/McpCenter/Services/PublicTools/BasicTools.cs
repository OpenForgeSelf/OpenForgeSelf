using System.Text.Json;
using ForgeSelf.Abstractions;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.McpCenter.Services;

/// <summary>
/// 平台基础小工具（2026-10-28 工具归属迁移自 AIAgent → mcp-center；mcp-center 整插件豁免前缀——
/// 用户拍板 2026-10-28 17:29「mcp中心的工具不用前缀，是特殊例外」，故用原名 get_current_time/calculate）。
/// 与插件主功能无直接关系的公共基础工具归 mcp-center（02-spec FR-3.1）。
/// </summary>
public class GetCurrentTimeToolFunction : IToolFunctionExtension
{
    public string Id => "mcp-center.get_current_time";
    public string Name => "get_current_time";
    public string PluginId => "mcp-center";
    public string Description => "获取当前的日期和时间。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""format"": {
            ""type"": ""string"",
            ""description"": ""时间格式，如 yyyy-MM-dd HH:mm:ss"",
            ""default"": ""yyyy-MM-dd HH:mm:ss""
        },
        ""timezone"": {
            ""type"": ""string"",
            ""description"": ""时区，默认使用本地时区"",
            ""default"": ""local""
        }
    },
    ""required"": []
}";

    public Task<string> ExecuteAsync(string parameters)
    {
        try
        {
            XTrace.Log.Debug("[McpCenter] 执行 get_current_time 工具函数");

            var format = "yyyy-MM-dd HH:mm:ss";
            if (!string.IsNullOrEmpty(parameters))
            {
                using var doc = JsonDocument.Parse(parameters);
                if (doc.RootElement.TryGetProperty("format", out var formatProp))
                {
                    format = formatProp.GetString() ?? format;
                }
            }

            var now = DateTime.Now;
            var result = now.ToString(format);

            var response = new
            {
                success = true,
                currentTime = result,
                timestamp = new DateTimeOffset(now).ToUnixTimeSeconds()
            };

            return Task.FromResult(JsonSerializer.Serialize(response));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[McpCenter] get_current_time 执行失败: {0}", ex.Message);
            return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
        }
    }
}

public class CalculateToolFunction : IToolFunctionExtension
{
    public string Id => "mcp-center.calculate";
    public string Name => "calculate";
    public string PluginId => "mcp-center";
    public string Description => "执行基本的数学计算，支持加减乘除。";

    public string ParametersJsonSchema => @"
{
    ""type"": ""object"",
    ""properties"": {
        ""expression"": {
            ""type"": ""string"",
            ""description"": ""数学表达式，如 1 + 2 * 3""
        }
    },
    ""required"": [""expression""]
}";

    public Task<string> ExecuteAsync(string parameters)
    {
        try
        {
            XTrace.Log.Debug("[McpCenter] 执行 calculate 工具函数，参数: {0}", parameters);

            using var doc = JsonDocument.Parse(parameters);
            var expression = doc.RootElement.GetProperty("expression").GetString() ?? string.Empty;

            var result = EvaluateExpression(expression);

            var response = new
            {
                success = true,
                expression = expression,
                result = result
            };

            return Task.FromResult(JsonSerializer.Serialize(response));
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[McpCenter] calculate 执行失败: {0}", ex.Message);
            return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
        }
    }

    private static double EvaluateExpression(string expression)
    {
        var table = new System.Data.DataTable();
        var result = table.Compute(expression, string.Empty);
        return Convert.ToDouble(result);
    }
}

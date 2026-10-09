using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AgentHub.Models;
using ForgeSelf.Api.Plugins.AgentHub.Services;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AgentHub.Tools;

/// <summary>
/// AgentHub 工具基类：统一从 DI 容器按 scope 取服务。
///
/// 注意：工具会被 AIAgent 直接调用，属**跨请求**生命周期，
/// 因此每次执行都要 CreateScope 取 Scoped 服务（不可缓存实例）。
/// </summary>
public abstract class AgentHubToolBase : IToolFunctionExtension
{
    /// <summary>DI 容器</summary>
    protected readonly IServiceProvider? Services;

    /// <inheritdoc />
    public abstract String Id { get; }

    /// <inheritdoc />
    public abstract String Name { get; }

    /// <inheritdoc />
    public String PluginId { get; }

    /// <inheritdoc />
    public abstract String Description { get; }

    /// <inheritdoc />
    public abstract String ParametersJsonSchema { get; }

    /// <summary>构造</summary>
    /// <param name="pluginId">插件标识</param>
    /// <param name="services">DI 容器</param>
    protected AgentHubToolBase(String pluginId, IServiceProvider? services)
    {
        PluginId = pluginId;
        Services = services;
    }

    /// <summary>取服务（宿主 DI，经宿主根 provider 回落；见 <see cref="AgentHubDi.ResolveHost{T}"/>）</summary>
    /// <typeparam name="T">服务类型</typeparam>
    /// <returns>服务实例；容器缺失返回 null</returns>
    protected T? GetService<T>() where T : class
        => AgentHubDi.ResolveHost<T>(Services);

    /// <inheritdoc />
    public abstract Task<String> ExecuteAsync(String parameters);

    /// <summary>统一的结果 JSON（工具返回必须可机读）</summary>
    /// <param name="success">是否成功</param>
    /// <param name="message">说明</param>
    /// <param name="data">数据</param>
    /// <returns>JSON 字符串</returns>
    protected static String Ok(Boolean success, String message, Object? data = null)
        => JsonSerializer.Serialize(new { success, message, data }, ToolJsonOpts);

    /// <summary>失败结果</summary>
    /// <param name="message">错误说明</param>
    /// <returns>JSON 字符串</returns>
    protected static String Fail(String message) => Ok(false, message);

    /// <summary>解析参数 JSON 为字典（坏格式返回空字典，不抛出）</summary>
    /// <param name="parameters">参数 JSON</param>
    /// <returns>字典</returns>
    protected static Dictionary<String, JsonElement> ParseArgs(String parameters)
    {
        if (String.IsNullOrWhiteSpace(parameters)) return [];

        try
        {
            var doc = JsonDocument.Parse(parameters);
            return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException ex)
        {
            XTrace.Log.Warn("[AgentHub] 工具参数 JSON 解析失败: {0}", ex.Message);
            return [];
        }
    }

    /// <summary>取字符串参数</summary>
    /// <param name="args">参数字典</param>
    /// <param name="key">键</param>
    /// <returns>值；不存在返回 null</returns>
    protected static String? ArgString(Dictionary<String, JsonElement> args, String key)
        => args.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>取整型参数</summary>
    /// <param name="args">参数字典</param>
    /// <param name="key">键</param>
    /// <returns>值；不存在/非法返回 null</returns>
    protected static Int32? ArgInt(Dictionary<String, JsonElement> args, String key)
        => args.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;

    /// <summary>取布尔参数</summary>
    /// <param name="args">参数字典</param>
    /// <param name="key">键</param>
    /// <returns>值；不存在返回 null</returns>
    protected static Boolean? ArgBool(Dictionary<String, JsonElement> args, String key)
        => args.TryGetValue(key, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;

    private static readonly JsonSerializerOptions ToolJsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}

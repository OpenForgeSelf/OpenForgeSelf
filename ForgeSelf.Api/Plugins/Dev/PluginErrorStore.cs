using System.Collections.Concurrent;

namespace ForgeSelf.Api.Plugins.Dev;

/// <summary>插件错误记录（完整保留异常要素，供 API/前端/诊断端点展示）。</summary>
/// <param name="Message">异常消息。</param>
/// <param name="ExceptionType">异常类型全名。</param>
/// <param name="StackTrace">异常堆栈（可能为空串）。</param>
/// <param name="OccurredAt">发生时间（本地时间）。</param>
public sealed record PluginErrorRecord(
    string Message,
    string ExceptionType,
    string StackTrace,
    DateTime OccurredAt);

/// <summary>
/// 插件错误仓库（进程内内存存储，dev 与 Production 均生效）。
/// <para>
/// 背景：PluginManager 历史上五处 catch 只保留 <c>ex.Message</c> 后即丢弃异常对象，
/// 前端只能看到一个 Error 徽标无从定位。本仓库在保留既有日志行为的同时记录完整异常，
/// 并在插件成功装载/初始化后清除对应记录。
/// </para>
/// </summary>
public static class PluginErrorStore
{
    private static readonly ConcurrentDictionary<string, PluginErrorRecord> Errors = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>记录插件错误（重复发生时覆盖为最新一次）。</summary>
    public static void Set(string pluginId, Exception ex)
    {
        if (string.IsNullOrWhiteSpace(pluginId) || ex == null)
            return;

        Errors[pluginId] = new PluginErrorRecord(
            Message: ex.Message,
            ExceptionType: ex.GetType().FullName ?? ex.GetType().Name,
            StackTrace: ex.StackTrace ?? string.Empty,
            OccurredAt: DateTime.Now);
    }

    /// <summary>读取插件最近一次错误记录。</summary>
    public static PluginErrorRecord? TryGet(string pluginId) =>
        Errors.TryGetValue(pluginId, out var record) ? record : null;

    /// <summary>清除插件错误记录（装载/初始化成功、成功重载后调用）。</summary>
    public static void Clear(string pluginId) => Errors.TryRemove(pluginId, out _);

    /// <summary>全部错误记录快照（diagnostics 用）。</summary>
    public static IReadOnlyDictionary<string, PluginErrorRecord> GetAll() =>
        Errors.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
}

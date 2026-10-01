using NewLife.Log;

namespace ForgeSelf.Api.Plugins.Dev;

/// <summary>
/// 插件日志作用域：以 <see cref="AsyncLocal{T}"/> 承载「当前日志归属的插件 Id」。
/// <para>
/// 在插件装载 / Apply / 卸载及插件控制器请求期 Push，
/// <see cref="PluginTaggedLog"/> 在写日志时读取并给消息加 <c>[plugin:&lt;id&gt;] </c> 前缀。
/// 已知限制：插件自起后台线程/任务（脱离请求与装载调用链）不经过本作用域，其日志无维度
/// （AsyncLocal 不跨线程流动）——按插件分文件的 <c>ILogService</c> 通道不受此限制。
/// </para>
/// </summary>
public static class PluginLogScope
{
    private static readonly AsyncLocal<string?> Current = new();

    /// <summary>当前作用域的插件 Id；不在任何插件作用域内为 null。</summary>
    public static string? CurrentPluginId => Current.Value;

    /// <summary>
    /// 进入插件日志作用域。返回 IDisposable，dispose 时恢复外层值（支持嵌套）。
    /// </summary>
    /// <param name="pluginId">插件 Id。</param>
    public static IDisposable Push(string pluginId)
    {
        var previous = Current.Value;
        Current.Value = pluginId;
        return new ScopeLease(previous);
    }

    private sealed class ScopeLease(string? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}

/// <summary>
/// <see cref="ILog"/> 装饰器：透传全部成员（成员清单依据 NewLife.Core 11.17.2026.701 的 ILog 定义：
/// 属性 Enable/Level + 方法 Debug/Info/Warn/Error/Fatal/Write），命中 <see cref="PluginLogScope"/> 时
/// 给消息 format 前缀 <c>[plugin:&lt;id&gt;] </c>，未命中零改动。
/// </summary>
/// <remarks>
/// 仅 dev 总闸下由 AppBuilder 装配（<c>XTrace.Log = new PluginTaggedLog(XTrace.Log)</c>）；
/// Production 不包装，日志路径与改动前逐字节一致。
/// </remarks>
public sealed class PluginTaggedLog(ILog inner) : ILog
{
    private readonly ILog _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    /// <inheritdoc />
    public bool Enable { get => _inner.Enable; set => _inner.Enable = value; }

    /// <inheritdoc />
    public NewLife.Log.LogLevel Level { get => _inner.Level; set => _inner.Level = value; }

    /// <inheritdoc />
    public void Debug(string format, params object[] args) => _inner.Debug(Tag(format), args);

    /// <inheritdoc />
    public void Info(string format, params object[] args) => _inner.Info(Tag(format), args);

    /// <inheritdoc />
    public void Warn(string format, params object[] args) => _inner.Warn(Tag(format), args);

    /// <inheritdoc />
    public void Error(string format, params object[] args) => _inner.Error(Tag(format), args);

    /// <inheritdoc />
    public void Fatal(string format, params object[] args) => _inner.Fatal(Tag(format), args);

    /// <inheritdoc />
    public void Write(NewLife.Log.LogLevel level, string format, params object?[] args) => _inner.Write(level, Tag(format), args);

    private static string Tag(string format)
    {
        var pluginId = PluginLogScope.CurrentPluginId;
        return pluginId is null ? format : $"[plugin:{pluginId}] " + format;
    }
}

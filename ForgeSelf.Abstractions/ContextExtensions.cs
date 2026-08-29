using System;
using ForgeSelf.Core;

namespace ForgeSelf.Abstractions;

/// <summary>
/// 插件上下文扩展：无参获取本插件专属数据目录。
/// 内部自动从共享上下文取 <see cref="IDataLocationService"/> 与 <see cref="PluginMetadata"/>（插件 id），
/// 插件无需关心路径来源与运行形态。
/// </summary>
public static class ContextExtensions
{
    /// <summary>本插件专属数据根目录（{宿主数据根}/Plugins/{pluginId}）。</summary>
    public static string GetPluginDataDirectory(this IContext ctx)
    {
        var dls = ctx.Get<IDataLocationService>()
            ?? throw new InvalidOperationException("IDataLocationService 未注册到插件上下文");
        var pluginId = ctx.Get<PluginMetadata>()?.Id ?? "";
        return dls.GetPluginDataDirectory(pluginId);
    }

    /// <summary>本插件专属数据根目录，并确保该目录已存在（落盘前调用，免去各插件自建目录）。</summary>
    public static string EnsurePluginDataDirectory(this IContext ctx)
    {
        var dls = ctx.Get<IDataLocationService>()
            ?? throw new InvalidOperationException("IDataLocationService 未注册到插件上下文");
        var pluginId = ctx.Get<PluginMetadata>()?.Id ?? "";
        return dls.EnsurePluginDataDirectory(pluginId);
    }
}

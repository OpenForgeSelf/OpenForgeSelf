using System.Collections.Concurrent;
using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Backend.Services;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins;

/// <summary>
/// 扩展点管理器，负责扩展点的注册、发现和收集
/// </summary>
public class ExtensionPointManager
{
    private readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, IExtensionPoint>> _extensionPoints = new();
    private readonly PluginManager _pluginManager;
    private readonly IToolRegistry? _toolRegistry;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="pluginManager">插件管理器</param>
    /// <param name="toolRegistry">全局工具注册表（可选，用于把工具函数扩展点桥接给 AI Agent / MCP）</param>
    public ExtensionPointManager(PluginManager pluginManager, IToolRegistry? toolRegistry = null)
    {
        _pluginManager = pluginManager;
        _toolRegistry = toolRegistry;
    }

    /// <summary>
    /// 注册扩展点
    /// </summary>
    /// <typeparam name="TExtension">扩展点类型</typeparam>
    /// <param name="extension">扩展点实例</param>
    /// <returns>是否注册成功</returns>
    public bool RegisterExtension<TExtension>(TExtension extension)
        where TExtension : class, IExtensionPoint
    {
        if (extension == null)
        {
            XTrace.Log.Error("注册扩展点失败：扩展点实例为空");
            return false;
        }

        try
        {
            var type = typeof(TExtension);
            var extensions = _extensionPoints.GetOrAdd(type, _ => new ConcurrentDictionary<string, IExtensionPoint>());

            if (extensions.TryAdd(extension.Id, extension))
            {
                XTrace.Log.Debug("注册扩展点成功: [{0}] {1} - {2}", type.Name, extension.Id, extension.Name);
                return true;
            }
            else
            {
                XTrace.Log.Warn("扩展点已存在，注册失败: [{0}] {1}", type.Name, extension.Id);
                return false;
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("注册扩展点异常: {0}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 注销扩展点
    /// </summary>
    /// <typeparam name="TExtension">扩展点类型</typeparam>
    /// <param name="extensionId">扩展点ID</param>
    /// <returns>是否注销成功</returns>
    public bool UnregisterExtension<TExtension>(string extensionId)
        where TExtension : class, IExtensionPoint
    {
        try
        {
            var type = typeof(TExtension);
            if (_extensionPoints.TryGetValue(type, out var extensions))
            {
                if (extensions.TryRemove(extensionId, out _))
                {
                    XTrace.Log.Debug("注销扩展点成功: [{0}] {1}", type.Name, extensionId);
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("注销扩展点异常: {0}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 获取指定类型的所有扩展点
    /// </summary>
    /// <typeparam name="TExtension">扩展点类型</typeparam>
    /// <returns>扩展点列表</returns>
    public IEnumerable<TExtension> GetExtensions<TExtension>()
        where TExtension : class, IExtensionPoint
    {
        var type = typeof(TExtension);
        if (_extensionPoints.TryGetValue(type, out var extensions))
        {
            return extensions.Values.Cast<TExtension>();
        }

        return Enumerable.Empty<TExtension>();
    }

    /// <summary>
    /// 获取指定类型的指定扩展点
    /// </summary>
    /// <typeparam name="TExtension">扩展点类型</typeparam>
    /// <param name="extensionId">扩展点ID</param>
    /// <returns>扩展点实例</returns>
    public TExtension? GetExtension<TExtension>(string extensionId)
        where TExtension : class, IExtensionPoint
    {
        var type = typeof(TExtension);
        if (_extensionPoints.TryGetValue(type, out var extensions))
        {
            if (extensions.TryGetValue(extensionId, out var extension))
            {
                return extension as TExtension;
            }
        }

        return null;
    }

    /// <summary>
    /// 从指定插件中发现并注册扩展点
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    public void DiscoverExtensionsFromPlugin(string pluginId)
    {
        try
        {
            var plugin = _pluginManager.GetPlugin(pluginId);
            if (plugin == null)
            {
                XTrace.Log.Warn("插件不存在，无法发现扩展点: {0}", pluginId);
                return;
            }

            XTrace.Log.Info("开始从插件发现扩展点: {0}", pluginId);

            var pluginType = plugin.GetType();
            var properties = pluginType.GetProperties()
                .Where(p => typeof(IEnumerable<IExtensionPoint>).IsAssignableFrom(p.PropertyType));

            foreach (var property in properties)
            {
                try
                {
                    var extensions = property.GetValue(plugin) as IEnumerable<IExtensionPoint>;
                    if (extensions != null)
                    {
                        foreach (var extension in extensions)
                        {
                            var extensionType = extension.GetType();
                            var interfaces = extensionType.GetInterfaces()
                                .Where(i => i != typeof(IExtensionPoint) && typeof(IExtensionPoint).IsAssignableFrom(i));

                        foreach (var iface in interfaces)
                        {
                            RegisterExtensionInternal(iface, extension);
                        }

                        // T032 修复：发现的工具函数扩展点同步注册到全局 ToolRegistry，使 AI Agent / MCP 可见
                        if (_toolRegistry != null && extension is IToolFunctionExtension toolFunction)
                        {
                            _toolRegistry.RegisterTool(toolFunction);
                        }
                        }
                    }
                }
                catch (Exception ex)
                {
                    XTrace.Log.Error("发现扩展点异常 [{0}.{1}]: {2}", pluginId, property.Name, ex.Message);
                }
            }

            XTrace.Log.Info("插件扩展点发现完成: {0}", pluginId);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("从插件发现扩展点失败 [{0}]: {1}", pluginId, ex.Message);
        }
    }

    /// <summary>
    /// 内部注册扩展点方法
    /// </summary>
    /// <param name="extensionType">扩展点类型</param>
    /// <param name="extension">扩展点实例</param>
    private void RegisterExtensionInternal(Type extensionType, IExtensionPoint extension)
    {
        try
        {
            var extensions = _extensionPoints.GetOrAdd(extensionType, _ => new ConcurrentDictionary<string, IExtensionPoint>());

            if (extensions.TryAdd(extension.Id, extension))
            {
                XTrace.Log.Debug("注册扩展点成功: [{0}] {1} - {2}", extensionType.Name, extension.Id, extension.Name);
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("注册扩展点异常: {0}", ex.Message);
        }
    }

    /// <summary>
    /// 移除指定插件的所有扩展点
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    public void RemovePluginExtensions(string pluginId)
    {
        try
        {
            XTrace.Log.Info("移除插件扩展点: {0}", pluginId);

            foreach (var kvp in _extensionPoints)
            {
                var extensionsToRemove = kvp.Value
                    .Where(e => e.Value.PluginId == pluginId)
                    .Select(e => e.Key)
                    .ToList();

                foreach (var extId in extensionsToRemove)
                {
                    kvp.Value.TryRemove(extId, out _);
                    // T032 修复：工具函数扩展点从全局 ToolRegistry 同步注销
                    if (_toolRegistry != null && kvp.Key == typeof(IToolFunctionExtension))
                    {
                        _toolRegistry.UnregisterTool(extId);
                    }
                    XTrace.Log.Debug("移除扩展点: [{0}] {1}", kvp.Key.Name, extId);
                }
            }

            XTrace.Log.Info("插件扩展点移除完成: {0}", pluginId);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("移除插件扩展点失败 [{0}]: {1}", pluginId, ex.Message);
        }
    }

    /// <summary>
    /// 获取所有已注册的扩展点类型
    /// </summary>
    /// <returns>扩展点类型列表</returns>
    public IEnumerable<Type> GetExtensionTypes()
    {
        return _extensionPoints.Keys;
    }

    /// <summary>
    /// 获取指定插件的所有扩展点
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>扩展点列表</returns>
    public IEnumerable<IExtensionPoint> GetPluginExtensions(string pluginId)
    {
        var result = new List<IExtensionPoint>();

        foreach (var kvp in _extensionPoints)
        {
            foreach (var extKvp in kvp.Value)
            {
                if (extKvp.Value.PluginId == pluginId)
                {
                    result.Add(extKvp.Value);
                }
            }
        }

        return result;
    }
}

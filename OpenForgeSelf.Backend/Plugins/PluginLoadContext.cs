using System.Reflection;
using System.Runtime.Loader;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins;

/// <summary>
/// 插件程序集加载上下文，用于插件隔离加载
/// </summary>
public class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _pluginPath;

    /// <summary>
    /// 插件名称
    /// </summary>
    public string PluginName { get; }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="pluginPath">插件目录路径</param>
    /// <param name="pluginName">插件名称</param>
    public PluginLoadContext(string pluginPath, string pluginName)
        : base(pluginName, isCollectible: true)
    {
        _pluginPath = pluginPath;
        _resolver = new AssemblyDependencyResolver(pluginPath);
        PluginName = pluginName;

        XTrace.Log.Debug("创建插件加载上下文: {0}", pluginName);
    }

    /// <summary>
    /// 加载程序集
    /// </summary>
    /// <param name="assemblyName">程序集名称</param>
    /// <returns>加载的程序集</returns>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            XTrace.Log.Debug("插件[{0}] 加载程序集: {1}", PluginName, assemblyName.Name);
            return LoadFromAssemblyPath(assemblyPath);
        }

        return null;
    }

    /// <summary>
    /// 加载非托管库
    /// </summary>
    /// <param name="unmanagedDllName">非托管库名称</param>
    /// <returns>非托管库句柄</returns>
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            XTrace.Log.Debug("插件[{0}] 加载非托管库: {1}", PluginName, unmanagedDllName);
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// 从指定路径加载程序集
    /// </summary>
    /// <param name="assemblyPath">程序集路径</param>
    /// <returns>加载的程序集</returns>
    public new Assembly LoadFromAssemblyPath(string assemblyPath)
    {
        return base.LoadFromAssemblyPath(assemblyPath);
    }
}

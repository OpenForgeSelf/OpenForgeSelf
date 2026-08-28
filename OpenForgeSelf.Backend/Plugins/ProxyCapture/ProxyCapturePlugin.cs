using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Core;
using NewLife.Log;
using OpenForgeSelf.Backend.Plugins.ProxyCapture.Core;

namespace OpenForgeSelf.Backend.Plugins.ProxyCapture;

/// <summary>
/// 抓包代理插件入口。负责初始化数据库/CA、启动已启用监听器、注册菜单，并在卸载时停止监听。
/// </summary>
public class ProxyCapturePlugin : IPlugin
{
    public List<IMenuExtension> MenuExtensions { get; private set; } = new();

    public void Apply(IContext ctx)
    {
        var pluginId = ctx.Get<PluginMetadata>()?.Id ?? "proxycapture.plugin";
        XTrace.Log.Info("[ProxyCapture] 初始化抓包代理插件");

        // 初始化数据库 + 启动已启用监听器（引擎为静态单例）
        CaptureEngine.SetDataDirectory(ctx.GetPluginDataDirectory());
        CaptureEngine.Instance.StartAll();

        RegisterMenuExtensions(pluginId);

        // 插件卸载时停止所有监听器
        ctx.Effect(() =>
        {
            CaptureEngine.Instance.StopAll();
            return new NullDisposable();
        });

        XTrace.Log.Info("[ProxyCapture] 抓包代理插件初始化完成");
    }

    private void RegisterMenuExtensions(string pluginId)
    {
        MenuExtensions.Add(new ProxyCaptureMenuExtension
        {
            Id = "proxycapture.menu.main",
            Name = "抓包代理",
            PluginId = pluginId,
            Icon = "fa-network-wired",
            Path = "/capture",
            Order = 220,
            ParentId = null
        });
    }
}

/// <summary>无操作清理对象，供 ctx.Effect 返回。</summary>
public class NullDisposable : IDisposable
{
    public void Dispose()
    {
    }
}

public class ProxyCaptureMenuExtension : IMenuExtension
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

using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.Home;

/// <summary>首页插件：view-only 全量插件化（Hero/常用功能/核心看板/待办与活动/系统监控）。
/// 无独立后端服务——首页所需的 workflow/scripts/skills/monitor/todos 数据均经宿主既有接口获取。
/// 仅贡献一个侧边栏「首页」菜单扩展点（Path 指向 manifest 直路径 /home，与宿主 `/` 重定向目标一致）。</summary>
public class HomePlugin : IPlugin
{
    public List<IMenuExtension> MenuExtensions { get; } = new();
    public List<IToolFunctionExtension> ToolExtensions { get; } = new();

    public void Apply(IContext ctx)
    {
        XTrace.Log.Info("[HomePlugin] 初始化首页插件");

        var pluginId = ctx.Get<PluginMetadata>()?.Id ?? "home";
        RegisterMenuExtensions(pluginId);

        XTrace.Log.Info("[HomePlugin] 首页插件初始化完成");
    }

    private void RegisterMenuExtensions(string pluginId)
    {
        // Path 直接指向 manifest 注册的真实路由 /home（非 /plugin/home 占位），
        // 与宿主 router `/` redirect 解析到的 frontend.route 保持一致，点击侧边栏即打开真实首页视图。
        MenuExtensions.Add(new HomeMenuExtension
        {
            Id = "home.menu.main",
            Name = "首页",
            PluginId = pluginId,
            Icon = "fa-solid fa-house",
            Path = "/home",
            Order = 1,
            ParentId = null
        });
    }
}

public class HomeMenuExtension : IMenuExtension
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

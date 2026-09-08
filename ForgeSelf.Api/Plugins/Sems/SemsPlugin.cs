using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using ForgeSelf.Api.Plugins.Sems.Services;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.Sems;

public class SemsPlugin : IPlugin
{
    public List<IMenuExtension> MenuExtensions { get; } = new();
    public List<IToolFunctionExtension> ToolExtensions { get; } = new();

    public void Apply(IContext ctx)
    {
        var pluginId = ctx.Get<PluginMetadata>()?.Id ?? "";
        XTrace.Log.Info("[SemsPlugin] 初始化软件工程管理系统插件");

        var services = ctx.Get<IServiceCollection>();
        services?.AddScoped<IProjectService, ProjectService>();
        // 运行管理（进程启停/检测）为内存会话单例（宿主重启清空，NFR-3）
        services?.AddSingleton<IRunnerService, RunnerService>();

        RegisterMenuExtensions(pluginId);

        XTrace.Log.Info("[SemsPlugin] 初始化完成");
    }

    private void RegisterMenuExtensions(string pluginId)
    {
        MenuExtensions.Add(new SemsMenuExtension
        {
            Id = "sems.menu.main",
            Name = "软件工程管理",
            PluginId = pluginId,
            Icon = "fa-diagram-project",
            Path = "/sems",
            Order = 150,
            ParentId = null
        });

        XTrace.Log.Debug("[SemsPlugin] 已注册菜单扩展点");
    }
}

public class SemsMenuExtension : IMenuExtension
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
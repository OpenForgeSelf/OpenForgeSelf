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
        RegisterToolExtensions(pluginId, ctx);

        XTrace.Log.Info("[SemsPlugin] 初始化完成");
    }

    /// <summary>
    /// 注册 sems 对外能力工具（13 个 <c>sems_*</c>）：宿主 ExtensionPointManager 反射收集本列表 →
    /// 写入宿主 IToolRegistry → 经 mcp-center 的 list_tools 枚举、universal_tool 转发调用。
    /// 工具一律委托插件服务层（IProjectService / IRunnerService），与自带界面的 HTTP 端点共用同一实现。
    /// </summary>
    private void RegisterToolExtensions(string pluginId, IServiceProvider services)
    {
        ToolExtensions.Add(new SemsListProjectsToolFunction(pluginId, services));
        ToolExtensions.Add(new SemsRegisterProjectToolFunction(pluginId, services));
        ToolExtensions.Add(new SemsUpdateProjectToolFunction(pluginId, services));
        ToolExtensions.Add(new SemsRemoveProjectToolFunction(pluginId, services));
        ToolExtensions.Add(new SemsListCommandsToolFunction(pluginId, services));
        ToolExtensions.Add(new SemsAddCommandToolFunction(pluginId, services));
        ToolExtensions.Add(new SemsUpdateCommandToolFunction(pluginId, services));
        ToolExtensions.Add(new SemsDeleteCommandToolFunction(pluginId, services));
        ToolExtensions.Add(new SemsListRunsToolFunction(pluginId, services));
        ToolExtensions.Add(new SemsCheckRunsToolFunction(pluginId, services));
        ToolExtensions.Add(new SemsRunCommandToolFunction(pluginId, services));
        ToolExtensions.Add(new SemsStopCommandToolFunction(pluginId, services));
        ToolExtensions.Add(new SemsStopRunToolFunction(pluginId, services));

        XTrace.Log.Debug("[SemsPlugin] 已注册 {0} 个对外工具（sems_*）", ToolExtensions.Count);
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
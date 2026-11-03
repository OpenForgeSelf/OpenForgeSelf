using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using ForgeSelf.Api.Plugins.SmartTerminal.Services;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.SmartTerminal;

/// <summary>
/// 智能终端插件：会话化终端 + 三级权限 + JSONL 审计 + Agent 工具（st_*）。
/// </summary>
public class SmartTerminalPlugin : IPlugin
{
    public List<IMenuExtension> MenuExtensions { get; } = new();
    public List<IToolFunctionExtension> ToolExtensions { get; } = new();

    public void Apply(IContext ctx)
    {
        var pluginId = ctx.Get<PluginMetadata>()?.Id ?? "smart-terminal";
        XTrace.Log.Info("[SmartTerminalPlugin] 初始化智能终端插件 (Id={0})", pluginId);

        string dataDir;
        try
        {
            dataDir = ctx.EnsurePluginDataDirectory();
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[SmartTerminalPlugin] EnsurePluginDataDirectory 失败，回落临时目录: {0}", ex.Message);
            dataDir = Path.Combine(Path.GetTempPath(), "ForgeSelf", "Plugins", pluginId);
            Directory.CreateDirectory(dataDir);
        }

        var services = ctx.Get<IServiceCollection>();
        services?.AddSingleton<IShellProfileDetector, ShellProfileDetector>();
        services?.AddSingleton<IPermissionClassifier>(_ => new PermissionClassifier(dataDir));
        services?.AddSingleton<IAuditLogStore>(_ => new AuditLogStore(dataDir));
        services?.AddSingleton<ITerminalSessionService, TerminalSessionService>();

        RegisterMenu(pluginId);
        RegisterTools(pluginId, ctx);

        XTrace.Log.Info("[SmartTerminalPlugin] 初始化完成；默认权限模式 AskWrite；数据目录 {0}", dataDir);
    }

    private void RegisterMenu(string pluginId)
    {
        MenuExtensions.Add(new SmartTerminalMenuExtension
        {
            Id = "smart-terminal.menu.main",
            Name = "智能终端",
            PluginId = pluginId,
            Icon = "fa-terminal",
            Path = "/smart-terminal",
            Order = 55,
            ParentId = null
        });
    }

    private void RegisterTools(string pluginId, IServiceProvider services)
    {
        ToolExtensions.Add(new StListShellsToolFunction(pluginId, services));
        ToolExtensions.Add(new StCreateSessionToolFunction(pluginId, services));
        ToolExtensions.Add(new StRunCommandToolFunction(pluginId, services));
        ToolExtensions.Add(new StGetOutputToolFunction(pluginId, services));
        ToolExtensions.Add(new StListSessionsToolFunction(pluginId, services));
        ToolExtensions.Add(new StSetPermissionModeToolFunction(pluginId, services));
        ToolExtensions.Add(new StQueryAuditToolFunction(pluginId, services));
        ToolExtensions.Add(new StResolveApprovalToolFunction(pluginId, services));
        XTrace.Log.Debug("[SmartTerminalPlugin] 已注册 {0} 个 st_* 工具", ToolExtensions.Count);
    }
}

public class SmartTerminalMenuExtension : IMenuExtension
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

using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AgentHub.Entities;
using ForgeSelf.Api.Plugins.AgentHub.Profiles;
using ForgeSelf.Api.Plugins.AgentHub.Services;
using ForgeSelf.Api.Plugins.AgentHub.Tools;
using ForgeSelf.Core;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AgentHub;

/// <summary>
/// Agent 中枢插件：外部 Agent 注册表 + 统一委派总线。
/// CLI 只是交互口之一，ACP / 长驻 HTTP 服务 / SDK 同为一级公民。
/// </summary>
public class AgentHubPlugin : IPlugin
{
    public List<IMenuExtension> MenuExtensions { get; private set; } = new();
    public List<IToolFunctionExtension> ToolExtensions { get; private set; } = new();

    /// <summary>插件 ID（供 transport 等内部组件取 profile 用）</summary>
    public const String PluginIdConst = "agent-hub";

    public void Apply(IContext ctx)
    {
        var pluginId = ctx.Get<PluginMetadata>()?.Id ?? "";
        XTrace.Log.Info("初始化 Agent 中枢插件");

        var services = ctx.Get<IServiceCollection>();
        if (services != null)
        {
            // 插件数据目录（{数据根}/Plugins/agent-hub/）：设置等随数据走的文件放这里，发布覆盖不影响
            var dataDir = ctx.EnsurePluginDataDirectory();
            RegisterServices(services, dataDir);
        }

        RegisterMenuExtensions(pluginId);
        RegisterTools(pluginId, ctx);

        // 确保表已创建（宿主 XCodeConfig 会经反射统一建表，这里触发实体元数据加载）
        EnsureTablesCreated();

        // G5：宿主启动即收尾——把库里遗留的非终态任务标为 Interrupted
        RecoverOrphanTasks(ctx);

        XTrace.Log.Info("Agent 中枢插件初始化完成");
    }

    /// <summary>注册插件服务</summary>
    /// <param name="dataDir">插件数据目录（设置文件等随数据走）</param>
    private static void RegisterServices(IServiceCollection services, String dataDir)
    {
        // profile 加载器：进程级单例（读文件 + 缓存）
        services.AddSingleton<ProfileLoader>();

        // 注册表与权限中枢：单例（内部维护跨任务状态）
        services.AddSingleton<IAgentRegistry, AgentRegistry>();
        services.AddSingleton<PermissionBroker>();

        // 插件设置：config.json 持久化（附加扫描目录），进程级单例，探测服务消费
        services.AddSingleton(new AgentHubSettingsStore(Path.Combine(dataDir, "config.json")));

        // 探测服务：单例（无状态）
        services.AddSingleton<IAgentProbeService, AgentProbeService>();

        // transport：单例（内部用静态表跟踪运行中进程，供取消）
        services.AddSingleton<IAgentTransport, CliTransport>();

        // 运行时会话状态跨请求（工具 + SSE 都要看），必须单例
        services.AddSingleton<DelegationRuntime>();

        XTrace.Log.Debug("Agent 中枢插件已注册服务：ProfileLoader / AgentRegistry / PermissionBroker / AgentProbeService / CliTransport / DelegationRuntime / AgentHubSettingsStore");
    }

    /// <summary>注册 AI 工具扩展（供 AIAgent 调用）</summary>
    private void RegisterTools(String pluginId, IServiceProvider services)
    {
        ToolExtensions.Add(new ListAgentsTool(pluginId, services));
        ToolExtensions.Add(new DelegateTaskTool(pluginId, services));
        ToolExtensions.Add(new GetTaskResultTool(pluginId, services));
        ToolExtensions.Add(new CancelTaskTool(pluginId, services));
        ToolExtensions.Add(new AskAgentTool(pluginId, services));

        XTrace.Log.Debug("Agent 中枢插件已注册 {0} 个工具函数", ToolExtensions.Count);
    }

    /// <summary>宿主启动收尾：遗留任务标记 Interrupted（G5）</summary>
    private static void RecoverOrphanTasks(IServiceProvider services)
    {
        try
        {
            var runtime = services.GetService<DelegationRuntime>();
            runtime?.RecoverOrphans();
        }
        catch (Exception ex)
        {
            // 收尾失败不应阻断宿主启动（表可能尚未建好）
            XTrace.Log.Warn("Agent 中枢插件启动收尾失败（不影响宿主启动）: {0}", ex.Message);
        }
    }

    private void RegisterMenuExtensions(string pluginId)
    {
        MenuExtensions.Add(new AgentHubMenuExtension
        {
            Id = "agent-hub.menu.main",
            Name = "Agent 中枢",
            PluginId = pluginId,
            Icon = "fa-sitemap",
            Path = "/agent-hub",
            Order = 260,
            ParentId = null
        });

        XTrace.Log.Debug("Agent 中枢插件已注册菜单扩展点");
    }

    /// <summary>
    /// 确保 AgentHub 的库与表就绪。
    /// 实现见 <see cref="Data.AgentHubTables.EnsureCreated"/>（单一真源，测试侧共用）——
    /// 插件 Apply 早于宿主建库，必须自行触发一次连接让 XCode 完成建表，详见该类注释。
    /// </summary>
    private static void EnsureTablesCreated()
    {
        if (!Data.AgentHubTables.EnsureCreated())
            XTrace.Log.Warn("Agent 中枢插件数据库初始化未成功，委派功能可能不可用");
    }
}

/// <summary>Agent 中枢菜单扩展。</summary>
public class AgentHubMenuExtension : IMenuExtension
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

using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using ForgeSelf.Api.Plugins.McpCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.McpCenter;

/// <summary>
/// MCP 中心插件（v2.1.0，前身 mcp-gateway v1.0.0 更名 + 整合宿主 mcp-tools）：
/// ① 对外 MCP 服务端——暴露独立 MCP 端口，对外仅 1 个万能工具（universal_tool），
///    入参 {tool, parameters} 经宿主 IToolRegistry 转发调用本项目全部工具；
/// ② 外部 MCP 客户端（v2.1.0）——按标准 MCP 协议（stdio / Streamable HTTP / 旧版 HTTP+SSE 全传输格式）
///    连接外部 MCP 服务器，外部工具经 universal_tool 的 mcp.<服务器id>.<工具名> 命名空间统一转发；
/// ③ 外部 MCP 服务器/工具管理（迁自宿主 mcp-tools：McpController/api/mcp + McpService）；
/// ④ 网关配置 API（api/mcp-center/config：查看/修改地址·端口·令牌，热重启内置服务器）。
/// 生命周期自管理（铁律 14）：Apply 内幂等启动自托管 Kestrel + 外部连接管理器；ctx.Effect 注册停止器，
/// 插件卸载/热重载时 Fiber 逆序释放先停服务器/断外部连接再卸载程序集。
/// </summary>
public class McpCenterPlugin : IPlugin
{
    public const string ServerName = "ForgeSelf McpCenter";

    /// <summary>
    /// 工具函数扩展点（v2.1.0 网关能力）：list_tools 枚举工具。
    /// 属性暴露 = 宿主 ExtensionPointManager.DiscoverExtensionsFromPlugin 自动发现并注册进
    /// 宿主 ToolRegistry（装配期宿主级 registry 可用；勿在 Apply 里手动 RegisterTool，
    /// 装配期 ctx.Get&lt;IToolRegistry&gt;() 为 null）；热重载时自动注销，无需手动清理。
    /// </summary>
    public List<IToolFunctionExtension> ToolExtensions { get; } = new();

    private McpGatewayServer? _server;

    public void Apply(IContext ctx)
    {
        var pluginId = ctx.Get<PluginMetadata>()?.Id ?? "";
        XTrace.Log.Info("[McpCenter] 初始化 MCP 中心插件（id={0}）", pluginId);

        try
        {
            var dataDir = ctx.EnsurePluginDataDirectory();
            var config = McpGatewayConfig.Load(dataDir);
            var version = (typeof(McpCenterPlugin).Assembly.GetName().Version?.ToString(3)) ?? "2.0.0";

            // v2.1.0 网关能力：工具清单枚举工具（list_tools）——供 universal_tool 转发「发现工具」。
            // 工具实例经 ToolExtensions 属性由宿主自动注册进 ToolRegistry（见属性注释）。
            ToolExtensions.Add(new ListToolsToolFunction(ctx));

            // 软依赖：宿主 IToolRegistry（拿不到则 McpService 降级为仅预置数据，网关照常启动）
            var registry = ctx.Get<IToolRegistry>();
            var mcpService = new McpService(registry);

            // v2.1.0 外部 MCP 客户端：配置存储 + 连接管理器（转发器路由 mcp.<id>.<tool> 依赖）
            var externalStore = new ExternalServersStore(dataDir);
            var clientManager = new McpClientManager(externalStore);

            var forwarder = new UniversalToolForwarder(ctx, clientManager);
            var handler = new McpJsonRpcHandler(forwarder, ServerName, version);
            var server = new McpGatewayServer(config, handler, version);
            _server = server;

            // 网关配置运行时单例：供配置 API 查询/更新/热重启
            var runtime = new McpCenterRuntime(dataDir, config, server, version);

            // 注册进插件子 provider：插件控制器（MCP 工具管理 / 网关配置 / 外部服务器）经 PluginAwareControllerActivator 解析
            var services = ctx.Get<IServiceCollection>();
            services?.AddSingleton<IMcpService>(mcpService);
            services?.AddSingleton(runtime);
            services?.AddSingleton(externalStore);
            services?.AddSingleton(clientManager);

            // 启动（幂等；内部 catch 所有异常，失败仅降级不阻塞宿主）+ 外部服务器按 enabled 建连
            _ = server.StartAsync();
            clientManager.EnsureStarted();

            // 停止器：插件 Fiber 逆序回滚（热重载/卸载）时先停服务器、断外部连接，再卸载 ALC
            ctx.Effect(() => new McpCenterStopDisposable(server, clientManager));

            XTrace.Log.Info("[McpCenter] MCP 中心插件初始化完成（{0}，协议版本 {1}，对外工具数 1，外部服务器 {2} 台，服务器/工具管理已就绪）",
                server.ListenUrl, McpJsonRpcHandler.ProtocolVersion, externalStore.Load().Count);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[McpCenter] MCP 中心插件初始化失败（网关降级为不可用，宿主其余功能不受影响）: {0}", ex.Message);
        }
    }
}

/// <summary>把服务器停止 + 外部连接断开挂到 ctx.Effect（IDisposable 契约）的适配器。</summary>
internal sealed class McpCenterStopDisposable : IDisposable
{
    private readonly McpGatewayServer _server;
    private readonly McpClientManager _clientManager;

    public McpCenterStopDisposable(McpGatewayServer server, McpClientManager clientManager)
    {
        _server = server;
        _clientManager = clientManager;
    }

    public void Dispose()
    {
        try
        {
            _server.StopAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[McpCenter] 停止 MCP 网关异常: {0}", ex.Message);
        }
        try
        {
            _clientManager.StopAllAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[McpCenter] 断开外部 MCP 连接异常: {0}", ex.Message);
        }
    }
}

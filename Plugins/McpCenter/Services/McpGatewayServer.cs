using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.McpCenter.Services;

/// <summary>
/// MCP 网关服务器（自管理 Kestrel，独立于宿主端口）。
/// 传输层与协议协商交由官方 MCP C# SDK（ModelContextProtocol.AspNetCore 2.2.0）托管；
/// 对外工具面**仍只有 1 个** universal_tool（见 <see cref="McpUniversalTool"/>），
/// 业务转发语义由 <see cref="UniversalToolForwarder"/> 承担，本类不复制任何一份。
/// 端点：/mcp（SDK MapMcp：POST JSON-RPC + GET SSE）、GET /health（运维探活，不鉴权）。
/// 会话模式（v2.4.0 可配置）：<c>config.Stateless=true</c>（默认）每请求独立、不发 session id，重启/切配置不断会话；
/// <c>false</c> 维持有状态（initialize 下发 Mcp-Session-Id，2026-10-08 旧拍板保留为可选项；宿主重启后旧 session 失效属规范行为，
/// 客户端须按 spec 重新 initialize，见 034-mcp-center.md「有状态会话语义」）。
/// 生命周期自管理（铁律 14）：StartAsync 幂等启动；StopAsync 停止并释放；插件 ctx.Effect 注册停止器。
/// 令牌：配置了 token 时 /mcp 要求 Authorization: Bearer &lt;token&gt;，否则 401；/health 永不鉴权。
/// </summary>
public sealed class McpGatewayServer : IAsyncDisposable
{
    private const int MaxRequestBodyBytes = 1024 * 1024; // 请求体上限 1 MB（改用 SDK 后经中间件保留该守卫）

    private readonly McpGatewayConfig _config;
    private readonly UniversalToolForwarder _forwarder;
    private readonly string _pluginVersion;

    private WebApplication? _app;
    private readonly object _sync = new();
    private bool _started;

    public McpGatewayServer(McpGatewayConfig config, UniversalToolForwarder forwarder, string pluginVersion)
    {
        _config = config;
        _forwarder = forwarder;
        _pluginVersion = pluginVersion;
    }

    public bool IsRunning => _started;
    public string ListenUrl => _config.ListenUrl;

    /// <summary>启动监听（幂等：已启动则跳过；启动失败记录日志，不向上抛，插件降级但不阻塞宿主）。</summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (_started)
            {
                return;
            }
        }

        try
        {
            var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = Array.Empty<string>() });
            builder.Logging.ClearProviders(); // 网关日志走 XTrace，避免宿主控制台重复输出
            builder.WebHost.UseUrls(_config.ListenUrl);

            // 工具外壳及其依赖的转发器交给 DI：SDK 的 WithTools<McpUniversalTool>() 由容器构造实例
            builder.Services.AddSingleton(_forwarder);
            builder.Services.AddSingleton<McpUniversalTool>();
            builder.Services
                .AddMcpServer(o =>
                {
                    // 034 契约：serverInfo = 「ForgeSelf McpCenter」+ 插件版本 + 描述（SDK 化后默认值曾是宿主程序集名/构建号，属契约回归）
                    o.ServerInfo = new ModelContextProtocol.Protocol.Implementation
                    {
                        Name = McpCenterPlugin.ServerName,
                        Version = _pluginVersion,
                        Description = "OpenForgeSelf MCP Center（万能工具网关，支持 MCP 2.0）"
                    };
                    // 注：tools 能力的 listChanged 旗标由 SDK 自动推导（有状态=true、无状态=缺键），o.Capabilities 显式覆写实测无效；
                    // 工具面恒 1 个 universal_tool（静态），实际从不发 list_changed——契约实况见 034-mcp-center.md
                })
                .WithHttpTransport(o => o.Stateless = _config.Stateless) // v2.4.0：会话模式入配置（默认无状态），热重启生效
                .WithTools<McpUniversalTool>();

            var app = builder.Build();

            // /mcp 的前置守卫：MapMcp 之后无法在 handler 内联，改由中间件覆盖全部 MCP 方法
            app.Use(async (context, next) =>
            {
                var isMcp = context.Request.Path.StartsWithSegments("/mcp");
                if (isMcp && !IsAuthorized(context.Request))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                // 保留 1 MB 请求体上限（原 HandlePostAsync 行为；改用 SDK 后经此中间件维持）
                if (isMcp && context.Request.ContentLength > MaxRequestBodyBytes)
                {
                    context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                    return;
                }

                await next(context);
            });

            app.MapMcp("/mcp");
            app.MapGet("/health", HandleHealthAsync);

            await app.StartAsync(cancellationToken);

            lock (_sync)
            {
                _app = app;
                _started = true;
            }

            XTrace.Log.Info("[McpCenter] MCP 网关已启动: {0}（官方 SDK 2.2.0 · 会话模式：{1} · 工具数 1）", _config.ListenUrl, _config.Stateless ? "无状态" : "有状态");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[McpCenter] MCP 网关启动失败（{0}），网关降级为不可用，宿主其余功能不受影响: {1}", _config.ListenUrl, ex.Message);
        }
    }

    /// <summary>停止并释放（可安全重复调用）。</summary>
    public async Task StopAsync()
    {
        WebApplication? app;
        lock (_sync)
        {
            app = _app;
            _app = null;
            _started = false;
        }

        if (app != null)
        {
            try
            {
                await app.StopAsync(TimeSpan.FromSeconds(3));
                await app.DisposeAsync();
                XTrace.Log.Info("[McpCenter] MCP 网关已停止: {0}", _config.ListenUrl);
            }
            catch (Exception ex)
            {
                XTrace.Log.Warn("[McpCenter] MCP 网关停止异常: {0}", ex.Message);
            }
        }
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    // ---------- 端点处理 ----------

    private bool IsAuthorized(HttpRequest request)
    {
        if (string.IsNullOrEmpty(_config.Token))
        {
            return true;
        }

        var auth = request.Headers.Authorization.ToString();
        return auth.Equals($"Bearer {_config.Token}", StringComparison.Ordinal);
    }

    private Task HandleHealthAsync(HttpContext context)
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(
            $"{{\"status\":\"ok\",\"version\":\"{_pluginVersion}\",\"tools\":1,\"listen\":\"{_config.ListenUrl}\"}}",
            context.RequestAborted);
    }
}

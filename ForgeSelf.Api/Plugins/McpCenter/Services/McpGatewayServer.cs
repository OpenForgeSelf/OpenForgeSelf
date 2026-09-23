using System.Text;
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
/// 端点：POST /mcp（JSON-RPC 请求）、GET /mcp（服务器推送流，仅心跳）、GET /health（运维探活）。
/// 生命周期自管理（铁律 14）：StartAsync 幂等启动；StopAsync 停止并释放；插件 ctx.Effect 注册停止器。
/// 令牌：配置了 token 时 POST/GET /mcp 要求 Authorization: Bearer &lt;token&gt;，否则 401。
/// </summary>
public sealed class McpGatewayServer : IAsyncDisposable
{
    private const int MaxRequestBodyBytes = 1024 * 1024; // 请求体上限 1 MB

    private readonly McpGatewayConfig _config;
    private readonly McpJsonRpcHandler _handler;
    private readonly string _pluginVersion;

    private WebApplication? _app;
    private readonly object _sync = new();
    private bool _started;

    public McpGatewayServer(McpGatewayConfig config, McpJsonRpcHandler handler, string pluginVersion)
    {
        _config = config;
        _handler = handler;
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

            var app = builder.Build();

            app.MapPost("/mcp", HandlePostAsync);
            app.MapGet("/mcp", HandleGetAsync);
            app.MapGet("/health", HandleHealthAsync);

            await app.StartAsync(cancellationToken);

            lock (_sync)
            {
                _app = app;
                _started = true;
            }

            XTrace.Log.Info("[McpCenter] MCP 网关已启动: {0}（协议版本 2025-06-18，工具数 1）", _config.ListenUrl);
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

    private async Task HandlePostAsync(HttpContext context)
    {
        if (!IsAuthorized(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // 限流读取请求体（上限 1 MB），避免恶意超大请求
        string body;
        try
        {
            using var buffer = new MemoryStream();
            await context.Request.Body.CopyToAsync(buffer, MaxRequestBodyBytes + 1, context.RequestAborted);
            if (buffer.Length > MaxRequestBodyBytes)
            {
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                return;
            }
            body = Encoding.UTF8.GetString(buffer.ToArray());
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = StatusCodes.Status408RequestTimeout;
            return;
        }

        var response = await _handler.HandleRequestAsync(body, context.RequestAborted);
        if (response == null)
        {
            // 通知/空批：202 Accepted 空体
            context.Response.StatusCode = StatusCodes.Status202Accepted;
            return;
        }

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(response, context.RequestAborted);
    }

    private async Task HandleGetAsync(HttpContext context)
    {
        if (!IsAuthorized(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // Streamable HTTP：GET 必须要求 text/event-stream，否则 405
        var accept = context.Request.Headers.Accept.ToString();
        if (!accept.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";

        try
        {
            // 本网关无服务器主动消息（tools/call 同步完成），仅保持连接 + 心跳
            while (!context.RequestAborted.IsCancellationRequested)
            {
                await context.Response.WriteAsync(": keep-alive\n\n", context.RequestAborted);
                await context.Response.Body.FlushAsync(context.RequestAborted);
                await Task.Delay(TimeSpan.FromSeconds(15), context.RequestAborted);
            }
        }
        catch (OperationCanceledException)
        {
            // 客户端断开，正常结束
        }
    }

    private Task HandleHealthAsync(HttpContext context)
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(
            $"{{\"status\":\"ok\",\"version\":\"{_pluginVersion}\",\"tools\":1,\"listen\":\"{_config.ListenUrl}\"}}",
            context.RequestAborted);
    }
}

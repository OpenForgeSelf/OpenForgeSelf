using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.McpCenter.Controllers;
using ForgeSelf.Api.Plugins.McpCenter.Models;
using ForgeSelf.Api.Plugins.McpCenter.Services;
using ForgeSelf.Api.Plugins.McpCenter.Services.McpClient;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.McpCenterTests;

/// <summary>
/// 外部 MCP 客户端集成测试（v2.1.0）：真实 node mock MCP 服务器（本目录 Fixtures/mock-mcp-server.js），
/// 覆盖全部三传输格式（stdio / streamable-http / 旧版 http-sse）的真连往返 + 万能工具 mcp. 前缀全链路转发。
/// 环境依赖：node 在 PATH（本项目前端构建环境必备）。
/// </summary>
public class McpClientIntegrationTests
{
    private static readonly string MockScript = Path.Combine(
        AppContext.BaseDirectory, "plugins", "McpCenterTests", "Fixtures", "mock-mcp-server.js");

    private static string TempDataDir() =>
        Path.Combine(Path.GetTempPath(), "mcpcenter-integration-tests", Guid.NewGuid().ToString("N"));

    private static Process StartNode(string args)
    {
        Assert.True(File.Exists(MockScript), $"mock 脚本不存在: {MockScript}");
        var psi = new ProcessStartInfo
        {
            FileName = "node",
            Arguments = $"\"{MockScript}\" {args}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        var p = Process.Start(psi)
            ?? throw new InvalidOperationException("无法启动 node mock MCP 服务器（node 是否在 PATH？）");
        return p;
    }

    /// <summary>http 模式：从 stdout 读 LISTENING &lt;port&gt;。</summary>
    private static int WaitListeningPort(Process p, int timeoutMs = 10000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (p.StandardOutput.Peek() >= 0)
            {
                var line = p.StandardOutput.ReadLine();
                if (line != null && line.StartsWith("LISTENING "))
                {
                    return int.Parse(line["LISTENING ".Length..]);
                }
            }
            Thread.Sleep(50);
        }
        throw new TimeoutException("mock MCP 服务器未在超时内报告监听端口");
    }

    private static void StopNode(Process p)
    {
        try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch (Exception) { }
        p.Dispose();
    }

    private static async Task<McpClientSession> ConnectSession(IMcpClientTransport transport)
    {
        var session = new McpClientSession(Config(), transport);
        await session.ConnectAsync(CancellationToken.None);
        return session;
    }

    private static McpExternalServerConfig Config() => new()
    {
        Id = "mock",
        Name = "Mock",
        Transport = "streamable-http",
        Url = "http://127.0.0.1:1/mcp"
    };

    [Fact]
    public async Task StdioTransport_EchoAndAdd_RoundTrip()
    {
        var p = StartNode("--mode stdio");
        try
        {
            var cfg = Config();
            cfg.Transport = "stdio";
            cfg.Framing = "lsp"; // mock 说 LSP 帧；显式指定避开 auto 探测的 5s 回退等待（auto 路径由 McpStdioFramingTests 专门覆盖）
            cfg.Command = "node";
            // 裸路径参数（ArgumentList 自动引号转义；手工带引号会作为字面量传给 node）
            cfg.Args = new() { MockScript, "--mode", "stdio" };

            await using var session = await ConnectSession(new StdioMcpTransport(cfg));
            Assert.Equal(2, session.ToolCount);
            // v2.2.0：客户端与 mock 均声明支持 2025-11-25（MCP 2.0）→ 协商结果应为 2.0（此前 2025-06-18）
            Assert.Contains("2025-11-25", session.ProtocolVersion);
            Assert.Equal("MockMcpServer", session.ServerInfoName);

            var echo = await session.CallToolAsync("echo", """{"text":"你好"}""", CancellationToken.None);
            using var echoDoc = JsonDocument.Parse(echo);
            Assert.Equal("你好", echoDoc.RootElement.GetProperty("echo").GetString());

            var add = await session.CallToolAsync("add", """{"a":2,"b":3}""", CancellationToken.None);
            using var addDoc = JsonDocument.Parse(add);
            Assert.Equal(5, addDoc.RootElement.GetProperty("sum").GetInt32());
        }
        finally { StopNode(p); }
    }

    [Fact]
    public async Task StreamableHttpTransport_EchoAndAdd_RoundTrip()
    {
        var p = StartNode("--mode http --port 0");
        try
        {
            var port = WaitListeningPort(p);
            var transport = new StreamableHttpMcpTransport($"http://127.0.0.1:{port}/mcp", null);
            await using var session = await ConnectSession(transport);
            Assert.Equal(2, session.ToolCount);

            var add = await session.CallToolAsync("add", """{"a":10,"b":32}""", CancellationToken.None);
            using var doc = JsonDocument.Parse(add);
            Assert.Equal(42, doc.RootElement.GetProperty("sum").GetInt32());
        }
        finally { StopNode(p); }
    }

    [Fact]
    public async Task LegacySseTransport_EchoAndAdd_RoundTrip()
    {
        var p = StartNode("--mode sse --port 0");
        try
        {
            var port = WaitListeningPort(p);
            var transport = new LegacySseMcpTransport($"http://127.0.0.1:{port}/sse", null);
            await using var session = await ConnectSession(transport);
            Assert.Equal(2, session.ToolCount);

            var echo = await session.CallToolAsync("echo", """{"text":"legacy-sse"}""", CancellationToken.None);
            using var doc = JsonDocument.Parse(echo);
            Assert.Equal("legacy-sse", doc.RootElement.GetProperty("echo").GetString());
        }
        finally { StopNode(p); }
    }

    [Fact]
    public async Task UniversalTool_McpPrefix_ForwardsToExternalServer()
    {
        var p = StartNode("--mode stdio");
        try
        {
            // 配置 stdio 服务器并保存 → manager 建连
            var cfg = Config();
            cfg.Id = "mock";
            cfg.Transport = "stdio";
            cfg.Framing = "lsp"; // 同上：mock 固定 LSP 帧，快速路径
            cfg.Command = "node";
            cfg.Args = new() { MockScript, "--mode", "stdio" };
            var store = new ExternalServersStore(TempDataDir());
            store.Save(new List<McpExternalServerConfig> { cfg });

            var manager = new McpClientManager(store);
            await manager.TryConnectAsync("mock");

            // 万能工具转发器：mcp.mock.add → 外部会话
            var forwarder = new UniversalToolForwarder(new FakeContext(), manager);
            var result = await forwarder.ForwardAsync("""{"tool":"mcp.mock.add","parameters":{"a":7,"b":8}}""");

            Assert.False(result.IsError);
            using var doc = JsonDocument.Parse(result.Text);
            Assert.Equal(15, doc.RootElement.GetProperty("sum").GetInt32());

            await manager.StopAllAsync();
        }
        finally { StopNode(p); }
    }

    [Fact]
    public async Task UniversalTool_McpPrefix_NotConnected_ReturnsClearError()
    {
        var store = new ExternalServersStore(TempDataDir());
        store.Save(new List<McpExternalServerConfig>
        {
            new() { Id = "deepwiki", Name = "DeepWiki", Transport = "streamable-http", Url = "http://127.0.0.1:9/mcp", Enabled = false }
        });
        var manager = new McpClientManager(store);
        var forwarder = new UniversalToolForwarder(new FakeContext(), manager);

        var result = await forwarder.ForwardAsync("""{"tool":"mcp.deepwiki.search","parameters":{"query":"x"}}""");

        Assert.True(result.IsError);
        using var doc = JsonDocument.Parse(result.Text);
        Assert.Contains("未连接", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task UniversalTool_McpPrefix_BadFormat_ReturnsFormatError()
    {
        var store = new ExternalServersStore(TempDataDir());
        var manager = new McpClientManager(store);
        var forwarder = new UniversalToolForwarder(new FakeContext(), manager);

        var result = await forwarder.ForwardAsync("""{"tool":"mcp.deepwiki","parameters":{}}""");

        Assert.True(result.IsError);
        using var doc = JsonDocument.Parse(result.Text);
        Assert.Contains("mcp.<服务器id>.<工具名>", doc.RootElement.GetProperty("error").GetString());
    }

    /// <summary>
    /// POST {id}/test 端点契约守卫（2026-10-17 修复批门禁② e2e 暴露）：
    /// 前端 apiPost 只解包 data，handleTestExternal 读 data.success/data.message/data.durationMs；
    /// 旧实现 data 只有 { id, ok } ⇒ 握手成功也误报「测试失败」。成功/失败两分支都锁形状。
    /// </summary>
    [Fact]
    public async Task TestEndpoint_Success_DataCarriesSuccessMessageDurationMs()
    {
        var p = StartNode("--mode http --port 0");
        try
        {
            var port = WaitListeningPort(p);
            var store = new ExternalServersStore(TempDataDir());
            store.Save(new List<McpExternalServerConfig>
            {
                new() { Id = "mock", Name = "Mock", Transport = "streamable-http", Url = $"http://127.0.0.1:{port}/mcp", Enabled = true }
            });
            var manager = new McpClientManager(store);
            var controller = new McpExternalController(store, manager);

            var action = await controller.Test("mock");
            var ok = Assert.IsType<OkObjectResult>(action.Result);
            var env = Assert.IsType<ApiResponse<object>>(ok.Value);
            Assert.True(env.Success);

            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(env.Data));
            Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("message").GetString()));
            Assert.True(doc.RootElement.GetProperty("durationMs").GetInt64() >= 0);
        }
        finally { StopNode(p); }
    }

    [Fact]
    public async Task TestEndpoint_Failure_Returns400WithRealMessage()
    {
        var store = new ExternalServersStore(TempDataDir());
        store.Save(new List<McpExternalServerConfig>
        {
            new() { Id = "down", Name = "Down", Transport = "streamable-http", Url = "http://127.0.0.1:9/mcp", Enabled = true }
        });
        var manager = new McpClientManager(store);
        var controller = new McpExternalController(store, manager);

        var action = await controller.Test("down");
        var status = Assert.IsType<ObjectResult>(action.Result);
        Assert.Equal(400, status.StatusCode);
        var env = Assert.IsType<ApiResponse<object>>(status.Value);
        Assert.False(env.Success);
        Assert.False(string.IsNullOrWhiteSpace(env.Message));
    }

    /// <summary>转发器测试用最小 IContext（不触碰宿主注册表——mcp. 前缀路由不依赖）。</summary>
    private sealed class FakeContext : ForgeSelf.Core.IContext
    {
        public T? Get<T>() where T : class => null;
        public object? GetService(Type serviceType) => null;
        public void Register<TService>(TService instance) where TService : class { }
        public void Register<TService, TImpl>() where TService : class where TImpl : class, TService, new() { }
        public void RegisterLocal<TService>(TService instance) where TService : class { }
        public IDisposable Effect(Func<IDisposable> sideEffect) => sideEffect();
        public ForgeSelf.Core.IEventBus Events => throw new NotSupportedException();
        public ForgeSelf.Core.IContext Derive() => throw new NotSupportedException();
    }
}

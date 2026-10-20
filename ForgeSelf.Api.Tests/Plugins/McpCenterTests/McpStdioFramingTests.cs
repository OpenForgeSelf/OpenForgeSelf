using System.Text.Json;
using ForgeSelf.Api.Plugins.McpCenter.Models;
using ForgeSelf.Api.Plugins.McpCenter.Services.McpClient;
using Xunit.Abstractions;

namespace ForgeSelf.Api.Tests.Plugins.McpCenterTests;

/// <summary>
/// PC-010 第二层：stdio 消息帧型自适应（NDJSON ↔ LSP Content-Length）。
/// 现场实测（2026-10-18 探针）：官方 TS SDK 服务器（server-everything）**只答换行分隔 JSON**，
/// 对它发 Content-Length 帧无响应；而本项目既有 mock 与规范 SHOULD 形态是 LSP 帧——
/// 单一帧型客户端连不上生态多数实现（npx 系全中招，含用户报障的 @playwright/mcp）。
/// 策略：auto=NDJSON 首发 + 5s 无响应换 LSP 帧重发一次，写帧跟随对方响应帧型；framing 配置可强制。
/// </summary>
public sealed class McpStdioFramingTests
{
    private static readonly string MockScript = Path.Combine(
        AppContext.BaseDirectory, "plugins", "McpCenterTests", "Fixtures", "mock-mcp-server.js");

    private readonly ITestOutputHelper _out;
    public McpStdioFramingTests(ITestOutputHelper @out) => _out = @out;

    private static McpExternalServerConfig StdioConfig(string mode, string framing) => new()
    {
        Id = "framing-mock",
        Name = "FramingMock",
        Transport = "stdio",
        Command = "node",
        Args = new List<string> { MockScript, "--mode", mode },
        Framing = framing
    };

    private static async Task<(JsonElement Init, int ToolCount)> ConnectAndList(StdioMcpTransport transport)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var init = await transport.InitializeAsync(new[] { "2025-06-18" }, cts.Token);
        var tools = await transport.ListToolsAsync(cts.Token);
        return (init, tools.GetProperty("tools").GetArrayLength());
    }

    [Fact]
    public async Task Auto_Framing_NdjsonServer_HandshakesOnFirstProbe()
    {
        Assert.True(File.Exists(MockScript), $"mock 脚本不存在: {MockScript}");
        var transport = new StdioMcpTransport(StdioConfig("stdio-ndjson", ""));
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var (_, toolCount) = await ConnectAndList(transport);
            sw.Stop();
            _out.WriteLine($"NDJSON 服务器 auto 握手耗时 {sw.ElapsedMilliseconds}ms");
            Assert.Equal(2, toolCount);
            // 首探测即中：不应吃满 5s 探测超时
            Assert.True(sw.ElapsedMilliseconds < StdioMcpTransport.FramingProbeTimeoutMs,
                "auto 模式连 NDJSON 服务器应首帧命中，而非等待探测回退");
        }
        finally { await transport.CloseAsync(); }
    }

    [Fact]
    public async Task Auto_Framing_LspServer_FallsBackAndHandshakes()
    {
        var transport = new StdioMcpTransport(StdioConfig("stdio", ""));
        try
        {
            var (_, toolCount) = await ConnectAndList(transport);
            // LSP-only 服务器不认 NDJSON 探测行 → 探测超时后换 LSP 帧重发必须成功
            Assert.Equal(2, toolCount);
            var echo = await transport.CallToolAsync("echo", """{"text":"回退帧"}""", CancellationToken.None);
            Assert.Contains("回退帧", echo.ToString());
        }
        finally { await transport.CloseAsync(); }
    }

    [Theory]
    [InlineData("ndjson", "stdio-ndjson")]
    [InlineData("lsp", "stdio")]
    [InlineData("LSP", "stdio")] // 大小写/空白容错
    public async Task Explicit_Framing_ConnectsWithoutProbe(string framing, string mode)
    {
        var transport = new StdioMcpTransport(StdioConfig(mode, framing));
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var (_, toolCount) = await ConnectAndList(transport);
            sw.Stop();
            Assert.Equal(2, toolCount);
            // 强制帧型 = 零探测成本（既有集成测试快速路径的依据）
            Assert.True(sw.ElapsedMilliseconds < StdioMcpTransport.FramingProbeTimeoutMs);
        }
        finally { await transport.CloseAsync(); }
    }

    [Fact]
    public void ParseFraming_MapsValues()
    {
        Assert.Null(StdioMcpTransport.ParseFraming(""));
        Assert.Null(StdioMcpTransport.ParseFraming(null));
        Assert.Null(StdioMcpTransport.ParseFraming("garbage")); // 非法值回落 auto，不炸
        Assert.NotNull(StdioMcpTransport.ParseFraming("ndjson"));
        Assert.NotNull(StdioMcpTransport.ParseFraming(" lsp "));
    }
}

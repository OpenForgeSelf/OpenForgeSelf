using System.Diagnostics;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.McpCenter.Models;
using ForgeSelf.Api.Plugins.McpCenter.Services;
using ForgeSelf.Api.Plugins.McpCenter.Services.McpClient;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.McpCenterTests;

/// <summary>
/// IMcpExternalGateway 适配器测试（v2.6.0，PILOT-072）：薄适配层必须与网关转发器行为一致——
/// 枚举只出已连接服务器的会话缓存工具（全名 mcp.&lt;id&gt;.&lt;名&gt;），调用直调语义（未连接抛中文错误、
/// 坏格式抛格式错误，与 UniversalToolForwarder 三态错误文案同源）。
/// 真连部分复用本目录 node mock MCP 服务器（stdio + LSP 帧快速路径，同 McpClientIntegrationTests 范式）。
/// </summary>
public class McpExternalGatewayAdapterTests
{
    private static readonly string MockScript = Path.Combine(
        AppContext.BaseDirectory, "plugins", "McpCenterTests", "Fixtures", "mock-mcp-server.js");

    private static string TempDataDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mcpcenter-adapter-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir); // 显式建目录（日记 2026-10-19 踩坑：Store 不自动建）
        return dir;
    }

    private static Process StartNodeMockStdio()
    {
        Assert.True(File.Exists(MockScript), $"mock 脚本不存在: {MockScript}");
        var p = Process.Start(new ProcessStartInfo
        {
            FileName = "node",
            Arguments = $"\"{MockScript}\" --mode stdio",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("无法启动 node mock");
        return p;
    }

    private static McpExternalServerConfig StdioConfig() => new()
    {
        Id = "mock",
        Name = "Mock",
        Transport = "stdio",
        Framing = "lsp", // mock 固定 LSP 帧，避开 auto 探测 5s 回退（探测路径由 McpStdioFramingTests 覆盖）
        Command = "node",
        Args = new() { MockScript, "--mode", "stdio" }
    };

    [Fact]
    public async Task ConnectedServer_ListAndInvoke_ViaRealSession()
    {
        var p = StartNodeMockStdio();
        try
        {
            var store = new ExternalServersStore(TempDataDir());
            store.Save(new List<McpExternalServerConfig> { StdioConfig() });
            var manager = new McpClientManager(store);
            await manager.TryConnectAsync("mock");
            var gateway = new McpExternalGatewayAdapter(manager);

            var tools = gateway.ListConnectedTools();
            Assert.Equal(2, tools.Count); // mock 固定暴露 echo + add
            Assert.Contains(tools, t => t.FullName == "mcp.mock.echo" && t.ServerId == "mock" && t.Name == "echo");
            var add = tools.Single(t => t.FullName == "mcp.mock.add");
            Assert.False(string.IsNullOrWhiteSpace(add.Description));
            using (JsonDocument.Parse(add.InputSchemaJson)) { } // schema 必须是可解析 JSON（list_tools 要透传）

            var result = await gateway.InvokeAsync("mcp.mock.add", """{"a":7,"b":8}""");
            using var doc = JsonDocument.Parse(result);
            Assert.Equal(15, doc.RootElement.GetProperty("sum").GetInt32());

            await manager.StopAllAsync();
        }
        finally { try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch (Exception) { } p.Dispose(); }
    }

    [Fact]
    public void NotConnected_ListReturnsEmpty_NoThrow()
    {
        // 配置存在但未建连：发现入口宁缺勿崩（Spec BC-4 降级语义）
        var store = new ExternalServersStore(TempDataDir());
        store.Save(new List<McpExternalServerConfig>
        {
            new() { Id = "deepwiki", Name = "DeepWiki", Transport = "streamable-http", Url = "http://127.0.0.1:9/mcp", Enabled = true }
        });
        var gateway = new McpExternalGatewayAdapter(new McpClientManager(store));

        Assert.Empty(gateway.ListConnectedTools());
    }

    [Fact]
    public async Task NotConnected_Invoke_ThrowsChineseNotConnectedError()
    {
        var store = new ExternalServersStore(TempDataDir());
        var gateway = new McpExternalGatewayAdapter(new McpClientManager(store));

        var ex = await Assert.ThrowsAsync<McpClientException>(
            () => gateway.InvokeAsync("mcp.deepwiki.search", """{"query":"x"}"""));
        Assert.Contains("未连接", ex.Message);
    }

    [Theory]
    [InlineData("mcp.deepwiki")]          // 缺工具名
    [InlineData("deepwiki.search")]       // 无前缀
    [InlineData("")]                     // 空串
    public async Task BadFormat_Invoke_ThrowsFormatError(string fullName)
    {
        var store = new ExternalServersStore(TempDataDir());
        var gateway = new McpExternalGatewayAdapter(new McpClientManager(store));

        var ex = await Assert.ThrowsAsync<McpClientException>(
            () => gateway.InvokeAsync(fullName, "{}"));
        // 与网关转发器同文案真源（消费方错误封套直接透传 ex.Message，两通道措辞必须一致）
        Assert.Contains("mcp.<服务器id>.<工具名>", ex.Message);
    }
}

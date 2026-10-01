using System.Text.Json;
using ForgeSelf.Api.Plugins.McpCenter.Models;
using ForgeSelf.Api.Plugins.McpCenter.Services.McpClient;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.McpCenterTests;

/// <summary>
/// MCP 客户端会话测试（v2.1.0）：协议版本协商（服务端回旧版本客户端接受）、
/// tools/list 快照（含 mcp.&lt;id&gt;.&lt;name&gt; 全名）、tools/call 文本提取与 isError 包装、错误传播。
/// 策略：Moq 假 IMcpClientTransport（传输层细节在集成测试覆盖）。
/// </summary>
public class McpClientSessionTests
{
    private static McpExternalServerConfig Config(string id = "mock") => new()
    {
        Id = id,
        Name = "Mock",
        Transport = "streamable-http",
        Url = "http://127.0.0.1:1/mcp"
    };

    private static JsonElement Result(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    [Fact]
    public async Task Connect_ServerReturnsLegacyVersion_ClientAcceptsAndKeepsIt()
    {
        var transport = new Mock<IMcpClientTransport>();
        transport.Setup(t => t.InitializeAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result("""{"protocolVersion":"2024-11-05","capabilities":{},"serverInfo":{"name":"Legacy","version":"0.1"}}"""));
        transport.Setup(t => t.ListToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result("""{"tools":[]}"""));

        var session = new McpClientSession(Config(), transport.Object);
        await session.ConnectAsync(CancellationToken.None);

        Assert.Equal("2024-11-05", session.ProtocolVersion);
        Assert.Equal("Legacy", session.ServerInfoName);
        transport.Verify(t => t.InitializeAsync(
            It.Is<IReadOnlyList<string>>(v => v.Contains("2025-11-25") && v.Contains("2025-06-18") && v.Contains("2025-03-26") && v.Contains("2024-11-05")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Connect_ToolsList_BuildsSnapshotWithFullNames()
    {
        var transport = new Mock<IMcpClientTransport>();
        transport.Setup(t => t.InitializeAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result("""{"protocolVersion":"2025-06-18","capabilities":{},"serverInfo":{"name":"Mock","version":"1"}}"""));
        transport.Setup(t => t.ListToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result("""{"tools":[{"name":"search","description":"搜索","inputSchema":{"type":"object"}},{"name":"ask"}]}"""));

        var session = new McpClientSession(Config("deepwiki"), transport.Object);
        await session.ConnectAsync(CancellationToken.None);

        Assert.Equal(2, session.ToolCount);
        var tools = session.GetTools();
        Assert.Equal("mcp.deepwiki.search", tools[0].FullName);
        Assert.Equal("search", tools[0].Name);
        Assert.Equal("搜索", tools[0].Description);
        Assert.Equal("""{"type":"object"}""", tools[0].InputSchemaJson);
        Assert.Equal("mcp.deepwiki.ask", tools[1].FullName);
    }

    [Fact]
    public async Task CallTool_TextContent_ConcatenatedAndReturned()
    {
        var transport = new Mock<IMcpClientTransport>();
        transport.Setup(t => t.CallToolAsync("search", """{"query":"x"}""", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result("""{"content":[{"type":"text","text":"{\"hits\":2}"},{"type":"text","text":" done"}],"isError":false}"""));

        var session = new McpClientSession(Config(), transport.Object);
        var text = await session.CallToolAsync("search", """{"query":"x"}""", CancellationToken.None);

        Assert.Equal("""{"hits":2} done""", text);
    }

    [Fact]
    public async Task CallTool_IsError_WrappedAsFailurePayload()
    {
        var transport = new Mock<IMcpClientTransport>();
        transport.Setup(t => t.CallToolAsync("bad", "{}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result("""{"content":[{"type":"text","text":"server exploded"}],"isError":true}"""));

        var session = new McpClientSession(Config(), transport.Object);
        var text = await session.CallToolAsync("bad", "{}", CancellationToken.None);

        using var doc = JsonDocument.Parse(text);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("server exploded", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task CallTool_TransportError_PropagatesAsMcpClientException()
    {
        var transport = new Mock<IMcpClientTransport>();
        transport.Setup(t => t.CallToolAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new McpClientException("connection reset"));

        var session = new McpClientSession(Config(), transport.Object);
        var ex = await Assert.ThrowsAsync<McpClientException>(() => session.CallToolAsync("x", "{}", CancellationToken.None));
        Assert.Contains("connection reset", ex.Message);
    }

    [Fact]
    public async Task CallTool_StructuredContent_PassthroughAsJson()
    {
        var transport = new Mock<IMcpClientTransport>();
        transport.Setup(t => t.CallToolAsync("gen", "{}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result("""{"content":[{"type":"image","data":"base64==","mimeType":"image/png"}],"isError":false}"""));

        var session = new McpClientSession(Config(), transport.Object);
        var text = await session.CallToolAsync("gen", "{}", CancellationToken.None);

        using var doc = JsonDocument.Parse(text);
        Assert.Equal("image", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("base64==", doc.RootElement.GetProperty("data").GetString());
    }
}

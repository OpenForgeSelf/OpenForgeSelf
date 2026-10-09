using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.McpCenter.Controllers;
using ForgeSelf.Api.Plugins.McpCenter.Models;
using ForgeSelf.Api.Plugins.McpCenter.Services;
using ForgeSelf.Api.Plugins.McpCenter.Services.McpClient;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.McpCenterTests;

/// <summary>
/// 工具测试台测试（v2.3.0）：会话层 CallToolDetailedAsync 的四种返回形态、
/// Manager.InvokeToolAsync 的前置校验、控制器 POST {id}/tools/invoke 的成功与三类错误。
/// 隔离：每个用例独立随机数据目录（只创建、不删除，见 plugin-development 铁律 10）。
/// 成功路径走真实 node mock MCP 服务器（Fixtures/mock-mcp-server.js，stdio），不依赖外网。
/// </summary>
public class McpToolInvokeTests
{
    private static readonly string MockScript = Path.Combine(
        AppContext.BaseDirectory, "plugins", "McpCenterTests", "Fixtures", "mock-mcp-server.js");

    private static string TempDataDir() =>
        Path.Combine(Path.GetTempPath(), "mcpcenter-invoke-tests", Guid.NewGuid().ToString("N"));

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

    private static Mock<IMcpClientTransport> TransportReturning(string json)
    {
        var t = new Mock<IMcpClientTransport>();
        t.Setup(x => x.CallToolAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result(json));
        return t;
    }

    /* ────────────────── 会话层 CallToolDetailedAsync ────────────────── */

    [Fact]
    public async Task CallToolDetailed_TextContent_ReturnsTextAndRawJson()
    {
        var t = TransportReturning("""{"content":[{"type":"text","text":"{\"sum\":7}"}],"isError":false}""");
        var session = new McpClientSession(Config(), t.Object);

        var outcome = await session.CallToolDetailedAsync("add", """{"a":3,"b":4}""", CancellationToken.None);

        Assert.False(outcome.IsError);
        Assert.Equal("""{"sum":7}""", outcome.Text);
        using var doc = JsonDocument.Parse(outcome.RawJson);
        Assert.Equal("text", doc.RootElement.GetProperty("content")[0].GetProperty("type").GetString());
    }

    [Fact]
    public async Task CallToolDetailed_IsError_KeepsFlagAndText()
    {
        var t = TransportReturning("""{"content":[{"type":"text","text":"server exploded"}],"isError":true}""");
        var session = new McpClientSession(Config(), t.Object);

        var outcome = await session.CallToolDetailedAsync("bad", "{}", CancellationToken.None);

        Assert.True(outcome.IsError);
        Assert.Equal("server exploded", outcome.Text);
    }

    [Fact]
    public async Task CallToolDetailed_StructuredContent_ItemJsonBecomesText()
    {
        var t = TransportReturning("""{"content":[{"type":"image","data":"base64==","mimeType":"image/png"}],"isError":false}""");
        var session = new McpClientSession(Config(), t.Object);

        var outcome = await session.CallToolDetailedAsync("gen", "{}", CancellationToken.None);

        using var doc = JsonDocument.Parse(outcome.Text);
        Assert.Equal("image", doc.RootElement.GetProperty("type").GetString());
        Assert.False(outcome.IsError);
    }

    [Fact]
    public async Task CallToolDetailed_NoContent_TextEmptyAndRawIsResult()
    {
        var t = TransportReturning("""{"structuredContent":{"ok":true},"isError":false}""");
        var session = new McpClientSession(Config(), t.Object);

        var outcome = await session.CallToolDetailedAsync("gen", "{}", CancellationToken.None);

        Assert.Equal(string.Empty, outcome.Text);
        using var doc = JsonDocument.Parse(outcome.RawJson);
        Assert.True(doc.RootElement.GetProperty("structuredContent").GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task CallTool_LegacyContract_StillConcatenatesText()
    {
        // 回归守卫：v2.3.0 把提取逻辑抽成 Inspect 后，CallToolAsync 的既有语义不得改变。
        var t = TransportReturning("""{"content":[{"type":"text","text":"{\"hits\":2}"},{"type":"text","text":" done"}],"isError":false}""");
        var session = new McpClientSession(Config(), t.Object);

        var text = await session.CallToolAsync("search", """{"query":"x"}""", CancellationToken.None);

        Assert.Equal("""{"hits":2} done""", text);
    }

    /* ────────────────── Manager.InvokeToolAsync 前置校验 ────────────────── */

    [Fact]
    public async Task Invoke_NotConnected_ThrowsWithClearMessage()
    {
        var store = new ExternalServersStore(TempDataDir());
        store.Save(new List<McpExternalServerConfig>
        {
            new() { Id = "deepwiki", Name = "DeepWiki", Transport = "streamable-http", Url = "http://127.0.0.1:9/mcp", Enabled = false }
        });
        var manager = new McpClientManager(store);

        var ex = await Assert.ThrowsAsync<McpClientException>(
            () => manager.InvokeToolAsync("deepwiki", "read_wiki_structure", "{}", CancellationToken.None));

        Assert.Contains("未连接", ex.Message);
    }

    [Fact]
    public async Task Invoke_EmptyToolName_Throws()
    {
        var manager = new McpClientManager(new ExternalServersStore(TempDataDir()));

        var ex = await Assert.ThrowsAsync<McpClientException>(
            () => manager.InvokeToolAsync("mock", "  ", "{}", CancellationToken.None));

        Assert.Contains("工具名不能为空", ex.Message);
    }

    /* ────────────────── 控制器 POST {id}/tools/invoke ────────────────── */

    private static McpExternalController Controller(ExternalServersStore store, McpClientManager manager)
    {
        var controller = new McpExternalController(store, manager)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        return controller;
    }

    private static ApiResponse<McpToolInvokeResult>? AsError(IActionResult result, int expectedStatus)
    {
        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(expectedStatus, obj.StatusCode);
        return Assert.IsType<ApiResponse<McpToolInvokeResult>>(obj.Value);
    }

    [Fact]
    public async Task Invoke_EmptyTool_Returns400()
    {
        var store = new ExternalServersStore(TempDataDir());
        var controller = Controller(store, new McpClientManager(store));

        var result = await controller.InvokeTool("mock", new McpToolInvokeRequest { Tool = "  ", ArgumentsJson = "{}" });

        var err = AsError(result.Result!, 400);
        Assert.Contains("工具名不能为空", err!.Message);
    }

    [Fact]
    public async Task Invoke_BadArgumentsJson_Returns400()
    {
        var store = new ExternalServersStore(TempDataDir());
        var controller = Controller(store, new McpClientManager(store));

        var result = await controller.InvokeTool("mock", new McpToolInvokeRequest { Tool = "add", ArgumentsJson = "{not json" });

        var err = AsError(result.Result!, 400);
        Assert.Contains("参数 JSON 解析失败", err!.Message);
    }

    [Fact]
    public async Task Invoke_ServerNotConnected_Returns400()
    {
        var store = new ExternalServersStore(TempDataDir());
        var controller = Controller(store, new McpClientManager(store));

        var result = await controller.InvokeTool("mock", new McpToolInvokeRequest { Tool = "add", ArgumentsJson = "{}" });

        var err = AsError(result.Result!, 400);
        Assert.Contains("未连接", err!.Message);
    }

    /// <summary>成功路径：真实 node mock（stdio）→ 连接 → invoke add(3,4) → sum=7，且耗时 &gt; 0。</summary>
    [Fact]
    public async Task Invoke_RealMockServer_ReturnsOkWithTextAndElapsed()
    {
        Assert.True(File.Exists(MockScript), $"mock 脚本不存在: {MockScript}");
        var psi = new ProcessStartInfo
        {
            FileName = "node",
            Arguments = $"\"{MockScript}\" --mode stdio",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("无法启动 node mock MCP 服务器（node 是否在 PATH？）");
        try
        {
            var store = new ExternalServersStore(TempDataDir());
            store.Save(new List<McpExternalServerConfig>
            {
                new()
                {
                    Id = "e2e-invoke", Name = "invoke mock", Enabled = true, Transport = "stdio",
                    Command = "node", Args = new() { MockScript, "--mode", "stdio" }
                }
            });
            var manager = new McpClientManager(store);
            await manager.TryConnectAsync("e2e-invoke");
            var controller = Controller(store, manager);

            var result = await controller.InvokeTool("e2e-invoke",
                new McpToolInvokeRequest { Tool = "add", ArgumentsJson = """{"a":3,"b":4}""" });

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<ApiResponse<McpToolInvokeResult>>(ok.Value);
            Assert.True(body.Success);
            var data = body.Data!;
            Assert.True(data.Ok);
            Assert.False(data.IsError);
            Assert.Equal("e2e-invoke", data.ServerId);
            Assert.Equal("add", data.Tool);
            Assert.True(data.ElapsedMs >= 0);
            using var doc = JsonDocument.Parse(data.Text);
            Assert.Equal(7, doc.RootElement.GetProperty("sum").GetInt32());

            await manager.StopAllAsync();
        }
        finally
        {
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch (Exception) { }
        }
    }

    /// <summary>工具不在清单中：连接真实 mock 后调一个不存在的工具名 → 400 并给出可用工具提示。</summary>
    [Fact]
    public async Task Invoke_UnknownTool_Returns400WithHints()
    {
        Assert.True(File.Exists(MockScript), $"mock 脚本不存在: {MockScript}");
        var psi = new ProcessStartInfo
        {
            FileName = "node",
            Arguments = $"\"{MockScript}\" --mode stdio",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("无法启动 node mock MCP 服务器（node 是否在 PATH？）");
        try
        {
            var store = new ExternalServersStore(TempDataDir());
            store.Save(new List<McpExternalServerConfig>
            {
                new()
                {
                    Id = "e2e-invoke2", Name = "invoke mock", Enabled = true, Transport = "stdio",
                    Command = "node", Args = new() { MockScript, "--mode", "stdio" }
                }
            });
            var manager = new McpClientManager(store);
            await manager.TryConnectAsync("e2e-invoke2");
            var controller = Controller(store, manager);

            var result = await controller.InvokeTool("e2e-invoke2",
                new McpToolInvokeRequest { Tool = "nope", ArgumentsJson = "{}" });

            var err = AsError(result.Result!, 400);
            Assert.Contains("不在该服务器的工具清单中", err!.Message);
            Assert.Contains("add", err.Message);

            await manager.StopAllAsync();
        }
        finally
        {
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch (Exception) { }
        }
    }
}

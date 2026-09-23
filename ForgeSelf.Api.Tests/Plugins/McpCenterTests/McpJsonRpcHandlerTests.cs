using System.Text.Json;
using System.Threading;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using ForgeSelf.Api.Plugins.McpCenter.Services;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.McpCenterTests;

/// <summary>
/// JSON-RPC 2.0 分发器单元测试（034）：协议方法契约（initialize / ping / tools/list / tools/call / 通知）、
/// 错误码（-32700 解析 / -32600 无效请求 / -32601 方法不存在 / -32602 参数无效 / -32603 内部错误）、批处理。
/// 策略：Moq 假 IToolRegistry 经真实转发器注入处理器，端到端验证 tools/call 的转发语义。
/// </summary>
public class McpJsonRpcHandlerTests
{
    private const string ServerName = "ForgeSelf McpGateway";
    private const string ServerVersion = "1.0.0";

    private static IToolFunctionExtension FakeTool(string name)
    {
        var mock = new Mock<IToolFunctionExtension>();
        mock.SetupGet(t => t.Name).Returns(name);
        mock.SetupGet(t => t.Id).Returns($"p.{name}");
        return mock.Object;
    }

    private static Mock<IToolRegistry> MakeRegistry()
    {
        var reg = new Mock<IToolRegistry>();
        reg.Setup(r => r.GetAllTools())
           .Returns(new[] { FakeTool("calculate"), FakeTool("get_current_time") });
        reg.Setup(r => r.GetTool(It.IsAny<string>()))
           .Returns<string?>(name => name is "calculate" or "get_current_time" ? FakeTool(name!) : null);
        return reg;
    }

    private static McpJsonRpcHandler CreateHandler(Mock<IToolRegistry>? registry = null)
    {
        var ctx = new Mock<IContext>();
        ctx.Setup(c => c.Get<IToolRegistry>()).Returns((registry ?? MakeRegistry()).Object!);
        var store = new ExternalServersStore(Path.Combine(Path.GetTempPath(), "mcpcenter-tests", Guid.NewGuid().ToString("N")));
        var forwarder = new UniversalToolForwarder(ctx.Object, new McpClientManager(store));
        return new McpJsonRpcHandler(forwarder, ServerName, ServerVersion);
    }

    private static async Task<JsonElement> SendAsync(McpJsonRpcHandler handler, string requestJson)
    {
        var response = await handler.HandleRequestAsync(requestJson);
        Assert.False(string.IsNullOrWhiteSpace(response));
        return JsonDocument.Parse(response!).RootElement.Clone();
    }

    [Fact]
    public async Task Initialize_ReturnsProtocolAndServerInfo()
    {
        var handler = CreateHandler();
        var json = await SendAsync(handler, """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test"}}}""");

        Assert.Equal(1, json.GetProperty("id").GetInt32());
        Assert.Equal("2025-06-18", json.GetProperty("result").GetProperty("protocolVersion").GetString());
        Assert.Equal(ServerName, json.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString());
        Assert.Equal(ServerVersion, json.GetProperty("result").GetProperty("serverInfo").GetProperty("version").GetString());
        Assert.False(json.GetProperty("result").GetProperty("capabilities").GetProperty("tools").GetProperty("listChanged").GetBoolean());
    }

    [Fact]
    public async Task Initialize_OldClientVersion_IsNegotiated()
    {
        var handler = CreateHandler();
        var json = await SendAsync(handler, """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05"}}""");

        // 客户端版本在支持集内 → 回显客户端版本
        Assert.Equal("2024-11-05", json.GetProperty("result").GetProperty("protocolVersion").GetString());
    }

    [Fact]
    public async Task Initialize_UnknownClientVersion_FallsBackToDefault()
    {
        var handler = CreateHandler();
        var json = await SendAsync(handler, """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2099-01-01"}}""");

        Assert.Equal(McpJsonRpcHandler.ProtocolVersion, json.GetProperty("result").GetProperty("protocolVersion").GetString());
    }

    [Fact]
    public async Task Ping_ReturnsEmptyResult()
    {
        var handler = CreateHandler();
        var json = await SendAsync(handler, """{"jsonrpc":"2.0","id":2,"method":"ping"}""");

        Assert.Equal(2, json.GetProperty("id").GetInt32());
        Assert.Equal(JsonValueKind.Object, json.GetProperty("result").ValueKind);
    }

    [Fact]
    public async Task ToolsList_ReturnsExactlyOneUniversalTool()
    {
        var handler = CreateHandler();
        var json = await SendAsync(handler, """{"jsonrpc":"2.0","id":3,"method":"tools/list"}""");

        var tools = json.GetProperty("result").GetProperty("tools");
        Assert.Equal(1, tools.GetArrayLength());
        Assert.Equal("universal_tool", tools[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task ToolsCall_Success_ForwardsToTargetTool()
    {
        var reg = MakeRegistry();
        const string targetResult = """{"success":true,"expression":"1+2","result":3}""";
        reg.Setup(r => r.ExecuteToolWithResultAsync("calculate", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ToolExecutionResult { Success = true, Result = targetResult });

        var handler = CreateHandler(reg);
        var json = await SendAsync(handler,
            """{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"universal_tool","arguments":{"tool":"calculate","parameters":{"expression":"1+2"}}}}""");

        var result = json.GetProperty("result");
        Assert.False(result.GetProperty("isError").GetBoolean());
        Assert.Equal(targetResult, result.GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Equal("text", result.GetProperty("content")[0].GetProperty("type").GetString());
        // 参数原样抵达目标工具（FR：透传语义）
        reg.Verify(r => r.ExecuteToolWithResultAsync("calculate", """{"expression":"1+2"}""", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToolsCall_UnknownExposedToolName_ReturnsInvalidParams()
    {
        var handler = CreateHandler();
        var json = await SendAsync(handler,
            """{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"other_tool","arguments":{}}}""");

        Assert.Equal(-32602, json.GetProperty("error").GetProperty("code").GetInt32());
        Assert.Contains("other_tool", json.GetProperty("error").GetProperty("message").GetString());
    }

    [Fact]
    public async Task ToolsCall_MissingName_ReturnsInvalidParams()
    {
        var handler = CreateHandler();
        var json = await SendAsync(handler,
            """{"jsonrpc":"2.0","id":6,"method":"tools/call","params":{"arguments":{}}}""");

        Assert.Equal(-32602, json.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task ToolsCall_UnknownTargetTool_ReturnsIsErrorWithHint()
    {
        var handler = CreateHandler();
        var json = await SendAsync(handler,
            """{"jsonrpc":"2.0","id":7,"method":"tools/call","params":{"name":"universal_tool","arguments":{"tool":"no_such","parameters":{}}}}""");

        var result = json.GetProperty("result");
        Assert.True(result.GetProperty("isError").GetBoolean());
        Assert.Contains("unknown tool", result.GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task Notification_Initialized_ReturnsNoResponse()
    {
        var handler = CreateHandler();
        var response = await handler.HandleRequestAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");

        Assert.Null(response);
    }

    [Fact]
    public async Task ParseError_ReturnsMinus32700()
    {
        var handler = CreateHandler();
        var json = await SendAsync(handler, "this is not json");

        Assert.Equal(-32700, json.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task InvalidRequest_BadJsonRpcVersion_ReturnsMinus32600()
    {
        var handler = CreateHandler();
        var json = await SendAsync(handler, """{"jsonrpc":"1.0","id":1,"method":"ping"}""");

        Assert.Equal(-32600, json.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task MethodNotFound_ReturnsMinus32601()
    {
        var handler = CreateHandler();
        var json = await SendAsync(handler, """{"jsonrpc":"2.0","id":8,"method":"no/such/method"}""");

        Assert.Equal(-32601, json.GetProperty("error").GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task Batch_MixedRequests_ReturnsArrayOfResponses()
    {
        var handler = CreateHandler();
        var json = await SendAsync(handler,
            """[{"jsonrpc":"2.0","id":10,"method":"ping"},{"jsonrpc":"2.0","id":11,"method":"tools/list"},{"jsonrpc":"2.0","method":"notifications/initialized"}]""");

        Assert.Equal(JsonValueKind.Array, json.ValueKind);
        Assert.Equal(2, json.GetArrayLength()); // 通知不产生响应
        Assert.Equal(10, json[0].GetProperty("id").GetInt32());
        Assert.Equal(11, json[1].GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Batch_EmptyArray_ReturnsInvalidRequest()
    {
        var handler = CreateHandler();
        var json = await SendAsync(handler, """[]""");

        Assert.Equal(-32600, json.GetProperty("error").GetProperty("code").GetInt32());
    }
}

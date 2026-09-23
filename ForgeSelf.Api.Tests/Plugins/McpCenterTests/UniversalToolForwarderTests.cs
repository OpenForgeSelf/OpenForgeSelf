using System.Text.Json;
using System.Threading;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using ForgeSelf.Api.Plugins.McpCenter.Services;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.McpCenterTests;

/// <summary>
/// 万能工具转发器单元测试（034）：MCP 网关对外唯一工具的转发语义，对齐 031 UniversalTool 四类合约——
/// ① 定义契约（tools/list 唯一条目）；② 解析与透传（对象/字符串参数/无参形态，结果原样回传）；
/// ③ 安全语义（防自引用、unknown 预检带已注册数量、拒绝路径零分发）；④ 兜底（未挂注册表/空输入/坏 JSON 优雅报错）。
/// 策略：Moq 假 IToolRegistry + 假 IContext（软依赖 ctx.Get&lt;IToolRegistry&gt;），零副作用纯逻辑单测。
/// </summary>
public class UniversalToolForwarderTests
{
    private const string PluginId = "McpGateway";

    private static IToolFunctionExtension FakeTool(string name)
    {
        var mock = new Mock<IToolFunctionExtension>();
        mock.SetupGet(t => t.Name).Returns(name);
        mock.SetupGet(t => t.Id).Returns($"{PluginId}.{name}");
        return mock.Object;
    }

    private static Mock<IContext> MakeContext(Mock<IToolRegistry>? registry = null)
    {
        var ctx = new Mock<IContext>();
        ctx.Setup(c => c.Get<IToolRegistry>()).Returns(registry?.Object!);
        return ctx;
    }

    private static Mock<IToolRegistry> MakeRegistry(bool known)
    {
        var reg = new Mock<IToolRegistry>();
        reg.Setup(r => r.GetAllTools())
           .Returns(new[] { FakeTool("time"), FakeTool("calculate"), FakeTool("read_file") });
        reg.Setup(r => r.GetTool(It.IsAny<string>()))
           .Returns<string?>(name => known ? FakeTool(name!) : null);
        return reg;
    }

    private static UniversalToolForwarder CreateForwarder(IContext ctx)
    {
        // v2.1.0：转发器依赖外部 MCP 客户端管理器（sealed，Moq 不可 mock）→ 真实实例 + 临时目录 store（无文件写入，仅路径）
        var store = new ExternalServersStore(Path.Combine(Path.GetTempPath(), "mcpcenter-tests", Guid.NewGuid().ToString("N")));
        return new UniversalToolForwarder(ctx, new McpClientManager(store));
    }

    private static void VerifyNeverDispatched(Mock<IToolRegistry> registry) =>
        registry.Verify(
            r => r.ExecuteToolWithResultAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);

    private static void VerifyDispatchedOnceWithParams(Mock<IToolRegistry> registry, string toolName, string expectedParamsJson) =>
        registry.Verify(
            r => r.ExecuteToolWithResultAsync(toolName, expectedParamsJson, It.IsAny<CancellationToken>()),
            Times.Once);

    [Fact]
    public void ToolDefinition_HasExactlyOneTool_NamedUniversalTool()
    {
        using var doc = JsonDocument.Parse(UniversalToolForwarder.ToolDefinitionJson);
        var root = doc.RootElement;

        Assert.Equal("universal_tool", root.GetProperty("name").GetString());
        Assert.Equal("object", root.GetProperty("inputSchema").GetProperty("type").GetString());
        var required = root.GetProperty("inputSchema").GetProperty("required");
        Assert.Equal(1, required.GetArrayLength());
        Assert.Equal("tool", required[0].GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("description").GetString()));
    }

    [Fact]
    public async Task Forward_ObjectForm_DispatchesAndReturnsVerbatim()
    {
        var reg = MakeRegistry(known: true);
        const string forwarded = """{"success":true,"data":"real-tool-result"}""";
        reg.Setup(r => r.ExecuteToolWithResultAsync("time", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ToolExecutionResult { Success = true, Result = forwarded });

        var forwarder = CreateForwarder(MakeContext(reg).Object);
        var input = """{"tool":"time","parameters":{"format":"HH:mm"}}""";

        var result = await forwarder.ForwardAsync(input);

        Assert.Equal(forwarded, result.Text);
        Assert.False(result.IsError);
        VerifyDispatchedOnceWithParams(reg, "time", """{"format":"HH:mm"}""");
    }

    [Fact]
    public async Task Forward_TargetToolFailure_PayloadPassedThroughVerbatimWithIsError()
    {
        var reg = MakeRegistry(known: true);
        const string errorPayload = """{"success":false,"error":{"code":"EXECUTION_FAILED","message":"boom"}}""";
        reg.Setup(r => r.ExecuteToolWithResultAsync("time", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ToolExecutionResult { Success = false, Result = errorPayload, ErrorMessage = "boom" });

        var forwarder = CreateForwarder(MakeContext(reg).Object);

        var result = await forwarder.ForwardAsync("""{"tool":"time","parameters":{}}""");

        Assert.Equal(errorPayload, result.Text);
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Forward_StringParametersForm_AlsoDispatches()
    {
        var reg = MakeRegistry(known: true);
        var forwarder = CreateForwarder(MakeContext(reg).Object);
        var input = """{"tool":"time","parameters":"{\"format\":\"HH:mm\"}"}""";

        await forwarder.ForwardAsync(input);

        VerifyDispatchedOnceWithParams(reg, "time", """{"format":"HH:mm"}""");
    }

    [Fact]
    public async Task Forward_ParametersAbsent_DispatchesWithFallback()
    {
        var reg = MakeRegistry(known: true);
        var forwarder = CreateForwarder(MakeContext(reg).Object);
        var input = """{"tool":"time"}""";

        await forwarder.ForwardAsync(input);

        VerifyDispatchedOnceWithParams(reg, "time", "{}");
    }

    [Fact]
    public async Task Forward_SelfReference_RejectedAndNeverDispatched()
    {
        var reg = MakeRegistry(known: true);
        var forwarder = CreateForwarder(MakeContext(reg).Object);
        var input = """{"tool":"universal_tool","parameters":{"tool":"time"}}""";

        var result = await forwarder.ForwardAsync(input);

        Assert.Contains("cannot invoke itself", result.Text);
        Assert.True(result.IsError);
        VerifyNeverDispatched(reg);
    }

    [Fact]
    public async Task Forward_UnknownTool_RejectedWithCountAndNeverDispatched()
    {
        var reg = MakeRegistry(known: false);
        var forwarder = CreateForwarder(MakeContext(reg).Object);
        var input = """{"tool":"no_such_tool","parameters":{}}""";

        var result = await forwarder.ForwardAsync(input);

        Assert.Contains("unknown tool", result.Text);
        Assert.Contains("no_such_tool", result.Text);
        Assert.Contains("3", result.Text);
        Assert.True(result.IsError);
        VerifyNeverDispatched(reg);
    }

    [Fact]
    public async Task Forward_MissingToolField_RejectedAndNeverDispatched()
    {
        var reg = MakeRegistry(known: true);
        var forwarder = CreateForwarder(MakeContext(reg).Object);
        var input = """{"parameters":{"x":1}}""";

        var result = await forwarder.ForwardAsync(input);

        Assert.False(string.IsNullOrWhiteSpace(result.Text));
        Assert.True(result.IsError);
        VerifyNeverDispatched(reg);
    }

    [Fact]
    public async Task Forward_EmptyInput_RejectedGracefully()
    {
        var reg = MakeRegistry(known: true);
        var forwarder = CreateForwarder(MakeContext(reg).Object);

        var result = await forwarder.ForwardAsync(string.Empty);

        Assert.False(string.IsNullOrWhiteSpace(result.Text));
        Assert.True(result.IsError);
        VerifyNeverDispatched(reg);
    }

    [Fact]
    public async Task Forward_InvalidJson_RejectedGracefully()
    {
        var reg = MakeRegistry(known: true);
        var forwarder = CreateForwarder(MakeContext(reg).Object);

        var result = await forwarder.ForwardAsync("this is not json at all");

        Assert.False(string.IsNullOrWhiteSpace(result.Text));
        Assert.True(result.IsError);
        VerifyNeverDispatched(reg);
    }

    [Fact]
    public async Task Forward_RegistryNotAttached_ReturnsErrorNotThrows()
    {
        var ctx = MakeContext(registry: null);
        ctx.Setup(c => c.Get<IToolRegistry>()).Returns((IToolRegistry?)null!);

        var forwarder = CreateForwarder(ctx.Object);
        var input = """{"tool":"time","parameters":{}}""";

        var result = await forwarder.ForwardAsync(input);

        Assert.False(string.IsNullOrWhiteSpace(result.Text));
        Assert.Contains("IToolRegistry", result.Text);
        Assert.True(result.IsError);
    }
}

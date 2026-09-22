using System.Threading;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using ForgeSelf.Api.Plugins.AIAgent;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins;

/// <summary>
/// UniversalTool 单元测试（031，适配重构后 UniversalTool：namespace ForgeSelf.Api.Plugins.AIAgent、
/// 构造 (pluginId, IContext)、ExecuteToolWithResultAsync 返回 ToolExecutionResult）。
/// 万能工具 = ToolRegistry 分发核之上的透传壳，本套测试锁四类合约：
/// ① 定义契约；② 解析与透传（对象/字符串参数/无参形态，结果原样回传）；
/// ③ 安全语义（防自引用、unknown 预检带已注册数量、拒绝路径零分发）；
/// ④ 兜底（未挂注册表/空输入/坏 JSON 均优雅报错而非抛异常）。
/// 策略：Moq 假 IToolRegistry + 假 IContext（软依赖 ctx.Get&lt;IToolRegistry&gt;），零副作用纯逻辑单测。
/// </summary>
public class UniversalToolTests
{
    private const string PluginId = "AIAgent";
    private const string SelfName = "universal_tool";

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

    private static UniversalTool CreateTool(IContext ctx) => new(PluginId, ctx);

    private static void VerifyNeverDispatched(Mock<IToolRegistry> registry) =>
        registry.Verify(
            r => r.ExecuteToolWithResultAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);

    private static void VerifyDispatchedOnceWithParams(Mock<IToolRegistry> registry, string toolName, string expectedParamsJson) =>
        registry.Verify(
            r => r.ExecuteToolWithResultAsync(toolName, expectedParamsJson, It.IsAny<CancellationToken>()),
            Times.Once);

    [Fact]
    public void Definition_Name_Should_Be_UniversalTool()
    {
        var tool = CreateTool(MakeContext().Object);

        Assert.Equal(SelfName, tool.Name);
        // Id = "插件名.工具名" 全小写（全库惯例：aiagent.read_file / filetools.file_rename）
        Assert.Equal("aiagent.universal_tool", tool.Id);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
    }

    [Fact]
    public async Task Execute_ObjectForm_ForwardsAndReturnsVerbatim()
    {
        var reg = MakeRegistry(known: true);
        const string forwarded = """{"success":true,"data":"real-tool-result"}""";
        reg.Setup(r => r.ExecuteToolWithResultAsync("time", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ToolExecutionResult { Success = true, Result = forwarded });

        var tool = CreateTool(MakeContext(reg).Object);
        var input = """{"tool":"time","parameters":{"format":"HH:mm"}}""";

        var result = await tool.ExecuteAsync(input);

        Assert.Equal(forwarded, result);            // 透传语义：原样回传，不二次包装
        VerifyDispatchedOnceWithParams(reg, "time", """{"format":"HH:mm"}""");  // 参数原样抵达目标工具（FR-1.1）
    }

    [Fact]
    public async Task Execute_TargetToolFailure_ErrorPayloadPassedThroughVerbatim()
    {
        // FR-1.2 后半句：失败时错误语义与直调一致——宿主错误负载原样透传，不二次包装。
        var reg = MakeRegistry(known: true);
        const string errorPayload = """{"success":false,"error":{"code":"EXECUTION_FAILED","message":"boom"}}""";
        reg.Setup(r => r.ExecuteToolWithResultAsync("time", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ToolExecutionResult { Success = false, Result = errorPayload, ErrorMessage = "boom" });

        var tool = CreateTool(MakeContext(reg).Object);

        var result = await tool.ExecuteAsync("""{"tool":"time","parameters":{}}""");

        Assert.Equal(errorPayload, result);
    }

    [Fact]
    public async Task Execute_StringParametersForm_AlsoDispatches()
    {
        var reg = MakeRegistry(known: true);
        var tool = CreateTool(MakeContext(reg).Object);
        var input = """{"tool":"time","parameters":"{\"format\":\"HH:mm\"}"}""";

        await tool.ExecuteAsync(input);

        // 兼容字符串形态：JSON 字符串解码后再透传，参数仍按原文抵达目标工具
        VerifyDispatchedOnceWithParams(reg, "time", """{"format":"HH:mm"}""");
    }

    [Fact]
    public async Task Execute_ParametersAbsent_DispatchesWithFallback()
    {
        var reg = MakeRegistry(known: true);
        var tool = CreateTool(MakeContext(reg).Object);
        var input = """{"tool":"time"}""";

        await tool.ExecuteAsync(input);

        VerifyDispatchedOnceWithParams(reg, "time", "{}");   // 无参数回退为合法空对象（不传 null/空串）
    }

    [Fact]
    public async Task Execute_SelfReference_RejectedAndNeverDispatched()
    {
        var reg = MakeRegistry(known: true);
        var tool = CreateTool(MakeContext(reg).Object);
        var input = """{"tool":"universal_tool","parameters":{"tool":"time"}}""";

        var result = await tool.ExecuteAsync(input);

        Assert.Contains(SelfName, result);          // 报错回显被拒目标
        Assert.Contains("cannot invoke itself", result);  // design §5.2 契约文案
        VerifyNeverDispatched(reg);                 // 防递归：拒绝路径零分发
    }

    [Fact]
    public async Task Execute_UnknownTool_RejectedWithCountAndNeverDispatched()
    {
        var reg = MakeRegistry(known: false);
        var tool = CreateTool(MakeContext(reg).Object);
        var input = """{"tool":"no_such_tool","parameters":{}}""";

        var result = await tool.ExecuteAsync(input);

        Assert.Contains("unknown tool", result);    // FR-1.3 文案契约
        Assert.Contains("no_such_tool", result);    // 报错点名未知工具
        Assert.Contains("3", result);               // 带已注册数量提示
        VerifyNeverDispatched(reg);                 // 预检失败不进分发
    }

    [Fact]
    public async Task Execute_MissingToolField_RejectedAndNeverDispatched()
    {
        var reg = MakeRegistry(known: true);
        var tool = CreateTool(MakeContext(reg).Object);
        var input = """{"parameters":{"x":1}}""";

        var result = await tool.ExecuteAsync(input);

        Assert.False(string.IsNullOrWhiteSpace(result));
        VerifyNeverDispatched(reg);
    }

    [Fact]
    public async Task Execute_EmptyInput_RejectedGracefully()
    {
        var reg = MakeRegistry(known: true);
        var tool = CreateTool(MakeContext(reg).Object);

        var result = await tool.ExecuteAsync(string.Empty);

        Assert.False(string.IsNullOrWhiteSpace(result));
        VerifyNeverDispatched(reg);
    }

    [Fact]
    public async Task Execute_InvalidJson_RejectedGracefully()
    {
        var reg = MakeRegistry(known: true);
        var tool = CreateTool(MakeContext(reg).Object);

        var result = await tool.ExecuteAsync("this is not json at all");

        Assert.False(string.IsNullOrWhiteSpace(result));
        VerifyNeverDispatched(reg);
    }

    [Fact]
    public async Task Execute_RegistryNotAttached_ReturnsErrorNotThrows()
    {
        // 软依赖模式：构造期不解析，首次使用 ctx.Get<IToolRegistry>()——
        // 宿主 seed 晚于插件 Apply 时可能暂无注册表，必须优雅降级。
        var ctx = MakeContext(registry: null);
        ctx.Setup(c => c.Get<IToolRegistry>()).Returns((IToolRegistry?)null!);

        var tool = CreateTool(ctx.Object);
        var input = """{"tool":"time","parameters":{}}""";

        var result = await tool.ExecuteAsync(input);

        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.Contains("IToolRegistry", result);   // 报错点名缺失的依赖
    }
}

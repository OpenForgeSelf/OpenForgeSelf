using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using ForgeSelf.Api.Plugins.McpCenter.Services;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins;

/// <summary>
/// UniversalTool 外部 MCP 工具路由测试（v1.9.0，PILOT-072）：内置 agent 直传 mcp.&lt;服务器id&gt;.&lt;工具名&gt;
/// 不再落「unknown tool」（历史缺口：问答回合 2026-10-19 实测确认此前内置 agent 内外都调不了外部工具）。
/// 锁四条语义：① mcp. 前缀在注册表分发之前分流（决策 D10——外部工具不在宿主注册表）；
/// ② 成功结果原样透传；③ 契约缺失（McpCenter 未启用）给中文引导错误而非 unknown；
/// ④ 非 mcp. 名的既有注册表路径零扰动。策略：Moq 假 IContext + 假 IMcpExternalGateway，纯逻辑零副作用。
/// </summary>
public class UniversalToolExternalRouteTests
{
    private const string PluginId = "mcp-center";

    private static Mock<IToolRegistry> MakeRegistry()
    {
        var reg = new Mock<IToolRegistry>();
        reg.Setup(r => r.GetAllTools()).Returns(Array.Empty<IToolFunctionExtension>());
        reg.Setup(r => r.GetTool(It.IsAny<string>())).Returns((IToolFunctionExtension?)null);
        return reg;
    }

    private static UniversalTool CreateTool(Mock<IToolRegistry> registry, IMcpExternalGateway? gateway)
    {
        var ctx = new Mock<IContext>();
        ctx.Setup(c => c.Get<IToolRegistry>()).Returns(registry.Object);
        if (gateway != null) ctx.Setup(c => c.Get<IMcpExternalGateway>()).Returns(gateway);
        return new UniversalTool(ctx.Object);
    }

    private static Mock<IMcpExternalGateway> MakeGateway()
    {
        var gw = new Mock<IMcpExternalGateway>();
        gw.Setup(g => g.InvokeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync("""{"success":true,"answer":"real-external-result"}""");
        return gw;
    }

    [Fact]
    public void Description_And_Schema_Advertise_ExternalTool_FullName_Form()
    {
        // 文案守卫：模型侧只能从 Description/schema 学到 mcp. 全名形态（回滚此文案即红）
        var tool = CreateTool(MakeRegistry(), gateway: null);

        Assert.Contains("mcp.<服务器id>.<工具名>", tool.Description);
        Assert.Contains("mcp.<服务器id>.<工具名>", tool.ParametersJsonSchema);
    }

    [Fact]
    public async Task External_Prefix_Routes_To_Gateway_And_Returns_Verbatim()
    {
        var reg = MakeRegistry();
        var gw = MakeGateway();
        var tool = CreateTool(reg, gw.Object);

        var result = await tool.ExecuteAsync("""{"tool":"mcp.deepwiki.ask_wiki_question","parameters":{"repoName":"microsoft/vscode"}}""");

        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("real-external-result", doc.RootElement.GetProperty("answer").GetString());
        gw.Verify(g => g.InvokeAsync(
            "mcp.deepwiki.ask_wiki_question",
            """{"repoName":"microsoft/vscode"}""",
            It.IsAny<CancellationToken>()), Times.Once);
        // 分流在注册表之前：mcp. 全名绝不进宿主分发（D10 隔离保持）
        reg.Verify(r => r.GetTool(It.IsAny<string>()), Times.Never);
        reg.Verify(r => r.ExecuteAsync(It.IsAny<ToolExecution>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Gateway_Failure_Returns_SuccessFalse_Envelope_With_Real_Message()
    {
        var gw = new Mock<IMcpExternalGateway>();
        gw.Setup(g => g.InvokeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
          .ThrowsAsync(new InvalidOperationException("外部服务器 'deepwiki' 未连接（请先在 MCP 中心连接并拉取工具清单）"));
        var tool = CreateTool(MakeRegistry(), gw.Object);

        var result = await tool.ExecuteAsync("""{"tool":"mcp.deepwiki.search","parameters":{"query":"x"}}""");

        using var doc = JsonDocument.Parse(result);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Contains("未连接", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Contract_Missing_Returns_Chinese_Guide_Not_UnknownTool()
    {
        // McpCenter 未启用/已卸载：ctx.Get 为 null → 中文引导错误，不得落「unknown tool」误导模型枚举注册表
        var reg = MakeRegistry();
        var tool = CreateTool(reg, gateway: null);

        var result = await tool.ExecuteAsync("""{"tool":"mcp.deepwiki.search","parameters":{}}""");

        using var doc = JsonDocument.Parse(result);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        var error = doc.RootElement.GetProperty("error").GetString();
        Assert.Contains("外部 MCP 能力不可用", error);
        Assert.DoesNotContain("unknown tool", error);
        reg.Verify(r => r.GetTool(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task NonPrefixed_Tool_Still_Goes_Registry_Path_Gateway_Untouched()
    {
        // 隔离验证：普通工具名零扰动——不触网关；unknown 语义保持（带已注册数量）
        var reg = MakeRegistry();
        var gw = MakeGateway();
        var tool = CreateTool(reg, gw.Object);

        var result = await tool.ExecuteAsync("""{"tool":"read_file","parameters":{"path":"a.txt"}}""");

        using var doc = JsonDocument.Parse(result);
        Assert.Contains("unknown tool", doc.RootElement.GetProperty("error").GetString());
        gw.Verify(g => g.InvokeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

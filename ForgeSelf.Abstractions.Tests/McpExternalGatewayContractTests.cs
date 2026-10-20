using ForgeSelf.Abstractions;
using Xunit;

namespace ForgeSelf.Abstractions.Tests;

/// <summary>
/// 外部 MCP 工具跨插件契约守卫测试（PILOT-072 / McpCenter v2.6.0）。
/// IMcpExternalGateway 是 McpCenter（提供方）与网关转发器 + 内置 AIAgent（两个消费方）之间的
/// 唯一能力接缝；McpExternalToolNames 是全名命名规则（mcp.&lt;服务器id&gt;.&lt;工具名&gt;）的跨插件单一真源
/// （AIAgent 因 ALC 隔离无法引用 McpCenter 程序集，规则必须上移契约层，否则两侧拆解语义会漂移）。
/// 本套测试锁定：① 前缀常量与拼装/拆解往返一致；② 拆解边界（坏格式一律 false，不得半解析）；
/// ③ 契约可被消费方以替身实现（接口形状不锁死在提供方类型上）。
/// </summary>
public class McpExternalGatewayContractTests
{
    [Fact]
    public void Prefix_Is_McpDot()
    {
        // 网关转发器、AIAgent 路由分支、list_tools 条目名三方共用的字面量，改动即破坏兼容调用
        Assert.Equal("mcp.", McpExternalToolNames.Prefix);
    }

    [Theory]
    [InlineData("deepwiki", "search", "mcp.deepwiki.search")]
    [InlineData("mock", "add", "mcp.mock.add")]
    [InlineData("srv-1", "a.b.c", "mcp.srv-1.a.b.c")] // 工具名本身含点：整段保留在工具名侧
    public void BuildFullName_Composes_PrefixedFullName(string serverId, string toolName, string expected)
    {
        Assert.Equal(expected, McpExternalToolNames.BuildFullName(serverId, toolName));
    }

    [Theory]
    [InlineData("mcp.deepwiki.search", "deepwiki", "search")]
    [InlineData("mcp.srv-1.a.b.c", "srv-1", "a.b.c")] // 首个点分割：服务器 id 不允许含点，其余归工具名
    [InlineData("MCP.x.y", null, null)]               // 前缀区分大小写（与转发器 StartsWith Ordinal 一致）
    [InlineData("mcp.deepwiki", null, null)]          // 缺工具名
    [InlineData("mcp.deepwiki.", null, null)]         // 工具名为空
    [InlineData("mcp..search", null, null)]           // 服务器 id 为空
    [InlineData("mcp.", null, null)]
    [InlineData("deepwiki.search", null, null)]       // 无前缀
    [InlineData("", null, null)]
    [InlineData(null, null, null)]
    public void TrySplit_Parses_Or_Rejects(string? fullName, string? expectedServerId, string? expectedToolName)
    {
        var ok = McpExternalToolNames.TrySplit(fullName, out var serverId, out var toolName);

        if (expectedServerId == null)
        {
            Assert.False(ok);
            Assert.Equal(string.Empty, serverId);
            Assert.Equal(string.Empty, toolName); // 拒绝路径不得半解析
        }
        else
        {
            Assert.True(ok);
            Assert.Equal(expectedServerId, serverId);
            Assert.Equal(expectedToolName, toolName);
        }
    }

    [Fact]
    public void Build_Then_Split_RoundTrips()
    {
        var full = McpExternalToolNames.BuildFullName("mock", "echo");

        Assert.True(McpExternalToolNames.TrySplit(full, out var serverId, out var toolName));
        Assert.Equal("mock", serverId);
        Assert.Equal("echo", toolName);
    }

    [Fact]
    public async Task Contract_Is_Implable_ByFake_And_InfoRecordCarriesAllFields()
    {
        // 消费方（ListTools 合并 / AIAgent 路由）单测全靠替身驱动——接口形状必须可零依赖实现
        var gateway = new FakeGateway();

        var listed = gateway.ListConnectedTools();
        var info = Assert.Single(listed);
        Assert.Equal("mcp.fake.ping", info.FullName);
        Assert.Equal("fake", info.ServerId);
        Assert.Equal("ping", info.Name);
        Assert.Equal("探活", info.Description);
        Assert.Equal("""{"type":"object"}""", info.InputSchemaJson);

        Assert.Equal("pong", await gateway.InvokeAsync("mcp.fake.ping", "{}", CancellationToken.None));
    }

    /// <summary>最小契约替身：一条工具 + 原样应答，供三个消费侧测试复用同形状。</summary>
    private sealed class FakeGateway : IMcpExternalGateway
    {
        public IReadOnlyList<McpExternalToolInfo> ListConnectedTools() => new[]
        {
            new McpExternalToolInfo("mcp.fake.ping", "fake", "ping", "探活", """{"type":"object"}""")
        };

        public Task<string> InvokeAsync(string fullName, string argumentsJson, CancellationToken ct = default)
            => Task.FromResult("pong");
    }
}

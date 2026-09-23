using ForgeSelf.Abstractions;
using ForgeSelf.Api.Services;
using ForgeSelf.Api.Plugins.McpCenter.Services;
using ForgeSelf.Api.Tests.Plugins;
using Moq;

namespace ForgeSelf.Api.Tests.Plugins.McpCenterTests;

public class McpServiceToolRegistryTests
{
    [Fact]
    public async Task GetToolsAsync_ToolRegisteredAfterConstruction_ReturnsToolDynamically()
    {
        // T032 验证：McpService 须在查询时从 ToolRegistry 动态同步，
        // 不能依赖构建期的一次性快照（构建期 ToolRegistry 为空）。
        var tool = new FakeToolFunctionExtension
        {
            Id = "tool.dyn1",
            Name = "Dynamic Tool",
            PluginId = "test.plugin",
            Description = "动态注册的工具"
        };

        var toolRegistryMock = new Mock<IToolRegistry>();
        toolRegistryMock.Setup(t => t.GetAllTools()).Returns(new List<IToolFunctionExtension> { tool });

        var mcp = new McpService(toolRegistryMock.Object);

        var tools = await mcp.GetToolsAsync("mcp-forgeself-local");

        tools.Should().Contain(t => t.Name == "Dynamic Tool");
    }

    [Fact]
    public async Task GetServersAsync_ToolRegisteredAfterConstruction_RefreshesServerToolCount()
    {
        // T032 验证：新增工具后，对应服务器的 toolCount 须同步刷新
        var tool = new FakeToolFunctionExtension
        {
            Id = "tool.dyn2",
            Name = "Dynamic Tool 2",
            PluginId = "test.plugin",
            Description = "动态注册的工具"
        };

        var toolRegistryMock = new Mock<IToolRegistry>();
        toolRegistryMock.Setup(t => t.GetAllTools()).Returns(new List<IToolFunctionExtension> { tool });

        var mcp = new McpService(toolRegistryMock.Object);

        var servers = await mcp.GetServersAsync();
        var local = servers.First(s => s.Id == "mcp-forgeself-local");

        local.ToolCount.Should().BeGreaterThan(4);
    }
}

using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.McpCenter.Services;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.McpCenterTests;

/// <summary>
/// McpService 单元测试（034 v2.0.0，迁自宿主 mcp-tools）：
/// ① 预置数据（4 服务器 / 12 工具 + toolCount）；② ToolRegistry 实时同步（新增/注销清理）；
/// ③ 工具开关与测试语义；④ 软依赖缺失（IToolRegistry=null）降级为仅预置数据。
/// </summary>
public class McpServiceTests
{
    private static IToolFunctionExtension FakeTool(string name, string id, string description = "")
    {
        var mock = new Mock<IToolFunctionExtension>();
        mock.SetupGet(t => t.Name).Returns(name);
        mock.SetupGet(t => t.Id).Returns(id);
        mock.SetupGet(t => t.Description).Returns(description);
        return mock.Object;
    }

    private static Mock<IToolRegistry> MakeRegistry(params IToolFunctionExtension[] tools)
    {
        var reg = new Mock<IToolRegistry>();
        reg.Setup(r => r.GetAllTools()).Returns(tools);
        return reg;
    }

    // ---------- 预置数据 ----------

    [Fact]
    public void 预置四服务器与十二工具()
    {
        var svc = new McpService(null);

        var servers = svc.GetServersAsync().Result;
        Assert.Equal(4, servers.Count);
        Assert.Contains(servers, s => s.Id == "mcp-forgeself-local" && s.Status == "connected");
        Assert.Contains(servers, s => s.Id == "mcp-github-api" && s.Status == "disconnected");
    }

    [Fact]
    public void 预置服务器工具数正确()
    {
        var svc = new McpService(null);

        var servers = svc.GetServersAsync().Result;
        var local = servers.First(s => s.Id == "mcp-forgeself-local");
        Assert.Equal(4, local.ToolCount); // 预置 4 个 dev 工具
    }

    [Fact]
    public void 软依赖缺失时仅提供预置数据且不崩溃()
    {
        var svc = new McpService(null);

        var tools = svc.GetToolsAsync("mcp-forgeself-local").Result;
        Assert.Equal(4, tools.Count); // 仅预置，无注册表同步
    }

    // ---------- ToolRegistry 实时同步 ----------

    [Fact]
    public void 从ToolRegistry同步真实工具到本地列表()
    {
        var registry = MakeRegistry(FakeTool("calculate", "real.calculate", "计算器"));
        var svc = new McpService(registry.Object);

        var tools = svc.GetToolsAsync("mcp-forgeself-local").Result;
        var synced = tools.FirstOrDefault(t => t.Id == "mcp-tool-real-real.calculate");

        Assert.NotNull(synced);
        Assert.Equal("calculate", synced!.Name);
        Assert.Equal("计算器", synced.Description);
        Assert.True(synced.IsEnabled);
    }

    [Fact]
    public void ToolRegistry注销的工具从列表清理()
    {
        var registry = MakeRegistry(FakeTool("time", "real.time"));
        var svc = new McpService(registry.Object);

        Assert.Contains(svc.GetToolsAsync("mcp-forgeself-local").Result, t => t.Id == "mcp-tool-real-real.time");

        // 注销后再次查询应清理
        registry.Setup(r => r.GetAllTools()).Returns(Array.Empty<IToolFunctionExtension>());
        var after = svc.GetToolsAsync("mcp-forgeself-local").Result;

        Assert.DoesNotContain(after, t => t.Id == "mcp-tool-real-real.time");
    }

    [Fact]
    public void 工具查询支持关键字与分类过滤()
    {
        var svc = new McpService(null);

        var byKeyword = svc.GetToolsAsync("mcp-filesystem", keyword: "read").Result;
        Assert.Single(byKeyword);
        Assert.Equal("read_file", byKeyword[0].Name);

        var byCategory = svc.GetToolsAsync("mcp-forgeself-local", category: "dev").Result;
        Assert.Equal(4, byCategory.Count);
    }

    // ---------- 工具开关与测试 ----------

    [Fact]
    public void 切换工具启停状态()
    {
        var svc = new McpService(null);

        var before = svc.GetToolsAsync("mcp-filesystem").Result.First(t => t.Name == "read_file");
        Assert.True(before.IsEnabled);

        var toggled = svc.ToggleToolAsync(before.Id).Result;
        Assert.False(toggled!.IsEnabled);

        var again = svc.ToggleToolAsync(before.Id).Result;
        Assert.True(again!.IsEnabled);
    }

    [Fact]
    public void 切换不存在的工具返回空()
    {
        var svc = new McpService(null);
        Assert.Null(svc.ToggleToolAsync("no_such_tool").Result);
    }

    [Fact]
    public void 测试工具禁用时失败并提示()
    {
        var svc = new McpService(null);
        var disabled = svc.GetToolsAsync("mcp-github-api").Result.First(t => !t.IsEnabled);

        var result = svc.TestToolAsync(disabled.Id).Result;

        Assert.False(result.Success);
        Assert.Contains("禁用", result.Message);
    }

    [Fact]
    public void 测试启用工具成功()
    {
        var svc = new McpService(null);
        var enabled = svc.GetToolsAsync("mcp-forgeself-local").Result.First(t => t.IsEnabled);

        var result = svc.TestToolAsync(enabled.Id).Result;

        Assert.True(result.Success);
        Assert.Contains("测试通过", result.Message);
    }

    [Fact]
    public void 测试不存在的工具返回失败()
    {
        var svc = new McpService(null);

        var result = svc.TestToolAsync("no_such_tool").Result;

        Assert.False(result.Success);
        Assert.Contains("不存在", result.Message);
    }

    [Fact]
    public void 测试服务器连接状态()
    {
        var svc = new McpService(null);

        var connected = svc.TestServerAsync("mcp-filesystem").Result;
        Assert.True(connected.Success);
        Assert.Contains("连接正常", connected.Message);

        var disconnected = svc.TestServerAsync("mcp-github-api").Result;
        Assert.False(disconnected.Success);
        Assert.Contains("连接失败", disconnected.Message);
    }
}

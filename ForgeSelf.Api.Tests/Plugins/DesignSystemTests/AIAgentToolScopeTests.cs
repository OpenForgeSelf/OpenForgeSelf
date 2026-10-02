using ForgeSelf.Api.Plugins.AIAgent.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>步骤 11：内置 AIAgent 工具作用域白名单——本插件 + memory-system + design-system（M1 新增）。</summary>
public class AIAgentToolScopeTests
{
    [Fact]
    public void 白名单_含本插件_记忆系统_设计系统()
    {
        var ids = AIAgentService.ToolScopePluginIds("ai-agent");
        ids.Should().Contain("ai-agent");
        ids.Should().Contain("memory-system");
        ids.Should().Contain("design-system");
    }

    [Fact]
    public void 白名单_消费方用大小写不敏感集合_数量固定()
    {
        var ids = AIAgentService.ToolScopePluginIds("x");
        ids.Count().Should().Be(3);
        // ResolveOwnToolDefinitions 用 OrdinalIgnoreCase HashSet 消费：大小写不影响命中
        var set = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
        set.Contains("DESIGN-SYSTEM").Should().BeTrue();
        set.Contains("Memory-System").Should().BeTrue();
    }
}

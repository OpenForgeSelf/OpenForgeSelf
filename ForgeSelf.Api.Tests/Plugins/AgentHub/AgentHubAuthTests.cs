using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.AgentHub;

/// <summary>
/// 铁律 17：AgentHub 管理面控制器必须类级 <c>[Authorize("ApiKeyPolicy")]</c>。
/// </summary>
public class AgentHubAuthTests
{
    private const string ApiKeyPolicy = "ApiKeyPolicy";

    private static IEnumerable<Type> Controllers()
    {
        var asm = typeof(ForgeSelf.Api.Plugins.AgentHub.Controllers.AgentHubAgentsController).Assembly;
        return asm.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t));
    }

    [Fact]
    public void AgentHub程序集内每个控制器都带类级ApiKeyPolicy()
    {
        var list = Controllers().ToList();
        list.Should().NotBeEmpty("AgentHub 至少应有一个控制器");

        foreach (var t in list)
        {
            var attr = t.GetCustomAttribute<AuthorizeAttribute>();
            attr.Should().NotBeNull($"{t.Name} 必须类级带 [Authorize(\"{ApiKeyPolicy}\")]");
            attr!.Policy.Should().Be(ApiKeyPolicy, $"{t.Name} 的策略必须是 {ApiKeyPolicy}");
        }
    }

    [Theory]
    [InlineData(typeof(ForgeSelf.Api.Plugins.AgentHub.Controllers.AgentHubAgentsController))]
    [InlineData(typeof(ForgeSelf.Api.Plugins.AgentHub.Controllers.AgentHubTasksController))]
    public void Agents与Tasks控制器显式ApiKeyPolicy(Type controllerType)
    {
        var attr = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        attr.Should().NotBeNull();
        attr!.Policy.Should().Be(ApiKeyPolicy);
    }
}

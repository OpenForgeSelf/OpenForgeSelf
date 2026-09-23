using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.McpCenterTests;

/// <summary>
/// 管理面鉴权回归测试（验收标准）：MCP 中心全部管理控制器必须类级带
/// [Authorize("ApiKeyPolicy")]，未带宿主 API 令牌访问一律 401。
/// 宿主对照：Controllers/AIProviderController.cs:21 同款策略。
/// </summary>
public class McpAdminAuthTests
{
    private const string ApiKeyPolicy = "ApiKeyPolicy";

    private static readonly (Type Type, string Route)[] AdminControllers =
    {
        (typeof(ForgeSelf.Api.Plugins.McpCenter.Controllers.McpController), "api/mcp"),
        (typeof(ForgeSelf.Api.Plugins.McpCenter.Controllers.McpExternalController), "api/mcp-center/servers"),
        (typeof(ForgeSelf.Api.Plugins.McpCenter.Controllers.McpCenterConfigController), "api/mcp-center/config")
    };

    [Theory]
    [MemberData(nameof(Controllers))]
    public void AdminController_Must_Have_ApiKeyPolicy_Authorize(Type controllerType, string route)
    {
        var attr = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        Assert.True(attr != null, $"{route} 控制器必须带 [Authorize(\"{ApiKeyPolicy}\")]");
        Assert.Equal(ApiKeyPolicy, attr!.Policy);
    }

    public static IEnumerable<object[]> Controllers =>
        AdminControllers.Select(c => new object[] { c.Type, c.Route });
}

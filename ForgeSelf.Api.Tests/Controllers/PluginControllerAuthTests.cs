using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace ForgeSelf.Api.Tests.Controllers;

/// <summary>
/// 宿主插件管理面鉴权回归测试（铁律 17 / 035 遗留 ④）：PluginController（插件列表/启停/
/// 安装/更新/回滚/设置）必须类级带 [Authorize("ApiKeyPolicy")]，未带宿主 API 令牌访问一律 401。
/// 对照：Controllers/AIProviderController.cs 同款策略；插件侧参照 McpAdminAuthTests。
/// </summary>
public class PluginControllerAuthTests
{
    private const string ApiKeyPolicy = "ApiKeyPolicy";

    [Fact]
    public void PluginController_Must_Have_ApiKeyPolicy_Authorize()
    {
        var attr = typeof(ForgeSelf.Api.Controllers.PluginController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.True(attr != null, "PluginController 必须带 [Authorize(\"ApiKeyPolicy\")]（插件管理面全端点）");
        Assert.Equal(ApiKeyPolicy, attr!.Policy);
    }
}

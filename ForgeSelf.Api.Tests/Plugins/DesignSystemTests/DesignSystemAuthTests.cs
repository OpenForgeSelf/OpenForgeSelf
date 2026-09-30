using System.Reflection;
using ForgeSelf.Api.Plugins.DesignSystem;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.DesignSystemTests;

/// <summary>
/// 管理面鉴权回归（AC15 / plugin-development 铁律 17）：
/// 宿主没有全局鉴权中间件，鉴权是逐控制器显式的；插件控制器经子 provider 注册进宿主 Kestrel，
/// 不加类级策略就等于裸 curl 可读全部设计系统数据。同 McpAdminAuthTests 的判据。
/// </summary>
public class DesignSystemAuthTests
{
    const string ApiKeyPolicy = "ApiKeyPolicy";

    static IEnumerable<Type> Controllers =>
        Assembly.GetAssembly(typeof(ForgeSelf.Api.Plugins.DesignSystem.Controllers.DesignSystemController))!
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract &&
                        (typeof(ControllerBase).IsAssignableFrom(t) || t.GetCustomAttribute<RouteAttribute>() != null));

    [Fact]
    public void 设计系统程序集内每个控制器都带ApiKeyPolicy()
    {
        var list = Controllers.ToList();
        list.Should().NotBeEmpty("DesignSystem 插件至少应有一个控制器");

        foreach (var t in list)
        {
            var attr = t.GetCustomAttribute<AuthorizeAttribute>();
            attr.Should().NotBeNull($"{t.Name} 是管理面控制器，必须类级带 [Authorize(\"{ApiKeyPolicy}\")]");
            attr!.Policy.Should().Be(ApiKeyPolicy, $"{t.Name} 的策略必须是 {ApiKeyPolicy}");
        }
    }

    [Fact]
    public void 路由前缀是单数api加插件id_且不与他插件冲突()
    {
        var route = typeof(ForgeSelf.Api.Plugins.DesignSystem.Controllers.DesignSystemController)
            .GetCustomAttribute<RouteAttribute>()!.Template;
        route.Should().Be("api/design-system");
    }

    [Fact]
    public void 清单契约四项未变()
    {
        // 前端契约变更会连带宿主路由/菜单 e2e 失败，这里把契约钉死在测试里
        var json = File.ReadAllText(Path.Combine(FindRepoRoot(), "Plugins", "DesignSystem", "plugin.json"));
        json.Should().Contain("\"views\": [\"DesignSystemView\"]");
        json.Should().Contain("\"route\": \"/design-system\"");
        json.Should().Contain("\"entry\": \"web/dist/index.js\"");
        json.Should().NotContain("example.com", "占位 IconUrl 不得留在清单里");
        // 版本号不钉死字面量（升级即假失败）：清单版本必须等于后端自报的模型版本，
        // 这条与 e2e 的"plugin.json == meta.modelVersion == 界面徽标"是同一份契约。
        json.Should().Contain($"\"Version\": \"{DesignSystemConstants.ModelVersion}\"",
            "plugin.json 版本必须与后端模型版本一致，否则界面徽标与清单自相矛盾");
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("找不到仓库根");
    }
}

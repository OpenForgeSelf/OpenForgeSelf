using System.Reflection;
using System.Text.Json;
using ForgeSelf.Api.Plugins.ToolBridge;
using ForgeSelf.Api.Plugins.ToolBridge.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForgeSelf.Api.Tests.Plugins.ToolBridgeTests;

/// <summary>
/// AC12 + 清单契约 + D3 的机器钉子。
/// 宿主**没有全局鉴权中间件**（策略声明 <c>ForgeSelf.Api/AppBuilder.cs:296-301</c>），鉴权逐控制器显式；
/// 本插件的端点会在用户机器上写文件、起进程 ⇒ 漏一条 [Authorize] 就是"无 token 也能操作这台机器"。
/// 判据形状照 <c>DesignSystemAuthTests.cs:18-35</c>（反射扫程序集，不靠自觉）。
/// </summary>
public class ToolBridgeAuthTests
{
    private const string ApiKeyPolicyName = "ApiKeyPolicy";

    private static Assembly PluginAssembly => Assembly.GetAssembly(typeof(ToolBridgeController))!;

    private static IEnumerable<Type> Controllers =>
        PluginAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract &&
                        (typeof(ControllerBase).IsAssignableFrom(t) || t.GetCustomAttribute<RouteAttribute>() != null));

    [Fact]
    public void 程序集内每个控制器都带类级ApiKeyPolicy_AC12()
    {
        var list = Controllers.ToList();
        list.Should().NotBeEmpty("ToolBridge 至少应有一个控制器");

        foreach (var t in list)
        {
            var attr = t.GetCustomAttribute<AuthorizeAttribute>();
            attr.Should().NotBeNull($"{t.Name} 是管理面控制器，必须类级带 [Authorize(\"{ApiKeyPolicyName}\")]");
            attr!.Policy.Should().Be(ApiKeyPolicyName, $"{t.Name} 的策略必须是 {ApiKeyPolicyName}");
        }
    }

    [Fact]
    public void 路由前缀是api加插件id()
    {
        typeof(ToolBridgeController).GetCustomAttribute<RouteAttribute>()!.Template
            .Should().Be("api/tool-bridge");
    }

    [Fact]
    public void 所有公开端点方法都不带允许匿名特性()
    {
        // 防止有人给单个端点开 [AllowAnonymous] 后门（端点级放行等于绕过类级策略）。
        foreach (var t in Controllers)
        {
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                m.GetCustomAttribute<AllowAnonymousAttribute>().Should().BeNull($"{t.Name}.{m.Name} 不得匿名放行");
            }
        }
    }

    [Fact]
    public void 清单四项与程序集EntryType一致()
    {
        var json = File.ReadAllText(Path.Combine(FindRepoRoot(), "Plugins", "ToolBridge", "plugin.json"));
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("Id").GetString().Should().Be("tool-bridge", "运行时 id 用 kebab-case（铁律 2）");
        root.GetProperty("EntryAssembly").GetString().Should().Be("ToolBridge.dll");
        root.GetProperty("EntryType").GetString().Should().Be("ForgeSelf.Api.Plugins.ToolBridge.ToolBridgePlugin");
        root.GetProperty("frontend").GetProperty("views")[0].GetString().Should().Be("ToolBridgeView");
        root.GetProperty("frontend").GetProperty("route").GetString().Should().Be("/tool-bridge");
        root.GetProperty("frontend").GetProperty("entry").GetString().Should().Be("web/dist/index.js");
        json.Should().NotContain("example.com", "占位 IconUrl 不得留在清单里");
    }

    [Fact]
    public void EntryType指向的类型确实实现IPlugin且程序集名匹配()
    {
        var type = PluginAssembly.GetType("ForgeSelf.Api.Plugins.ToolBridge.ToolBridgePlugin");
        type.Should().NotBeNull();
        typeof(ForgeSelf.Abstractions.IPlugin).IsAssignableFrom(type!).Should().BeTrue();
        PluginAssembly.GetName().Name.Should().Be("ToolBridge");
    }

    [Fact]
    public void 本插件不向宿主注册工具扩展点_D3()
    {
        // 03-plan 决策 D3：不注册（避免与 aiagent.read_file/write_file/list_files 同名并存、
        // 也避免撑大内置 agent 的 prompt）。要接进内置 agent 须另立批次出 ADR。
        var plugin = new ToolBridgePlugin();
        plugin.ToolExtensions.Should().BeEmpty();
        plugin.MenuExtensions.Should().BeEmpty("菜单/路由只在 plugin.json frontend 声明一处（铁律 19①）");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("找不到仓库根");
    }
}

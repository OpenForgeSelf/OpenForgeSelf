using System.Reflection;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Controllers;
using ForgeSelf.Api.Models.Plugins;
using ForgeSelf.Api.Plugins;
using ForgeSelf.Api.Plugins.Abstractions;
using ForgeSelf.Api.Plugins.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeSelf.Api.Tests.Plugins;

/// <summary>
/// 批次A-D2：GetMenuItems 合并 manifest 派生项的回归测试（AC-8 三例）。
/// </summary>
public class PluginMenuItemsMergeTests
{
    private readonly TempPluginDirectory _tempDir;
    private readonly PluginManager _manager;

    public PluginMenuItemsMergeTests()
    {
        _tempDir = new TempPluginDirectory();
        var services = new ServiceCollection();
        _manager = new PluginManager(services.BuildServiceProvider(), Mock.Of<IPermissionChecker>());
        _manager.SetPluginsDirectory(_tempDir.RootPath);
    }

    [Fact]
    public void GetMenuItems_EnabledManifestPlugin_DerivesItemWithCorrectFields()
    {
        // 已启用且声明 frontend.menu 的纯 manifest 插件：按 manifest 机械派生，Id=<pluginId>.menu.manifest，
        // Name/Path/Icon 与 frontend.menu/route/icon 逐字段相等（AC-4/AC-8①）。
        CreatePlugin("mcp-center", "MCP 中心", "/mcp-center", "connection");
        SetStateRunning("mcp-center");

        var items = GetMenuItems();

        var derived = items.Should().ContainSingle(i => i.PluginId == "mcp-center").Subject;
        derived.Id.Should().Be("mcp-center.menu.manifest");
        derived.Name.Should().Be("MCP 中心");
        derived.Path.Should().Be("/mcp-center");
        derived.Icon.Should().Be("connection");
    }

    [Fact]
    public void GetMenuItems_PluginWithMenuExtension_NotDuplicated()
    {
        // 同插件已有 IMenuExtension 项时跳过 manifest 项防双发（AC-5/AC-8②），保留扩展点原值。
        CreatePlugin("quick-links", "快捷链接", "/quick-links", "fa-link");
        SetStateRunning("quick-links");
        var extensionPointManager = new ExtensionPointManager(_manager);
        extensionPointManager.RegisterExtension<IMenuExtension>(new FakeMenuExtension
        {
            Id = "quicklinks.menu.main",
            Name = "快捷链接",
            PluginId = "quick-links",
            Icon = "fa-link",
            Path = "/quick-links",
            Order = 100
        });

        var items = GetMenuItems(extensionPointManager);

        items.Should().ContainSingle(i => i.PluginId == "quick-links")
            .Which.Id.Should().Be("quicklinks.menu.main");
    }

    [Fact]
    public void GetMenuItems_DisabledManifestPlugin_NotEmitted()
    {
        // 禁用（未运行）插件不发（AC-2 口径 + AC-8③）：DiscoverPlugins 后未启用 → 状态非 Running。
        CreatePlugin("design-system", "设计系统", "/design-system", "fa-palette");

        var items = GetMenuItems();

        items.Should().NotContain(i => i.PluginId == "design-system");
    }

    private void CreatePlugin(string pluginId, string menu, string route, string icon)
    {
        _tempDir.CreatePluginManifest(pluginId, m =>
        {
            m.Frontend = new FrontendContributes
            {
                Views = new List<string> { "MainView" },
                Menu = menu,
                Route = route,
                Icon = icon,
                Entry = "web/dist/index.js"
            };
        });
        _manager.DiscoverPlugins();
    }

    private void SetStateRunning(string pluginId)
    {
        var field = typeof(PluginManager).GetField("_pluginStates", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var states = (System.Collections.Concurrent.ConcurrentDictionary<string, PluginState>)field.GetValue(_manager)!;
        states.AddOrUpdate(pluginId, PluginState.Running, (_, _) => PluginState.Running);
    }

    private List<PluginMenuItemDto> GetMenuItems(ExtensionPointManager? extensionPointManager = null)
    {
        extensionPointManager ??= new ExtensionPointManager(_manager);
        var packagerService = new PluginPackagerService(_manager);
        var versionService = new PluginVersionService(_manager);
        var controller = new PluginController(
            _manager,
            extensionPointManager,
            versionService,
            packagerService,
            new PluginInstallerService(_manager, packagerService, versionService),
            new PluginScaffolderService());

        var ok = controller.GetMenuItems().Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>().Subject;
        return ok.Value.Should().BeAssignableTo<ApiResponse<List<PluginMenuItemDto>>>().Subject.Data;
    }

    private class FakeMenuExtension : IMenuExtension
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string PluginId { get; init; } = string.Empty;
        public string Icon { get; init; } = string.Empty;
        public string Path { get; init; } = string.Empty;
        public int Order { get; init; }
        public string? ParentId { get; init; }
        public IReadOnlyList<IMenuExtension>? Children => null;
    }
}

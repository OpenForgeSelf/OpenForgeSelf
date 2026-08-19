using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Backend.Controllers;
using OpenForgeSelf.Backend.Models.Plugins;
using OpenForgeSelf.Backend.Plugins;
using OpenForgeSelf.Backend.Plugins.Abstractions;
using OpenForgeSelf.Backend.Plugins.Services;

namespace OpenForgeSelf.Backend.Tests.Plugins;

public class PluginFrontendManifestTests : IDisposable
{
    private readonly TempPluginDirectory _tempDir;
    private readonly PluginManager _manager;

    public PluginFrontendManifestTests()
    {
        _tempDir = new TempPluginDirectory();
        var services = new ServiceCollection();
        _manager = new PluginManager(services.BuildServiceProvider(), Mock.Of<IPermissionChecker>());
        _manager.SetPluginsDirectory(_tempDir.RootPath);
    }

    [Fact]
    public void DiscoverPlugins_FrontendBlock_IsDeserialized()
    {
        // plugin.json 中的 frontend 块（camelCase 序列化 → 大小写不敏感反序列化）应被解析到 PluginMetadata.Frontend。
        _tempDir.CreatePluginManifest("memory.plugin", m =>
        {
            m.Frontend = new FrontendContributes
            {
                Views = new List<string> { "MemoryView" },
                Menu = "记忆",
                Route = "/memory",
                Icon = "fa-brain"
            };
        });

        _manager.DiscoverPlugins();

        var metadata = _manager.GetPluginMetadata("memory.plugin");
        metadata.Should().NotBeNull();
        metadata!.Frontend.Should().NotBeNull();
        metadata.Frontend!.Views.Should().ContainSingle("MemoryView");
        metadata.Frontend.Menu.Should().Be("记忆");
        metadata.Frontend.Route.Should().Be("/memory");
        metadata.Frontend.Icon.Should().Be("fa-brain");
    }

    [Fact]
    public void DiscoverPlugins_ProvidesConsumes_AreDeserialized()
    {
        // plugin.json 中的 provides/consumes 应被反序列化到 PluginMetadata；缺失时保持默认空列表。
        _tempDir.CreatePluginManifest("capability.plugin", m =>
        {
            m.Provides = new List<string> { "chat.completion", "tool.calling" };
            m.Consumes = new List<string> { "config.read" };
        });
        _tempDir.CreatePluginManifest("plain.plugin"); // 无 provides/consumes 字段

        _manager.DiscoverPlugins();

        var metadata = _manager.GetPluginMetadata("capability.plugin");
        metadata.Should().NotBeNull();
        metadata!.Provides.Should().Equal("chat.completion", "tool.calling");
        metadata.Consumes.Should().Equal("config.read");

        var plain = _manager.GetPluginMetadata("plain.plugin");
        plain.Should().NotBeNull();
        plain!.Provides.Should().BeEmpty();
        plain.Consumes.Should().BeEmpty();
    }

    [Fact]
    public void GetFrontendManifest_ReturnsPluginsWithFrontend()
    {
        _tempDir.CreatePluginManifest("memory.plugin", m =>
        {
            m.Frontend = new FrontendContributes
            {
                Views = new List<string> { "MemoryView" },
                Menu = "记忆",
                Route = "/memory",
                Icon = "fa-brain"
            };
        });
        _tempDir.CreatePluginManifest("plain.plugin"); // 无 frontend 块
        _manager.DiscoverPlugins();

        var controller = CreateController();

        var result = controller.GetFrontendManifest();

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeAssignableTo<ApiResponse<List<PluginFrontendManifestDto>>>().Subject;

        payload.Success.Should().BeTrue();
        payload.Data.Should().NotBeNull();
        payload.Data.Should().HaveCount(2);

        var memory = payload.Data.Should().ContainSingle(m => m.Id == "memory.plugin").Subject;
        memory.Name.Should().NotBeEmpty();
        memory.Frontend.Should().NotBeNull();
        memory.Frontend!.Menu.Should().Be("记忆");
        memory.Frontend.Views.Should().ContainSingle("MemoryView");
        memory.Frontend.Route.Should().Be("/memory");
        memory.Frontend.Icon.Should().Be("fa-brain");
        // 仅发现未加载 → IsEnabled 应为 false（状态字段存在且语义正确）
        memory.IsEnabled.Should().BeFalse();

        var plain = payload.Data.Should().ContainSingle(m => m.Id == "plain.plugin").Subject;
        plain.Frontend.Should().BeNull();
    }

    private PluginController CreateController()
    {
        var extensionPointManager = new ExtensionPointManager(_manager);
        var versionService = new PluginVersionService(_manager);
        var packagerService = new PluginPackagerService(_manager);
        var installerService = new PluginInstallerService(_manager, packagerService, versionService);
        var scaffolderService = new PluginScaffolderService();

        return new PluginController(
            _manager,
            extensionPointManager,
            versionService,
            packagerService,
            installerService,
            scaffolderService);
    }

    public void Dispose()
    {
        _tempDir.Dispose();
    }
}

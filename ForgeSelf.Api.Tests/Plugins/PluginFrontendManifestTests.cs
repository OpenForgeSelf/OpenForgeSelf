using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Controllers;
using ForgeSelf.Api.Models.Plugins;
using ForgeSelf.Api.Plugins;
using ForgeSelf.Api.Plugins.Abstractions;
using ForgeSelf.Api.Plugins.Services;

namespace ForgeSelf.Api.Tests.Plugins;

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

    [Fact]
    public void DiscoverPlugins_Frontend_Entry_IsDeserialized_AndValidPath()
    {
        // T011：AI 代理插件 plugin.json（camelCase 的 frontend.entry）应反序列化到 FrontendContributes.Entry，
        // 且声明路径必须落在 web/dist/ 下的合法相对路径（契约入口固定 index.js）。
        _tempDir.CreatePluginManifest("ai-agent.plugin", m =>
        {
            m.Frontend = new FrontendContributes
            {
                Views = new List<string> { "AiAgentView" },
                Menu = "AI Agent",
                Route = "/ai-agent",
                Icon = "fa-robot",
                Entry = "web/dist/index.js"
            };
        });

        _manager.DiscoverPlugins();

        var metadata = _manager.GetPluginMetadata("ai-agent.plugin");
        metadata.Should().NotBeNull();
        metadata!.Frontend.Should().NotBeNull();
        metadata.Frontend!.Entry.Should().Be("web/dist/index.js");

        // 路径校验合法：以 web/dist/ 开头、以可静态化扩展名结尾、不含穿越斜杠。
        var entry = metadata.Frontend.Entry!;
        entry.StartsWith("web/dist/", StringComparison.OrdinalIgnoreCase).Should().BeTrue();
        entry.Contains("..").Should().BeFalse();
        Path.GetExtension(entry).Should().Be(".js");
    }

    [Fact]
    public void GetFrontendManifest_Returns_Entry_WhenDeclared()
    {
        // T011：清单接口应把 Entry 随 Frontend 一并下发，供前端拼装资源 URL（?v=）。
        _tempDir.CreatePluginManifest("ai-agent.plugin", m =>
        {
            m.Frontend = new FrontendContributes
            {
                Views = new List<string> { "AiAgentView" },
                Menu = "AI Agent",
                Route = "/ai-agent",
                Icon = "fa-robot",
                Entry = "web/dist/index.js"
            };
        });
        _manager.DiscoverPlugins();

        var controller = CreateController();

        var result = controller.GetFrontendManifest();
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeAssignableTo<ApiResponse<List<PluginFrontendManifestDto>>>().Subject;

        var plugin = payload.Data.Should().ContainSingle(p => p.Id == "ai-agent.plugin").Subject;
        plugin.Frontend.Should().NotBeNull();
        plugin.Frontend!.Entry.Should().Be("web/dist/index.js");
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

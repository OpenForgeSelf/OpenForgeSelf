using System.Reflection;
using OpenForgeSelf.Backend.Plugins;
using OpenForgeSelf.Backend.Plugins.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace OpenForgeSelf.Backend.Tests.Plugins;

public class ExtensionPointManagerTests
{
    private readonly PluginManager _pluginManager;
    private readonly ExtensionPointManager _manager;

    public ExtensionPointManagerTests()
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var permissionChecker = Mock.Of<IPermissionChecker>();
        _pluginManager = new PluginManager(serviceProvider, permissionChecker);
        _manager = new ExtensionPointManager(_pluginManager);
    }

    [Fact]
    public void RegisterExtension_ValidExtension_ReturnsTrue()
    {
        var extension = new FakeMenuExtension { Id = "test.menu1", PluginId = "test.plugin" };

        var result = _manager.RegisterExtension<IMenuExtension>(extension);

        result.Should().BeTrue();
    }

    [Fact]
    public void RegisterExtension_NullExtension_ReturnsFalse()
    {
        var result = _manager.RegisterExtension<IMenuExtension>(null!);

        result.Should().BeFalse();
    }

    [Fact]
    public void RegisterExtension_DuplicateId_ReturnsFalse()
    {
        var extension1 = new FakeMenuExtension { Id = "test.menu1", PluginId = "test.plugin" };
        var extension2 = new FakeMenuExtension { Id = "test.menu1", PluginId = "test.plugin" };

        _manager.RegisterExtension<IMenuExtension>(extension1);
        var result = _manager.RegisterExtension<IMenuExtension>(extension2);

        result.Should().BeFalse();
    }

    [Fact]
    public void GetExtensions_NoExtensionsRegistered_ReturnsEmpty()
    {
        var extensions = _manager.GetExtensions<IMenuExtension>();

        extensions.Should().BeEmpty();
    }

    [Fact]
    public void GetExtensions_SingleExtension_ReturnsExtension()
    {
        var extension = new FakeMenuExtension { Id = "test.menu1", PluginId = "test.plugin" };
        _manager.RegisterExtension<IMenuExtension>(extension);

        var extensions = _manager.GetExtensions<IMenuExtension>().ToList();

        extensions.Should().HaveCount(1);
        extensions[0].Id.Should().Be("test.menu1");
    }

    [Fact]
    public void GetExtensions_MultipleExtensions_ReturnsAllExtensions()
    {
        var ext1 = new FakeMenuExtension { Id = "test.menu1", PluginId = "test.plugin" };
        var ext2 = new FakeMenuExtension { Id = "test.menu2", PluginId = "test.plugin" };
        var ext3 = new FakeMenuExtension { Id = "test.menu3", PluginId = "test.plugin" };
        _manager.RegisterExtension<IMenuExtension>(ext1);
        _manager.RegisterExtension<IMenuExtension>(ext2);
        _manager.RegisterExtension<IMenuExtension>(ext3);

        var extensions = _manager.GetExtensions<IMenuExtension>().ToList();

        extensions.Should().HaveCount(3);
        extensions.Select(e => e.Id).Should().BeEquivalentTo("test.menu1", "test.menu2", "test.menu3");
    }

    [Fact]
    public void GetExtension_ExistingExtension_ReturnsExtension()
    {
        var extension = new FakeMenuExtension { Id = "test.menu1", PluginId = "test.plugin" };
        _manager.RegisterExtension<IMenuExtension>(extension);

        var result = _manager.GetExtension<IMenuExtension>("test.menu1");

        result.Should().NotBeNull();
        result!.Id.Should().Be("test.menu1");
    }

    [Fact]
    public void GetExtension_NonExistingExtension_ReturnsNull()
    {
        var result = _manager.GetExtension<IMenuExtension>("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public void UnregisterExtension_ExistingExtension_ReturnsTrue()
    {
        var extension = new FakeMenuExtension { Id = "test.menu1", PluginId = "test.plugin" };
        _manager.RegisterExtension<IMenuExtension>(extension);

        var result = _manager.UnregisterExtension<IMenuExtension>("test.menu1");

        result.Should().BeTrue();
        _manager.GetExtension<IMenuExtension>("test.menu1").Should().BeNull();
    }

    [Fact]
    public void UnregisterExtension_NonExistingExtension_ReturnsFalse()
    {
        var result = _manager.UnregisterExtension<IMenuExtension>("nonexistent");

        result.Should().BeFalse();
    }

    [Fact]
    public void GetExtensionTypes_NoExtensions_ReturnsEmpty()
    {
        var types = _manager.GetExtensionTypes();

        types.Should().BeEmpty();
    }

    [Fact]
    public void GetExtensionTypes_WithExtensions_ReturnsTypes()
    {
        var menuExt = new FakeMenuExtension { Id = "test.menu1", PluginId = "test.plugin" };
        var toolExt = new FakeToolFunctionExtension { Id = "test.tool1", PluginId = "test.plugin" };
        _manager.RegisterExtension<IMenuExtension>(menuExt);
        _manager.RegisterExtension<IToolFunctionExtension>(toolExt);

        var types = _manager.GetExtensionTypes().ToList();

        types.Should().Contain(typeof(IMenuExtension));
        types.Should().Contain(typeof(IToolFunctionExtension));
    }

    [Fact]
    public void GetPluginExtensions_NoExtensionsForPlugin_ReturnsEmpty()
    {
        var extensions = _manager.GetPluginExtensions("test.plugin");

        extensions.Should().BeEmpty();
    }

    [Fact]
    public void GetPluginExtensions_WithPluginExtensions_ReturnsAllExtensionsForPlugin()
    {
        var ext1 = new FakeMenuExtension { Id = "test.menu1", PluginId = "plugin.a" };
        var ext2 = new FakeMenuExtension { Id = "test.menu2", PluginId = "plugin.a" };
        var ext3 = new FakeMenuExtension { Id = "test.menu3", PluginId = "plugin.b" };
        _manager.RegisterExtension<IMenuExtension>(ext1);
        _manager.RegisterExtension<IMenuExtension>(ext2);
        _manager.RegisterExtension<IMenuExtension>(ext3);

        var extensions = _manager.GetPluginExtensions("plugin.a").ToList();

        extensions.Should().HaveCount(2);
        extensions.Select(e => e.Id).Should().BeEquivalentTo("test.menu1", "test.menu2");
    }

    [Fact]
    public void RemovePluginExtensions_ExistingPlugin_RemovesAllExtensions()
    {
        var ext1 = new FakeMenuExtension { Id = "test.menu1", PluginId = "plugin.a" };
        var ext2 = new FakeMenuExtension { Id = "test.menu2", PluginId = "plugin.a" };
        var ext3 = new FakeMenuExtension { Id = "test.menu3", PluginId = "plugin.b" };
        _manager.RegisterExtension<IMenuExtension>(ext1);
        _manager.RegisterExtension<IMenuExtension>(ext2);
        _manager.RegisterExtension<IMenuExtension>(ext3);

        _manager.RemovePluginExtensions("plugin.a");

        _manager.GetPluginExtensions("plugin.a").Should().BeEmpty();
        _manager.GetPluginExtensions("plugin.b").Should().HaveCount(1);
    }

    [Fact]
    public void RemovePluginExtensions_NonExistingPlugin_DoesNotThrow()
    {
        var action = () => _manager.RemovePluginExtensions("nonexistent");

        action.Should().NotThrow();
    }

    [Fact]
    public void DiscoverExtensionsFromPlugin_PluginNotExists_DoesNotThrow()
    {
        var action = () => _manager.DiscoverExtensionsFromPlugin("nonexistent");

        action.Should().NotThrow();
    }

    [Fact]
    public void DiscoverExtensionsFromPlugin_PluginWithMenuExtensions_DiscoversExtensions()
    {
        var plugin = new FakeMenuPlugin
        {
            Id = "test.menu.plugin",
            Name = "Test Menu Plugin"
        };
        plugin.AddMenuExtension(new FakeMenuExtension
        {
            Id = "menu.test1",
            Name = "Test Menu 1",
            PluginId = "test.menu.plugin"
        });
        plugin.AddMenuExtension(new FakeMenuExtension
        {
            Id = "menu.test2",
            Name = "Test Menu 2",
            PluginId = "test.menu.plugin"
        });

        InjectPluginIntoManager(plugin);

        _manager.DiscoverExtensionsFromPlugin("test.menu.plugin");

        var menus = _manager.GetExtensions<IMenuExtension>().ToList();
        menus.Should().HaveCount(2);
        menus.Select(m => m.Id).Should().BeEquivalentTo("menu.test1", "menu.test2");
    }

    [Fact]
    public void DiscoverExtensionsFromPlugin_PluginWithToolExtensions_DiscoversExtensions()
    {
        var plugin = new FakeToolPlugin
        {
            Id = "test.tool.plugin",
            Name = "Test Tool Plugin"
        };
        plugin.AddToolExtension(new FakeToolFunctionExtension
        {
            Id = "tool.test1",
            Name = "Test Tool 1",
            PluginId = "test.tool.plugin"
        });

        InjectPluginIntoManager(plugin);

        _manager.DiscoverExtensionsFromPlugin("test.tool.plugin");

        var tools = _manager.GetExtensions<IToolFunctionExtension>().ToList();
        tools.Should().HaveCount(1);
        tools[0].Id.Should().Be("tool.test1");
    }

    [Fact]
    public void RegisterExtension_MenuExtension_CanRetrieveByMenuType()
    {
        var extension = new FakeMenuExtension
        {
            Id = "menu.settings",
            Name = "Settings",
            PluginId = "test.plugin",
            Icon = "fa-cog",
            Path = "/settings",
            Order = 10
        };

        _manager.RegisterExtension<IMenuExtension>(extension);

        var menus = _manager.GetExtensions<IMenuExtension>().ToList();
        menus.Should().HaveCount(1);
        menus[0].Id.Should().Be("menu.settings");
        menus[0].Name.Should().Be("Settings");
        menus[0].Icon.Should().Be("fa-cog");
        menus[0].Path.Should().Be("/settings");
        menus[0].Order.Should().Be(10);
    }

    [Fact]
    public void RegisterExtension_ToolFunctionExtension_CanRetrieveByToolType()
    {
        var extension = new FakeToolFunctionExtension
        {
            Id = "tool.format",
            Name = "Format JSON",
            PluginId = "test.plugin",
            Description = "Formats JSON string",
            ParametersJsonSchema = "{\"type\":\"object\"}"
        };

        _manager.RegisterExtension<IToolFunctionExtension>(extension);

        var tools = _manager.GetExtensions<IToolFunctionExtension>().ToList();
        tools.Should().HaveCount(1);
        tools[0].Id.Should().Be("tool.format");
        tools[0].Description.Should().Be("Formats JSON string");
    }

    [Fact]
    public void RegisterExtension_DifferentTypes_DoNotInterfere()
    {
        var menuExt = new FakeMenuExtension { Id = "item1", PluginId = "test.plugin" };
        var toolExt = new FakeToolFunctionExtension { Id = "item1", PluginId = "test.plugin" };

        _manager.RegisterExtension<IMenuExtension>(menuExt);
        _manager.RegisterExtension<IToolFunctionExtension>(toolExt);

        _manager.GetExtensions<IMenuExtension>().Should().HaveCount(1);
        _manager.GetExtensions<IToolFunctionExtension>().Should().HaveCount(1);
    }

    [Fact]
    public void GetPluginExtensions_MultipleTypes_ReturnsAllExtensionsOfPlugin()
    {
        var menuExt = new FakeMenuExtension { Id = "menu1", PluginId = "plugin.a" };
        var toolExt = new FakeToolFunctionExtension { Id = "tool1", PluginId = "plugin.a" };
        var otherExt = new FakeMenuExtension { Id = "menu2", PluginId = "plugin.b" };

        _manager.RegisterExtension<IMenuExtension>(menuExt);
        _manager.RegisterExtension<IToolFunctionExtension>(toolExt);
        _manager.RegisterExtension<IMenuExtension>(otherExt);

        var extensions = _manager.GetPluginExtensions("plugin.a").ToList();

        extensions.Should().HaveCount(2);
        extensions.Select(e => e.Id).Should().BeEquivalentTo("menu1", "tool1");
    }

    private void InjectPluginIntoManager(IPlugin plugin)
    {
        var pluginsField = typeof(PluginManager).GetField(
            "_plugins",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var statesField = typeof(PluginManager).GetField(
            "_pluginStates",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var metadatasField = typeof(PluginManager).GetField(
            "_metadatas",
            BindingFlags.NonPublic | BindingFlags.Instance);

        pluginsField.Should().NotBeNull();
        statesField.Should().NotBeNull();
        metadatasField.Should().NotBeNull();

        var plugins = (System.Collections.Concurrent.ConcurrentDictionary<string, IPlugin>)pluginsField!.GetValue(_pluginManager)!;
        var states = (System.Collections.Concurrent.ConcurrentDictionary<string, PluginState>)statesField!.GetValue(_pluginManager)!;
        var metadatas = (System.Collections.Concurrent.ConcurrentDictionary<string, PluginMetadata>)metadatasField!.GetValue(_pluginManager)!;

        plugins.TryAdd(plugin.Id, plugin);
        states.TryAdd(plugin.Id, PluginState.Running);
        metadatas.TryAdd(plugin.Id, new PluginMetadata
        {
            Id = plugin.Id,
            Name = plugin.Name,
            Version = plugin.Version
        });
    }
}

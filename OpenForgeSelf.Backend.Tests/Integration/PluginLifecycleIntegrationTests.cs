using System.Reflection;
using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Backend.Plugins;
using OpenForgeSelf.Backend.Plugins.Abstractions;
using OpenForgeSelf.Backend.Tests.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace OpenForgeSelf.Backend.Tests.Integration;

public class PluginLifecycleIntegrationTests : IDisposable
{
    private readonly TempPluginDirectory _tempDir;
    private readonly Mock<IPermissionChecker> _mockPermissionChecker;
    private readonly IServiceProvider _serviceProvider;

    public PluginLifecycleIntegrationTests()
    {
        _tempDir = new TempPluginDirectory();
        _mockPermissionChecker = new Mock<IPermissionChecker>();
        var services = new ServiceCollection();
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void FullLifecycle_SinglePlugin_TransitionsCorrectly()
    {
        var manager = CreateManager();
        var plugin = new FakePlugin();
        var metadata = PluginManifestGenerator.CreateBasic("test.lifecycle");
        SetupPluginWithMetadata(manager, "test.lifecycle", metadata);
        LoadPluginDirectly(manager, "test.lifecycle", plugin);

        manager.GetPluginState("test.lifecycle").Should().Be(PluginState.Loaded);
        plugin.ApplyCalled.Should().BeFalse();

        var initResult = manager.InitializePlugin("test.lifecycle");
        initResult.Should().BeTrue();
        manager.GetPluginState("test.lifecycle").Should().Be(PluginState.Running);
        plugin.ApplyCalled.Should().BeTrue();
        plugin.Context.Should().NotBeNull();

        // 插件在 Apply 中可通过 ctx.Get<PluginMetadata>() 拿到自身 Id
        plugin.Context!.Get<PluginMetadata>()!.Id.Should().Be("test.lifecycle");

        var destroyResult = manager.DestroyPlugin("test.lifecycle");
        destroyResult.Should().BeTrue();
        manager.GetPluginState("test.lifecycle").Should().Be(PluginState.Destroyed);
        manager.GetPlugin("test.lifecycle").Should().BeNull();
    }

    [Fact]
    public void FullLifecycle_MultiplePlugins_InitializeAll()
    {
        var manager = CreateManager();
        var plugin1 = new FakePlugin();
        var plugin2 = new FakePlugin();
        var plugin3 = new FakePlugin();
        var metadata1 = PluginManifestGenerator.CreateBasic("test.multi1");
        var metadata2 = PluginManifestGenerator.CreateBasic("test.multi2");
        var metadata3 = PluginManifestGenerator.CreateBasic("test.multi3");

        SetupPluginWithMetadata(manager, "test.multi1", metadata1);
        SetupPluginWithMetadata(manager, "test.multi2", metadata2);
        SetupPluginWithMetadata(manager, "test.multi3", metadata3);

        LoadPluginDirectly(manager, "test.multi1", plugin1);
        LoadPluginDirectly(manager, "test.multi2", plugin2);
        LoadPluginDirectly(manager, "test.multi3", plugin3);

        manager.LoadedPluginIds.Should().HaveCount(3);

        manager.InitializePlugin("test.multi1");
        manager.InitializePlugin("test.multi2");
        manager.InitializePlugin("test.multi3");

        manager.GetPluginState("test.multi1").Should().Be(PluginState.Running);
        manager.GetPluginState("test.multi2").Should().Be(PluginState.Running);
        manager.GetPluginState("test.multi3").Should().Be(PluginState.Running);

        plugin1.ApplyCalled.Should().BeTrue();
        plugin2.ApplyCalled.Should().BeTrue();
        plugin3.ApplyCalled.Should().BeTrue();
    }

    [Fact]
    public void Lifecycle_ApplyException_StateGoesToError()
    {
        var manager = CreateManager();
        var plugin = new FakePlugin();
        plugin.ApplyException = new InvalidOperationException("Apply failed");
        var metadata = PluginManifestGenerator.CreateBasic("test.apply.error");
        SetupPluginWithMetadata(manager, "test.apply.error", metadata);
        LoadPluginDirectly(manager, "test.apply.error", plugin);

        var result = manager.InitializePlugin("test.apply.error");

        result.Should().BeFalse();
        manager.GetPluginState("test.apply.error").Should().Be(PluginState.Error);
        plugin.ApplyCalled.Should().BeTrue();
    }

    [Fact]
    public void Lifecycle_InitializeWithoutMetadata_Fails()
    {
        var manager = CreateManager();
        var plugin = new FakePlugin();
        LoadPluginDirectly(manager, "test.nometadata", plugin);

        var result = manager.InitializePlugin("test.nometadata");

        result.Should().BeFalse();
        plugin.ApplyCalled.Should().BeFalse();
    }

    [Fact]
    public void Lifecycle_DisablePlugin_DestroysAndRemoves()
    {
        var manager = CreateManager();
        var plugin = new FakePlugin();
        var metadata = PluginManifestGenerator.CreateBasic("test.disable");
        SetupPluginWithMetadata(manager, "test.disable", metadata);
        LoadPluginDirectly(manager, "test.disable", plugin);
        manager.InitializePlugin("test.disable");

        var result = manager.DisablePlugin("test.disable");

        result.Should().BeTrue();
        manager.GetPluginState("test.disable").Should().Be(PluginState.Destroyed);
        manager.GetPlugin("test.disable").Should().BeNull();
    }

    [Fact]
    public void Lifecycle_StopAndUnloadAll_UnloadsAllPlugins()
    {
        var manager = CreateManager();

        var plugin1 = new FakePlugin();
        var plugin2 = new FakePlugin();
        var plugin3 = new FakePlugin();

        var metadata1 = PluginManifestGenerator.CreateBasic("test.order1");
        var metadata2 = PluginManifestGenerator.CreateBasic("test.order2");
        var metadata3 = PluginManifestGenerator.CreateBasic("test.order3");

        SetupPluginWithMetadata(manager, "test.order1", metadata1);
        SetupPluginWithMetadata(manager, "test.order2", metadata2);
        SetupPluginWithMetadata(manager, "test.order3", metadata3);

        LoadPluginDirectly(manager, "test.order1", plugin1);
        LoadPluginDirectly(manager, "test.order2", plugin2);
        LoadPluginDirectly(manager, "test.order3", plugin3);

        manager.InitializePlugin("test.order1");
        manager.InitializePlugin("test.order2");
        manager.InitializePlugin("test.order3");

        manager.LoadedPluginIds.Should().HaveCount(3);

        manager.StopAndUnloadAllPlugins();

        manager.LoadedPluginIds.Should().BeEmpty();
        manager.GetPluginState("test.order1").Should().Be(PluginState.Destroyed);
        manager.GetPluginState("test.order2").Should().Be(PluginState.Destroyed);
        manager.GetPluginState("test.order3").Should().Be(PluginState.Destroyed);
    }

    [Fact]
    public void Lifecycle_StateTransitions_ValidateAllStates()
    {
        var states = Enum.GetValues<PluginState>();
        states.Should().NotBeEmpty();
        states.Should().Contain(PluginState.NotLoaded);
        states.Should().Contain(PluginState.Loaded);
        states.Should().Contain(PluginState.Initializing);
        states.Should().Contain(PluginState.Initialized);
        states.Should().Contain(PluginState.Starting);
        states.Should().Contain(PluginState.Running);
        states.Should().Contain(PluginState.Stopping);
        states.Should().Contain(PluginState.Stopped);
        states.Should().Contain(PluginState.Destroying);
        states.Should().Contain(PluginState.Destroyed);
        states.Should().Contain(PluginState.Error);
    }

    private PluginManager CreateManager()
    {
        return new PluginManager(_serviceProvider, _mockPermissionChecker.Object);
    }

    private static void SetupPluginWithMetadata(PluginManager manager, string pluginId, PluginMetadata metadata)
    {
        var metadatasField = typeof(PluginManager).GetField("_metadatas", BindingFlags.NonPublic | BindingFlags.Instance);
        var pluginStatesField = typeof(PluginManager).GetField("_pluginStates", BindingFlags.NonPublic | BindingFlags.Instance);

        metadatasField.Should().NotBeNull();
        pluginStatesField.Should().NotBeNull();

        var metadatasDict = (System.Collections.Concurrent.ConcurrentDictionary<string, PluginMetadata>)metadatasField!.GetValue(manager)!;
        var pluginStatesDict = (System.Collections.Concurrent.ConcurrentDictionary<string, PluginState>)pluginStatesField!.GetValue(manager)!;

        metadatasDict.AddOrUpdate(pluginId, metadata, (_, _) => metadata);
        pluginStatesDict.AddOrUpdate(pluginId, PluginState.NotLoaded, (_, _) => PluginState.NotLoaded);
    }

    private static void LoadPluginDirectly(PluginManager manager, string pluginId, IPlugin plugin)
    {
        var pluginsField = typeof(PluginManager).GetField("_plugins", BindingFlags.NonPublic | BindingFlags.Instance);
        var pluginStatesField = typeof(PluginManager).GetField("_pluginStates", BindingFlags.NonPublic | BindingFlags.Instance);

        pluginsField.Should().NotBeNull();
        pluginStatesField.Should().NotBeNull();

        var pluginsDict = (System.Collections.Concurrent.ConcurrentDictionary<string, IPlugin>)pluginsField!.GetValue(manager)!;
        var pluginStatesDict = (System.Collections.Concurrent.ConcurrentDictionary<string, PluginState>)pluginStatesField!.GetValue(manager)!;

        pluginsDict.AddOrUpdate(pluginId, plugin, (_, _) => plugin);
        pluginStatesDict.AddOrUpdate(pluginId, PluginState.Loaded, (_, _) => PluginState.Loaded);
    }

    public void Dispose()
    {
        _tempDir.Dispose();
    }
}

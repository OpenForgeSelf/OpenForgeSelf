using System.Reflection;
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
        var plugin = new FakePlugin { Id = "test.lifecycle" };
        var metadata = PluginManifestGenerator.CreateBasic("test.lifecycle");
        SetupPluginWithMetadata(manager, plugin, metadata);

        manager.GetPluginState("test.lifecycle").Should().Be(PluginState.NotLoaded);

        var loadResult = LoadPluginDirectly(manager, plugin, metadata);
        loadResult.Should().BeTrue();
        manager.GetPluginState("test.lifecycle").Should().Be(PluginState.Loaded);
        plugin.InitializeCalled.Should().BeFalse();

        var initResult = manager.InitializePlugin("test.lifecycle");
        initResult.Should().BeTrue();
        manager.GetPluginState("test.lifecycle").Should().Be(PluginState.Initialized);
        plugin.InitializeCalled.Should().BeTrue();
        plugin.StartCalled.Should().BeFalse();

        var startResult = manager.StartPlugin("test.lifecycle");
        startResult.Should().BeTrue();
        manager.GetPluginState("test.lifecycle").Should().Be(PluginState.Running);
        plugin.StartCalled.Should().BeTrue();
        plugin.StopCalled.Should().BeFalse();

        var stopResult = manager.StopPlugin("test.lifecycle");
        stopResult.Should().BeTrue();
        manager.GetPluginState("test.lifecycle").Should().Be(PluginState.Stopped);
        plugin.StopCalled.Should().BeTrue();
        plugin.DestroyCalled.Should().BeFalse();

        var destroyResult = manager.DestroyPlugin("test.lifecycle");
        destroyResult.Should().BeTrue();
        manager.GetPluginState("test.lifecycle").Should().Be(PluginState.Destroyed);
        plugin.DestroyCalled.Should().BeTrue();
        manager.GetPlugin("test.lifecycle").Should().BeNull();
    }

    [Fact]
    public void FullLifecycle_MultiplePlugins_LoadAndStartAll()
    {
        var manager = CreateManager();
        var plugin1 = new FakePlugin { Id = "test.multi1" };
        var plugin2 = new FakePlugin { Id = "test.multi2" };
        var plugin3 = new FakePlugin { Id = "test.multi3" };
        var metadata1 = PluginManifestGenerator.CreateBasic("test.multi1");
        var metadata2 = PluginManifestGenerator.CreateBasic("test.multi2");
        var metadata3 = PluginManifestGenerator.CreateBasic("test.multi3");

        SetupPluginWithMetadata(manager, plugin1, metadata1);
        SetupPluginWithMetadata(manager, plugin2, metadata2);
        SetupPluginWithMetadata(manager, plugin3, metadata3);

        LoadPluginDirectly(manager, plugin1, metadata1);
        LoadPluginDirectly(manager, plugin2, metadata2);
        LoadPluginDirectly(manager, plugin3, metadata3);

        manager.LoadedPluginIds.Should().HaveCount(3);

        manager.InitializePlugin("test.multi1");
        manager.InitializePlugin("test.multi2");
        manager.InitializePlugin("test.multi3");

        manager.StartPlugin("test.multi1");
        manager.StartPlugin("test.multi2");
        manager.StartPlugin("test.multi3");

        manager.GetPluginState("test.multi1").Should().Be(PluginState.Running);
        manager.GetPluginState("test.multi2").Should().Be(PluginState.Running);
        manager.GetPluginState("test.multi3").Should().Be(PluginState.Running);

        plugin1.StartCalled.Should().BeTrue();
        plugin2.StartCalled.Should().BeTrue();
        plugin3.StartCalled.Should().BeTrue();
    }

    [Fact]
    public void Lifecycle_InitializeException_StateGoesToError()
    {
        var manager = CreateManager();
        var plugin = new FakePlugin { Id = "test.init.error" };
        plugin.InitializeException = new InvalidOperationException("Init failed");
        var metadata = PluginManifestGenerator.CreateBasic("test.init.error");
        SetupPluginWithMetadata(manager, plugin, metadata);
        LoadPluginDirectly(manager, plugin, metadata);

        var result = manager.InitializePlugin("test.init.error");

        result.Should().BeFalse();
        manager.GetPluginState("test.init.error").Should().Be(PluginState.Error);
        plugin.InitializeCalled.Should().BeTrue();
    }

    [Fact]
    public void Lifecycle_StartException_StateGoesToError()
    {
        var manager = CreateManager();
        var plugin = new FakePlugin { Id = "test.start.error" };
        plugin.StartException = new InvalidOperationException("Start failed");
        var metadata = PluginManifestGenerator.CreateBasic("test.start.error");
        SetupPluginWithMetadata(manager, plugin, metadata);
        LoadPluginDirectly(manager, plugin, metadata);
        manager.InitializePlugin("test.start.error");

        var result = manager.StartPlugin("test.start.error");

        result.Should().BeFalse();
        manager.GetPluginState("test.start.error").Should().Be(PluginState.Error);
        plugin.StartCalled.Should().BeTrue();
    }

    [Fact]
    public void Lifecycle_StopException_StateGoesToError()
    {
        var manager = CreateManager();
        var plugin = new FakePlugin { Id = "test.stop.error" };
        plugin.StopException = new InvalidOperationException("Stop failed");
        var metadata = PluginManifestGenerator.CreateBasic("test.stop.error");
        SetupPluginWithMetadata(manager, plugin, metadata);
        LoadPluginDirectly(manager, plugin, metadata);
        manager.InitializePlugin("test.stop.error");
        manager.StartPlugin("test.stop.error");

        var result = manager.StopPlugin("test.stop.error");

        result.Should().BeFalse();
        manager.GetPluginState("test.stop.error").Should().Be(PluginState.Error);
        plugin.StopCalled.Should().BeTrue();
    }

    [Fact]
    public void Lifecycle_StartWithoutInitialized_Fails()
    {
        var manager = CreateManager();
        var plugin = new FakePlugin { Id = "test.start.noinit" };
        var metadata = PluginManifestGenerator.CreateBasic("test.start.noinit");
        SetupPluginWithMetadata(manager, plugin, metadata);
        LoadPluginDirectly(manager, plugin, metadata);

        var result = manager.StartPlugin("test.start.noinit");

        result.Should().BeFalse();
        manager.GetPluginState("test.start.noinit").Should().Be(PluginState.Loaded);
        plugin.StartCalled.Should().BeFalse();
    }

    [Fact]
    public void Lifecycle_RestartAfterStop_Succeeds()
    {
        var manager = CreateManager();
        var plugin = new FakePlugin { Id = "test.restart" };
        var metadata = PluginManifestGenerator.CreateBasic("test.restart");
        SetupPluginWithMetadata(manager, plugin, metadata);
        LoadPluginDirectly(manager, plugin, metadata);
        manager.InitializePlugin("test.restart");
        manager.StartPlugin("test.restart");

        manager.StopPlugin("test.restart");
        manager.GetPluginState("test.restart").Should().Be(PluginState.Stopped);

        plugin.Reset();
        var restartResult = manager.StartPlugin("test.restart");

        restartResult.Should().BeTrue();
        manager.GetPluginState("test.restart").Should().Be(PluginState.Running);
        plugin.StartCalled.Should().BeTrue();
    }

    [Fact]
    public void Lifecycle_EnablePlugin_FullLifecycle()
    {
        var manager = CreateManager();
        var plugin = new FakePlugin { Id = "test.enable" };
        var metadata = PluginManifestGenerator.CreateBasic("test.enable");
        SetupPluginWithMetadata(manager, plugin, metadata);

        var result = EnablePluginDirectly(manager, plugin, metadata);

        result.Should().BeTrue();
        manager.GetPluginState("test.enable").Should().Be(PluginState.Running);
        plugin.InitializeCalled.Should().BeTrue();
        plugin.StartCalled.Should().BeTrue();
    }

    [Fact]
    public void Lifecycle_DisablePlugin_FromRunningToDestroyed()
    {
        var manager = CreateManager();
        var plugin = new FakePlugin { Id = "test.disable" };
        var metadata = PluginManifestGenerator.CreateBasic("test.disable");
        SetupPluginWithMetadata(manager, plugin, metadata);
        EnablePluginDirectly(manager, plugin, metadata);

        var result = manager.DisablePlugin("test.disable");

        result.Should().BeTrue();
        manager.GetPluginState("test.disable").Should().Be(PluginState.Destroyed);
        plugin.StopCalled.Should().BeTrue();
        plugin.DestroyCalled.Should().BeTrue();
        manager.GetPlugin("test.disable").Should().BeNull();
    }

    [Fact]
    public void Lifecycle_StopAndUnloadAll_UnloadsAllPlugins()
    {
        var manager = CreateManager();

        var plugin1 = new FakePlugin { Id = "test.order1" };
        var plugin2 = new FakePlugin { Id = "test.order2" };
        var plugin3 = new FakePlugin { Id = "test.order3" };

        var metadata1 = PluginManifestGenerator.CreateBasic("test.order1");
        var metadata2 = PluginManifestGenerator.CreateBasic("test.order2");
        var metadata3 = PluginManifestGenerator.CreateBasic("test.order3");

        SetupPluginWithMetadata(manager, plugin1, metadata1);
        SetupPluginWithMetadata(manager, plugin2, metadata2);
        SetupPluginWithMetadata(manager, plugin3, metadata3);

        LoadPluginDirectly(manager, plugin1, metadata1);
        LoadPluginDirectly(manager, plugin2, metadata2);
        LoadPluginDirectly(manager, plugin3, metadata3);

        manager.InitializePlugin("test.order1");
        manager.InitializePlugin("test.order2");
        manager.InitializePlugin("test.order3");

        manager.StartPlugin("test.order1");
        manager.StartPlugin("test.order2");
        manager.StartPlugin("test.order3");

        manager.LoadedPluginIds.Should().HaveCount(3);

        manager.StopAndUnloadAllPlugins();

        manager.LoadedPluginIds.Should().BeEmpty();
        plugin1.DestroyCalled.Should().BeTrue();
        plugin2.DestroyCalled.Should().BeTrue();
        plugin3.DestroyCalled.Should().BeTrue();
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

    private static void SetupPluginWithMetadata(PluginManager manager, IPlugin plugin, PluginMetadata metadata)
    {
        var metadatasField = typeof(PluginManager).GetField("_metadatas", BindingFlags.NonPublic | BindingFlags.Instance);
        var pluginStatesField = typeof(PluginManager).GetField("_pluginStates", BindingFlags.NonPublic | BindingFlags.Instance);

        metadatasField.Should().NotBeNull();
        pluginStatesField.Should().NotBeNull();

        var metadatasDict = (System.Collections.Concurrent.ConcurrentDictionary<string, PluginMetadata>)metadatasField!.GetValue(manager)!;
        var pluginStatesDict = (System.Collections.Concurrent.ConcurrentDictionary<string, PluginState>)pluginStatesField!.GetValue(manager)!;

        metadatasDict.AddOrUpdate(metadata.Id, metadata, (_, _) => metadata);
        pluginStatesDict.AddOrUpdate(metadata.Id, PluginState.NotLoaded, (_, _) => PluginState.NotLoaded);
    }

    private static bool LoadPluginDirectly(PluginManager manager, IPlugin plugin, PluginMetadata metadata)
    {
        var pluginsField = typeof(PluginManager).GetField("_plugins", BindingFlags.NonPublic | BindingFlags.Instance);
        var loadContextsField = typeof(PluginManager).GetField("_loadContexts", BindingFlags.NonPublic | BindingFlags.Instance);
        var pluginStatesField = typeof(PluginManager).GetField("_pluginStates", BindingFlags.NonPublic | BindingFlags.Instance);

        pluginsField.Should().NotBeNull();
        pluginStatesField.Should().NotBeNull();

        var pluginsDict = (System.Collections.Concurrent.ConcurrentDictionary<string, IPlugin>)pluginsField!.GetValue(manager)!;
        var pluginStatesDict = (System.Collections.Concurrent.ConcurrentDictionary<string, PluginState>)pluginStatesField!.GetValue(manager)!;

        pluginsDict.AddOrUpdate(metadata.Id, plugin, (_, _) => plugin);
        pluginStatesDict.AddOrUpdate(metadata.Id, PluginState.Loaded, (_, _) => PluginState.Loaded);

        return true;
    }

    private static bool EnablePluginDirectly(PluginManager manager, IPlugin plugin, PluginMetadata metadata)
    {
        LoadPluginDirectly(manager, plugin, metadata);
        manager.InitializePlugin(metadata.Id);
        return manager.StartPlugin(metadata.Id);
    }

    public void Dispose()
    {
        _tempDir.Dispose();
    }
}

using System.Reflection;
using OpenForgeSelf.Backend.Plugins;
using OpenForgeSelf.Backend.Plugins.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace OpenForgeSelf.Backend.Tests.Plugins;

public class PluginManagerTests : IDisposable
{
    private readonly TempPluginDirectory _tempDir;
    private readonly Mock<IPermissionChecker> _mockPermissionChecker;
    private readonly IServiceProvider _serviceProvider;

    public PluginManagerTests()
    {
        _tempDir = new TempPluginDirectory();
        _mockPermissionChecker = new Mock<IPermissionChecker>();
        var services = new ServiceCollection();
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void SetPluginsDirectory_WhenCalled_SetsDirectoryPath()
    {
        var manager = CreateManager();
        var testDir = _tempDir.RootPath;

        manager.SetPluginsDirectory(testDir);

        manager.PluginsDirectory.Should().Be(testDir);
    }

    [Fact]
    public void DiscoverPlugins_EmptyDirectory_ReturnsEmptyList()
    {
        var manager = CreateManager();
        manager.SetPluginsDirectory(_tempDir.RootPath);

        var result = manager.DiscoverPlugins();

        result.Should().BeEmpty();
    }

    [Fact]
    public void DiscoverPlugins_NonExistentDirectory_ReturnsEmptyList()
    {
        var manager = CreateManager();
        var nonExistentPath = Path.Combine(_tempDir.RootPath, "nonexistent");
        manager.SetPluginsDirectory(nonExistentPath);

        var result = manager.DiscoverPlugins();

        result.Should().BeEmpty();
    }

    [Fact]
    public void DiscoverPlugins_SingleValidPlugin_ReturnsPluginMetadata()
    {
        var manager = CreateManager();
        manager.SetPluginsDirectory(_tempDir.RootPath);
        _tempDir.CreatePluginManifest("test.plugin1");

        var result = manager.DiscoverPlugins();

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("test.plugin1");
        result[0].Name.Should().Be("Test Plugin test.plugin1");
    }

    [Fact]
    public void DiscoverPlugins_MultiplePlugins_ReturnsAllMetadatas()
    {
        var manager = CreateManager();
        manager.SetPluginsDirectory(_tempDir.RootPath);
        _tempDir.CreatePluginManifest("test.plugin1");
        _tempDir.CreatePluginManifest("test.plugin2");
        _tempDir.CreatePluginManifest("test.plugin3");

        var result = manager.DiscoverPlugins();

        result.Should().HaveCount(3);
        result.Select(p => p.Id).Should().BeEquivalentTo("test.plugin1", "test.plugin2", "test.plugin3");
    }

    [Fact]
    public void DiscoverPlugins_DirectoryWithoutManifest_SkipsDirectory()
    {
        var manager = CreateManager();
        manager.SetPluginsDirectory(_tempDir.RootPath);
        _tempDir.CreatePluginDirectory("test.nomanifest");
        _tempDir.CreatePluginManifest("test.valid");

        var result = manager.DiscoverPlugins();

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("test.valid");
    }

    [Fact]
    public void DiscoverPlugins_PluginDirectory_SetCorrectly()
    {
        var manager = CreateManager();
        manager.SetPluginsDirectory(_tempDir.RootPath);
        _tempDir.CreatePluginManifest("test.plugin1");

        var result = manager.DiscoverPlugins();

        var expectedDir = Path.Combine(_tempDir.RootPath, "test.plugin1");
        result[0].PluginDirectory.Should().Be(expectedDir);
    }

    [Fact]
    public void DiscoverPlugins_Dependencies_ParsedCorrectly()
    {
        var manager = CreateManager();
        manager.SetPluginsDirectory(_tempDir.RootPath);
        _tempDir.CreatePluginManifest("test.plugin1", m =>
        {
            m.WithDependencies("test.dep1", "test.dep2");
        });

        var result = manager.DiscoverPlugins();

        result[0].Dependencies.Should().BeEquivalentTo("test.dep1", "test.dep2");
    }

    [Fact]
    public void DiscoverPlugins_InitialState_IsNotLoaded()
    {
        var manager = CreateManager();
        manager.SetPluginsDirectory(_tempDir.RootPath);
        _tempDir.CreatePluginManifest("test.plugin1");

        manager.DiscoverPlugins();
        var state = manager.GetPluginState("test.plugin1");

        state.Should().Be(PluginState.NotLoaded);
    }

    [Fact]
    public void GetPluginMetadata_ExistingPlugin_ReturnsMetadata()
    {
        var manager = CreateManager();
        manager.SetPluginsDirectory(_tempDir.RootPath);
        _tempDir.CreatePluginManifest("test.plugin1");
        manager.DiscoverPlugins();

        var metadata = manager.GetPluginMetadata("test.plugin1");

        metadata.Should().NotBeNull();
        metadata!.Id.Should().Be("test.plugin1");
    }

    [Fact]
    public void GetPluginMetadata_NonExistingPlugin_ReturnsNull()
    {
        var manager = CreateManager();

        var metadata = manager.GetPluginMetadata("nonexistent");

        metadata.Should().BeNull();
    }

    [Fact]
    public void GetPluginState_NonExistingPlugin_ReturnsDefault()
    {
        var manager = CreateManager();

        var state = manager.GetPluginState("nonexistent");

        state.Should().Be(default(PluginState));
    }

    [Fact]
    public void LoadPlugin_NonExistingPlugin_ReturnsFalse()
    {
        var manager = CreateManager();

        var result = manager.LoadPlugin("nonexistent");

        result.Should().BeFalse();
    }

    [Fact]
    public void LoadPlugin_NoAssemblyFile_ReturnsFalse()
    {
        var manager = CreateManager();
        manager.SetPluginsDirectory(_tempDir.RootPath);
        _tempDir.CreatePluginManifest("test.plugin1");
        manager.DiscoverPlugins();

        var result = manager.LoadPlugin("test.plugin1");

        result.Should().BeFalse();
        manager.GetPluginState("test.plugin1").Should().Be(PluginState.Error);
    }

    [Fact]
    public void InitializePlugin_PluginNotLoaded_ReturnsFalse()
    {
        var manager = CreateManager();

        var result = manager.InitializePlugin("nonexistent");

        result.Should().BeFalse();
    }

    [Fact]
    public void StartPlugin_PluginNotLoaded_ReturnsFalse()
    {
        var manager = CreateManager();

        var result = manager.StartPlugin("nonexistent");

        result.Should().BeFalse();
    }

    [Fact]
    public void StopPlugin_PluginNotLoaded_ReturnsFalse()
    {
        var manager = CreateManager();

        var result = manager.StopPlugin("nonexistent");

        result.Should().BeFalse();
    }

    [Fact]
    public void DestroyPlugin_PluginNotLoaded_ReturnsFalse()
    {
        var manager = CreateManager();

        var result = manager.DestroyPlugin("nonexistent");

        result.Should().BeFalse();
    }

    [Fact]
    public void GetPlugin_PluginNotLoaded_ReturnsNull()
    {
        var manager = CreateManager();

        var plugin = manager.GetPlugin("nonexistent");

        plugin.Should().BeNull();
    }

    [Fact]
    public void LoadedPluginIds_NoPluginsLoaded_ReturnsEmpty()
    {
        var manager = CreateManager();

        var ids = manager.LoadedPluginIds;

        ids.Should().BeEmpty();
    }

    [Fact]
    public void GetAllMetadatas_NoPlugins_ReturnsEmpty()
    {
        var manager = CreateManager();

        var metadatas = manager.GetAllMetadatas();

        metadatas.Should().BeEmpty();
    }

    [Fact]
    public void GetAllMetadatas_AfterDiscovery_ReturnsAllMetadatas()
    {
        var manager = CreateManager();
        manager.SetPluginsDirectory(_tempDir.RootPath);
        _tempDir.CreatePluginManifest("test.plugin1");
        _tempDir.CreatePluginManifest("test.plugin2");
        manager.DiscoverPlugins();

        var metadatas = manager.GetAllMetadatas().ToList();

        metadatas.Should().HaveCount(2);
    }

    [Fact]
    public void EnablePlugin_NonExistingPlugin_ReturnsFalse()
    {
        var manager = CreateManager();

        var result = manager.EnablePlugin("nonexistent");

        result.Should().BeFalse();
    }

    [Fact]
    public void DisablePlugin_NonExistingPlugin_ReturnsFalse()
    {
        var manager = CreateManager();

        var result = manager.DisablePlugin("nonexistent");

        result.Should().BeFalse();
    }

    [Fact]
    public void LoadAndStartAllPlugins_EmptyDirectory_DoesNotThrow()
    {
        var manager = CreateManager();
        manager.SetPluginsDirectory(_tempDir.RootPath);

        var action = () => manager.LoadAndStartAllPlugins();

        action.Should().NotThrow();
    }

    [Fact]
    public void StopAndUnloadAllPlugins_NoPlugins_DoesNotThrow()
    {
        var manager = CreateManager();

        var action = () => manager.StopAndUnloadAllPlugins();

        action.Should().NotThrow();
    }

    [Fact]
    public void TopologicalSort_SinglePlugin_NoDependencies_ReturnsSinglePlugin()
    {
        var metadatas = new List<PluginMetadata>
        {
            PluginManifestGenerator.CreateBasic("plugin.a")
        };

        var sorted = InvokeTopologicalSort(metadatas);

        sorted.Should().HaveCount(1);
        sorted[0].Id.Should().Be("plugin.a");
    }

    [Fact]
    public void TopologicalSort_LinearDependencies_SortsInOrder()
    {
        var metadatas = new List<PluginMetadata>
        {
            PluginManifestGenerator.CreateBasic("plugin.c").WithDependencies("plugin.b"),
            PluginManifestGenerator.CreateBasic("plugin.a"),
            PluginManifestGenerator.CreateBasic("plugin.b").WithDependencies("plugin.a")
        };

        var sorted = InvokeTopologicalSort(metadatas);

        sorted.Select(p => p.Id).Should().ContainInOrder("plugin.a", "plugin.b", "plugin.c");
    }

    [Fact]
    public void TopologicalSort_MultipleDependencies_SortsCorrectly()
    {
        var metadatas = new List<PluginMetadata>
        {
            PluginManifestGenerator.CreateBasic("plugin.d").WithDependencies("plugin.b", "plugin.c"),
            PluginManifestGenerator.CreateBasic("plugin.a"),
            PluginManifestGenerator.CreateBasic("plugin.b").WithDependencies("plugin.a"),
            PluginManifestGenerator.CreateBasic("plugin.c").WithDependencies("plugin.a")
        };

        var sorted = InvokeTopologicalSort(metadatas);

        var idxA = sorted.FindIndex(p => p.Id == "plugin.a");
        var idxB = sorted.FindIndex(p => p.Id == "plugin.b");
        var idxC = sorted.FindIndex(p => p.Id == "plugin.c");
        var idxD = sorted.FindIndex(p => p.Id == "plugin.d");

        idxA.Should().BeLessThan(idxB);
        idxA.Should().BeLessThan(idxC);
        idxB.Should().BeLessThan(idxD);
        idxC.Should().BeLessThan(idxD);
    }

    [Fact]
    public void TopologicalSort_CircularDependency_DoesNotThrow()
    {
        var metadatas = new List<PluginMetadata>
        {
            PluginManifestGenerator.CreateBasic("plugin.a").WithDependencies("plugin.b"),
            PluginManifestGenerator.CreateBasic("plugin.b").WithDependencies("plugin.a")
        };

        var action = () => InvokeTopologicalSort(metadatas);

        action.Should().NotThrow();
    }

    [Fact]
    public void TopologicalSort_UnknownDependency_IgnoresIt()
    {
        var metadatas = new List<PluginMetadata>
        {
            PluginManifestGenerator.CreateBasic("plugin.a").WithDependencies("unknown.dep")
        };

        var sorted = InvokeTopologicalSort(metadatas);

        sorted.Should().HaveCount(1);
        sorted[0].Id.Should().Be("plugin.a");
    }

    private PluginManager CreateManager()
    {
        return new PluginManager(_serviceProvider, _mockPermissionChecker.Object);
    }

    private static List<PluginMetadata> InvokeTopologicalSort(List<PluginMetadata> metadatas)
    {
        var manager = new PluginManager(
            new ServiceCollection().BuildServiceProvider(),
            Mock.Of<IPermissionChecker>());

        var method = typeof(PluginManager).GetMethod(
            "TopologicalSort",
            BindingFlags.NonPublic | BindingFlags.Instance);

        method.Should().NotBeNull();

        var result = method!.Invoke(manager, new object[] { metadatas });
        return (List<PluginMetadata>)result!;
    }

    public void Dispose()
    {
        _tempDir.Dispose();
    }
}

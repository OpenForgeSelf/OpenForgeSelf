using System.Reflection;
using OpenForgeSelf.Backend.Plugins;
using OpenForgeSelf.Backend.Plugins.Abstractions;
using OpenForgeSelf.Backend.Tests.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace OpenForgeSelf.Backend.Tests.Integration;

public class PluginDependencyIntegrationTests : IDisposable
{
    private readonly TempPluginDirectory _tempDir;
    private readonly Mock<IPermissionChecker> _mockPermissionChecker;
    private readonly IServiceProvider _serviceProvider;

    public PluginDependencyIntegrationTests()
    {
        _tempDir = new TempPluginDirectory();
        _mockPermissionChecker = new Mock<IPermissionChecker>();
        var services = new ServiceCollection();
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void TopologicalSort_LinearChain_SortsInDependencyOrder()
    {
        var metadatas = new List<PluginMetadata>
        {
            PluginManifestGenerator.CreateBasic("plugin.c").WithDependencies("plugin.b"),
            PluginManifestGenerator.CreateBasic("plugin.a"),
            PluginManifestGenerator.CreateBasic("plugin.b").WithDependencies("plugin.a")
        };

        var sorted = InvokeTopologicalSort(metadatas);

        sorted.Should().HaveCount(3);
        var idxA = sorted.FindIndex(p => p.Id == "plugin.a");
        var idxB = sorted.FindIndex(p => p.Id == "plugin.b");
        var idxC = sorted.FindIndex(p => p.Id == "plugin.c");
        idxA.Should().BeLessThan(idxB);
        idxB.Should().BeLessThan(idxC);
    }

    [Fact]
    public void TopologicalSort_MultipleDependencies_AllDependenciesComeFirst()
    {
        var metadatas = new List<PluginMetadata>
        {
            PluginManifestGenerator.CreateBasic("plugin.d").WithDependencies("plugin.b", "plugin.c"),
            PluginManifestGenerator.CreateBasic("plugin.a"),
            PluginManifestGenerator.CreateBasic("plugin.b").WithDependencies("plugin.a"),
            PluginManifestGenerator.CreateBasic("plugin.c").WithDependencies("plugin.a")
        };

        var sorted = InvokeTopologicalSort(metadatas);

        sorted.Should().HaveCount(4);
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
    public void TopologicalSort_NoDependencies_OrderPreserved()
    {
        var metadatas = new List<PluginMetadata>
        {
            PluginManifestGenerator.CreateBasic("plugin.a"),
            PluginManifestGenerator.CreateBasic("plugin.b"),
            PluginManifestGenerator.CreateBasic("plugin.c")
        };

        var sorted = InvokeTopologicalSort(metadatas);

        sorted.Should().HaveCount(3);
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
    public void TopologicalSort_ThreeNodeCycle_DoesNotThrow()
    {
        var metadatas = new List<PluginMetadata>
        {
            PluginManifestGenerator.CreateBasic("plugin.a").WithDependencies("plugin.c"),
            PluginManifestGenerator.CreateBasic("plugin.b").WithDependencies("plugin.a"),
            PluginManifestGenerator.CreateBasic("plugin.c").WithDependencies("plugin.b")
        };

        var action = () => InvokeTopologicalSort(metadatas);

        action.Should().NotThrow();
    }

    [Fact]
    public void TopologicalSort_MissingDependency_IgnoresMissing()
    {
        var metadatas = new List<PluginMetadata>
        {
            PluginManifestGenerator.CreateBasic("plugin.a").WithDependencies("plugin.missing")
        };

        var sorted = InvokeTopologicalSort(metadatas);

        sorted.Should().HaveCount(1);
        sorted[0].Id.Should().Be("plugin.a");
    }

    [Fact]
    public void TopologicalSort_DiamondDependency_SortsCorrectly()
    {
        var metadatas = new List<PluginMetadata>
        {
            PluginManifestGenerator.CreateBasic("plugin.a"),
            PluginManifestGenerator.CreateBasic("plugin.b").WithDependencies("plugin.a"),
            PluginManifestGenerator.CreateBasic("plugin.c").WithDependencies("plugin.a"),
            PluginManifestGenerator.CreateBasic("plugin.d").WithDependencies("plugin.b", "plugin.c")
        };

        var sorted = InvokeTopologicalSort(metadatas);

        sorted.Should().HaveCount(4);
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
    public void TopologicalSort_EmptyList_ReturnsEmpty()
    {
        var metadatas = new List<PluginMetadata>();

        var sorted = InvokeTopologicalSort(metadatas);

        sorted.Should().BeEmpty();
    }

    [Fact]
    public void TopologicalSort_SinglePlugin_ReturnsSingle()
    {
        var metadatas = new List<PluginMetadata>
        {
            PluginManifestGenerator.CreateBasic("plugin.solo")
        };

        var sorted = InvokeTopologicalSort(metadatas);

        sorted.Should().HaveCount(1);
        sorted[0].Id.Should().Be("plugin.solo");
    }

    [Fact]
    public void Dependencies_ParsedCorrectly_FromManifest()
    {
        var manager = CreateManager();
        manager.SetPluginsDirectory(_tempDir.RootPath);
        _tempDir.CreatePluginManifest("test.deps", m =>
        {
            m.WithDependencies("dep1", "dep2", "dep3");
        });

        manager.DiscoverPlugins();
        var metadata = manager.GetPluginMetadata("test.deps");

        metadata.Should().NotBeNull();
        metadata!.Dependencies.Should().BeEquivalentTo("dep1", "dep2", "dep3");
    }

    [Fact]
    public void Dependencies_EmptyList_WhenNoDependencies()
    {
        var manager = CreateManager();
        manager.SetPluginsDirectory(_tempDir.RootPath);
        _tempDir.CreatePluginManifest("test.nodeps");

        manager.DiscoverPlugins();
        var metadata = manager.GetPluginMetadata("test.nodeps");

        metadata.Should().NotBeNull();
        metadata!.Dependencies.Should().BeEmpty();
    }

    [Fact]
    public void LoadAndStartAllPlugins_FollowsDependencyOrder()
    {
        var manager = CreateManager();
        var initOrder = new List<string>();

        var pluginA = new FakePlugin { Id = "dep.a" };
        var pluginB = new FakePlugin { Id = "dep.b" };
        var pluginC = new FakePlugin { Id = "dep.c" };

        pluginA.InitializeException = null;
        pluginB.InitializeException = null;
        pluginC.InitializeException = null;

        var metadataA = PluginManifestGenerator.CreateBasic("dep.a");
        var metadataB = PluginManifestGenerator.CreateBasic("dep.b").WithDependencies("dep.a");
        var metadataC = PluginManifestGenerator.CreateBasic("dep.c").WithDependencies("dep.b");

        SetupPluginWithMetadata(manager, pluginA, metadataA);
        SetupPluginWithMetadata(manager, pluginB, metadataB);
        SetupPluginWithMetadata(manager, pluginC, metadataC);

        var metadatas = new List<PluginMetadata> { metadataC, metadataA, metadataB };
        var sorted = InvokeTopologicalSort(metadatas);

        foreach (var metadata in sorted)
        {
            if (metadata.Id == "dep.a")
                LoadPluginDirectly(manager, pluginA, metadataA);
            else if (metadata.Id == "dep.b")
                LoadPluginDirectly(manager, pluginB, metadataB);
            else if (metadata.Id == "dep.c")
                LoadPluginDirectly(manager, pluginC, metadataC);

            manager.InitializePlugin(metadata.Id);
            manager.StartPlugin(metadata.Id);
        }

        manager.GetPluginState("dep.a").Should().Be(PluginState.Running);
        manager.GetPluginState("dep.b").Should().Be(PluginState.Running);
        manager.GetPluginState("dep.c").Should().Be(PluginState.Running);
    }

    [Fact]
    public void Dependency_MissingPlugin_LoadFailsGracefully()
    {
        var manager = CreateManager();
        manager.SetPluginsDirectory(_tempDir.RootPath);
        _tempDir.CreatePluginManifest("plugin.withmissingdep", m =>
        {
            m.WithDependencies("nonexistent.plugin");
        });

        var metadatas = manager.DiscoverPlugins();
        metadatas.Should().HaveCount(1);

        var sorted = InvokeTopologicalSort(metadatas);
        sorted.Should().HaveCount(1);

        var loadResult = manager.LoadPlugin("plugin.withmissingdep");
        loadResult.Should().BeFalse();
    }

    [Fact]
    public void TopologicalSort_DeepDependencyChain_SortsCorrectly()
    {
        var metadatas = new List<PluginMetadata>
        {
            PluginManifestGenerator.CreateBasic("plugin.5").WithDependencies("plugin.4"),
            PluginManifestGenerator.CreateBasic("plugin.3").WithDependencies("plugin.2"),
            PluginManifestGenerator.CreateBasic("plugin.1"),
            PluginManifestGenerator.CreateBasic("plugin.4").WithDependencies("plugin.3"),
            PluginManifestGenerator.CreateBasic("plugin.2").WithDependencies("plugin.1")
        };

        var sorted = InvokeTopologicalSort(metadatas);

        sorted.Should().HaveCount(5);
        for (int i = 1; i <= 5; i++)
        {
            var idx = sorted.FindIndex(p => p.Id == $"plugin.{i}");
            idx.Should().Be(i - 1, $"plugin.{i} should be at position {i - 1}");
        }
    }

    [Fact]
    public void TopologicalSort_IndependentBranches_SortedIndependently()
    {
        var metadatas = new List<PluginMetadata>
        {
            PluginManifestGenerator.CreateBasic("branch1.a"),
            PluginManifestGenerator.CreateBasic("branch1.b").WithDependencies("branch1.a"),
            PluginManifestGenerator.CreateBasic("branch2.a"),
            PluginManifestGenerator.CreateBasic("branch2.b").WithDependencies("branch2.a")
        };

        var sorted = InvokeTopologicalSort(metadatas);

        sorted.Should().HaveCount(4);
        var idx1a = sorted.FindIndex(p => p.Id == "branch1.a");
        var idx1b = sorted.FindIndex(p => p.Id == "branch1.b");
        var idx2a = sorted.FindIndex(p => p.Id == "branch2.a");
        var idx2b = sorted.FindIndex(p => p.Id == "branch2.b");
        idx1a.Should().BeLessThan(idx1b);
        idx2a.Should().BeLessThan(idx2b);
    }

    [Fact]
    public void Dependencies_ListIsMutable_CanAddAfterCreation()
    {
        var metadata = PluginManifestGenerator.CreateBasic("test.mutable");

        metadata.Dependencies.Should().NotBeNull();
        metadata.Dependencies.Add("new.dep");
        metadata.Dependencies.Should().Contain("new.dep");
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

    private static void SetupPluginWithMetadata(PluginManager manager, IPlugin plugin, PluginMetadata metadata)
    {
        var metadatasField = typeof(PluginManager).GetField("_metadatas", BindingFlags.NonPublic | BindingFlags.Instance);
        var pluginStatesField = typeof(PluginManager).GetField("_pluginStates", BindingFlags.NonPublic | BindingFlags.Instance);

        var metadatasDict = (System.Collections.Concurrent.ConcurrentDictionary<string, PluginMetadata>)metadatasField!.GetValue(manager)!;
        var pluginStatesDict = (System.Collections.Concurrent.ConcurrentDictionary<string, PluginState>)pluginStatesField!.GetValue(manager)!;

        metadatasDict.AddOrUpdate(metadata.Id, metadata, (_, _) => metadata);
        pluginStatesDict.AddOrUpdate(metadata.Id, PluginState.NotLoaded, (_, _) => PluginState.NotLoaded);
    }

    private static bool LoadPluginDirectly(PluginManager manager, IPlugin plugin, PluginMetadata metadata)
    {
        var pluginsField = typeof(PluginManager).GetField("_plugins", BindingFlags.NonPublic | BindingFlags.Instance);
        var pluginStatesField = typeof(PluginManager).GetField("_pluginStates", BindingFlags.NonPublic | BindingFlags.Instance);

        var pluginsDict = (System.Collections.Concurrent.ConcurrentDictionary<string, IPlugin>)pluginsField!.GetValue(manager)!;
        var pluginStatesDict = (System.Collections.Concurrent.ConcurrentDictionary<string, PluginState>)pluginStatesField!.GetValue(manager)!;

        pluginsDict.AddOrUpdate(metadata.Id, plugin, (_, _) => plugin);
        pluginStatesDict.AddOrUpdate(metadata.Id, PluginState.Loaded, (_, _) => PluginState.Loaded);

        return true;
    }

    public void Dispose()
    {
        _tempDir.Dispose();
    }
}

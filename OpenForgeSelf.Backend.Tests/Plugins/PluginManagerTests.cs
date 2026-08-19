using System.Reflection;
using OpenForgeSelf.Backend.Plugins;
using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Backend.Plugins.Abstractions;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
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

    private interface IMarker { }
    private sealed class Marker : IMarker { }

    [Fact]
    public void InitializePlugin_HotEnablePath_ProvidesServiceCollection()
    {
        // 热启用路径（RegisterAllServices 后，再对单独插件 InitializePlugin）
        // 必须为该插件注入独立的 ServiceCollection（非宿主集合），供插件 DI 自注册。
        var manager = CreateManager();
        manager.SetPluginsDirectory(_tempDir.RootPath);

        var services = new ServiceCollection();
        manager.RegisterAllServices(services);

        var plugin = new FakePlugin();
        var metadata = PluginManifestGenerator.CreateBasic("test.hotenable");
        InjectPlugin(manager, "test.hotenable", metadata, plugin);

        var result = manager.InitializePlugin("test.hotenable");

        result.Should().BeTrue();
        plugin.Context.Should().NotBeNull();
        plugin.Context!.Get<IServiceCollection>().Should().NotBeNull();
        plugin.Context!.Get<IServiceCollection>().Should().NotBeSameAs(services);
    }

    [Fact]
    public void ProvideHostServices_SeedsCuratedContracts_IntoPluginFiberContext()
    {
        // 启动后 ProvideHostServices(app.Services) 把宿主应提供的契约 seed 进插件根上下文：
        // 派生 Fiber 的 Get<T>() 应能经父级链解析到（对标 Cordis app.service + 派生上下文继承）。
        var manager = CreateManager();
        manager.SetPluginsDirectory(_tempDir.RootPath);

        var services = new ServiceCollection();
        // ICronParser 在「宿主 → 插件」契约清单内（curated），应被 seed。
        services.AddSingleton<ICronParser>(new FakeCronParser());
        // IMarker 不在清单内，验证 seed 是精选而非把宿主全部服务透传。
        services.AddSingleton<IMarker, Marker>();
        services.AddSingleton(new ExtensionPointManager(manager));
        manager.RegisterAllServices(services);

        var plugin = new FakePlugin();
        var metadata = PluginManifestGenerator.CreateBasic("test.bridge");
        InjectPlugin(manager, "test.bridge", metadata, plugin);
        manager.InitializePlugin("test.bridge");

        var hostProvider = services.BuildServiceProvider();
        manager.ProvideHostServices(hostProvider);

        // curated 契约：插件上下文经父级链继承到宿主 seed 的实例。
        var cronParser = plugin.Context!.Get<ICronParser>();
        cronParser.Should().NotBeNull();
        cronParser.Should().BeSameAs(hostProvider.GetRequiredService<ICronParser>());

        // 非 curated 服务：不回落宿主 DI，返回 null（证明已移除透传）。
        plugin.Context!.Get<IMarker>().Should().BeNull();
    }

    private sealed class FakeCronParser : ICronParser
    {
        public bool IsValid(string cronExpression) => true;
        public DateTime? GetNextRunTime(string cronExpression, DateTime afterTime, TimeZoneInfo? timeZone = null)
            => afterTime.AddMinutes(1);
        public List<DateTime> GetNextRunTimes(string cronExpression, DateTime afterTime, int count, TimeZoneInfo? timeZone = null)
            => Enumerable.Range(1, count).Select(i => afterTime.AddMinutes(i)).ToList();
    }

    [Fact]
    public void DiscoverAllExtensions_DiscoversExtensions_ForLoadedPlugins()
    {
        // 启动路径接线：DiscoverAllExtensions 应对所有已加载插件发现扩展点，
        // 修复「启动后菜单/工具扩展为空」的缺陷（缺陷2）。
        var manager = CreateManager();
        manager.SetPluginsDirectory(_tempDir.RootPath);

        var extensionManager = new ExtensionPointManager(manager);
        var services = new ServiceCollection();
        services.AddSingleton(extensionManager);
        manager.RegisterAllServices(services);

        var plugin = new FakeMenuPlugin();
        plugin.AddMenuExtension(new FakeMenuExtension
        {
            Id = "menu.hot",
            Name = "Hot Menu",
            PluginId = "test.ext"
        });
        var metadata = PluginManifestGenerator.CreateBasic("test.ext");
        InjectPlugin(manager, "test.ext", metadata, plugin);
        manager.InitializePlugin("test.ext");

        manager.DiscoverAllExtensions(extensionManager);

        extensionManager.GetExtensions<IMenuExtension>()
            .Should().ContainSingle(m => m.Id == "menu.hot");
    }

    [Fact]
    public void DiscoverAllExtensions_IsIdempotent_DoesNotDuplicate()
    {
        // 缺陷2 要求 discover 幂等：重复发现不产生重复条目（扩展点注册使用 TryAdd）。
        var manager = CreateManager();
        manager.SetPluginsDirectory(_tempDir.RootPath);

        var extensionManager = new ExtensionPointManager(manager);
        var services = new ServiceCollection();
        services.AddSingleton(extensionManager);
        manager.RegisterAllServices(services);

        var plugin = new FakeMenuPlugin();
        plugin.AddMenuExtension(new FakeMenuExtension
        {
            Id = "menu.idempotent",
            Name = "Idempotent Menu",
            PluginId = "test.idempotent"
        });
        var metadata = PluginManifestGenerator.CreateBasic("test.idempotent");
        InjectPlugin(manager, "test.idempotent", metadata, plugin);
        manager.InitializePlugin("test.idempotent");

        manager.DiscoverAllExtensions(extensionManager);
        manager.DiscoverAllExtensions(extensionManager);

        extensionManager.GetExtensions<IMenuExtension>()
            .Should().ContainSingle(m => m.Id == "menu.idempotent");
    }

    [Fact]
    public void RegisterPluginApplicationParts_ThenDestroyPlugin_RemovesApplicationPart()
    {
        // 动态端点移除：注册 → 销毁后 ApplicationParts 不再包含该插件的程序集，且变更通知各触发一次。
        var manager = CreateManager();
        var plugin = new FakePlugin();
        var metadata = PluginManifestGenerator.CreateBasic("test.parts");
        InjectPlugin(manager, "test.parts", metadata, plugin);
        var assembly = typeof(PluginManager).Assembly;
        InjectPluginAssembly(manager, "test.parts", assembly);

        var partManager = new ApplicationPartManager();
        var notified = 0;
        manager.RegisterPluginApplicationParts(partManager, () => notified++);

        var matchingParts = partManager.ApplicationParts.OfType<AssemblyPart>().Count(ap => ReferenceEquals(ap.Assembly, assembly));
        matchingParts.Should().Be(1);
        notified.Should().Be(1, "注册新增部件应触发一次刷新通知");

        var destroyed = manager.DestroyPlugin("test.parts");

        destroyed.Should().BeTrue();
        var remainingParts = partManager.ApplicationParts.OfType<AssemblyPart>().Count(ap => ReferenceEquals(ap.Assembly, assembly));
        remainingParts.Should().Be(0);
        partManager.ApplicationParts.Should().BeEmpty();
        notified.Should().Be(2, "移除部件应再触发一次刷新通知");
    }

    [Fact]
    public void RegisterPluginApplicationParts_IsIdempotent_DoesNotDuplicateParts()
    {
        // 幂等：重复注册不产生重复部件，且只有首次实际新增时触发通知。
        var manager = CreateManager();
        var plugin = new FakePlugin();
        var metadata = PluginManifestGenerator.CreateBasic("test.idem");
        InjectPlugin(manager, "test.idem", metadata, plugin);
        InjectPluginAssembly(manager, "test.idem", typeof(PluginManager).Assembly);

        var partManager = new ApplicationPartManager();
        var notified = 0;
        manager.RegisterPluginApplicationParts(partManager, () => notified++);
        manager.RegisterPluginApplicationParts(partManager, () => notified++);

        partManager.ApplicationParts.Should().ContainSingle();
        notified.Should().Be(1);
    }

    [Fact]
    public void DestroyPlugin_WithoutPartManager_DoesNotThrow()
    {
        // 未设置 ApplicationPartManager（null 跳过）：卸载不炸；重复卸载返回 false 且不炸（幂等）。
        var manager = CreateManager();
        var plugin = new FakePlugin();
        var metadata = PluginManifestGenerator.CreateBasic("test.nopm");
        InjectPlugin(manager, "test.nopm", metadata, plugin);

        manager.DestroyPlugin("test.nopm").Should().BeTrue();
        manager.DestroyPlugin("test.nopm").Should().BeFalse();
    }

    [Fact]
    public void DestroyPlugin_UnregisteredApplicationPart_DoesNotThrowAndLeavesPartsUntouched()
    {
        // 已设置 partManager 但该插件从未注册过部件：卸载不炸，且不影响已注册的其他部件。
        var manager = CreateManager();
        var plugin = new FakePlugin();
        var metadata = PluginManifestGenerator.CreateBasic("test.unreg");
        InjectPlugin(manager, "test.unreg", metadata, plugin);

        var partManager = new ApplicationPartManager();
        manager.RegisterPluginApplicationParts(partManager);

        manager.DestroyPlugin("test.unreg").Should().BeTrue();

        partManager.ApplicationParts.Should().BeEmpty();
    }

    [Fact]
    public void RemovePluginAssembly_ReturnsAssembly_AndIsIdempotent()
    {
        // RemovePluginAssembly 返回并移除该插件的独立程序集；重复调用返回 null 不抛异常。
        var manager = CreateManager();
        var assembly = typeof(PluginManager).Assembly;
        InjectPluginAssembly(manager, "test.asm", assembly);

        manager.RemovePluginAssembly("test.asm").Should().BeSameAs(assembly);
        manager.RemovePluginAssembly("test.asm").Should().BeNull();
    }

    private PluginManager CreateManager()
    {
        return new PluginManager(_serviceProvider, _mockPermissionChecker.Object);
    }

    private static void InjectPlugin(PluginManager manager, string pluginId, PluginMetadata metadata, IPlugin plugin)
    {
        var pluginsField = typeof(PluginManager).GetField("_plugins", BindingFlags.NonPublic | BindingFlags.Instance);
        var statesField = typeof(PluginManager).GetField("_pluginStates", BindingFlags.NonPublic | BindingFlags.Instance);
        var metadatasField = typeof(PluginManager).GetField("_metadatas", BindingFlags.NonPublic | BindingFlags.Instance);

        pluginsField.Should().NotBeNull();
        statesField.Should().NotBeNull();
        metadatasField.Should().NotBeNull();

        var plugins = (System.Collections.Concurrent.ConcurrentDictionary<string, IPlugin>)pluginsField!.GetValue(manager)!;
        var states = (System.Collections.Concurrent.ConcurrentDictionary<string, PluginState>)statesField!.GetValue(manager)!;
        var metadatas = (System.Collections.Concurrent.ConcurrentDictionary<string, PluginMetadata>)metadatasField!.GetValue(manager)!;

        plugins.TryAdd(pluginId, plugin);
        states.TryAdd(pluginId, PluginState.Loaded);
        metadatas.TryAdd(pluginId, metadata);
    }

    private static void InjectPluginAssembly(PluginManager manager, string pluginId, Assembly assembly)
    {
        var assembliesField = typeof(PluginManager).GetField("_pluginAssemblies", BindingFlags.NonPublic | BindingFlags.Instance);
        assembliesField.Should().NotBeNull();

        var assemblies = (System.Collections.Concurrent.ConcurrentDictionary<string, Assembly>)assembliesField!.GetValue(manager)!;
        assemblies.TryAdd(pluginId, assembly);
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

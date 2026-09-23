using ForgeSelf.Api.Plugins;
using ForgeSelf.Api.Plugins.Abstractions;
using ForgeSelf.Api.Plugins.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeSelf.Api.Tests.Plugins;

public class PluginHotReloadWatcherTests
{
    private readonly TempPluginDirectory _tempDir;
    private readonly PluginManager _manager;

    public PluginHotReloadWatcherTests()
    {
        _tempDir = new TempPluginDirectory();
        var services = new ServiceCollection();
        _manager = new PluginManager(services.BuildServiceProvider(), Mock.Of<IPermissionChecker>());
        _manager.SetPluginsDirectory(_tempDir.RootPath);
    }

    [Fact]
    public void GetPluginFolder_ReturnsTopLevelFolder()
    {
        var path = Path.Combine(_tempDir.RootPath, "MemorySystem", "versions", "1.0.0", "MemorySystem.dll");

        PluginHotReloadWatcher.GetPluginFolder(_tempDir.RootPath, path).Should().Be("MemorySystem");
    }

    [Fact]
    public void GetPluginFolder_Backups_ReturnsNull()
    {
        var path = Path.Combine(_tempDir.RootPath, "_backups", "x", "1.0.0");

        PluginHotReloadWatcher.GetPluginFolder(_tempDir.RootPath, path).Should().BeNull();
    }

    [Fact]
    public void GetPluginFolder_OutsideRoot_ReturnsNull()
    {
        var path = Path.Combine(Path.GetTempPath(), "other", "x.dll");

        PluginHotReloadWatcher.GetPluginFolder(_tempDir.RootPath, path).Should().BeNull();
    }

    [Fact]
    public void ResolvePluginId_MapsFolderToPluginId()
    {
        _tempDir.CreatePluginManifest("test.watch");
        _manager.DiscoverPlugins();

        var watcher = new PluginHotReloadWatcher(_manager);
        var path = Path.Combine(_tempDir.RootPath, "test.watch", "current");

        watcher.ResolvePluginId(path).Should().Be("test.watch");
    }

    [Fact]
    public void ResolvePluginId_UnknownFolder_ReturnsNull()
    {
        var watcher = new PluginHotReloadWatcher(_manager);
        var path = Path.Combine(_tempDir.RootPath, "unknown.folder", "current");

        watcher.ResolvePluginId(path).Should().BeNull();
    }

    [Fact]
    public void OnPluginChanged_ReloadsRunningEmbeddedPlugin()
    {
        // 使用真实独立程序集插件入口类型（Plugins/<id>/TextTools.dll），验证「停用→重载」调度链路；
        // 不依赖真实 DLL / ALC 回收（该部分在测试环境受限，见 AGENTS 边界说明）。
        // 注：全部 11 插件已拆独立程序集（ADR D2），主程序集无内嵌插件，改用 TextToolsPlugin 独立 DLL。
        _tempDir.CreatePluginManifest("test.watch", m =>
        {
            m.EntryAssembly = "TextTools.dll";
            m.EntryType = "ForgeSelf.Api.Plugins.TextTools.TextToolsPlugin";
        });
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "Plugins", "TextTools", "TextTools.dll"),
            Path.Combine(_tempDir.RootPath, "test.watch", "TextTools.dll"));

        var services = new ServiceCollection();
        _manager.RegisterAllServices(services);

        _manager.GetPluginState("test.watch").Should().Be(PluginState.Running);
        var before = _manager.GetPlugin("test.watch");

        var watcher = new PluginHotReloadWatcher(_manager);
        var result = watcher.OnPluginChanged("test.watch");

        result.Should().BeTrue();
        _manager.GetPlugin("test.watch").Should().NotBeSameAs(before);
        _manager.GetPluginState("test.watch").Should().Be(PluginState.Running);
    }

    [Fact]
    public void OnPluginChanged_UnknownPlugin_ReturnsFalse()
    {
        var watcher = new PluginHotReloadWatcher(_manager);

        watcher.OnPluginChanged("unknown.plugin").Should().BeFalse();
    }
}

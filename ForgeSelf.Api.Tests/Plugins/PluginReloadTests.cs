using ForgeSelf.Api.Plugins;
using ForgeSelf.Api.Plugins.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeSelf.Api.Tests.Plugins;

public class PluginReloadTests : IDisposable
{
    private readonly TempPluginDirectory _tempDir;
    private readonly PluginManager _manager;

    public PluginReloadTests()
    {
        _tempDir = new TempPluginDirectory();
        var services = new ServiceCollection();
        _manager = new PluginManager(services.BuildServiceProvider(), Mock.Of<IPermissionChecker>());
        _manager.SetPluginsDirectory(_tempDir.RootPath);
    }

    [Fact]
    public void ReloadPlugin_RunningEmbeddedPlugin_StopsAndReloadsNewInstance()
    {
        // 使用真实独立程序集插件入口类型（Plugins/<id>/TextTools.dll）验证「停用→重载」序列；
        // 不依赖真实 DLL / ALC 回收（测试环境受限，端到端 ALC 回收留待运行时验证）。
        // 注：全部 11 插件已拆独立程序集（ADR D2），主程序集无内嵌插件，改用 TextToolsPlugin 独立 DLL。
        _tempDir.CreatePluginManifest("test.reload", m =>
        {
            m.EntryAssembly = "TextTools.dll"; // 独立程序集插件，位于 Plugins/<id>/TextTools.dll
            m.EntryType = "ForgeSelf.Api.Plugins.TextTools.TextToolsPlugin";
        });
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "Plugins", "TextTools", "TextTools.dll"),
            Path.Combine(_tempDir.RootPath, "test.reload", "TextTools.dll"));

        var services = new ServiceCollection();
        _manager.RegisterAllServices(services); // 挂载 + Apply（状态 Running）

        _manager.GetPluginState("test.reload").Should().Be(PluginState.Running);
        var before = _manager.GetPlugin("test.reload");
        before.Should().NotBeNull();

        var result = _manager.ReloadPlugin("test.reload");

        result.Should().BeTrue();
        var after = _manager.GetPlugin("test.reload");
        after.Should().NotBeNull();
        after.Should().NotBeSameAs(before);
        _manager.GetPluginState("test.reload").Should().Be(PluginState.Running);
    }

    [Fact]
    public void ReloadPlugin_NonExistingPlugin_ReturnsFalse()
    {
        _manager.ReloadPlugin("nonexistent").Should().BeFalse();
    }

    [Fact]
    public void ReloadPlugin_NotRunningPlugin_RefreshesMetadataAndReturnsTrue()
    {
        _tempDir.CreatePluginManifest("test.idle", m =>
        {
            m.EntryAssembly = "ForgeSelf.dll";
            m.EntryType = "ForgeSelf.Api.Plugins.AIAgent.AIAgentPlugin";
        });
        _manager.DiscoverPlugins(); // 状态 NotLoaded

        var result = _manager.ReloadPlugin("test.idle");

        result.Should().BeTrue();
        _manager.GetPluginState("test.idle").Should().Be(PluginState.NotLoaded);
    }

    public void Dispose() => _tempDir.Dispose();
}

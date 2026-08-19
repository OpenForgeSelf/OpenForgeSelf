using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Backend.Plugins;
using OpenForgeSelf.Backend.Plugins.Abstractions;
using OpenForgeSelf.Backend.Services;
using OpenForgeSelf.Backend.Tests.Plugins;
using OpenForgeSelf.Core;
using Moq;

namespace OpenForgeSelf.Backend.Tests.Plugins;

public class PluginLifecycleEventTests : IDisposable
{
    private readonly TempPluginDirectory _tempDir;

    public PluginLifecycleEventTests()
    {
        _tempDir = new TempPluginDirectory();
    }

    [Fact]
    public void LoadAndDestroyPlugin_EmitsLifecycleEvents()
    {
        // 准备一个独立程序集插件（TextTools），其 DLL 已由 StagePluginDllsToTestOutput 同步到测试输出。
        _tempDir.CreatePluginManifest("test.lifecycle", m =>
        {
            m.EntryAssembly = "TextTools.dll";
            m.EntryType = "OpenForgeSelf.Backend.Plugins.TextTools.TextToolsPlugin";
        });
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "Plugins", "TextTools", "TextTools.dll"),
            Path.Combine(_tempDir.RootPath, "test.lifecycle", "TextTools.dll"));

        var eventBus = new EventBus();
        var received = new List<PluginLifecycleEvent>();
        eventBus.On<PluginLifecycleEvent>("plugin/loaded", e => { received.Add(e); return Task.CompletedTask; });
        eventBus.On<PluginLifecycleEvent>("plugin/unloaded", e => { received.Add(e); return Task.CompletedTask; });

        var registry = new PluginServiceRegistry();
        var manager = new PluginManager(
            new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider(),
            Mock.Of<IPermissionChecker>(),
            registry)
        {
            EventBus = eventBus
        };
        manager.SetPluginsDirectory(_tempDir.RootPath);
        manager.DiscoverPlugins();

        manager.LoadPlugin("test.lifecycle").Should().BeTrue();
        received.Should().ContainSingle(e => e.Action == "loaded" && e.PluginId == "test.lifecycle");

        manager.DestroyPlugin("test.lifecycle").Should().BeTrue();
        received.Should().ContainSingle(e => e.Action == "unloaded" && e.PluginId == "test.lifecycle");
    }

    public void Dispose() => _tempDir.Dispose();
}

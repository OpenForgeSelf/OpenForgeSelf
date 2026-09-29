using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins;
using ForgeSelf.Api.Plugins.Abstractions;
using ForgeSelf.Api.Services;
using ForgeSelf.Api.Tests.Plugins;
using ForgeSelf.Core;
using Moq;

namespace ForgeSelf.Api.Tests.Plugins;

public class PluginLifecycleEventTests
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
            m.EntryType = "ForgeSelf.Api.Plugins.TextTools.TextToolsPlugin";
        });
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "plugins", "TextTools", "TextTools.dll"),
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
}

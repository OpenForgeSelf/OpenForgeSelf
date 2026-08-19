using OpenForgeSelf.Backend.Plugins;
using OpenForgeSelf.Backend.Plugins.Abstractions;
using OpenForgeSelf.Backend.Services;
using OpenForgeSelf.Backend.Tests.Plugins;

namespace OpenForgeSelf.Backend.Tests.Integration;

public class PluginServiceLifecycleIntegrationTests : IDisposable
{
    private readonly TempPluginDirectory _tempDir;

    public PluginServiceLifecycleIntegrationTests()
    {
        _tempDir = new TempPluginDirectory();
    }

    [Fact]
    public void DestroyPlugin_ThenHostResolvePluginService_Throws()
    {
        // 用真实独立程序集插件（TextTools）验证：RegisterAllServices 挂载 DI 子容器后，
        // 宿主经转发描述符可解析插件服务；DestroyPlugin 卸载后宿主解析应失败（不再返回旧实例）。
        // 注：全部 11 插件已拆独立程序集（ADR D2），主程序集无内嵌插件；TextToolsPlugin
        // 从测试输出同步的独立 DLL 加载（其 ITextStatsService → TextStatsService 无构造依赖，裸测试容器可解析）。
        _tempDir.CreatePluginManifest("test.di", m =>
        {
            m.EntryAssembly = "TextTools.dll";
            m.EntryType = "OpenForgeSelf.Backend.Plugins.TextTools.TextToolsPlugin";
        });
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "Plugins", "TextTools", "TextTools.dll"),
            Path.Combine(_tempDir.RootPath, "test.di", "TextTools.dll"));

        var registry = new PluginServiceRegistry();
        var manager = new PluginManager(
            new ServiceCollection().BuildServiceProvider(),
            Mock.Of<IPermissionChecker>(),
            registry);
        manager.SetPluginsDirectory(_tempDir.RootPath);

        IServiceCollection host = new ServiceCollection();
        manager.RegisterAllServices(host);
        foreach (var descriptor in registry.CollectForwardDescriptors())
        {
            host.Add(descriptor);
        }

        var provider = host.BuildServiceProvider();

        // 宿主 provider 构建后构建插件子 provider（Cordis 模式无宿主回落，与 AppBuilder 顺序一致）。
        registry.BuildAll();

        // 插件经 PluginLoadContext 独立加载，其服务类型与测试程序集（默认 ALC）非同一 Type，
        // 从已加载插件程序集反射取 ITextStatsService 类型（与注册进 registry 的 ServiceType 身份一致）。
        var serviceType = manager.GetLoadedPluginAssemblies()
            .Single(a => a.GetName().Name == "TextTools")
            .GetType("OpenForgeSelf.Backend.Plugins.TextTools.Services.ITextStatsService")!;

        provider.GetRequiredService(serviceType).Should().NotBeNull();

        manager.DestroyPlugin("test.di").Should().BeTrue();

        provider.Invoking(p => p.GetRequiredService(serviceType))
            .Should().Throw<InvalidOperationException>();
    }

    public void Dispose() => _tempDir.Dispose();
}

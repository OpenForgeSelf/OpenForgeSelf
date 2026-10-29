using ForgeSelf.Api.Plugins;
using ForgeSelf.Api.Plugins.Abstractions;
using ForgeSelf.Api.Services;
using ForgeSelf.Api.Tests.Plugins;

namespace ForgeSelf.Api.Tests.Integration;

public class PluginServiceLifecycleIntegrationTests
{
    private readonly TempPluginDirectory _tempDir;

    public PluginServiceLifecycleIntegrationTests()
    {
        _tempDir = new TempPluginDirectory();
    }

    [Fact]
    public void DestroyPlugin_ThenHostResolvePluginService_Throws()
    {
        // 用真实独立程序集插件（SamplePlugin）验证：RegisterAllServices 挂载 DI 子容器后，
        // 宿主经转发描述符可解析插件服务；DestroyPlugin 卸载后宿主解析应失败（不再返回旧实例）。
        // 注：全部插件已拆独立程序集（ADR D2），主程序集无内嵌插件；SamplePlugin
        // 从测试输出同步的独立 DLL 加载（其设置页服务无构造依赖，裸测试容器可解析）。2026-10-28 text-tools 并入 dev-tools 后载具换 SamplePlugin。
        _tempDir.CreatePluginManifest("test.di", m =>
        {
            m.EntryAssembly = "SamplePlugin.dll";
            m.EntryType = "ForgeSelf.Api.Plugins.SamplePlugin.SamplePlugin";
        });
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "plugins", "SamplePlugin", "SamplePlugin.dll"),
            Path.Combine(_tempDir.RootPath, "test.di", "SamplePlugin.dll"));

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
        // 从已加载插件程序集反射取 ISampleGreetingService 类型（SamplePlugin 在 Apply 里 AddScoped 的服务，
        // 与注册进 registry 的 ServiceType 身份一致；SamplePlugin 类本身不进 DI）。
        var serviceType = manager.GetLoadedPluginAssemblies()
            .Single(a => a.GetName().Name == "SamplePlugin")
            .GetType("ForgeSelf.Api.Plugins.SamplePlugin.ISampleGreetingService")!;

        provider.GetRequiredService(serviceType).Should().NotBeNull();

        manager.DestroyPlugin("test.di").Should().BeTrue();

        provider.Invoking(p => p.GetRequiredService(serviceType))
            .Should().Throw<InvalidOperationException>();
    }
}

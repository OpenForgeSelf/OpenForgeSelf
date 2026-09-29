using ForgeSelf.Api.Plugins;
using ForgeSelf.Api.Plugins.Abstractions;
using ForgeSelf.Api.Services;
using ForgeSelf.Core;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins;

/// <summary>
/// B3（040）门禁 A7：插件 Fiber 内 emit 必须能冒泡到宿主 DI 单例总线上的平台级 <c>tools/*</c> 监听器。
/// 这是 B8 把 <c>tools/execute</c> / <c>tools/post-execute</c> 改成 waterfall 后能真正被插件拦截的前提：
/// 根 Context 的 EventBus 是自建实例，与宿主 DI 单例并非同一对象，不做「根总线←宿主单例」打通即空转。
/// </summary>
public class PluginEventBusBubblingTests
{
    private readonly TempPluginDirectory _tempDir = new();

    /// <summary>冒泡载荷：模拟一次 <c>tools/pre-execute</c> 的调用上下文。</summary>
    private sealed record ToolCallPayload(string ToolName);

    [Fact]
    public async Task PluginFiber_Emit_ReachesHostSingletonBus()
    {
        // Arrange：真实独立程序集插件（TextTools），其 DLL 已由 StagePluginDllsToTestOutput 同步到测试输出
        _tempDir.CreatePluginManifest("test.bus", m =>
        {
            m.EntryAssembly = "TextTools.dll";
            m.EntryType = "ForgeSelf.Api.Plugins.TextTools.TextToolsPlugin";
        });
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "plugins", "TextTools", "TextTools.dll"),
            Path.Combine(_tempDir.RootPath, "test.bus", "TextTools.dll"));

        // 宿主 DI 单例总线（对应 AppBuilder 的 AddSingleton<IEventBus>(new EventBus())）
        var hostBus = new EventBus();
        string? seenToolName = null;
        hostBus.OnSerial<ToolCallPayload, string>("tools/pre-execute", p =>
        {
            seenToolName = p.ToolName;
            return Task.FromResult<string?>("deny:host");
        });

        var registry = new PluginServiceRegistry();
        var manager = new PluginManager(
            new ServiceCollection().BuildServiceProvider(),
            Mock.Of<IPermissionChecker>(),
            registry)
        {
            // 宿主启动时注入平台单例总线 → 应把其挂为根 Context 总线的父
            EventBus = hostBus
        };
        manager.SetPluginsDirectory(_tempDir.RootPath);

        // Act：注册插件服务（会为每个插件建 Fiber 并 Apply）
        manager.RegisterAllServices(new ServiceCollection());
        registry.BuildAll();

        // 插件 Fiber 的 IContext 由 MountPlugin 注册进插件子容器（ForgeSelf.Core 类型身份与测试侧一致）
        var pluginContext = (IContext?)registry.Resolve(typeof(IContext));

        Assert.NotNull(pluginContext);

        var decision = await pluginContext!.Events
            .SerialAsync<ToolCallPayload, string>("tools/pre-execute", new ToolCallPayload("demo-tool"));

        // Assert：父链上的宿主单例监听器被触达，且短路值回到子上下文
        Assert.Equal("demo-tool", seenToolName);
        Assert.Equal("deny:host", decision);
    }

    [Fact]
    public async Task HostBus_NotAttached_WhenEventBusIsNull()
    {
        // 未注入平台总线时保持既有行为：不发事件、不抛异常（单元测试与独立构造场景兼容）
        _tempDir.CreatePluginManifest("test.bus.null", m =>
        {
            m.EntryAssembly = "TextTools.dll";
            m.EntryType = "ForgeSelf.Api.Plugins.TextTools.TextToolsPlugin";
        });
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "plugins", "TextTools", "TextTools.dll"),
            Path.Combine(_tempDir.RootPath, "test.bus.null", "TextTools.dll"));

        var registry = new PluginServiceRegistry();
        var manager = new PluginManager(
            new ServiceCollection().BuildServiceProvider(),
            Mock.Of<IPermissionChecker>(),
            registry);
        manager.SetPluginsDirectory(_tempDir.RootPath);
        manager.RegisterAllServices(new ServiceCollection());
        registry.BuildAll();

        var pluginContext = (IContext?)registry.Resolve(typeof(IContext));

        Assert.NotNull(pluginContext);
        // 无父总线时 SerialAsync 返回默认值，不抛
        Assert.Null(await pluginContext!.Events
            .SerialAsync<ToolCallPayload, string>("tools/pre-execute", new ToolCallPayload("demo-tool")));
    }
}

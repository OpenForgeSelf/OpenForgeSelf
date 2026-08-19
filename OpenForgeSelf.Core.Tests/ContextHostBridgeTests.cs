using OpenForgeSelf.Core;
using Xunit;

namespace OpenForgeSelf.Core.Tests;

/// <summary>
/// 验证上下文「宿主提供 → 子插件继承」机制（对标 Cordis 的 app.service + 派生上下文继承）。
/// 宿主应在初始化阶段把应提供的契约 seed 进根上下文；各 Fiber 派生上下文经父级链继承消费，
/// 而非回落某个宿主 MS DI 容器（宿主服务透传已移除）。
/// </summary>
public class ContextHostBridgeTests
{
    private interface IHostService { }
    private sealed class HostService : IHostService { }

    [Fact]
    public void GetService_RegisteredOnRootContext_VisibleToDerivedFiber()
    {
        // 宿主把服务 seed 进根上下文（对标 Cordis app.service），派生 Fiber 经父级链继承。
        var root = new Context();
        var host = new HostService();
        root.Register<IHostService>(host);

        var fiber = new Fiber(root);

        Assert.Same(host, fiber.Context.Get<IHostService>());
        Assert.Same(host, fiber.Context.GetService(typeof(IHostService)));
    }

    [Fact]
    public void GetService_LocalRegistration_TakesPrecedenceOverParent()
    {
        // 子插件在自身上下文注册的同名服务优先于父级（继承可被覆盖，对标 Cordis）。
        var root = new Context();
        var parent = new HostService();
        root.Register<IHostService>(parent);

        var local = new HostService();
        var fiber = new Fiber(root);
        fiber.Context.Register<IHostService>(local);

        Assert.Same(local, fiber.Context.Get<IHostService>());
    }

    [Fact]
    public void GetService_NotInChain_ReturnsNull()
    {
        // 父级链上不存在的服务返回 null（不再回落宿主 DI）。
        var root = new Context();
        var fiber = new Fiber(root);

        Assert.Null(fiber.Context.Get<IHostService>());
        Assert.Null(fiber.Context.GetService(typeof(IHostService)));
    }

    [Fact]
    public void GetService_DeepNesting_InheritsFromAncestor()
    {
        // 多层派生仍继承根上下文提供的服务。
        var root = new Context();
        var host = new HostService();
        root.Register<IHostService>(host);

        var mid = new Fiber(root);
        var leaf = new Fiber(mid.Context);

        Assert.Same(host, leaf.Context.Get<IHostService>());
    }
}

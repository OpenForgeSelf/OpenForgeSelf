using ForgeSelf.Core;
using Xunit;

namespace ForgeSelf.Core.Tests;

public class FiberTests
{
    private interface IMarker { }
    private sealed class Marker : IMarker { }

    [Fact]
    public void Mount_ExecutesApplyOnDerivedScope()
    {
        var parent = new Context();
        var fiber = new Fiber(parent);
        var marker = new Marker();

        fiber.Mount(ctx => ctx.RegisterLocal<IMarker>(marker));

        Assert.Same(marker, fiber.Context.Get<IMarker>());
        Assert.Null(parent.Get<IMarker>());
    }

    [Fact]
    public void Dispose_RollsBackEffectsInReverseOrder()
    {
        var parent = new Context();
        var fiber = new Fiber(parent);
        var order = new List<string>();

        fiber.Mount(ctx =>
        {
            ctx.Effect(() => Disposable.Create(() => order.Add("first")));
            ctx.Effect(() => Disposable.Create(() => order.Add("second")));
        });

        fiber.Dispose();

        Assert.Equal(new[] { "second", "first" }, order);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var parent = new Context();
        var fiber = new Fiber(parent);

        fiber.Mount(ctx => ctx.Effect(() => Disposable.Create(() => { })));

        fiber.Dispose();
        fiber.Dispose();
    }

    [Fact]
    public void Dispose_UnregistersServices()
    {
        var parent = new Context();
        var fiber = new Fiber(parent);
        fiber.Mount(ctx => ctx.Register<IMarker>(new Marker()));

        Assert.NotNull(fiber.Context.Get<IMarker>());

        fiber.Dispose();

        Assert.Null(fiber.Context.Get<IMarker>());
    }
}

public class ContextTests
{
    private interface IMarker { }
    private sealed class Marker : IMarker { }

    [Fact]
    public void Dispose_UnregistersServices()
    {
        var ctx = new Context();
        ctx.Register<IMarker>(new Marker());

        Assert.NotNull(ctx.Get<IMarker>());

        ctx.Dispose();

        Assert.Null(ctx.Get<IMarker>());
    }
}

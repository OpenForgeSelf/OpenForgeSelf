using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Core;
using Xunit;

namespace OpenForgeSelf.Abstractions.Tests;

public class PluginContractTests
{
    private interface IMarker { }
    private sealed class Marker : IMarker { }

    private sealed class GreetPlugin : IPlugin
    {
        public bool EffectDisposed { get; private set; }

        public void Apply(IContext ctx)
        {
            ctx.Register<IMarker>(new Marker());
            ctx.Effect(() => Disposable.Create(() => EffectDisposed = true));
        }
    }

    [Fact]
    public void Apply_RegistersServiceAndEffect_AndFiberDispose_RollsBackBoth()
    {
        var parent = new Context();
        var fiber = new Fiber(parent);
        var plugin = new GreetPlugin();

        fiber.Mount(plugin.Apply);

        Assert.NotNull(fiber.Context.Get<IMarker>());
        Assert.False(plugin.EffectDisposed);

        fiber.Dispose();

        Assert.Null(fiber.Context.Get<IMarker>());
        Assert.True(plugin.EffectDisposed);
    }
}

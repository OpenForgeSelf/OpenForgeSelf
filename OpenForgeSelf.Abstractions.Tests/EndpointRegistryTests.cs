using OpenForgeSelf.Abstractions;
using OpenForgeSelf.Core;
using Xunit;

namespace OpenForgeSelf.Abstractions.Tests;

public class EndpointRegistryTests
{
    private sealed class FakeEndpointRegistry : IEndpointRegistry
    {
        public List<string> Patterns { get; } = new();
        public List<Delegate> Handlers { get; } = new();

        public IDisposable Map(string pattern, Delegate handler)
        {
            Patterns.Add(pattern);
            Handlers.Add(handler);
            return Disposable.Create(() => Patterns.Remove(pattern));
        }
    }

    [Fact]
    public void Map_RecordsPattern_AndReturnsDisposableHandle()
    {
        var registry = new FakeEndpointRegistry();
        Func<string> handler = () => "ok";

        IDisposable handle = registry.Map("/greet", handler);

        Assert.Equal(new[] { "/greet" }, registry.Patterns);
        Assert.Same(handler, registry.Handlers[0]);

        handle.Dispose();

        Assert.Empty(registry.Patterns);
    }
}

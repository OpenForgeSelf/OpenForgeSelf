using ForgeSelf.Core;
using Xunit;

namespace ForgeSelf.Core.Tests;

/// <summary>
/// 验证 WaterfallAsync 环绕中间件的注册入口 OnWaterfall：注册后可被
/// WaterfallAsync 消费，按洋葱顺序执行，且支持短路（不调用 next()）。
/// </summary>
public class EventBusTests
{
    [Fact]
    public async Task OnWaterfall_Middleware_RunsInOnionOrder()
    {
        // Arrange
        var bus = new EventBus();
        var order = new List<string>();

        bus.OnWaterfall<string, string>("test", async (payload, next) =>
        {
            order.Add($"before1:{payload}");
            var result = await next();
            order.Add($"after1:{result}");
            return $"[1:{result}]";
        });
        bus.OnWaterfall<string, string>("test", async (payload, next) =>
        {
            order.Add($"before2:{payload}");
            var result = await next();
            order.Add($"after2:{result}");
            return $"[2:{result}]";
        });

        // Act
        var result = await bus.WaterfallAsync("test", "hello", () => Task.FromResult("core"));

        // Assert：先注册的中间件在最外层（before 先执行、after 最后执行）。
        Assert.Equal("[1:[2:core]]", result);
        Assert.Equal(
            new[] { "before1:hello", "before2:hello", "after2:core", "after1:[2:core]" },
            order);
    }

    [Fact]
    public async Task OnWaterfall_Middleware_CanShortCircuit()
    {
        // Arrange
        var bus = new EventBus();
        var innerCalled = false;

        bus.OnWaterfall<string, string>("test", (payload, next) =>
        {
            // 短路：不调用 next()，直接返回结果。
            return Task.FromResult($"short-circuit:{payload}");
        });

        // Act
        var result = await bus.WaterfallAsync("test", "hello", () =>
        {
            innerCalled = true;
            return Task.FromResult("fallback");
        });

        // Assert
        Assert.Equal("short-circuit:hello", result);
        Assert.False(innerCalled);
    }

    [Fact]
    public async Task OnWaterfall_Dispose_UnregistersMiddleware()
    {
        // Arrange
        var bus = new EventBus();

        var subscription = bus.OnWaterfall<string, string>("test", (payload, next) =>
            Task.FromResult($"wrapped:{payload}"));

        // Act：注销后 fallback 直接生效，中间件不再包裹。
        subscription.Dispose();
        var result = await bus.WaterfallAsync("test", "hello", () => Task.FromResult("fallback"));

        // Assert
        Assert.Equal("fallback", result);
    }
}

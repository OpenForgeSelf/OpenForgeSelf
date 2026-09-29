using Xunit;

namespace ForgeSelf.Core.Tests;

/// <summary>
/// B3（040）事件总线父子冒泡：子 emit 沿父链朝根传播，父 emit 不反向传给子（不对称）。
/// 测试经 <see cref="Context"/> 派生链取总线，覆盖真实接线路径（Context → EventBus 父指针）。
/// </summary>
public class EventBusBubblingTests
{
    /// <summary>测试载荷：一次冒泡的载体（工具名）。</summary>
    private sealed record Payload(string Name);

    private static (Context Parent, Context Child) NewPair()
    {
        var parent = new Context();
        var child = (Context)parent.Derive();
        return (parent, child);
    }

    // 测试 1：子 emit → 父收到
    [Fact]
    public async Task ChildEmit_ParentReceives()
    {
        var (parent, child) = NewPair();
        var parentCount = 0;
        var childCount = 0;
        parent.Events.On<Payload>("tools/pre-execute", _ => { parentCount++; return Task.CompletedTask; });
        child.Events.On<Payload>("tools/pre-execute", _ => { childCount++; return Task.CompletedTask; });

        await child.Events.EmitAsync("tools/pre-execute", new Payload("demo"));

        Assert.Equal(1, childCount);
        Assert.Equal(1, parentCount);
    }

    // 测试 2：父 emit → 子不收（不对称，匹配 Cordis 语义）
    [Fact]
    public async Task ParentEmit_ChildDoesNotReceive()
    {
        var (parent, child) = NewPair();
        var childCount = 0;
        child.Events.On<Payload>("tools/pre-execute", _ => { childCount++; return Task.CompletedTask; });

        await parent.Events.EmitAsync("tools/pre-execute", new Payload("demo"));

        Assert.Equal(0, childCount);
    }

    // 测试 3：父 OnSerial 返回非 null → 子拿到短路值
    [Fact]
    public async Task ChildSerial_ParentCanShortCircuit()
    {
        var (parent, child) = NewPair();
        parent.Events.OnSerial<Payload, string>("tools/pre-execute",
            p => Task.FromResult<string?>("deny:" + p.Name));

        var result = await child.Events.SerialAsync<Payload, string>("tools/pre-execute", new Payload("demo"));

        Assert.Equal("deny:demo", result);
    }

    // 测试 3b：子自身监听器优先于父（先自身后父）
    [Fact]
    public async Task ChildSerial_SelfWinsOverParent()
    {
        var (parent, child) = NewPair();
        parent.Events.OnSerial<Payload, string>("tools/pre-execute", _ => Task.FromResult<string?>("parent"));
        child.Events.OnSerial<Payload, string>("tools/pre-execute", _ => Task.FromResult<string?>("child"));

        var result = await child.Events.SerialAsync<Payload, string>("tools/pre-execute", new Payload("demo"));

        Assert.Equal("child", result);
    }

    // 测试 4：Waterfall 冒泡，父中间件在最外层（先进入、最后返回）
    [Fact]
    public async Task Waterfall_ParentMiddlewareIsOutermost()
    {
        var (parent, child) = NewPair();
        var order = new List<string>();

        parent.Events.OnWaterfall<Payload, string>("tools/execute", async (p, next) =>
        {
            order.Add("parent-before");
            var r = await next();
            order.Add("parent-after");
            return "[parent:" + r + "]";
        });
        child.Events.OnWaterfall<Payload, string>("tools/execute", async (p, next) =>
        {
            order.Add("child-before");
            var r = await next();
            order.Add("child-after");
            return "[child:" + r + "]";
        });

        var result = await child.Events.WaterfallAsync("tools/execute", new Payload("demo"),
            () => { order.Add("core"); return Task.FromResult("core"); });

        Assert.Equal(
            new[] { "parent-before", "child-before", "core", "child-after", "parent-after" },
            order);
        Assert.Equal("[parent:[child:core]]", result);
    }

    // 测试 5：5 层嵌套链不重复触发、不死循环
    [Fact]
    public async Task DeepChain_NoLoop_NoDuplicate()
    {
        var root = new Context();
        var rootCount = 0;
        root.Events.On<Payload>("tools/pre-execute", _ => { rootCount++; return Task.CompletedTask; });

        // 派生 5 层：root → c1 → c2 → c3 → c4 → c5
        var node = root;
        var leaf = root;
        for (var i = 0; i < 5; i++)
        {
            leaf = (Context)node.Derive();
            node = leaf;
        }

        await leaf.Events.EmitAsync("tools/pre-execute", new Payload("demo"));

        Assert.Equal(1, rootCount);

        // 反向：根 emit 不回流到任何子层（子层无监听器，仅断言不抛、不死循环）
        var leafCount = 0;
        leaf.Events.On<Payload>("tools/pre-execute", _ => { leafCount++; return Task.CompletedTask; });
        await root.Events.EmitAsync("tools/pre-execute", new Payload("demo"));
        Assert.Equal(0, leafCount);
        Assert.Equal(2, rootCount);
    }

    // 测试 6：注销句柄后不再触达；子 Dispose 不影响父的监听器表
    [Fact]
    public async Task Dispose_UnsubscribesFromParent()
    {
        var (parent, child) = NewPair();
        var total = 0;
        parent.Events.On<Payload>("tools/pre-execute", _ => { total++; return Task.CompletedTask; });
        var childSub = child.Events.On<Payload>("tools/pre-execute", _ => { total++; return Task.CompletedTask; });

        await child.Events.EmitAsync("tools/pre-execute", new Payload("demo"));
        Assert.Equal(2, total);

        // 注销子的订阅句柄后，只剩父监听器被触达
        childSub.Dispose();
        await child.Events.EmitAsync("tools/pre-execute", new Payload("demo"));
        Assert.Equal(3, total);

        // 子 Dispose 只清自身监听器，父监听器表不受影响
        child.Dispose();
        await parent.Events.EmitAsync("tools/pre-execute", new Payload("demo"));
        Assert.Equal(4, total);
    }

    // 附加：Parallel 模式同样冒泡
    [Fact]
    public async Task ChildParallel_ParentReceives()
    {
        var (parent, child) = NewPair();
        var parentCount = 0;
        parent.Events.On<Payload>("tools/result", _ => { parentCount++; return Task.CompletedTask; });
        child.Events.On<Payload>("tools/result", _ => { parentCount++; return Task.CompletedTask; });

        await child.Events.ParallelAsync("tools/result", new Payload("demo"));

        Assert.Equal(2, parentCount);
    }
}

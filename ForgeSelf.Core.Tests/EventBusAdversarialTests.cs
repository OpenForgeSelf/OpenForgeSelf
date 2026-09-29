using System.Reflection;
using Xunit;

namespace ForgeSelf.Core.Tests;

/// <summary>
/// B3（040）QA 独立反驳性测试（与工程师的 EventBusBubblingTests 互不复用）：
/// 只打边界与负面场景 —— 自引用/间接成环、SetParent 幂等与首挂优先、
/// 深链逐层恰好一次、Waterfall 父短路、跨三代 Serial 穿透、Dispose 语义边界。
/// <para>
/// <see cref="EventBus.SetParent"/> 与 <c>_parent</c> 为 internal（仅对宿主 ForgeSelf 开放），
/// 测试侧经反射访问 —— 这同时反向验证了「SetParent 不是公开 API」这一门禁。
/// </para>
/// </summary>
public class EventBusAdversarialTests
{
    private static readonly MethodInfo SetParentMethod =
        typeof(EventBus).GetMethod("SetParent",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("EventBus.SetParent(internal) 反射不到");

    private static readonly FieldInfo ParentField =
        typeof(EventBus).GetField("_parent",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("EventBus._parent 反射不到");

    private static void SetParent(EventBus child, EventBus parent)
        => SetParentMethod.Invoke(child, new[] { (object)parent });

    private static EventBus? GetParent(EventBus bus)
        => (EventBus?)ParentField.GetValue(bus);

    /// <summary>沿父链上行，返回链长；超过 maxSteps 仍不落地视为成环。</summary>
    private static int WalkUp(EventBus bus, int maxSteps = 32)
    {
        var node = bus;
        for (var i = 0; i < maxSteps; i++)
        {
            node = GetParent(node);
            if (node is null)
            {
                return i;
            }
        }

        return -1; // 成环
    }

    // ---- 1. 自引用：SetParent(this) 必须被拒绝（否则 Emit 即栈溢出/死循环） ----
    [Fact]
    public void SetParent_SelfReference_Rejected()
    {
        var bus = new EventBus();
        var count = 0;
        bus.On<object>("x", _ => { count++; return Task.CompletedTask; });

        // 不得抛（实现选择静默忽略）；关键是 _parent 必须仍为 null
        SetParent(bus, bus);

        Assert.Null(GetParent(bus));

        // emit 恰好派发自身一次（若成环此处会溢出，测试进程直接崩）
        bus.EmitAsync<object>("x", null!).GetAwaiter().GetResult();
        Assert.Equal(1, count);
    }

    // ---- 2. 间接成环：A→B 后再 B→A，必须被拒绝（成环 = Emit 死循环） ----
    // 【QA 发现 2026-09-28】当前实现（EventBus.cs SetParent，仅拒直接自引用 + 首挂优先）
    // 允许 A.SetParent(B); B.SetParent(A) 构成 a→b→a 环，父链上行永不落地，Emit 将死循环。
    // 严重度：低——SetParent 为 internal，宿主唯一调用点（PluginManager.EventBus → AttachParentBus）
    // 传入的是新建宿主单例总线（无父），实际接线不可能成环；属防御性加固项。
    // 已回报主理人转工程师：建议 SetParent 内沿 parent 父链上行探环（含 this 即拒绝/抛 ArgumentException）。
    // 【已修复 2026-09-28】B4 批次工程师加固：SetParent 沿 parent 父链上行探环，含 this 即抛 ArgumentException。
    [Fact]
    public void SetParent_IndirectCycle_Rejected()
    {
        var a = new EventBus();
        var b = new EventBus();

        SetParent(a, b); // a.parent = b，合法

        // 期望：给 b 挂 a 会构成 a→b→a 环，实现应拒绝（抛异常或静默忽略均可）
        try
        {
            SetParent(b, a);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is ArgumentException)
        {
            // 实现选择显式抛 ArgumentException：拒绝成功，符合预期
            return;
        }

        // 实现若选择静默忽略：父链必须仍无环（b 上行必须能落地）
        Assert.True(WalkUp(b) >= 0,
            "SetParent 允许间接成环（a→b→a）：父链上行 32 步不落地，EmitAsync 将死循环");
    }

    // ---- 3. 幂等：同一父重复挂载 → 只投递一次 ----
    [Fact]
    public async Task SetParent_SameParentTwice_DispatchedExactlyOnce()
    {
        var parent = new EventBus();
        var child = new EventBus();
        var count = 0;
        parent.On<object>("x", _ => { count++; return Task.CompletedTask; });

        SetParent(child, parent);
        SetParent(child, parent); // 重复挂同一父

        await child.EmitAsync<object>("x", null!);
        Assert.Equal(1, count);
    }

    // ---- 4. 首挂优先：第二个不同父被忽略，不得双投 ----
    [Fact]
    public async Task SetParent_SecondDifferentParent_FirstWins()
    {
        var p1 = new EventBus();
        var p2 = new EventBus();
        var child = new EventBus();
        var c1 = 0;
        var c2 = 0;
        p1.On<object>("x", _ => { c1++; return Task.CompletedTask; });
        p2.On<object>("x", _ => { c2++; return Task.CompletedTask; });

        SetParent(child, p1);
        SetParent(child, p2); // 已有父 → 忽略

        await child.EmitAsync<object>("x", null!);
        Assert.Equal(1, c1);
        Assert.Equal(0, c2);
    }

    // ---- 5. 深链（6 层）：每层监听器恰好触发一次，中段不得重复投递 ----
    [Fact]
    public async Task DeepChain_SixLevels_EachLevelReceivesExactlyOnce()
    {
        var root = new Context();
        var node = root;
        var counts = new List<int>();
        var buses = new List<IEventBus>();
        for (var i = 0; i < 6; i++)
        {
            var ctx = (Context)node.Derive();
            var idx = i;
            counts.Add(0);
            ctx.Events.On<object>("x", _ => { counts[idx]++; return Task.CompletedTask; });
            buses.Add(ctx.Events);
            node = ctx;
        }

        await node.Events.EmitAsync<object>("x", null!);

        // 6 个子层各 1 次（root 无监听器）
        Assert.All(counts, c => Assert.Equal(1, c));
    }

    // ---- 6. Waterfall 父短路：父不调 next → 子中间件与 fallback 都不得执行 ----
    [Fact]
    public async Task Waterfall_ParentShortCircuit_SkipsChildAndFallback()
    {
        var parent = new Context();
        var child = (Context)parent.Derive();
        var childRan = false;
        var fallbackRan = false;

        parent.Events.OnWaterfall<object, string>("x", (_, _) =>
            Task.FromResult("P"));
        child.Events.OnWaterfall<object, string>("x", (_, next) =>
        {
            childRan = true;
            return next();
        });

        var result = await child.Events.WaterfallAsync<object, string>("x", null!,
            () => { fallbackRan = true; return Task.FromResult("core"); });

        Assert.Equal("P", result);
        Assert.False(childRan, "父短路后子中间件不应执行");
        Assert.False(fallbackRan, "父短路后 fallback 不应执行");
    }

    // ---- 7. Waterfall 三代链：祖父最外层，序严格为 gp→p→c→core→c→p→gp ----
    [Fact]
    public async Task Waterfall_ThreeLevels_GrandparentIsOutermost()
    {
        var gp = new Context();
        var p = (Context)gp.Derive();
        var c = (Context)p.Derive();
        var order = new List<string>();

        gp.Events.OnWaterfall<object, string>("x", async (_, next) =>
        {
            order.Add("gp-before");
            var r = await next();
            order.Add("gp-after");
            return $"[gp:{r}]";
        });
        p.Events.OnWaterfall<object, string>("x", async (_, next) =>
        {
            order.Add("p-before");
            var r = await next();
            order.Add("p-after");
            return $"[p:{r}]";
        });
        c.Events.OnWaterfall<object, string>("x", async (_, next) =>
        {
            order.Add("c-before");
            var r = await next();
            order.Add("c-after");
            return $"[c:{r}]";
        });

        var result = await c.Events.WaterfallAsync<object, string>("x", null!,
            () => { order.Add("core"); return Task.FromResult("core"); });

        Assert.Equal(
            new[] { "gp-before", "p-before", "c-before", "core", "c-after", "p-after", "gp-after" },
            order);
        Assert.Equal("[gp:[p:[c:core]]]", result);
    }

    // ---- 8. Serial 跨代穿透：父返回 null → 继续向祖父要值 ----
    [Fact]
    public async Task Serial_ParentReturnsNull_FallsThroughToGrandparent()
    {
        var gp = new Context();
        var p = (Context)gp.Derive();
        var c = (Context)p.Derive();

        gp.Events.OnSerial<object, string>("x", _ => Task.FromResult<string?>("gp"));
        p.Events.OnSerial<object, string>("x", _ => Task.FromResult<string?>(null));

        var result = await c.Events.SerialAsync<object, string>("x", null!);
        Assert.Equal("gp", result);
    }

    // ---- 9. Dispose 语义：只清自身监听器，父链派发链路不被破坏 ----
    [Fact]
    public async Task Dispose_ClearsOnlyOwnHandlers_ParentChainStillWorks()
    {
        var parent = new Context();
        var child = (Context)parent.Derive();
        var pc = 0;
        var cc = 0;
        parent.Events.On<object>("x", _ => { pc++; return Task.CompletedTask; });
        child.Events.On<object>("x", _ => { cc++; return Task.CompletedTask; });

        ((EventBus)child.Events).Dispose();

        // 子 emit：自身监听器已清，但仍冒泡到父（Dispose 只清表，不断链）
        await child.Events.EmitAsync<object>("x", null!);
        Assert.Equal(0, cc);
        Assert.Equal(1, pc);

        // 父 emit 不回流到子
        await parent.Events.EmitAsync<object>("x", null!);
        Assert.Equal(0, cc);
        Assert.Equal(2, pc);
    }
}

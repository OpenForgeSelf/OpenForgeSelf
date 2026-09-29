using System.Collections.Concurrent;

namespace ForgeSelf.Core;

/// <summary>
/// 类型化事件总线的默认实现：四种分发模式对标 Cordis 的 emit / waterfall / parallel / serial。
/// </summary>
/// <remarks>
/// B3（040）新增<b>父子冒泡</b>：子总线派发完自身监听器后，沿 <see cref="_parent"/> 朝根方向继续派发；
/// 父总线 emit 不会反向传给子（<b>不对称</b>，匹配 Cordis 语义）。父指针只朝根方向，天然无环。
/// <para>
/// 宿主可经 <see cref="SetParent"/> 把平台级单例总线挂为插件根上下文总线的父总线，
/// 使插件 Fiber 内的 emit 能触达平台级 <c>tools/*</c> 监听器
/// （根 Context 自建总线与宿主 DI 单例本不是同一实例，必须事后打通，见 PluginManager.EventBus）。
/// </para>
/// <para>
/// <see cref="IEventBus"/> 公开签名不变：冒泡只改内部派发顺序，不新增/不改公开成员。
/// </para>
/// </remarks>
public sealed class EventBus : IEventBus, IDisposable
{
    private readonly ConcurrentDictionary<string, List<object>> _handlers = new();

    /// <summary>父总线（朝根方向）。仅由构造函数或 <see cref="SetParent"/> 写入，之后不再变更。</summary>
    private EventBus? _parent;

    public EventBus() : this(null) { }

    /// <summary>以指定父总线构造（B3 冒泡）。父可为 null（根总线）。</summary>
    internal EventBus(EventBus? parent)
    {
        _parent = parent;
    }

    /// <summary>
    /// 一次性挂载父总线（供宿主把平台单例总线挂到插件根 Context 的总线上）。
    /// 幂等：已有父总线则忽略；拒绝自引用与<b>间接成环</b>（沿 parent 父链上行，回到 this 即抛），
    /// 杜绝 <c>A.SetParent(B); B.SetParent(A)</c> 这类环导致 Emit 死循环。
    /// </summary>
    /// <param name="parent">父总线（朝根方向）。</param>
    /// <exception cref="ArgumentException">本次挂载将构成父子环。</exception>
    internal void SetParent(EventBus parent)
    {
        ArgumentNullException.ThrowIfNull(parent);

        // 自引用会立刻成环，直接拒绝
        if (ReferenceEquals(parent, this))
        {
            return;
        }

        // 父链探环：若 parent 的父链上行能回到 this，则本次挂载将成环（如 a→b 后再 b→a）
        for (var node = parent; node is not null; node = node._parent)
        {
            if (ReferenceEquals(node, this))
            {
                throw new ArgumentException("SetParent 将构成总线父子环（父链上行回到自身），拒绝挂载", nameof(parent));
            }
        }

        _parent ??= parent;
    }

    public IDisposable On<TEvent>(string name, Func<TEvent, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var list = _handlers.GetOrAdd(name, static _ => new List<object>());
        lock (list)
        {
            list.Add(handler);
        }

        return Disposable.Create(() =>
        {
            lock (list)
            {
                list.Remove(handler);
            }
        });
    }

    public IDisposable OnSerial<TEvent, TResult>(string name, Func<TEvent, Task<TResult?>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var list = _handlers.GetOrAdd(name, static _ => new List<object>());
        lock (list)
        {
            list.Add(handler);
        }

        return Disposable.Create(() =>
        {
            lock (list)
            {
                list.Remove(handler);
            }
        });
    }

    public IDisposable OnWaterfall<TEvent, TResult>(string name, Func<TEvent, Func<Task<TResult>>, Task<TResult>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var list = _handlers.GetOrAdd(name, static _ => new List<object>());
        lock (list)
        {
            list.Add(handler);
        }

        return Disposable.Create(() =>
        {
            lock (list)
            {
                list.Remove(handler);
            }
        });
    }

    public async Task EmitAsync<TEvent>(string name, TEvent payload)
    {
        await DispatchSelfAsync(name, payload);

        // 只朝根方向冒泡，父不会反向传给子
        if (_parent is not null)
        {
            await _parent.EmitAsync(name, payload);
        }
    }

    public async Task ParallelAsync<TEvent>(string name, TEvent payload)
    {
        await DispatchSelfParallelAsync(name, payload);

        if (_parent is not null)
        {
            await _parent.ParallelAsync(name, payload);
        }
    }

    public async Task<TResult?> SerialAsync<TEvent, TResult>(string name, TEvent payload)
    {
        // 自身监听器优先（第一个非 null 即短路），拿不到再交给父链
        var own = await DispatchSelfSerialAsync<TEvent, TResult>(name, payload);
        if (own is not null)
        {
            return own;
        }

        return _parent is null ? default : await _parent.SerialAsync<TEvent, TResult>(name, payload);
    }

    public async Task<TResult> WaterfallAsync<TEvent, TResult>(
        string name,
        TEvent payload,
        Func<Task<TResult>> fallback)
    {
        // 父中间件位于最外层：先执行、最后返回 —— 保证父能包裹/短路整条子链
        if (_parent is not null)
        {
            return await _parent.WaterfallAsync(name, payload, () => DispatchSelfWaterfallAsync(name, payload, fallback));
        }

        return await DispatchSelfWaterfallAsync(name, payload, fallback);
    }

    /// <summary>仅派发自身监听器（Emit 模式），不冒泡。</summary>
    private async Task DispatchSelfAsync<TEvent>(string name, TEvent payload)
    {
        if (!_handlers.TryGetValue(name, out var list))
            return;

        foreach (var h in Snapshot(list))
        {
            if (h is Func<TEvent, Task> fn)
                await fn(payload);
        }
    }

    /// <summary>仅并发派发自身监听器（Parallel 模式），不冒泡。</summary>
    private async Task DispatchSelfParallelAsync<TEvent>(string name, TEvent payload)
    {
        if (!_handlers.TryGetValue(name, out var list))
            return;

        var tasks = Snapshot(list)
            .OfType<Func<TEvent, Task>>()
            .Select(fn => fn(payload))
            .ToArray();

        await Task.WhenAll(tasks);
    }

    /// <summary>仅按序派发自身监听器（Serial 模式），不冒泡。</summary>
    private async Task<TResult?> DispatchSelfSerialAsync<TEvent, TResult>(string name, TEvent payload)
    {
        if (!_handlers.TryGetValue(name, out var list))
            return default;

        foreach (var h in Snapshot(list))
        {
            if (h is Func<TEvent, Task<TResult?>> fn)
            {
                var result = await fn(payload);
                if (result is not null)
                    return result;
            }
        }

        return default;
    }

    /// <summary>仅对自身中间件做环绕派发（Waterfall 模式），不冒泡。</summary>
    private async Task<TResult> DispatchSelfWaterfallAsync<TEvent, TResult>(
        string name,
        TEvent payload,
        Func<Task<TResult>> fallback)
    {
        if (!_handlers.TryGetValue(name, out var list) || list.Count == 0)
            return await fallback();

        Func<Task<TResult>> next = fallback;
        var middleware = Snapshot(list)
            .OfType<Func<TEvent, Func<Task<TResult>>, Task<TResult>>>()
            .ToArray();

        for (var i = middleware.Length - 1; i >= 0; i--)
        {
            var captured = next;
            var mw = middleware[i];
            next = () => mw(payload, captured);
        }

        return await next();
    }

    /// <summary>只清理自身监听器，不动父总线（父由拥有者各自释放）。</summary>
    public void Dispose() => _handlers.Clear();

    private static object[] Snapshot(List<object> list)
    {
        lock (list)
        {
            return list.ToArray();
        }
    }
}

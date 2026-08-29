using System.Collections.Concurrent;

namespace ForgeSelf.Core;

/// <summary>
/// <see cref="IContext"/> 的默认实现。
/// 服务注册分两种语义（对标 Cordis，见 docs/06-research/001-deepseek-harness-plugin-architecture.md §5.6 裁决 A/D/F）：
/// <list type="bullet">
/// <item><see cref="Register{TService}"/> = 全局服务（对标 <c>provide()</c>）：写入 root 持有的共享服务表，
/// 所有上下文（含兄弟 Fiber）可见；注册同时走 <c>Effect</c>，所属 Fiber 逆序回滚（Dispose）时自动摘除（可逆 effect）。</item>
/// <item><see cref="RegisterLocal{TService}"/> = 本地值（对标直接赋值非声明属性）：仅当前上下文可见，
/// 不进入共享表，兄弟插件不可见；用于插件框架私有对象（PluginMetadata/IServiceCollection 等）。</item>
/// </list>
/// 解析顺序（裁决 D）= 本地值 → 全局共享表。
/// 宿主 seed 契约（PluginManager.ProvideHostServices 写 root）常驻 app 生命周期，root 永不卸载故不摘除（裁决 B/C）。
/// </summary>
public sealed class Context : IContext, IDisposable
{
    private readonly ConcurrentDictionary<Type, object> _services = new();
    private readonly List<IDisposable> _disposers = new();
    private readonly Context? _parent;
    private readonly Context _root;
    private readonly ConcurrentDictionary<Type, (object Instance, Context? Provider)>? _sharedServices;
    private readonly EventBus _events = new();

    public Context(Context? parent = null)
    {
        _parent = parent;
        _root = parent?.GetRoot() ?? this;
        // 共享服务表仅由 root 持有；非 root 上下文经 _root 引用访问。
        _sharedServices = ReferenceEquals(_root, this)
            ? new ConcurrentDictionary<Type, (object Instance, Context? Provider)>()
            : null;
    }

    public IEventBus Events => _events;

    public void Register<TService>(TService instance) where TService : class
        => Register(typeof(TService), instance);

    public void Register(Type serviceType, object instance)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(instance);
        // 全局服务（对标 Cordis provide()）：写入 root 共享表，任意兄弟上下文可见。
        SharedServices[serviceType] = (instance, this);
        // 提供即 effect：所属 Fiber 逆序回滚（本上下文 Dispose）时自动从共享表摘除。
        Effect(() => new SharedServiceRemoval(this, serviceType, instance));
    }

    public void Register<TService, TImpl>() where TService : class where TImpl : class, TService, new()
        => Register<TService>(new TImpl());

    public void RegisterLocal<TService>(TService instance) where TService : class
        => RegisterLocal(typeof(TService), instance);

    public void RegisterLocal(Type serviceType, object instance)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(instance);
        // 本地值（对标 Cordis 直接赋值非声明属性）：仅当前上下文可见，不进入共享表。
        _services[serviceType] = instance;
    }

    public TService? Get<TService>() where TService : class
        => GetService(typeof(TService)) as TService;

    public IDisposable Effect(Func<IDisposable> sideEffect)
    {
        ArgumentNullException.ThrowIfNull(sideEffect);
        var disposer = sideEffect();
        _disposers.Add(disposer);
        return new EffectHandle(() => _disposers.Remove(disposer), disposer);
    }

    public IContext Derive() => new Context(this);

    public object? GetService(Type serviceType)
    {
        // 解析顺序（裁决 D）：本地值优先于同名全局服务 → 全局共享表。
        if (_services.TryGetValue(serviceType, out var local))
            return local;
        return SharedServices.TryGetValue(serviceType, out var entry) ? entry.Instance : null;
    }

    public void Dispose()
    {
        for (var i = _disposers.Count - 1; i >= 0; i--)
            _disposers[i].Dispose();
        _disposers.Clear();
        _services.Clear();
        // 仅 root 自身 dispose 时清空共享表；插件 fiber 摘除只靠 effect 摘各自条目，不动共享表本身。
        _sharedServices?.Clear();
        _events.Dispose();
    }

    private ConcurrentDictionary<Type, (object Instance, Context? Provider)> SharedServices
        => _root._sharedServices!;

    private Context GetRoot()
    {
        var node = this;
        while (node._parent is { } parent)
            node = parent;
        return node;
    }

    private sealed class EffectHandle : IDisposable
    {
        private readonly Action _remove;
        private readonly IDisposable _inner;
        private int _disposed;

        public EffectHandle(Action remove, IDisposable inner)
        {
            _remove = remove;
            _inner = inner;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _remove();
                _inner.Dispose();
            }
        }
    }

    /// <summary>
    /// 共享表条目摘除器（提供即 effect 的实现）：所属上下文 Dispose 时从 root 共享表移除本注册。
    /// 仅当共享表当前条目仍为本注册的实例时摘除，避免误删后来覆盖的同名服务。
    /// </summary>
    private sealed class SharedServiceRemoval : IDisposable
    {
        private readonly Context _owner;
        private readonly Type _serviceType;
        private readonly object _instance;
        private int _disposed;

        public SharedServiceRemoval(Context owner, Type serviceType, object instance)
        {
            _owner = owner;
            _serviceType = serviceType;
            _instance = instance;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                var shared = _owner.SharedServices;
                if (shared.TryGetValue(_serviceType, out var entry)
                    && ReferenceEquals(entry.Instance, _instance))
                {
                    shared.TryRemove(_serviceType, out _);
                }
            }
        }
    }
}

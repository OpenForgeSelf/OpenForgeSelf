using System.Collections.Concurrent;

namespace OpenForgeSelf.Core;

/// <summary>
/// <see cref="IContext"/> 的默认实现。
/// 服务注册为「类型 → 实例」的本地字典；派生上下文向上查找父级（对标 Cordis 的上下文继承链）。
/// 副作用按注册逆序释放（与 Cordis Fiber 的 dispose 语义一致）。
/// </summary>
/// <remarks>
/// 本实现不持有宿主 MS DI 的回落引用（已移除「宿主服务透传」）：宿主应在初始化时把「应提供给插件的服务」
/// 显式 seed 进根上下文（对标 Cordis 的 <c>app.service(name, instance)</c>），子插件经父级链继承消费，
/// 而非让每个 Fiber 派生上下文回落宿主 DI 容器。这样既消除宿主 Scoped/Transient 生命周期语义失真，
/// 又让插件消费能力接缝（ILlmRuntime/ISessionStore/…）的方式与 Cordis 一致。
/// </remarks>
public sealed class Context : IContext, IDisposable
{
    private readonly ConcurrentDictionary<Type, object> _services = new();
    private readonly List<IDisposable> _disposers = new();
    private readonly Context? _parent;
    private readonly EventBus _events = new();

    public Context(Context? parent = null)
    {
        _parent = parent;
    }

    public IEventBus Events => _events;

    public void Register<TService>(TService instance) where TService : class
        => Register(typeof(TService), instance);

    public void Register(Type serviceType, object instance)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(instance);
        _services[serviceType] = instance;
    }

    public void Register<TService, TImpl>() where TService : class where TImpl : class, TService, new()
        => Register<TService>(new TImpl());

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
        if (_services.TryGetValue(serviceType, out var instance))
            return instance;

        // 仅继承父级上下文已注册的能力；不再回落宿主 MS DI（宿主服务已在初始化时 seed 进根上下文）。
        return _parent?.GetService(serviceType);
    }

    public void Dispose()
    {
        for (var i = _disposers.Count - 1; i >= 0; i--)
            _disposers[i].Dispose();
        _disposers.Clear();
        _services.Clear();
        _events.Dispose();
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
}

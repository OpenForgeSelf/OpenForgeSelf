namespace OpenForgeSelf.Core;

/// <summary>
/// 插件生命周期作用域（对标 Cordis 的 Fiber）：
/// 包装一个派生 <see cref="Context"/>，<see cref="Mount"/> 执行插件贡献函数，
/// <see cref="Dispose"/> 逆序回滚副作用并注销全部服务。释放操作幂等。
/// </summary>
public sealed class Fiber : IDisposable
{
    private readonly Context _scope;
    private int _disposed;

    public Fiber(IContext parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        _scope = new Context(parent as Context);
        Context = _scope;
    }

    public IContext Context { get; }

    public void Mount(Action<IContext> apply)
    {
        ArgumentNullException.ThrowIfNull(apply);
        apply(Context);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            _scope.Dispose();
    }
}

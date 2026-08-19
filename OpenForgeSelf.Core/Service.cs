namespace OpenForgeSelf.Core;

/// <summary>
/// 服务基类（对标 Cordis 的 Service）：贡献一个命名服务。
/// 派生类在构造函数中调用 <c>Context.Register&lt;TService&gt;(this)</c> 完成注册，
/// 通过 <see cref="Inject"/> 声明依赖的其他服务名（加载顺序由宿主据此拓扑排序）。
/// </summary>
public abstract class Service : IDisposable
{
    /// <summary>声明本服务依赖的其他服务名（对标 Cordis 的 inject）。</summary>
    public static IReadOnlyList<string> Inject { get; } = Array.Empty<string>();

    protected Service(IContext ctx, string name)
    {
        Context = ctx;
        Name = name;
    }

    public IContext Context { get; }

    public string Name { get; }

    protected virtual void Dispose(bool disposing)
    {
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

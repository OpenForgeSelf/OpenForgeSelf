namespace ForgeSelf.Abstractions;

/// <summary>
/// 工具单调守卫注册表（B8/A2）。
/// </summary>
/// <remarks>
/// <b>阻抗修正（偏离设计稿 §2.7）</b>：设计稿把本接口放 <c>ForgeSelf.Core</c>，但工程引用方向是
/// Abstractions → Core（Core 为底层，无法反向引用 Abstractions 的 <see cref="ToolExecution"/> 类型），
/// 故守卫契约与其载荷同面落 <b>ForgeSelf.Abstractions</b>。签名与设计稿逐字一致。
/// </remarks>
public interface IToolGuardRegistry
{
    /// <summary>
    /// 注册一个守卫。守卫返回 string = 拒绝理由；null = 维持现状。<b>没有 allow 结果——只减不增</b>：
    /// 任一守卫非 null → 最终拒绝，后续闸门（含 waterfall）无法撤销。
    /// 返回的 <see cref="IDisposable"/> 用于注销（插件卸载 / ctx.Effect 回滚时 Dispose，门禁 11）。
    /// </summary>
    IDisposable AddGuard(Func<ToolExecution, string?> guard);

    /// <summary>当前全部守卫（快照，线程安全）。</summary>
    IReadOnlyList<Func<ToolExecution, string?>> All();
}

/// <summary><see cref="IToolGuardRegistry"/> 默认实现：内存列表 + 锁，Dispose 即注销。</summary>
public sealed class ToolGuardRegistry : IToolGuardRegistry
{
    private readonly object _sync = new();
    private readonly List<Func<ToolExecution, string?>> _guards = new();

    /// <inheritdoc />
    public IDisposable AddGuard(Func<ToolExecution, string?> guard)
    {
        ArgumentNullException.ThrowIfNull(guard);

        lock (_sync)
        {
            _guards.Add(guard);
        }

        return new GuardHandle(this, guard);
    }

    /// <inheritdoc />
    public IReadOnlyList<Func<ToolExecution, string?>> All()
    {
        lock (_sync)
        {
            return _guards.ToArray();
        }
    }

    private void Remove(Func<ToolExecution, string?> guard)
    {
        lock (_sync)
        {
            _guards.Remove(guard);
        }
    }

    /// <summary>守卫句柄：Dispose 即从注册表移除对应守卫（幂等）。</summary>
    private sealed class GuardHandle(ToolGuardRegistry owner, Func<ToolExecution, string?> guard) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            owner.Remove(guard);
        }
    }
}

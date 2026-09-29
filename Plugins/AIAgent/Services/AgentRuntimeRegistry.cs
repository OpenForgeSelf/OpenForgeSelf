using System.Collections.Concurrent;
using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

/// <summary>
/// B5（041）Agent <b>运行时</b>注册表：按会话 id 管理 <see cref="IAgent"/> 生命周期（创建 / 复用 / 注销）。
/// </summary>
/// <remarks>
/// <b>命名说明</b>：本类<b>不是</b>同目录的 <c>AgentRegistryService.cs</c>（那是 Agent <b>人设/定义</b>注册表：
/// <c>RegisterAgent/GetAgent/…</c>）。041 §1 B5-4 已核实该文件被占用，故本运行时注册表命名为
/// <c>AgentRuntimeRegistry</c>，实现 <see cref="IAgentRegistry"/>。
/// <para>
/// 生命周期：注册表自身是<b>单例</b>（插件 Apply 阶段构造，同时注册进宿主 DI 与 ctx 共享服务表），
/// 会话 Agent 跨请求存活（连续对话复用同一实例）；注销时连带释放创建它时开立的 DI scope。
/// </para>
/// </remarks>
public sealed class AgentRuntimeRegistry : IAgentRegistry
{
    private readonly ConcurrentDictionary<string, Entry> _agents = new(StringComparer.Ordinal);
    private readonly Func<string, AgentOptions, IAgent> _factory;

    /// <param name="factory">
    /// Agent 工厂：由插件 Apply 阶段注入（解析 <see cref="IAIAgentService"/> → <c>CreateAgent</c>），
    /// 注册表只负责生命周期，不关心依赖怎么来。
    /// </param>
    public AgentRuntimeRegistry(Func<string, AgentOptions, IAgent> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <inheritdoc />
    public Task<IAgent> GetOrCreateAsync(string sessionId, AgentOptions? options = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        var effective = options ?? new AgentOptions();

        if (_agents.TryGetValue(sessionId, out var existing))
        {
            // 复用：同步最新选项（模型/工具白名单可能被用户改过），避免「换了模型却仍用旧 Agent」
            ResolveLoopAgent(existing.Agent)?.ApplyOptions(effective);
            return Task.FromResult(existing.Agent);
        }

        var created = _factory(sessionId, effective);
        _agents[sessionId] = new Entry(created);
        return Task.FromResult(created);
    }

    /// <inheritdoc />
    public Task<IAgent?> TryGetAsync(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return Task.FromResult(_agents.TryGetValue(sessionId, out var entry) ? entry.Agent : null);
    }

    /// <inheritdoc />
    public async Task<bool> DisposeAsync(string sessionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        if (!_agents.TryRemove(sessionId, out var entry))
        {
            return false;
        }

        switch (entry.Agent)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync();
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }

        return true;
    }

    /// <summary>从可能带 scope 包装的 Agent 里取出状态机本体。</summary>
    private static ReactLoopAgent? ResolveLoopAgent(IAgent agent)
        => agent switch
        {
            ReactLoopAgent loop => loop,
            ScopedAgent scoped => scoped.Inner as ReactLoopAgent,
            _ => null
        };

    private sealed record Entry(IAgent Agent);
}

/// <summary>
/// 带 DI scope 的 Agent 包装：会话 Agent 存活期长于单个请求，其依赖（scoped 服务）必须挂在自己的 scope 上，
/// 注销时连 scope 一起释放，杜绝「请求结束 → 依赖被回收 → Agent 悬空」。
/// </summary>
public sealed class ScopedAgent : IAgent, IAsyncDisposable
{
    /// <summary>被包装的 Agent（状态机本体）。</summary>
    public IAgent Inner { get; }

    private readonly IDisposable? _scope;
    private bool _disposed;

    public ScopedAgent(IAgent inner, IDisposable? scope)
    {
        Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _scope = scope;
    }

    /// <inheritdoc />
    public string SessionId => Inner.SessionId;

    /// <inheritdoc />
    public AgentOptions Options => Inner.Options;

    /// <inheritdoc />
    public IInbox Inbox => Inner.Inbox;

    /// <inheritdoc />
    public AgentStatus Status => Inner.Status;

    /// <inheritdoc />
    public IAsyncEnumerable<TurnFrame> RunAsync(CancellationToken ct = default) => Inner.RunAsync(ct);

    /// <inheritdoc />
    public Task CancelAsync(AgentCancelCause cause, bool keepInbox = false) => Inner.CancelAsync(cause, keepInbox);

    /// <inheritdoc />
    public Task WhenIdleAsync(CancellationToken ct = default) => Inner.WhenIdleAsync(ct);

    /// <summary>释放：先释放 Agent 本体，再释放 DI scope。</summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (Inner is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (Inner is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _scope?.Dispose();
    }
}

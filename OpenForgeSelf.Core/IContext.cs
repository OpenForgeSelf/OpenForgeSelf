namespace OpenForgeSelf.Core;

/// <summary>
/// 共享上下文：服务定位器 + 可逆副作用 + 事件总线。
/// 对标 deepseek-harness / Cordis 的 Context——「没有内核，只有插件」的底座。
/// </summary>
public interface IContext : IServiceProvider
{
    /// <summary>注册一个能力（服务定义 = 接口，服务提供者 = 实例）。</summary>
    void Register<TService>(TService instance) where TService : class;

    /// <summary>注册一个能力（按实现类型创建实例）。</summary>
    void Register<TService, TImpl>() where TService : class where TImpl : class, TService, new();

    /// <summary>消费一个能力；不存在时返回 null（探测而非强断）。</summary>
    TService? Get<TService>() where TService : class;

    /// <summary>注册可逆副作用：返回的 disposer 在插件卸载时（或手动调用时）被释放。</summary>
    IDisposable Effect(Func<IDisposable> sideEffect);

    /// <summary>类型化事件总线。</summary>
    IEventBus Events { get; }

    /// <summary>派生上下文（用于会话/作用域隔离，注册可被独立清理）。</summary>
    IContext Derive();
}

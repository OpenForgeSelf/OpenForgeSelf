namespace OpenForgeSelf.Core;

/// <summary>
/// 共享上下文：服务定位器 + 可逆副作用 + 事件总线。
/// 对标 deepseek-harness / Cordis 的 Context——「没有内核，只有插件」的底座。
/// </summary>
public interface IContext : IServiceProvider
{
    /// <summary>
    /// 注册一个全局服务（对标 Cordis <c>provide()</c>）：写入 root 共享服务表，所有上下文（含兄弟 Fiber）可见；
    /// 提供即 effect，所属 Fiber 逆序回滚时自动摘除。
    /// </summary>
    void Register<TService>(TService instance) where TService : class;

    /// <summary>注册一个全局服务（按实现类型创建实例）。</summary>
    void Register<TService, TImpl>() where TService : class where TImpl : class, TService, new();

    /// <summary>
    /// 注册一个本地值（对标 Cordis 直接赋值非声明属性）：仅当前上下文可见，不进入共享表，
    /// 兄弟插件不可见；用于插件框架私有对象（PluginMetadata/IServiceCollection 等）。
    /// </summary>
    void RegisterLocal<TService>(TService instance) where TService : class;

    /// <summary>消费一个能力；不存在时返回 null（探测而非强断）。解析顺序 = 本地值 → 全局共享表。</summary>
    TService? Get<TService>() where TService : class;

    /// <summary>注册可逆副作用：返回的 disposer 在插件卸载时（或手动调用时）被释放。</summary>
    IDisposable Effect(Func<IDisposable> sideEffect);

    /// <summary>类型化事件总线。</summary>
    IEventBus Events { get; }

    /// <summary>派生上下文（用于会话/作用域隔离，注册可被独立清理）。</summary>
    IContext Derive();
}

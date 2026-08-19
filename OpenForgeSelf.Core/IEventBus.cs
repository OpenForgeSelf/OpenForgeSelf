namespace OpenForgeSelf.Core;

/// <summary>
/// 类型化事件总线。四种分发模式对标 Cordis 的 emit / waterfall / parallel / serial。
/// 事件名用「命名空间/动作」风格，如 tools/pre-execute、agent/request、session/event。
/// </summary>
public interface IEventBus
{
    /// <summary>同步广播：依次等待每个监听器，忽略返回值。</summary>
    Task EmitAsync<TEvent>(string name, TEvent payload);

    /// <summary>环绕中间件：每个监听器可转换 next() 结果或短路。</summary>
    Task<TResult> WaterfallAsync<TEvent, TResult>(string name, TEvent payload, Func<Task<TResult>> fallback);

    /// <summary>并发执行所有监听器并等待全部完成。</summary>
    Task ParallelAsync<TEvent>(string name, TEvent payload);

    /// <summary>按序执行，第一个返回非 null 的监听器短路并返回该值。</summary>
    Task<TResult?> SerialAsync<TEvent, TResult>(string name, TEvent payload);

    /// <summary>注册监听器，返回用于注销的句柄。</summary>
    IDisposable On<TEvent>(string name, Func<TEvent, Task> handler);

    /// <summary>注册按序短路监听器（供 <see cref="SerialAsync{TEvent, TResult}"/> 消费），返回用于注销的句柄。</summary>
    IDisposable OnSerial<TEvent, TResult>(string name, Func<TEvent, Task<TResult?>> handler);

    /// <summary>注册环绕中间件（供 <see cref="WaterfallAsync{TEvent, TResult}"/> 消费），返回用于注销的句柄。</summary>
    IDisposable OnWaterfall<TEvent, TResult>(string name, Func<TEvent, Func<Task<TResult>>, Task<TResult>> handler);
}

using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Services;

/// <summary>
/// 极简事件广播器（零依赖，不引入 System.Reactive）：线程安全的订阅/推送/退订。
/// 两个 <see cref="ISessionStore"/> 实现（内存测试替身与持久化实现）共用，
/// 保证观察流语义一致，不各写一份。
/// </summary>
internal sealed class SessionEventSubject : IObservable<SessionEvent>
{
    private readonly object _gate = new();
    private readonly List<IObserver<SessionEvent>> _observers = new();

    public IDisposable Subscribe(IObserver<SessionEvent> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        lock (_gate)
        {
            _observers.Add(observer);
        }

        return new Subscription(this, observer);
    }

    public void OnNext(SessionEvent evt)
    {
        IObserver<SessionEvent>[] snapshot;
        lock (_gate)
        {
            snapshot = _observers.ToArray();
        }

        foreach (var observer in snapshot)
        {
            observer.OnNext(evt);
        }
    }

    private void Unsubscribe(IObserver<SessionEvent> observer)
    {
        lock (_gate)
        {
            _observers.Remove(observer);
        }
    }

    private sealed class Subscription(SessionEventSubject owner, IObserver<SessionEvent> observer) : IDisposable
    {
        public void Dispose() => owner.Unsubscribe(observer);
    }
}

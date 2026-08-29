using System.Collections.Concurrent;

namespace ForgeSelf.Core;

public sealed class EventBus : IEventBus, IDisposable
{
    private readonly ConcurrentDictionary<string, List<object>> _handlers = new();

    public IDisposable On<TEvent>(string name, Func<TEvent, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var list = _handlers.GetOrAdd(name, static _ => new List<object>());
        lock (list)
        {
            list.Add(handler);
        }

        return Disposable.Create(() =>
        {
            lock (list)
            {
                list.Remove(handler);
            }
        });
    }

    public IDisposable OnSerial<TEvent, TResult>(string name, Func<TEvent, Task<TResult?>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var list = _handlers.GetOrAdd(name, static _ => new List<object>());
        lock (list)
        {
            list.Add(handler);
        }

        return Disposable.Create(() =>
        {
            lock (list)
            {
                list.Remove(handler);
            }
        });
    }

    public IDisposable OnWaterfall<TEvent, TResult>(string name, Func<TEvent, Func<Task<TResult>>, Task<TResult>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var list = _handlers.GetOrAdd(name, static _ => new List<object>());
        lock (list)
        {
            list.Add(handler);
        }

        return Disposable.Create(() =>
        {
            lock (list)
            {
                list.Remove(handler);
            }
        });
    }

    public async Task EmitAsync<TEvent>(string name, TEvent payload)
    {
        if (!_handlers.TryGetValue(name, out var list))
            return;

        foreach (var h in Snapshot(list))
        {
            if (h is Func<TEvent, Task> fn)
                await fn(payload);
        }
    }

    public async Task ParallelAsync<TEvent>(string name, TEvent payload)
    {
        if (!_handlers.TryGetValue(name, out var list))
            return;

        var tasks = Snapshot(list)
            .OfType<Func<TEvent, Task>>()
            .Select(fn => fn(payload))
            .ToArray();

        await Task.WhenAll(tasks);
    }

    public async Task<TResult?> SerialAsync<TEvent, TResult>(string name, TEvent payload)
    {
        if (!_handlers.TryGetValue(name, out var list))
            return default;

        foreach (var h in Snapshot(list))
        {
            if (h is Func<TEvent, Task<TResult?>> fn)
            {
                var result = await fn(payload);
                if (result is not null)
                    return result;
            }
        }

        return default;
    }

    public async Task<TResult> WaterfallAsync<TEvent, TResult>(
        string name,
        TEvent payload,
        Func<Task<TResult>> fallback)
    {
        if (!_handlers.TryGetValue(name, out var list) || list.Count == 0)
            return await fallback();

        Func<Task<TResult>> next = fallback;
        var middleware = Snapshot(list)
            .OfType<Func<TEvent, Func<Task<TResult>>, Task<TResult>>>()
            .ToArray();

        for (var i = middleware.Length - 1; i >= 0; i--)
        {
            var captured = next;
            var mw = middleware[i];
            next = () => mw(payload, captured);
        }

        return await next();
    }

    public void Dispose() => _handlers.Clear();

    private static object[] Snapshot(List<object> list)
    {
        lock (list)
        {
            return list.ToArray();
        }
    }
}

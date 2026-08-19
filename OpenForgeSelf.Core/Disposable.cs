namespace OpenForgeSelf.Core;

public static class Disposable
{
    public static IDisposable Create(Action action) => new ActionDisposable(action);

    private sealed class ActionDisposable : IDisposable
    {
        private Action? _action;

        public ActionDisposable(Action action) => _action = action;

        public void Dispose() => Interlocked.Exchange(ref _action, null)?.Invoke();
    }
}

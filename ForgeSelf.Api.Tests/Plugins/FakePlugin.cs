using ForgeSelf.Abstractions;
using ForgeSelf.Core;

namespace ForgeSelf.Api.Tests.Plugins;

public class FakePlugin : IPlugin
{
    public bool ApplyCalled { get; private set; }
    public IContext? Context { get; private set; }
    public Exception? ApplyException { get; set; }

    public void Apply(IContext ctx)
    {
        ApplyCalled = true;
        Context = ctx;
        if (ApplyException != null)
            throw ApplyException;
    }

    public void Reset()
    {
        ApplyCalled = false;
        Context = null;
        ApplyException = null;
    }
}

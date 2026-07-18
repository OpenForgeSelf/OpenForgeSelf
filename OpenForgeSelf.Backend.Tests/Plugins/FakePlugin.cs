using OpenForgeSelf.Backend.Plugins.Abstractions;

namespace OpenForgeSelf.Backend.Tests.Plugins;

public class FakePlugin : IPlugin
{
    public string Id { get; set; } = "test.fake.plugin";
    public string Name { get; set; } = "Fake Plugin";
    public string Version { get; set; } = "1.0.0";
    public string Author { get; set; } = "Test Author";
    public string Description { get; set; } = "A fake plugin for testing";
    public string IconUrl { get; set; } = "https://example.com/icon.png";

    public bool InitializeCalled { get; private set; }
    public bool StartCalled { get; private set; }
    public bool StopCalled { get; private set; }
    public bool DestroyCalled { get; private set; }

    public IServiceProvider? ServiceProvider { get; private set; }

    public Exception? InitializeException { get; set; }
    public Exception? StartException { get; set; }
    public Exception? StopException { get; set; }
    public Exception? DestroyException { get; set; }

    public void Initialize(IServiceProvider services)
    {
        InitializeCalled = true;
        ServiceProvider = services;
        if (InitializeException != null)
            throw InitializeException;
    }

    public void Start()
    {
        StartCalled = true;
        if (StartException != null)
            throw StartException;
    }

    public void Stop()
    {
        StopCalled = true;
        if (StopException != null)
            throw StopException;
    }

    public void Destroy()
    {
        DestroyCalled = true;
        if (DestroyException != null)
            throw DestroyException;
    }

    public void Reset()
    {
        InitializeCalled = false;
        StartCalled = false;
        StopCalled = false;
        DestroyCalled = false;
        ServiceProvider = null;
        InitializeException = null;
        StartException = null;
        StopException = null;
        DestroyException = null;
    }
}

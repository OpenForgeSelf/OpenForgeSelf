using System.ServiceProcess;

namespace ForgeSelf.Api.Services;

/// <summary>
/// <see cref="IServiceController"/> 的默认实现，包装 <see cref="ServiceController"/>。
/// </summary>
public class ServiceControllerWrapper : IServiceController
{
    private readonly ServiceController _controller;

    public ServiceControllerWrapper(string serviceName)
    {
        _controller = new ServiceController(serviceName);
    }

    public ServiceControllerStatus Status => _controller.Status;

    public void Dispose() => _controller.Dispose();

    public void Refresh() => _controller.Refresh();

    public void Start() => _controller.Start();

    public void Stop() => _controller.Stop();

    public void WaitForStatus(ServiceControllerStatus desiredStatus, TimeSpan timeout)
        => _controller.WaitForStatus(desiredStatus, timeout);
}
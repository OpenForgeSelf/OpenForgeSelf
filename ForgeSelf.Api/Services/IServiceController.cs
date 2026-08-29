using System.ServiceProcess;

namespace ForgeSelf.Api.Services;

/// <summary>
/// 抽象 Windows 服务控制器接口，用于解耦 <see cref="ServiceManager"/> 对
/// <see cref="ServiceController"/> 的直接依赖，便于单元测试。
/// </summary>
public interface IServiceController : IDisposable
{
    /// <summary>获取服务的当前状态。</summary>
    ServiceControllerStatus Status { get; }

    /// <summary>刷新服务状态。</summary>
    void Refresh();

    /// <summary>启动服务。</summary>
    void Start();

    /// <summary>停止服务。</summary>
    void Stop();

    /// <summary>等待服务达到指定状态，超时后抛出 <see cref="System.ServiceProcess.TimeoutException"/>。</summary>
    void WaitForStatus(ServiceControllerStatus desiredStatus, TimeSpan timeout);
}
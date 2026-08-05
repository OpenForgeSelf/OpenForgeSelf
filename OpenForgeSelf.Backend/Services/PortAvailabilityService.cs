using System.Net.NetworkInformation;

namespace OpenForgeSelf.Backend.Services;

/// <summary>端口可用性检测服务接口</summary>
public interface IPortAvailabilityService
{
  /// <summary>检查端口是否被占用</summary>
  /// <param name="port">端口号</param>
  /// <returns>true 表示可用，false 表示已被占用</returns>
  Task<Boolean> IsPortInUseAsync(Int32 port);
}

/// <summary>端口可用性检测服务实现</summary>
public class PortAvailabilityService : IPortAvailabilityService
{
  /// <summary>检查端口是否被占用</summary>
  public Task<Boolean> IsPortInUseAsync(Int32 port)
  {
    var isInUse = IPGlobalProperties
        .GetIPGlobalProperties()
        .GetActiveTcpListeners()
        .Any(endpoint => endpoint.Port == port);

    return Task.FromResult(isInUse);
  }
}

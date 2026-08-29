using ForgeSelf.Api.Models;

namespace ForgeSelf.Api.Services;

/// <summary>端口配置服务</summary>
public interface IPortConfigurationService
{
  /// <summary>获取当前配置的端口</summary>
  /// <returns>端口号，未配置则返回 null</returns>
  Task<Int32?> GetPortAsync(CancellationToken cancellationToken = default);

  /// <summary>设置监听端口</summary>
  /// <param name="port">端口号（1024-65535）</param>
  /// <param name="cancellationToken">取消令牌</param>
  /// <returns>更新后的端口号</returns>
  Task<Int32> SetPortAsync(Int32 port, CancellationToken cancellationToken = default);

  /// <summary>检查端口是否可用</summary>
  /// <param name="port">端口号</param>
  /// <param name="cancellationToken">取消令牌</param>
  /// <returns>true 表示可用，false 表示已被占用</returns>
  Task<Boolean> IsPortAvailableAsync(Int32 port, CancellationToken cancellationToken = default);
}

/// <summary>端口配置服务实现（基于配置文件）</summary>
public class PortConfigurationService : IPortConfigurationService
{
  private readonly IPortAvailabilityService _portAvailabilityService;

  public PortConfigurationService(IPortAvailabilityService portAvailabilityService)
  {
    _portAvailabilityService = portAvailabilityService;
  }

  /// <summary>获取当前配置的端口</summary>
  public Task<Int32?> GetPortAsync(CancellationToken cancellationToken = default)
  {
    return Task.FromResult<Int32?>(ForgeSetting.Current.PortNumber);
  }

  /// <summary>设置监听端口</summary>
  public async Task<Int32> SetPortAsync(Int32 port, CancellationToken cancellationToken = default)
  {
    if (port < 1024 || port > 65535)
      throw new ArgumentOutOfRangeException(nameof(port), "端口号必须在 1024-65535 范围内");

    // 检查端口是否可用
    var isInUse = await _portAvailabilityService.IsPortInUseAsync(port);
    var available = !isInUse;
    System.Diagnostics.Debug.WriteLine($"[PortConfig] 检查端口 {port}: IsPortInUse={isInUse}, Available={available}");
    if (!available)
      throw new InvalidOperationException($"端口 {port} 已被占用，请选择其他端口");

    // 更新配置文件
    ForgeSetting.Current.PortNumber = port;
    ForgeSetting.Current.Save();

    return port;
  }

  /// <summary>检查端口是否可用</summary>
  public async Task<Boolean> IsPortAvailableAsync(Int32 port, CancellationToken cancellationToken = default)
  {
    // IsPortInUseAsync 返回 true 表示被占用，取反后 true 表示可用
    var isInUse = await _portAvailabilityService.IsPortInUseAsync(port);
    return !isInUse;
  }
}

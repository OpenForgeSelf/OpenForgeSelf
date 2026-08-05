using System.Diagnostics;
using OpenForgeSelf.Backend.Models;

namespace OpenForgeSelf.Backend.Services;

/// <summary>应用程序重启服务接口</summary>
public interface IApplicationRestartService
{
  /// <summary>重启当前应用程序（从 Forge 配置文件读取新端口）</summary>
  /// <param name="delaySeconds">延迟秒数</param>
  Task RestartAsync(Int32 delaySeconds = 5);
}

/// <summary>应用程序重启服务实现（从 Forge 配置文件读取端口）</summary>
public class ApplicationRestartService : IApplicationRestartService
{
  private readonly ILogger<ApplicationRestartService> _logger;
  private readonly IHostApplicationLifetime _lifetime;

  public ApplicationRestartService(
      ILogger<ApplicationRestartService> logger,
      IHostApplicationLifetime lifetime)
  {
    _logger = logger;
    _lifetime = lifetime;
  }

  /// <summary>重启当前应用程序（从 Forge 配置文件读取新端口）</summary>
  public async Task RestartAsync(Int32 delaySeconds = 5)
  {
    var newPort = ForgeSetting.Current.PortNumber;
    var currentArgs = Environment.GetCommandLineArgs();

    // 判断启动模式：无参数或带 --console 都是控制台模式
    var isConsoleMode = currentArgs.Length == 0 || currentArgs.Contains("--console");

    _logger.LogInformation("检测到启动模式：{Mode}，将在 {Delay} 秒后重启，新端口：{Port}...",
        isConsoleMode ? "控制台模式" : "服务模式", delaySeconds, newPort);

    // 服务模式不支持自动重启
    if (!isConsoleMode)
    {
      _logger.LogWarning("服务模式下不支持自动重启，请手动重启服务");
      throw new InvalidOperationException("服务模式下不支持自动重启，请通过服务管理器重启");
    }

    // 延迟指定时间
    await Task.Delay(delaySeconds * 1000);

    // 获取当前进程路径
    var processPath = Process.GetCurrentProcess().MainModule?.FileName ??
                      AppContext.BaseDirectory;

    _logger.LogInformation("重启进程：{Path}，新端口：{Port}", processPath, newPort);

    // 启动新进程（控制台模式）
    var startInfo = new ProcessStartInfo
    {
      FileName = processPath,
      UseShellExecute = true,
      WindowStyle = ProcessWindowStyle.Normal
    };

    // 始终带 --console 参数
    startInfo.ArgumentList.Add("--console");

    var newProcess = Process.Start(startInfo);
    _logger.LogInformation("新进程已启动，PID: {Pid}", newProcess.Id);

    // 立即停止当前应用
    _logger.LogInformation("正在停止当前应用...");
    _lifetime.StopApplication();
  }
}

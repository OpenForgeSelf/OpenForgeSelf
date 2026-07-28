using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using Microsoft.Extensions.Options;
using NewLife.Log;
using OpenForgeSelf.Backend.Models;

namespace OpenForgeSelf.Backend.Services;

/// <summary>
/// Windows 服务管理器。
/// 封装服务的安装/卸载/状态检测/启停控制，通过 SCM (Service Control Manager) 操作。
/// 读取 <see cref="ServiceConfig"/> 获取服务元数据（服务名、显示名、描述）。
/// 参考 data-model.md §1 ServiceInstallState 状态驱动逻辑：
///   - IsInstalled == false → 菜单显示「安装服务」
///   - IsInstalled == true  → 菜单显示「卸载服务」、「重启服务」
///   - Status == Running    → 「重启服务」可点击
/// </summary>
public class ServiceManager : IServiceManager
{
    private readonly ServiceConfig _config;
    private readonly string _serviceFilePath;
    private readonly Func<string, IServiceController> _controllerFactory;

    /// <summary>
    /// 初始化服务管理器。
    /// </summary>
    /// <param name="config">服务配置</param>
    /// <param name="serviceFilePath">
    /// 当前可执行文件路径，用于服务注册时的 binPath。
    /// 默认取 <see cref="Environment.ProcessPath"/>。
    /// </param>
    /// <param name="controllerFactory">
    /// 服务控制器工厂，用于创建 <see cref="IServiceController"/> 实例。
    /// 默认使用 <see cref="ServiceControllerWrapper"/>；传入 mock 工厂以支持单元测试。
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> 为 null</exception>
    /// <exception cref="InvalidOperationException">无法确定可执行文件路径</exception>
    public ServiceManager(
        ServiceConfig config,
        string? serviceFilePath = null,
        Func<string, IServiceController>? controllerFactory = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _serviceFilePath = serviceFilePath ?? Environment.ProcessPath
            ?? throw new InvalidOperationException("无法确定可执行文件路径");
        _controllerFactory = controllerFactory ?? (name => new ServiceControllerWrapper(name));
    }

    /// <summary>
    /// 查询服务是否已安装到 SCM。
    /// </summary>
    /// <returns>服务是否已安装</returns>
    public bool IsInstalled()
    {
        try
        {
            using var sc = _controllerFactory(_config.ServiceName);
            // 不抛异常即存在
            _ = sc.Status;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// 获取服务当前运行状态。
    /// 若服务未安装，返回 <see cref="ServiceControllerStatus.Stopped"/>。
    /// </summary>
    /// <returns>服务控制器状态</returns>
    public ServiceControllerStatus GetStatus()
    {
        try
        {
            using var sc = _controllerFactory(_config.ServiceName);
            sc.Refresh();
            return sc.Status;
        }
        catch (InvalidOperationException)
        {
            // 服务未安装，视为 Stopped
            return ServiceControllerStatus.Stopped;
        }
    }

    /// <summary>
    /// 安装 Windows 服务。
    /// 需要管理员权限，否则抛出 <see cref="UnauthorizedAccessException"/>。
    /// 使用 sc.exe create 注册服务，并设置显示名称、描述和启动类型。
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">当前进程不具有管理员权限</exception>
    /// <exception cref="Win32Exception">sc.exe 执行失败</exception>
    public void Install()
    {
        if (!RequireAdmin())
            throw new UnauthorizedAccessException("安装服务需要管理员权限，请以管理员身份运行。");

        if (IsInstalled())
        {
            XTrace.Log.Info("服务 '{0}' 已安装，跳过安装。", _config.ServiceName);
            return;
        }

        XTrace.Log.Info("正在安装服务 '{0}'...", _config.ServiceName);

        // sc.exe create 语法：参数名= 值（等号后必须有一个空格）
        var args = $"create \"{_config.ServiceName}\"" +
            $" binPath= \"{_serviceFilePath}\"" +
            $" displayName= \"{_config.DisplayName}\"" +
            $" start= {(_config.AutoStart ? "auto" : "demand")}";

        if (!RunScCommand(args))
            throw new Win32Exception("sc.exe create 失败。");

        // 单独设置服务描述（sc.exe create 不支持 description 参数）
        RunScCommand($"description \"{_config.ServiceName}\" \"{_config.Description}\"");

        XTrace.Log.Info("服务 '{0}' 安装成功。", _config.ServiceName);
    }

    /// <summary>
    /// 卸载 Windows 服务。
    /// 需要管理员权限，否则抛出 <see cref="UnauthorizedAccessException"/>。
    /// 卸载前自动停止正在运行的服务。
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">当前进程不具有管理员权限</exception>
    /// <exception cref="Win32Exception">sc.exe 执行失败</exception>
    public void Uninstall()
    {
        if (!RequireAdmin())
            throw new UnauthorizedAccessException("卸载服务需要管理员权限，请以管理员身份运行。");

        if (!IsInstalled())
        {
            XTrace.Log.Info("服务 '{0}' 未安装，跳过卸载。", _config.ServiceName);
            return;
        }

        // 如果服务正在运行，先停止
        if (GetStatus() == ServiceControllerStatus.Running)
        {
            Stop();
        }

        XTrace.Log.Info("正在卸载服务 '{0}'...", _config.ServiceName);

        if (!RunScCommand($"delete \"{_config.ServiceName}\""))
            throw new Win32Exception("sc.exe delete 失败。");

        XTrace.Log.Info("服务 '{0}' 卸载成功。", _config.ServiceName);
    }

    /// <summary>
    /// 重启服务：停止后启动。
    /// </summary>
    /// <exception cref="InvalidOperationException">服务未安装</exception>
    public void Restart()
    {
        if (!IsInstalled())
            throw new InvalidOperationException($"服务 '{_config.ServiceName}' 未安装，无法重启。");

        Stop();
        Start();
    }

    /// <summary>
    /// 启动服务。
    /// </summary>
    /// <exception cref="InvalidOperationException">启动失败</exception>
    public void Start()
    {
        try
        {
            using var sc = _controllerFactory(_config.ServiceName);
            sc.Refresh();

            if (sc.Status == ServiceControllerStatus.Running)
            {
                XTrace.Log.Info("服务 '{0}' 已在运行。", _config.ServiceName);
                return;
            }

            XTrace.Log.Info("正在启动服务 '{0}'...", _config.ServiceName);
            sc.Start();
            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            XTrace.Log.Info("服务 '{0}' 启动成功。", _config.ServiceName);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            throw new InvalidOperationException($"启动服务 '{_config.ServiceName}' 失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 停止服务。
    /// </summary>
    /// <exception cref="InvalidOperationException">停止失败</exception>
    public void Stop()
    {
        try
        {
            using var sc = _controllerFactory(_config.ServiceName);
            sc.Refresh();

            if (sc.Status == ServiceControllerStatus.Stopped)
            {
                XTrace.Log.Info("服务 '{0}' 已停止。", _config.ServiceName);
                return;
            }

            XTrace.Log.Info("正在停止服务 '{0}'...", _config.ServiceName);
            sc.Stop();
            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            XTrace.Log.Info("服务 '{0}' 已停止。", _config.ServiceName);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            throw new InvalidOperationException($"停止服务 '{_config.ServiceName}' 失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 检测当前进程是否有管理员权限。
    /// 非 Windows 平台始终返回 false。
    /// </summary>
    /// <returns>是否有管理员权限</returns>
    public static bool RequireAdmin()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 执行 sc.exe 命令，等待退出并返回是否成功。
    /// </summary>
    /// <param name="arguments">sc.exe 命令行参数</param>
    /// <returns>退出码为 0 时返回 true</returns>
    private static bool RunScCommand(string arguments)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };

            process.Start();
            // 等待退出，30 秒超时
            var exited = process.WaitForExit(30_000);

            if (!exited)
            {
                XTrace.Log.Error("sc.exe 执行超时 (30s): {0}", arguments);
                try { process.Kill(entireProcessTree: true); } catch { /* 忽略 */ }
                return false;
            }

            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                XTrace.Log.Error("sc.exe 执行失败 (exit={0}): {1}", process.ExitCode, error);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("sc.exe 执行异常: {0}", ex.Message);
            return false;
        }
    }
}
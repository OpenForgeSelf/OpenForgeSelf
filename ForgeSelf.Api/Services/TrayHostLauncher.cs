using System.Diagnostics;
using NewLife.Log;
using ForgeSelf.Api.Models;

namespace ForgeSelf.Api.Services;

/// <summary>
/// 在 Windows 上拉起独立的 ForgeSelf.Tray 进程。Linux 与 FORGESELF_NO_TRAY=1 都不拉起。
/// </summary>
public static class TrayHostLauncher
{
    public const string NoTrayVariable = "FORGESELF_NO_TRAY";
    public const string TrayExeVariable = "FORGESELF_TRAY_EXE";

    public static bool ShouldLaunch(bool isWindows, string? noTrayValue)
        => isWindows && !string.Equals(noTrayValue, "1", StringComparison.Ordinal);

    /// <summary>交互模式：托盘点「退出」会通过管道让本进程停止。</summary>
    public static ServicePipeServer? StartInteractive(WebApplication app)
    {
        if (!ShouldLaunch(OperatingSystem.IsWindows(), Environment.GetEnvironmentVariable(NoTrayVariable)))
        {
            XTrace.Log.Info("跳过托盘进程（非 Windows 或 {0}=1）", NoTrayVariable);
            return null;
        }

        var trayExe = FindTrayExecutable();
        if (trayExe == null)
        {
            XTrace.Log.Warn("未找到 ForgeSelf.Tray.exe，本次不显示托盘");
            return null;
        }

        var port = ForgeSetting.Current.PortNumber;
        var pipeName = $"ForgeSelf-Tray-{Environment.ProcessId}";
        var server = new ServicePipeServer(pipeName);
        server.OnClientShutdown = () =>
        {
            XTrace.Log.Info("托盘请求退出，停止 Web 宿主");
            _ = app.StopAsync();
        };
        server.Start();

        var started = StartTrayProcess(trayExe, port, pipeName, "interactive");
        if (!started)
        {
            server.Dispose();
            return null;
        }

        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Register(() => server.SendShutdown());
        return server;
    }

    public static bool StartTrayProcess(string trayExe, int port, string pipeName, string role)
    {
        var arguments = TrayProcessStarter.BuildTrayArguments(port, pipeName, role, ResolveApiExecutable());
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = trayExe,
                Arguments = arguments,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(trayExe) ?? AppContext.BaseDirectory,
            });
            if (process == null)
            {
                XTrace.Log.Warn("启动托盘进程失败：Process.Start 返回 null");
                return false;
            }

            XTrace.Log.Info("已启动托盘进程 pid={0} role={1}", process.Id, role);
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("启动托盘进程失败: {0}", ex.Message);
            return false;
        }
    }

    public static string? FindTrayExecutable()
    {
        var fromEnv = Environment.GetEnvironmentVariable(TrayExeVariable);
        if (!string.IsNullOrWhiteSpace(fromEnv) && File.Exists(fromEnv))
            return Path.GetFullPath(fromEnv);

        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "ForgeSelf.Tray.exe"),
        };

        var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        foreach (var configuration in new[] { "Debug", "Release" })
        {
            candidates.Add(Path.Combine(
                repo, "ForgeSelf.Tray", "bin", configuration, "net10.0-windows", "ForgeSelf.Tray.exe"));
            candidates.Add(Path.Combine(
                repo, "ForgeSelf.Tray", "bin", configuration, "net10.0-windows", "win-x64", "ForgeSelf.Tray.exe"));
        }

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        return null;
    }

    /// <summary>
    /// 服务注册用的业务层入口。优先已发布的 ForgeSelf.exe，开发态回退到 ForgeSelf.dll。
    /// </summary>
    public static string ResolveApiExecutable()
    {
        var process = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(process)
            && process.EndsWith("ForgeSelf.exe", StringComparison.OrdinalIgnoreCase))
            return process;

        var exe = Path.Combine(AppContext.BaseDirectory, "ForgeSelf.exe");
        if (File.Exists(exe)) return exe;

        var dll = Path.Combine(AppContext.BaseDirectory, "ForgeSelf.dll");
        if (File.Exists(dll)) return dll;

        return process ?? dll;
    }
}

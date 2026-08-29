using Microsoft.Extensions.Options;
using NewLife.Agent;
using NewLife.Log;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Services;

namespace ForgeSelf.Api;

/// <summary>
/// Windows 服务宿主，继承 <see cref="ServiceBase"/>。
/// OnStart 中构建并启动 ASP.NET Core Web Host，
/// OnStop 中优雅关闭。
/// 服务模式下可通过 <see cref="ServiceConfig.ShowTrayInServiceMode"/> 选择启动托盘辅助进程。
/// </summary>
public class WindowsService : ServiceBase
{
    private WebApplication? _app;
    private ServicePipeServer? _pipeServer;

    /// <summary>
    /// 初始化 Windows 服务实例。
    /// 设置服务名称、显示名称和描述。
    /// </summary>
    public WindowsService()
    {
        ServiceName = "ForgeSelf";
        DisplayName = "铸己匣 ForgeSelf 服务";
        Description = "ForgeSelf 后端服务，提供 AI 助手、自动化工作流和 Web 管理界面";
    }

    /// <summary>
    /// 服务启动时由 SCM 调用。构建并启动 ASP.NET Core Web Host。
    /// 启动后检查 <see cref="ServiceConfig.ShowTrayInServiceMode"/>，
    /// 若为 true 则通过 <see cref="TrayProcessStarter"/> 在用户会话中启动托盘辅助进程。
    /// </summary>
    /// <param name="reason">启动原因。</param>
    public override void StartWork(string reason)
    {
        XTrace.Log.Info("WindowsService.StartWork: 开始构建 Web Host (reason: {0})", reason);

        _app = AppBuilder.CreateWebApplication(Environment.GetCommandLineArgs());

        // StartAsync 非阻塞启动，.GetAwaiter().GetResult() 等待启动完成再返回
        // 确保 SCM 认为服务已就绪
        _app.StartAsync().GetAwaiter().GetResult();

        XTrace.Log.Info("WindowsService.StartWork: Web Host 启动完成，服务运行中");

        // 检查是否需要在服务模式下启动托盘辅助进程
        TryStartTrayProcess();
    }

    /// <summary>
    /// 读取配置，若 <see cref="ServiceConfig.ShowTrayInServiceMode"/> 为 true，
    /// 则启动命名管道服务器并在用户会话中启动托盘辅助进程。
    /// 辅助进程复用主程序 executable，通过 <c>--tray</c> 参数模式运行。
    /// 参考 research.md §5 Session 0 隔离问题：服务运行在 Session 0，
    /// 无法直接显示托盘图标，需通过 CreateProcessAsUser 在用户会话中启动。
    /// </summary>
    private void TryStartTrayProcess()
    {
        try
        {
            var showTray = _app!.Configuration.GetValue<bool>("Service:ShowTrayInServiceMode", false);
            if (!showTray)
            {
                XTrace.Log.Info("WindowsService: Service.ShowTrayInServiceMode=false，服务模式下不显示托盘图标");
                return;
            }

            XTrace.Log.Info("WindowsService: Service.ShowTrayInServiceMode=true，准备启动托盘辅助进程");

            // 获取端口和可执行文件路径（使用 ForgeSetting 配置的端口）
            var port = ForgeSetting.Current.PortNumber;
            var executablePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("无法确定可执行文件路径");

            // 创建命名管道服务器（管道名称基于当前进程 ID，确保唯一性）
            var pipeName = $"ForgeSelf-Tray-{Environment.ProcessId}";
            _pipeServer = new ServicePipeServer(pipeName);
            _pipeServer.Start();

            // 构建 --tray 参数
            var arguments = TrayProcessStarter.BuildTrayArguments(port, pipeName);

            // 在用户会话中启动托盘辅助进程
            var started = TrayProcessStarter.StartTrayProcess(executablePath, arguments);
            if (started)
            {
                XTrace.Log.Info("WindowsService: 托盘辅助进程已启动，管道名称: {0}", pipeName);
            }
            else
            {
                XTrace.Log.Warn("WindowsService: 启动托盘辅助进程失败（无活跃用户会话或权限不足），服务继续运行");
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("WindowsService: 启动托盘辅助进程异常: {0}", ex.Message);
            // 不阻止服务继续运行
        }
    }

    /// <summary>
    /// 服务停止时由 SCM 调用。优雅关闭 Web Host。
    /// 同时向托盘辅助进程发送关闭信号。
    /// </summary>
    /// <param name="reason">停止原因。</param>
    public override void StopWork(string reason)
    {
        XTrace.Log.Info("WindowsService.StopWork: 开始关闭 Web Host (reason: {0})", reason);

        // 先通知托盘辅助进程退出
        if (_pipeServer != null)
        {
            _pipeServer.SendShutdown();
            _pipeServer.Stop();
            _pipeServer.Dispose();
            _pipeServer = null;
        }

        if (_app != null)
        {
            try
            {
                _app.StopAsync().GetAwaiter().GetResult();
                XTrace.Log.Info("WindowsService.StopWork: Web Host 已停止");
            }
            catch (Exception ex)
            {
                XTrace.Log.Error("WindowsService.StopWork: 停止 Web Host 时发生异常: {0}", ex.Message);
            }
            finally
            {
                _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _app = null;
            }
        }

        XTrace.Log.Info("WindowsService.StopWork: 服务已停止");
    }
}
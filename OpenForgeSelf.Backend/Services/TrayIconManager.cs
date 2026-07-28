using System.Diagnostics;
using System.Runtime.InteropServices;
using H.NotifyIcon.Core;
using NewLife.Log;
using OpenForgeSelf.Backend.Models;

namespace OpenForgeSelf.Backend.Services;

/// <summary>
/// 系统托盘图标管理器。
/// 在独立 STA 线程上运行 WinForms 消息泵，与 ASP.NET Core 主线程完全分离。
/// 提供托盘图标的显示/隐藏、气泡提示、右键菜单构建等功能。
/// 使用 H.NotifyIcon 库的 <see cref="TrayIconWithContextMenu"/> 实现。
/// 参考 research.md §2 H.NotifyIcon 使用方式。
/// </summary>
/// <remarks>
/// 线程模型：
/// - 所有 WinForms 操作（创建/销毁 TrayIconWithContextMenu、构建菜单、弹出 MessageBox）在专用 STA 线程上执行
/// - 公开方法（Show/Hide/ShowBalloonTip）可从任意线程调用，内部通过隐藏窗体的 <see cref="Control.Invoke"/> 封送到 STA 线程
/// - 回调委托（onCheckUpdate/onExit）在 STA 线程上下文中触发，调用方需注意线程亲和性
/// </remarks>
public class TrayIconManager : IDisposable
{
    private readonly IServiceManager _serviceManager;
    private readonly int _port;
    private readonly string _appName;
    private readonly string _appVersion;
    private readonly string _copyright;
    private Action? _onCheckUpdate;
    private Action? _onExit;

    private Thread? _staThread;
    private TrayIconWithContextMenu? _trayIcon;
    private Form? _hiddenForm;
    private readonly ManualResetEventSlim _readyEvent = new(false);
    private bool _disposed;

    /// <summary>
    /// 在构造后配置回调委托。用于 DI 注册后，在启动时注入「检查更新」和「退出」回调。
    /// </summary>
    /// <param name="onCheckUpdate">「检查更新」菜单项点击回调</param>
    /// <param name="onExit">「退出」菜单项点击回调</param>
    public void Configure(Action? onCheckUpdate = null, Action? onExit = null)
    {
        _onCheckUpdate = onCheckUpdate;
        _onExit = onExit;
    }

    /// <summary>
    /// 初始化托盘图标管理器。
    /// </summary>
    /// <param name="serviceManager">服务管理器，用于动态判断服务安装/运行状态</param>
    /// <param name="port">Web 监听端口，用于「打开主界面」菜单项</param>
    /// <param name="onCheckUpdate">「检查更新」菜单项点击回调</param>
    /// <param name="onExit">「退出」菜单项点击回调，应优雅关闭应用</param>
    /// <param name="appName">应用名称，用于「关于」对话框</param>
    /// <param name="appVersion">应用版本号，用于「关于」对话框</param>
    /// <param name="copyright">版权信息，用于「关于」对话框</param>
    /// <exception cref="ArgumentNullException"><paramref name="serviceManager"/> 为 null</exception>
    public TrayIconManager(
        IServiceManager serviceManager,
        int port,
        Action? onCheckUpdate = null,
        Action? onExit = null,
        string appName = "OpenForgeSelf",
        string appVersion = "1.0.0.0",
        string copyright = "© 2026 OpenForgeSelf")
    {
        _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
        _port = port;
        _onCheckUpdate = onCheckUpdate;
        _onExit = onExit;
        _appName = appName;
        _appVersion = appVersion;
        _copyright = copyright;
    }

    /// <summary>
    /// 创建托盘图标并显示在系统托盘区域。
    /// 内部启动专用 STA 线程，创建隐藏窗体承载 <see cref="TrayIconWithContextMenu"/>。
    /// 多次调用仅首次生效。
    /// </summary>
    /// <exception cref="ObjectDisposedException">对象已释放</exception>
    /// <exception cref="TimeoutException">STA 线程启动超时（5秒）</exception>
    public void Show()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_staThread != null)
            return; // 已显示，幂等

        _readyEvent.Reset();

        _staThread = new Thread(StaThreadMain)
        {
            Name = "TrayIcon STA Thread",
            IsBackground = true,
        };
        _staThread.SetApartmentState(ApartmentState.STA);
        _staThread.Start();

        if (!_readyEvent.Wait(5000))
            throw new TimeoutException("托盘图标 STA 线程启动超时（5秒）。");
    }

    /// <summary>
    /// 移除托盘图标并退出 STA 线程。
    /// 多次调用安全（幂等），可在退出或 Dispose 时调用。
    /// </summary>
    public void Hide()
    {
        if (_hiddenForm is { IsDisposed: false })
        {
            if (_hiddenForm.InvokeRequired)
            {
                _hiddenForm.Invoke(() => HideInternal());
            }
            else
            {
                HideInternal();
            }

            // 等待 STA 线程退出
            _staThread?.Join(3000);
        }

        _hiddenForm = null;
        _trayIcon = null;
        _staThread = null;
    }

    /// <summary>
    /// 弹出托盘气泡提示。
    /// 可在任意线程调用，内部封送到 STA 线程。
    /// </summary>
    /// <param name="title">标题</param>
    /// <param name="text">正文</param>
    /// <param name="icon">提示图标类型，默认 Info</param>
    public void ShowBalloonTip(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        if (_trayIcon == null)
            return;

        if (_hiddenForm is { IsDisposed: false })
        {
            var action = () =>
            {
                var notificationIcon = icon switch
                {
                    ToolTipIcon.Info => NotificationIcon.Info,
                    ToolTipIcon.Warning => NotificationIcon.Warning,
                    ToolTipIcon.Error => NotificationIcon.Error,
                    _ => NotificationIcon.None,
                };

                _trayIcon!.ShowNotification(title, text, notificationIcon);
            };

            if (_hiddenForm.InvokeRequired)
                _hiddenForm.Invoke(action);
            else
                action();
        }
    }

    /// <summary>
    /// 释放所有资源，移除托盘图标并退出 STA 线程。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Hide();
        _readyEvent.Dispose();
        GC.SuppressFinalize(this);
    }

    // ======================================================================
    // STA 线程内部
    // ======================================================================

    /// <summary>
    /// STA 线程入口：创建隐藏窗体，启动 WinForms 消息泵。
    /// </summary>
    private void StaThreadMain()
    {
        _hiddenForm = new Form
        {
            WindowState = FormWindowState.Minimized,
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.None,
            ControlBox = false,
            Text = _appName,
        };

        _hiddenForm.Load += OnHiddenFormLoad;
        _hiddenForm.FormClosing += OnHiddenFormClosing;

        Application.Run(_hiddenForm);
    }

    private void OnHiddenFormLoad(object? sender, EventArgs e)
    {
        try
        {
            XTrace.Log.Info("TrayIconManager: 创建托盘图标");

            // TODO: 使用自定义应用图标替换 SystemIcons.Application
            // 可在项目中添加 .ico 资源文件，例如 OpenForgeSelf.ico
            _trayIcon = new TrayIconWithContextMenu
            {
                Icon = SystemIcons.Application.Handle,
                ToolTip = _appName,
            };

            // 构建右键菜单
            _trayIcon.ContextMenu = BuildMenu();

            // 创建托盘图标
            _trayIcon.Create();
        }
        catch (Exception ex)
        {
            // 托盘图标创建失败（如无桌面环境、测试环境等），记录日志但不影响应用运行
            XTrace.Log.Error("TrayIconManager: 创建托盘图标失败: {0}", ex.Message);
        }
        finally
        {
            _readyEvent.Set();
        }
    }

    private void OnHiddenFormClosing(object? sender, FormClosingEventArgs e)
    {
        XTrace.Log.Info("TrayIconManager: 关闭托盘图标");

        _trayIcon?.Dispose();
        _trayIcon = null;
    }

    /// <summary>
    /// 构建右键菜单，根据服务安装状态动态显示菜单项。
    /// </summary>
    private PopupMenu BuildMenu()
    {
        var menu = new PopupMenu();

        // ── 打开主界面 ──
        menu.Items.Add(new PopupMenuItem("打开主界面(&O)", OnOpenMainPage));

        menu.Items.Add(new PopupMenuSeparator());

        // ── 检查更新 ──
        menu.Items.Add(new PopupMenuItem("检查更新(&U)", (_, _) => _onCheckUpdate?.Invoke()));

        menu.Items.Add(new PopupMenuSeparator());

        // ── 服务管理（动态） ──
        // 根据 ServiceManager.IsInstalled() 动态显示「安装/卸载/重启服务」
        // 参考 data-model.md §1 ServiceInstallState 状态驱动逻辑
        AppendServiceMenuItems(menu);

        menu.Items.Add(new PopupMenuSeparator());

        // ── 关于 ──
        menu.Items.Add(new PopupMenuItem("关于(&A)", (_, _) => ShowAboutDialog()));

        // ── 退出 ──
        menu.Items.Add(new PopupMenuItem("退出(&X)", (_, _) => _onExit?.Invoke()));

        return menu;
    }

    /// <summary>
    /// 根据服务安装状态追加服务管理菜单项。
    /// </summary>
    private void AppendServiceMenuItems(PopupMenu menu)
    {
        var isInstalled = _serviceManager.IsInstalled();

        if (isInstalled)
        {
            // 服务已安装 → 显示「卸载服务」和「重启服务」
            menu.Items.Add(new PopupMenuItem("卸载服务(&N)", (_, _) =>
            {
                try
                {
                    _serviceManager.Uninstall();
                    XTrace.Log.Info("TrayIconManager: 服务卸载成功");
                    InvokeMessageBox("服务已卸载。", _appName);
                }
                catch (Exception ex)
                {
                    XTrace.Log.Error("TrayIconManager: 卸载服务失败: {0}", ex.Message);
                    InvokeMessageBox($"卸载服务失败：{ex.Message}", "错误");
                }
            }));

            menu.Items.Add(new PopupMenuItem("重启服务(&R)", (_, _) =>
            {
                try
                {
                    _serviceManager.Restart();
                    XTrace.Log.Info("TrayIconManager: 服务重启成功");
                    InvokeMessageBox("服务已重启。", _appName);
                }
                catch (Exception ex)
                {
                    XTrace.Log.Error("TrayIconManager: 重启服务失败: {0}", ex.Message);
                    InvokeMessageBox($"重启服务失败：{ex.Message}", "错误");
                }
            }));
        }
        else
        {
            // 服务未安装 → 显示「安装服务」
            menu.Items.Add(new PopupMenuItem("安装服务(&I)", (_, _) =>
            {
                try
                {
                    _serviceManager.Install();
                    XTrace.Log.Info("TrayIconManager: 服务安装成功");
                    InvokeMessageBox("服务安装成功。", _appName);
                }
                catch (Exception ex)
                {
                    XTrace.Log.Error("TrayIconManager: 安装服务失败: {0}", ex.Message);
                    InvokeMessageBox($"安装服务失败：{ex.Message}", "错误");
                }
            }));
        }
    }

    /// <summary>
    /// 打开主界面：在默认浏览器中打开 http://localhost:{port}。
    /// </summary>
    private void OnOpenMainPage(object? sender, EventArgs e)
    {
        try
        {
            var url = $"http://localhost:{_port}";
            XTrace.Log.Info("TrayIconManager: 打开主界面: {0}", url);

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("TrayIconManager: 打开浏览器失败: {0}", ex.Message);
        }
    }

    /// <summary>
    /// 显示「关于」对话框，包含应用名、版本、版权信息。
    /// </summary>
    private void ShowAboutDialog()
    {
        var message = $"{_appName}\n版本: {_appVersion}\n{_copyright}";
        InvokeMessageBox(message, "关于");
    }

    /// <summary>
    /// 在 STA 线程上显示 MessageBox。已在 STA 线程时直接调用，否则通过 Invoke 封送。
    /// </summary>
    private void InvokeMessageBox(string text, string caption)
    {
        if (_hiddenForm is { IsDisposed: false })
        {
            if (_hiddenForm.InvokeRequired)
            {
                _hiddenForm.Invoke(() => MessageBox.Show(text, caption));
            }
            else
            {
                MessageBox.Show(text, caption);
            }
        }
    }

    /// <summary>
    /// 在 STA 线程上执行内部清理操作。
    /// 此方法必须在 STA 线程上调用。
    /// </summary>
    private void HideInternal()
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
        _hiddenForm?.Close();
        _hiddenForm = null;
    }
}
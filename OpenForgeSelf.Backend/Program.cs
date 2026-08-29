using System.IO.Pipes;
using System.Text;
using NewLife.Agent;
using NewLife.Log;
using OpenForgeSelf.Backend;
using OpenForgeSelf.Backend.Models;
using OpenForgeSelf.Backend.Services;
using System.Reflection;
using System.Threading;

// 测试环境检测：WebApplicationFactory 调用入口点时，跳过 Mutex、服务和托盘逻辑，
// 直接创建 WebApplication 让测试框架接管宿主生命周期。
var entryAssembly = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name;
if (entryAssembly == "testhost" || entryAssembly == "testhost.exe")
{
    var app = AppBuilder.CreateWebApplication(args);
    app.Run();
    return 0;
}

// 统一配置文件落盘位置（数据根/Config）：必须早于任何 XTrace 日志写入与 Config<T>.Current 访问。
// NewLife FileConfigProvider 在初始化时会按 FileName 所在目录建立 FileSystemWatcher；
// 若放任其用默认相对路径（程序目录 Config/），发布目录下该目录不存在，启动首行即报
// 「FileSystemWatcher 创建失败：...\publish\Config does not exist」。
// 此处抢在一切 NewLife 配置访问之前重定向，AppBuilder 内再调用一次（幂等，覆盖 DI 环境判定结果）。
ConfigUnifier.UnifyAllConfigFiles(
    Path.Combine(DataLocationService.ResolveHostDataDirectory(), "Config"));

// --tray 模式：服务模式下的托盘辅助进程，由 WindowsService 通过 CreateProcessAsUser 启动。
// 此模式运行在用户会话中，通过命名管道与主服务通信，作为独立进程承载托盘图标。
// 解决 Session 0 隔离问题：服务运行在 Session 0 无法直接显示 UI，需在用户会话中启动此进程。
var trayArgIndex = Array.FindIndex(args, a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase));
if (trayArgIndex >= 0)
{
    return RunTrayMode(args, trayArgIndex);
}

// 单实例保护：使用全局 Mutex 防止多实例冲突。
// 注意废弃态（abandoned）：上一实例被强杀（taskkill /F）或崩溃后，Global Mutex 未被释放，
// 此时 new Mutex 返回 createdNew=false 但并无存活实例真实持有它，直接报错会导致应用无法重启。
// 处理：Mutex 已存在时用 WaitOne(0) 探测——被其它存活进程真实持有则拒绝启动；
// 持有者已消亡（废弃态）会抛 AbandonedMutexException，此时本线程已接管所有权，视为可启动。
const string mutexName = @"Global\OpenForgeSelf-{B1C2D3E4-F5G6-7890-ABCD-EF1234567890}";
using var mutex = new Mutex(true, mutexName, out var createdNew);
if (!createdNew)
{
    try
    {
        // WaitOne(0) 立即探测：被存活进程持有返回 false；废弃态抛 AbandonedMutexException
        if (!mutex.WaitOne(0))
        {
            Console.Error.WriteLine("错误：另一个实例已在运行，请勿重复启动。");
            XTrace.Log.Error("另一个实例已在运行，退出。");
            return 1;
        }
    }
    catch (AbandonedMutexException)
    {
        // 上一实例异常退出（强杀/崩溃）遗留的废弃 Mutex，无真实实例在运行 → 本实例接管，继续启动
        XTrace.Log.Warn("检测到废弃的全局 Mutex（疑似上一实例异常退出），本实例已接管启动。");
    }
}

// 解析命令行参数
string? firstArg = args.Length > 0 ? args[0].ToLowerInvariant() : null;

// install / uninstall 命令：安装或卸载 Windows 服务
if (firstArg is "install" or "-install" or "--install")
{
    XTrace.Log.Info("正在安装 Windows 服务...");
    new OpenForgeSelf.Backend.WindowsService().Main(args);
    return 0;
}

if (firstArg is "uninstall" or "-uninstall" or "--uninstall")
{
    XTrace.Log.Info("正在卸载 Windows 服务...");
    new OpenForgeSelf.Backend.WindowsService().Main(args);
    return 0;
}

// --console 显式指定控制台调试模式
if (firstArg == "--console")
{
    // 过滤掉 --console 参数，避免传递给 WebApplication
    var consoleArgs = args.Where(a => !string.Equals(a, "--console", StringComparison.OrdinalIgnoreCase)).ToArray();
    XTrace.UseConsole();
    XTrace.Log.Level = NewLife.Log.LogLevel.Info;

    var app = AppBuilder.CreateWebApplication(consoleArgs);
    var trayIcon = StartTrayIcon(app);
    XTrace.Log.Info("Main: 开始 app.Run()（--console 模式）");
    app.Run();
    XTrace.Log.Info("Main: app.Run() 已返回（--console 模式），清理托盘");
    trayIcon?.Hide();
    XTrace.Log.Info("Main: 进程退出（--console 模式）");
    return 0;
}

// 无参数：自动判断运行模式
if (args.Length == 0)
{
    if (!Environment.UserInteractive)
    {
        // 服务模式：被 SCM 启动，委托给 NewLife.Agent 服务宿主
        new OpenForgeSelf.Backend.WindowsService().Main(args);
        return 0;
    }

    // 控制台调试模式：正常启动 WebApplication
    XTrace.UseConsole();
    XTrace.Log.Level = NewLife.Log.LogLevel.Info;

    var app = AppBuilder.CreateWebApplication(args);
    var trayIcon = StartTrayIcon(app);
    XTrace.Log.Info("Main: 开始 app.Run()（普通模式）");
    app.Run();
    XTrace.Log.Info("Main: app.Run() 已返回（普通模式），清理托盘");
    trayIcon?.Hide();
    XTrace.Log.Info("Main: 进程退出（普通模式）");
    return 0;
}

// 其他参数（如 -run, -start, -stop 等）委托给 NewLife.Agent 处理
new OpenForgeSelf.Backend.WindowsService().Main(args);
return 0;

/// <summary>
/// 托盘辅助进程入口点（--tray 模式）。
/// 由 WindowsService 在服务模式下通过 <see cref="TrayProcessStarter"/> 启动到用户会话中。
/// 解析 --pipe-name 和 --port 参数，创建 TrayIconManager 并通过命名管道与主服务通信。
/// 当用户点击「退出」或收到主服务 shutdown 信号时退出。
/// </summary>
static int RunTrayMode(string[] args, int trayArgIndex)
{
    // 解析参数
    var pipeName = "";
    // 优先从参数读取端口，否则使用 ForgeSetting 配置的端口
    var port = 0;

    for (int i = trayArgIndex + 1; i < args.Length; i++)
    {
        if (args[i].Equals("--pipe-name", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            pipeName = args[++i];
        else if (args[i].Equals("--port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            int.TryParse(args[++i], out port);
    }

    // 如果参数未提供端口，使用 ForgeSetting 配置的端口
    if (port == 0)
        port = ForgeSetting.Current.PortNumber;

    XTrace.UseConsole();
    XTrace.Log.Level = NewLife.Log.LogLevel.Info;
    XTrace.Log.Info("TrayMode: 托盘辅助进程启动 (pipe={0}, port={1})", pipeName, port);

    // 创建 ServiceManager 用于菜单项
    var serviceConfig = new ServiceConfig();
    var serviceManager = new ServiceManager(serviceConfig);

    // 创建 TrayIconManager（不依赖 DI 容器，直接构造）
    using var trayIcon = new TrayIconManager(serviceManager, port);

    // 标记退出信号
    using var exitEvent = new ManualResetEventSlim(false);

    // 配置回调
    trayIcon.Configure(
        onCheckUpdate: () =>
        {
            XTrace.Log.Info("TrayMode: 用户点击「检查更新」（由主服务处理）");
        },
        onExit: () =>
        {
            XTrace.Log.Info("TrayMode: 用户点击「退出」（线程 ID={0}），设置退出信号", Environment.CurrentManagedThreadId);
            exitEvent.Set();
        });

    // 启动托盘图标
    trayIcon.Show();
    XTrace.Log.Info("TrayMode: 托盘图标已显示");

    // 连接到主服务的命名管道，监听 shutdown 信号
    if (!string.IsNullOrEmpty(pipeName))
    {
        Task.Run(async () =>
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.In);
                await pipe.ConnectAsync(10000);
                using var reader = new StreamReader(pipe, Encoding.UTF8);

                XTrace.Log.Info("TrayMode: 已连接到服务管道，等待信号...");

                var message = await reader.ReadLineAsync();
                if (message?.Trim().Equals("shutdown", StringComparison.OrdinalIgnoreCase) == true)
                {
                    XTrace.Log.Info("TrayMode: 收到主服务 shutdown 信号，退出");
                    exitEvent.Set();
                }
            }
            catch (Exception ex)
            {
                XTrace.Log.Warn("TrayMode: 管道连接失败或异常: {0}", ex.Message);
            }
        });
    }

    // 等待退出信号（用户点击「退出」或服务发送 shutdown）
    exitEvent.Wait();
    XTrace.Log.Info("TrayMode: 收到退出信号，开始清理托盘");
    trayIcon.Hide();
    XTrace.Log.Info("TrayMode: 托盘辅助进程退出");
    return 0;
}

/// <summary>
/// 在控制台模式下启动托盘图标管理器。
/// 通过 DI 容器解析 ServiceManager 和 TrayIconManager，注册生命周期回调及更新检查，
/// 在 STA 线程上启动托盘图标，并在后台触发启动时版本检查。
/// </summary>
/// <param name="app">已配置的 WebApplication 实例</param>
/// <returns>TrayIconManager 实例，应用退出前应调用其 Hide() 方法</returns>
static TrayIconManager? StartTrayIcon(WebApplication app)
{
    try
    {
        var port = ForgeSetting.Current.PortNumber;

        // 从 DI 容器解析服务
        var trayIconManager = app.Services.GetRequiredService<TrayIconManager>();
        var updateService = app.Services.GetRequiredService<UpdateService>();

        // 配置回调委托
        trayIconManager.Configure(
            onCheckUpdate: () =>
            {
                XTrace.Log.Info("TrayIconManager: 用户点击「检查更新」");
                try
                {
                    var result = updateService.CheckForUpdatesAsync().GetAwaiter().GetResult();
                    if (result.HasUpdate)
                    {
                        trayIconManager.ShowBalloonTip(
                            "有新版本可用",
                            $"版本 {result.LatestVersion} 已可用，请点击「检查更新」下载。",
                            System.Windows.Forms.ToolTipIcon.Info);
                    }
                    else
                    {
                        trayIconManager.ShowBalloonTip(
                            "已是最新版本",
                            "当前已是最新版本。",
                            System.Windows.Forms.ToolTipIcon.Info);
                    }
                }
                catch (Exception ex)
                {
                    XTrace.Log.Error("检查更新失败: {0}", ex.Message);
                }
            },
            onExit: () =>
            {
                XTrace.Log.Info("TrayIconManager: 用户点击「退出」，正在关闭应用...（线程 ID={0}）", Environment.CurrentManagedThreadId);
                // 修复托盘退出死锁：不能在此（托盘 STA 线程）同步等待 StopAsync()。
                // StopAsync 触发 ApplicationStopping → trayIconManager.Hide() → _hiddenForm.Invoke(...)
                // 需要 STA 线程处理消息，而 STA 线程正被 GetAwaiter().GetResult() 阻塞 → 死锁，
                // 表现为提示「正在退出」但进程不退、web 仍可访问、托盘菜单无响应。
                // 先同步关闭托盘（当前就在 STA 线程，InvokeRequired=false 直接执行），
                // 再异步触发 host 停止，避免阻塞托盘消息循环。
                try
                {
                    trayIconManager.Hide();
                    XTrace.Log.Info("TrayIconManager: 托盘图标已隐藏，准备异步停止 host");
                }
                catch (Exception ex)
                {
                    XTrace.Log.Error("TrayIconManager: Hide() 抛异常: {0}", ex);
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        XTrace.Log.Info("TrayIconManager: 调用 app.StopAsync() 开始（线程 ID={0}）", Environment.CurrentManagedThreadId);
                        await app.StopAsync();
                        XTrace.Log.Info("TrayIconManager: app.StopAsync() 已完成");
                    }
                    catch (Exception ex)
                    {
                        XTrace.Log.Error("停止应用异常: {0}", ex);
                    }
                });
            });

        // 应用退出时清理托盘图标
        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Register(() => trayIconManager.Hide());

        trayIconManager.Show();
        XTrace.Log.Info("托盘图标已启动（端口: {0}）", port);

        // 触发启动时更新检查（非阻塞，延迟 3 秒让应用完全就绪）
        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(3000);
                var result = await updateService.CheckForUpdatesAsync();
                if (result.HasUpdate)
                {
                    trayIconManager.ShowBalloonTip(
                        "发现新版本",
                        $"版本 {result.LatestVersion} 已可用，请点击「检查更新」下载。",
                        System.Windows.Forms.ToolTipIcon.Info);
                }
            }
            catch (Exception ex)
            {
                XTrace.Log.Error("启动时更新检查失败: {0}", ex.Message);
            }
        });

        return trayIconManager;
    }
    catch (Exception ex)
    {
        XTrace.Log.Error("启动托盘图标失败: {0}", ex.Message);
        return null;
    }
}
using NewLife.Log;
using XCode;
using ForgeSelf.Api;
using ForgeSelf.Api.Services;
using ForgeSelf.Api.Services.Terminal;

// 测试环境检测：WebApplicationFactory 调用入口点时，跳过 Mutex、服务和托盘逻辑，
// 直接创建 WebApplication 让测试框架接管宿主生命周期。
var entryAssembly = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name;
if (entryAssembly == "testhost" || entryAssembly == "testhost.exe")
{
    var app = AppBuilder.CreateWebApplication(args);
    app.Run();
    return 0;
}

// 统一配置文件落盘位置（数据根/config）：必须早于任何 XTrace 日志写入与 Config<T>.Current 访问。
// NewLife FileConfigProvider 在初始化时会按 FileName 所在目录建立 FileSystemWatcher；
// 若放任其用默认相对路径（程序目录 Config/），发布目录下该目录不存在，启动首行即报
// 「FileSystemWatcher 创建失败：...\publish\Config does not exist」。
// 此处抢在一切 NewLife 配置访问之前重定向，AppBuilder 内再调用一次（幂等，覆盖 DI 环境判定结果）。
var hostDataDir = DataLocationService.ResolveHostDataDirectory();

// 日志外置到数据根/log（输入37 目录小写 + 空间优化）：NewLife 默认日志落程序目录 Log/，
// 随版本目录残留（每版本一份）造成空间浪费。必须在任何 XTrace 日志写入之前设置
// （XTrace 首次写入即按默认路径初始化，之后再改 LogPath 不再生效——冒烟实测）。
// 本设置在首次 NewLife 调用（ConfigUnifier）之前；AppBuilder 内保留幂等重复设置。
// XTrace.LogPath 是独立静态属性（实测：设 Setting.LogPath 不联动 XTrace，日志仍落默认 Log/），
// 必须直接设 XTrace.LogPath 才生效；Setting.LogPath 同步持久化供其他组件读取。
NewLife.Setting.Current.LogPath = Path.Combine(hostDataDir, "log");
XTrace.LogPath = Path.Combine(hostDataDir, "log");
NewLife.Setting.Current.Save();

ConfigUnifier.UnifyAllConfigFiles(
    Path.Combine(hostDataDir, "config"));

// 反向工程强制全开（2026-11-12 用户指令，pilot/2026-11-12-xcode-migration-full）：SQLite 不支持
// ALTER TABLE ... ALTER COLUMN，实体结构变更（如 ChatTurn 增列 RequestHeaders/ResponseHeaders）
// 必须由 XCode「备份-重建-回填」落库；Migration=On（仅新建）时结构漂移会永久悬置——
// 2026-11-12 17:20 运行实例实证。Full=完全（新建+修改+删除）。
// 时序：必须晚于 ConfigUnifier 的 FileName 重定向（否则 Current 读旧路径），
// 早于 AddXCode（AppBuilder 内访问 XCodeSetting）与首次 DAL 访问；每次启动强制归位，
// 不依赖 {数据根}/config/XCode.config 是否被手工改动。testhost 早退分支不经过此处，
// 测试夹具 Migration=On 约定（PC-014）不受影响。
XTrace.Log.Info("XCode 反向工程强制为 Full（Migration=Full，新建+修改+删除）");
XCodeSetting.Current.Migration = XCode.DataAccessLayer.Migration.Full;
try
{
    XCodeSetting.Current.Save();
}
catch (Exception ex)
{
    // B9-5 同风格：文件落盘失败仅告警降级（内存值已生效），宿主启动不受阻
    XTrace.Log.Warn("[Program] XCodeSetting 落盘失败（已降级，Migration=Full 仅本进程生效）: {0}", ex.Message);
}

// 托盘已拆到 ForgeSelf.Tray.exe。旧的 --tray 是把同一个 API exe 再拉起一次，这里不再进入那个模式。
if (args.Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase)))
{
    XTrace.Log.Warn("参数 --tray 已从 API 移除，托盘是独立程序 ForgeSelf.Tray。本次改为启动 Web 宿主。");
    args = args.Where(a => !a.Equals("--tray", StringComparison.OrdinalIgnoreCase)).ToArray();
}

// 单实例保护：使用全局 Mutex 防止多实例冲突。
// 注意废弃态（abandoned）：上一实例被强杀（taskkill /F）或崩溃后，Global Mutex 未被释放，
// 此时 new Mutex 返回 createdNew=false 但并无存活实例真实持有它，直接报错会导致应用无法重启。
// 处理：Mutex 已存在时用 WaitOne(0) 探测——被其它存活进程真实持有则拒绝启动；
// 持有者已消亡（废弃态）会抛 AbandonedMutexException，此时本线程已接管所有权，视为可启动。
// 实例标识（env FORGESelf_INSTANCE_ID 或 --instance-id=<id>）可让同一台机器并存多个独立实例
// （如 e2e 测试宿主与开发宿主各自持有独立 Mutex，互不阻塞）；缺省保持原 Mutex 名以兼容既有行为。
var instanceId = Environment.GetEnvironmentVariable("FORGESelf_INSTANCE_ID");
var instArg = Array.Find(args, a => a.StartsWith("--instance-id=", StringComparison.OrdinalIgnoreCase));
if (instArg != null) instanceId = instArg["--instance-id=".Length..];
var mutexName = instanceId == null
    ? @"Global\ForgeSelf-{B1C2D3E4-F5G6-7890-ABCD-EF1234567890}"
    : $@"Global\ForgeSelf-{{B1C2D3E4-F5G6-7890-ABCD-EF1234567890}}-{instanceId}";
using var mutex = new Mutex(true, mutexName, out var createdNew);
if (!createdNew)
{
    try
    {
        // WaitOne(0) 立即探测：被存活进程持有返回 false；废弃态抛 AbandonedMutexException
        if (!mutex.WaitOne(0))
        {
            var forwarded = TerminalLaunchBootstrap.TryForwardToRunningInstance(args);
            if (forwarded == 0)
            {
                Console.WriteLine("已把打开请求交给正在运行的铸己匣。");
                XTrace.Log.Info("另一个实例已在运行，打开请求已转交。");
                return 0;
            }
            if (forwarded == 2)
            {
                Console.Error.WriteLine("另一个实例已在运行，但打开请求没有送达。");
                XTrace.Log.Error("另一个实例已在运行，打开请求转交失败。");
                return 2;
            }
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

// 协议 URL / --open-terminal 收进智能终端收件箱，并从参数里拿掉，避免被当成服务命令。
args = TerminalLaunchBootstrap.PreparePrimary(args);

// 解析命令行参数
string? firstArg = args.Length > 0 ? args[0].ToLowerInvariant() : null;

// install / uninstall 命令：安装或卸载 Windows 服务
if (firstArg is "install" or "-install" or "--install")
{
    XTrace.Log.Info("正在安装 Windows 服务...");
    new ForgeSelf.Api.WindowsService().Main(args);
    return 0;
}

if (firstArg is "uninstall" or "-uninstall" or "--uninstall")
{
    XTrace.Log.Info("正在卸载 Windows 服务...");
    new ForgeSelf.Api.WindowsService().Main(args);
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
    using var trayPipe = TrayHostLauncher.StartInteractive(app);
    TerminalLaunchBootstrap.OpenUiIfRequested();
    XTrace.Log.Info("Main: 开始 app.Run()（--console 模式）");
    app.Run();
    trayPipe?.SendShutdown();
    XTrace.Log.Info("Main: 进程退出（--console 模式）");
    return 0;
}

// 无参数：自动判断运行模式
if (args.Length == 0)
{
    if (!Environment.UserInteractive)
    {
        // 服务模式：被 SCM 启动，委托给 NewLife.Agent 服务宿主
        new ForgeSelf.Api.WindowsService().Main(args);
        return 0;
    }

    // 控制台调试模式：正常启动 WebApplication
    XTrace.UseConsole();
    XTrace.Log.Level = NewLife.Log.LogLevel.Info;

    var app = AppBuilder.CreateWebApplication(args);
    using var trayPipe = TrayHostLauncher.StartInteractive(app);
    TerminalLaunchBootstrap.OpenUiIfRequested();
    XTrace.Log.Info("Main: 开始 app.Run()（普通模式）");
    app.Run();
    trayPipe?.SendShutdown();
    XTrace.Log.Info("Main: 进程退出（普通模式）");
    return 0;
}

// 其他参数（如 -run, -start, -stop 等）委托给 NewLife.Agent 处理
new ForgeSelf.Api.WindowsService().Main(args);
return 0;

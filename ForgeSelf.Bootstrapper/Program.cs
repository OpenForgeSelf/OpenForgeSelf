using System.Diagnostics;

// OpenForgeSelf 安装根启动器（QQNT 式目录结构，批次2.1，输入34；单文件化 2026-09-28 输入36）。
// 安装根公共层只含：本启动器 ForgeSelf.exe（.NET 单文件，公共不变）+ update-agent.ps1 +
// Plugins/（插件目录，与 versions/ 并排）；宿主业务层（ForgeSelf.exe 单文件 + wwwroot +
// appsettings.json）按版本放在 versions/<ver>/，由本启动器按 versions/current 指针
// （或 --forge-version=<v> 强制指定）以子进程方式拉起。
// 为什么是子进程而非 Assembly.Load：单文件发布（PublishSingleFile）后业务层是 apphost
// 可执行文件（原生壳 + 内嵌 bundle），无法 LoadFromAssemblyPath 作为托管程序集加载，
// 因此启动器改为进程拉起并保持存活到业务层退出（update-agent 的 HostPid 即业务层进程，
// 应用新版本后重启本启动器再拉起新版本）。
// 结构真源：docs/04-standards/packaging-upgrade-backup.md §3（QQNT T1-T6）。

var exitCode = Run(args);
Environment.ExitCode = exitCode;
return;

static int Run(string[] args)
{
    try
    {
        // 安装根定位：自包含单文件（无论是否压缩）会把 bundle 解压到
        // DOTNET_BUNDLE_EXTRACT_BASE_DIR 缓存目录运行，AppContext.BaseDirectory 指向提取目录
        // （实测 2026-09-28），不能用作安装根。Environment.ProcessPath 返回进程启动的真实
        // exe 路径（apphost 仍在安装根），用它取安装根目录。
        var root = Path.GetDirectoryName(Environment.ProcessPath)
            ?? throw new InvalidOperationException("无法确定启动器自身路径（Environment.ProcessPath 为空）");
        var versionsDir = Path.Combine(root, "versions");
        if (!Directory.Exists(versionsDir))
        {
            Console.Error.WriteLine($"[boot] 未找到 versions 目录: {versionsDir}");
            return 1;
        }

        // 版本解析：优先 --forge-version=<v>（回退/指定版本通道），否则读 versions/current 指针
        var forced = args.FirstOrDefault(a => a.StartsWith("--forge-version=", StringComparison.OrdinalIgnoreCase));
        var current = forced != null
            ? forced["--forge-version=".Length..].Trim().TrimStart('v', 'V')
            : (File.Exists(Path.Combine(versionsDir, "current"))
                ? File.ReadAllText(Path.Combine(versionsDir, "current")).Trim()
                : null);

        if (string.IsNullOrWhiteSpace(current))
        {
            Console.Error.WriteLine("[boot] versions/current 指针缺失或为空，无法确定宿主版本。");
            return 1;
        }

        var versionDir = Path.Combine(versionsDir, current);
        if (!Directory.Exists(versionDir))
        {
            Console.Error.WriteLine($"[boot] 版本目录不存在: {versionDir}（当前指针 = {current}）");
            return 1;
        }

        var hostExe = Path.Combine(versionDir, "ForgeSelf.exe");
        if (!File.Exists(hostExe))
        {
            Console.Error.WriteLine($"[boot] 业务层单文件缺失: {hostExe}");
            return 1;
        }

        // 透传参数给业务层，但 --forge-version 是启动器专用（业务层从自身 Assembly.Location
        // 即知版本，无需该参数；误传未知参数可能触发业务层未知参数处理）。
        var passthrough = args.Where(a => !a.StartsWith("--forge-version=", StringComparison.OrdinalIgnoreCase)).ToArray();

        var psi = new ProcessStartInfo(hostExe)
        {
            WorkingDirectory = versionDir,   // 业务层 ContentRoot 解析基准（AppBuilder 保 CWD 逻辑）
            UseShellExecute = false,
        };
        // 业务层是 FDD 单文件：运行时/共享框架由公共层 app-local 提供（DOTNET_ROOT = 安装根），
        // 否则 apphost 找不到运行时（versions/<ver>/ 内无 hostfxr）。
        psi.Environment["DOTNET_ROOT"] = root;
        foreach (var a in passthrough) psi.ArgumentList.Add(a);

        var proc = Process.Start(psi);
        if (proc == null)
        {
            Console.Error.WriteLine($"[boot] 无法启动业务层: {hostExe}");
            return 1;
        }

        // 保持存活直到业务层退出：update-agent 以业务层进程为 HostPid 等待退出后应用新版本，
        // 再重启本启动器拉起新版本；本启动器随之退出，转发业务层退出码。
        proc.WaitForExit();
        return proc.ExitCode;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[boot] 启动失败: {ex}");
        return 1;
    }
}
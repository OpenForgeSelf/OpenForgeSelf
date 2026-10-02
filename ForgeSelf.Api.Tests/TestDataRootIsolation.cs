using System.Runtime.CompilerServices;
using ForgeSelf.Api;
using NewLife;
using NewLife.Log;

namespace ForgeSelf.Api.Tests;

/// <summary>
/// 测试进程数据根自动隔离（2026-10-02 输入4）：
/// 在测试程序集加载时（任何用例执行前）执行，为<b>未显式设置</b> <c>FORGESELF_DATA_ROOT</c> 的测试进程
/// 兜底指向仓库内隔离目录，杜绝 <c>dotnet test</c> 触发 <c>Program.cs</c> / <c>AppBuilder</c> 顶层代码时
/// 把日志、配置、数据库连接串写到真实宿主数据根 <c>%UserProfile%/.forgeself</c>。
/// 与 e2e / CI / 操作者显式前缀不冲突：已设置时本机制短路，保持原隔离链路不变。
/// </summary>
/// <remarks>
/// 仅设 <c>FORGESELF_DATA_ROOT</c> 并不足够（2026-10-02 输入4 实证）：<c>NewLife.Setting</c> /
/// <c>XCodeSetting</c> 等框架 <c>Config&lt;T&gt;</c> 的 <see cref="FileConfigProvider.FileName"/> 默认相对程序目录
/// （<c>Config\Core.config</c>）。若程序目录曾残留一份固化了宿主 <c>LogPath</c>/连接串的旧配置
/// （由 <c>Program.cs:38</c> / <c>AppBuilder.cs:89</c> 的 <c>Save()</c> 早于 <c>ConfigUnifier</c> 重定向所产生），
/// 测试进程读到它后会绕过隔离，仍尝试写宿主日志目录甚至连宿主库。
/// 故此处除兜底数据根外，还必须把日志路径与全部已知 Config 文件一并重定向到隔离根。
/// </remarks>
internal static class TestDataRootIsolation
{
    /// <summary>隔离目录相对仓库根的位置（<c>.temp/</c> 已 gitignore）。</summary>
    private const string IsolationRelativePath = ".temp/dotnet-test";

    /// <summary>
    /// 是否由本机制实际写入数据根（true = 兜底生效；false = 外部已显式设置，本机制短路）。
    /// 供守卫测试区分「自动隔离」与「外部覆盖」两种情形。
    /// </summary>
    internal static bool Applied { get; private set; }

    /// <summary>隔离根绝对路径（外部已设置时取外部值）；供守卫测试断言日志/配置落点。</summary>
    internal static string? IsolationRoot { get; private set; }

    /// <summary>
    /// 程序集模块初始化钩子：确保测试进程数据根、日志路径、全部已知 Config 文件都落在隔离根。
    /// 数据根仅在变量未设置/空白时兜底；显式设置（e2e / CI / 操作者前缀）一律尊重（日志/配置仍随之归位）。
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize()
    {
        var existing = Environment.GetEnvironmentVariable("FORGESELF_DATA_ROOT");
        if (!string.IsNullOrWhiteSpace(existing))
        {
            // 已由外部显式隔离（e2e / CI / 手工前缀）→ 不覆盖数据根，但日志/配置仍需归位到同根。
            IsolationRoot = existing;
            RedirectLogAndConfig(existing!);
            return;
        }

        var runRoot = Path.Combine(
            FindRepoRoot(),
            IsolationRelativePath,
            $"{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}");

        try
        {
            Directory.CreateDirectory(runRoot);
        }
        catch
        {
            // 建目录失败不阻断测试（后续逻辑会自行落盘并按需报错），保持兜底尽力而为。
        }

        Environment.SetEnvironmentVariable("FORGESELF_DATA_ROOT", runRoot);
        Applied = true;
        IsolationRoot = runRoot;
        RedirectLogAndConfig(runRoot);
    }

    /// <summary>
    /// 把 NewLife 日志路径与全部已知 <c>Config&lt;T&gt;</c> 文件重定向到隔离根，
    /// 切断「读程序目录残留配置（固化宿主 LogPath / 连接串）→ 写宿主数据 / 日志 / 库」链路。
    /// </summary>
    /// <param name="root">隔离根绝对路径。</param>
    private static void RedirectLogAndConfig(string root)
    {
        try
        {
            // ① 先重定向全部已知 Config 的 FileName（含 NewLife.Setting / XCodeSetting），
            //    必须早于任何 XTrace / NewLife.Setting.Current 访问：
            //    NewLife Config<T> 在文件缺失时会按「当前 FileName」自动创建默认配置，
            //    若此刻 FileName 仍是默认的相对程序目录路径，就会在 bin/Config/ 留下 Core.config 脏文件
            //    （2026-10-02 输入4 实证）。此处先赋值，后续 .Current 一律从隔离目录创建/加载。
            ConfigUnifier.UnifyAllConfigFiles(Path.Combine(root, "config"));

            // ② 日志：须早于任何 XTrace 写入（XTrace 首次写入即按当前 LogPath 初始化，之后修改不再生效）。
            XTrace.LogPath = Path.Combine(root, "log");

            // ③ 再显式固化 Setting.LogPath 并落盘，保证隔离目录内配置自洽。
            NewLife.Setting.Current.LogPath = Path.Combine(root, "log");
            NewLife.Setting.Current.Save();
        }
        catch
        {
            // 隔离尽力而为：模块初始化抛异常会让整个测试程序集加载失败，故一律吞掉。
            // 即使此处失败，TestDataRootIsolationGuardTests 仍会在用例层暴露越界。
        }
    }

    /// <summary>
    /// 从测试程序集输出目录向上定位仓库根（含 <c>ForgeSelf.slnx</c> 的目录）；
    /// 与 <c>DesignSystemAuthTests.FindRepoRoot</c> 同判据，保证测试内定位一致。
    /// 定位不到时退化到输出目录（仍不落用户目录），绝不回落真实宿主根。
    /// </summary>
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? AppContext.BaseDirectory;
    }
}
using System.Diagnostics;
using System.Text;
using FluentAssertions;
using ForgeSelf.Api.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Services;

/// <summary>
/// 安装根解析（2026-10-04 输入19）：宿主业务层在 <c>versions/&lt;ver&gt;/</c> 内时，更新链路必须把
/// 安装根（公共层）交给 update-agent，否则新版本落进 <c>versions/&lt;ver&gt;/versions/&lt;new&gt;/</c> 逐代嵌套。
/// 除纯函数判据外，还有一条「与随包分发的 update-agent.ps1 归一化结果一致」的实跑判据——
/// 因为「老宿主 + 新代理」正是嵌套被触发的那一次升级，两侧规则必须同源。
/// </summary>
public class HostInstallRootTests
{
    private static string Fixture(string relative) =>
        Path.GetFullPath(Path.Combine(Path.GetTempPath(),
            "host_install_root_fixture", relative.Replace('/', Path.DirectorySeparatorChar)));

    public static TheoryData<string, string, int> Cases => new()
    {
        // 起点 → 期望安装根 → 期望 versions 层数
        { Fixture("bin/Debug/net10.0-windows"), Fixture("bin/Debug/net10.0-windows"), 0 },
        { Fixture("tools/ForgeSelf"), Fixture("tools/ForgeSelf"), 0 },
        { Fixture("tools/ForgeSelf/versions/2.7.2.0"), Fixture("tools/ForgeSelf"), 1 },
        { Fixture("tools/ForgeSelf/versions/2.7.2.0/"), Fixture("tools/ForgeSelf"), 1 },
        { Fixture("tools/ForgeSelf/Versions/2.2.11"), Fixture("tools/ForgeSelf"), 1 },
        { Fixture("tools/ForgeSelf/versions/2.2.11/versions/2.7.2.0"), Fixture("tools/ForgeSelf"), 2 },
        // 现场形态：D:\src\tools\ForgeSelf\versions\2.2.11\versions\2.2.2026.0930\versions\2.7.2.0
        { Fixture("tools/ForgeSelf/versions/2.2.11/versions/2.2.2026.0930/versions/2.7.2.0"), Fixture("tools/ForgeSelf"), 3 },
        // 反证：父目录不是 versions 的同形状路径不得上跳
        { Fixture("tools/ForgeSelf/backup/2.7.2.0"), Fixture("tools/ForgeSelf/backup/2.7.2.0"), 0 },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void 归一化到安装根并计层数(string startDir, string expectedRoot, int expectedLayers)
    {
        HostInstallRoot.Resolve(startDir).Should().Be(expectedRoot);
        HostInstallRoot.VersionLayerCount(startDir).Should().Be(expectedLayers);
    }

    [Fact]
    public void 由可执行文件路径解析安装根()
    {
        var nested = Path.Combine(Fixture("tools/ForgeSelf/versions/2.2.11/versions/2.7.2.0"), "ForgeSelf.exe");
        HostInstallRoot.ResolveFromExecutable(nested).Should().Be(Fixture("tools/ForgeSelf"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void 拿不到程序目录时抛而不猜(string? processPath)
    {
        var act = () => HostInstallRoot.ResolveFromExecutable(processPath);
        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// 实跑随包代理脚本里的同名函数，逐条比对归一化结果与层数。
    /// 判据失效方向：只测 C# 一侧时，脚本里的规则被改坏（例如漏掉上跳）仍然全绿。
    /// </summary>
    [Fact]
    public void 与update_agent脚本的归一化规则一致()
    {
        var script = FindRepositoryFile("scripts", "update-agent.ps1");
        var body = ExtractFunction(script, "function Resolve-ForgeInstallRoot");

        var probeDir = Path.Combine(Path.GetTempPath(), $"host_root_probe_{Guid.NewGuid():N}");
        Directory.CreateDirectory(probeDir);
        var probeScript = Path.Combine(probeDir, "probe.ps1");
        File.WriteAllText(probeScript, body + """

$ErrorActionPreference = 'Stop'
foreach ($p in $args) {
    $r = Resolve-ForgeInstallRoot $p
    '{0}|{1}|{2}' -f $p, $r.Root, $r.Layers
}
""", new UTF8Encoding(false));

        var inputs = Cases.Cast<object[]>().Select(c => (string)c[0]).ToArray();
        var (exitCode, stdout, stderr) = RunPowershell(probeScript, inputs);
        exitCode.Should().Be(0, $"pwsh 探针应正常退出，stderr: {stderr}");

        var lines = stdout.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.Contains('|'))
            .ToArray();
        lines.Should().HaveCount(inputs.Length, "每个输入都应回一行结果");

        foreach (var line in lines)
        {
            var parts = line.Split('|');
            var start = parts[0];
            var psRoot = parts[1];
            var psLayers = int.Parse(parts[2]);

            HostInstallRoot.Resolve(start).Should().Be(psRoot, "宿主侧与代理脚本侧的安装根归一化必须同结果");
            HostInstallRoot.VersionLayerCount(start).Should().Be(psLayers, "两侧的版本层计数必须同结果");
        }

        try { Directory.Delete(probeDir, recursive: true); }
        catch (IOException) { /* 探针脚本是临时件，删不掉由 TMP 目录卫生兜底 */ }
    }

    /// <summary>
    /// 接线守卫：更新链路必须把归一化后的安装根交给代理。判据失效方向：本文件只测纯函数时，
    /// <c>StagedUpdateService</c> 退回「业务层目录当安装根」仍然全绿——而那正是嵌套的成因。
    /// </summary>
    [Fact]
    public void 更新链路必须用安装根解析而非业务层目录()
    {
        var source = FindRepositoryFile("ForgeSelf.Api", "Services", "StagedUpdateService.cs");
        source.Should().Contain("HostInstallRoot.Resolve(", "传给代理的必须是归一化后的安装根");
        source.Should().NotContain("var installDir = Path.GetDirectoryName(Environment.ProcessPath)",
            "业务层自身目录不是安装根，直接当安装根用会逐代嵌套");
    }

    /// <summary>
    /// 端到端跑一次随包代理：故意按「老宿主」的形态把业务层版本目录当 <c>-InstallDir</c> 传进去，
    /// 断言新版本落到<b>真安装根</b>的 <c>versions/&lt;new&gt;/</c>、<c>current</c> 指过去，且版本目录内
    /// <b>不再长出 <c>versions/</c></b>。这是本缺陷唯一有意义的复现形态（老 exe 已装在用户机器上，
    /// 改不动它的 C#，只有随包代理能拦住自己）。
    /// 隔离：<c>LOCALAPPDATA</c> 重定向到本次临时目录 ⇒ 代理日志与它清理的 <c>Backups</c> 都不落用户配置目录；
    /// <c>-ExeName</c> 故意给一个不存在的名字，使步骤 9 在「启动进程」前就失败，测试不会拉起任何真实程序。
    /// </summary>
    [Fact]
    public void 老宿主传错安装根时代理把新版本落到真根且不嵌套()
    {
        var sandbox = Path.Combine(Path.GetTempPath(), $"update_agent_layout_{Guid.NewGuid():N}");
        var localAppData = Path.Combine(sandbox, "AppData", "Local");
        var root = Path.Combine(sandbox, "ForgeSelfInstall");
        var oldVersionDir = Path.Combine(root, "versions", "1.0.0");
        var staged = Path.Combine(sandbox, "Updates", "tag-1", "extracted");

        // 已装的老布局（含历史嵌套，用来验证"不会继续往里长"）
        Directory.CreateDirectory(Path.Combine(oldVersionDir, "plugins", "SamplePlugin"));
        File.WriteAllText(Path.Combine(oldVersionDir, "ForgeSelf.exe"), "stub business layer");
        File.WriteAllText(Path.Combine(oldVersionDir, "appsettings.json"), "{}");
        File.WriteAllText(Path.Combine(root, "versions", "current"), "1.0.0");
        // 公共层（根启动器 + 代理脚本）
        File.WriteAllText(Path.Combine(root, "ForgeSelf.exe"), "stub launcher");

        // 更新包 = QQNT 布局（根公共层 + versions/<new>/ + 内置插件随版本）
        var newVersionDir = Path.Combine(staged, "versions", "2.0.0");
        Directory.CreateDirectory(Path.Combine(newVersionDir, "plugins", "SamplePlugin"));
        File.WriteAllText(Path.Combine(newVersionDir, "ForgeSelf.exe"), "stub business layer 2");
        File.WriteAllText(Path.Combine(newVersionDir, "plugins", "SamplePlugin", "plugin.json"), "{\"id\":\"sample\"}");
        File.WriteAllText(Path.Combine(staged, "ForgeSelf.exe"), "stub launcher 2");

        var agentScript = FindRepositoryFilePath("scripts", "update-agent.ps1");
        var agentInStaged = Path.Combine(staged, "update-agent.ps1");
        File.Copy(agentScript, agentInStaged, overwrite: true);
        File.WriteAllText(Path.Combine(root, "update-agent.ps1"), File.ReadAllText(agentScript));

        var deadPid = StartAndExitQuickProcess();

        Collected r;
        try
        {
            r = RunAgentAndCollect(agentInStaged, deadPid, oldVersionDir, staged, localAppData, root);
        }
        finally
        {
            try { Directory.Delete(sandbox, recursive: true); }
            catch (IOException) { /* 临时沙箱删不掉由 TMP 目录卫生兜底 */ }
        }

        // 落点先断言（它们才是"不嵌套"的直接证据），日志随后
        r.NewExePlaced.Should().BeTrue(
            $"新版本应落真安装根的 versions/2.0.0/（exit={r.ExitCode}，代理日志：{r.AgentLog}）");
        r.PluginPlaced.Should().BeTrue("内置插件应随版本一起落到版本目录");
        r.NestedInsideVersion.Should().BeFalse($"绝不允许在 versions/<ver>/ 内再建 versions/: {r.AgentLog}");
        r.CurrentPointer.Should().Be("2.0.0", "current 指针应切到新版本（升级一次即回正）");
        r.AgentLoggedToSandbox.Should().BeTrue("代理日志应落在被重定向的临时 LOCALAPPDATA，而非用户配置目录");
        // 归一化确实发生了（老宿主的错传被代理纠正）
        r.AgentLog.Should().Contain("安装根归一化", "把版本目录当安装根传入时必须被纠正");
        r.AgentLog.Should().Contain($"安装根 = {root}", "归一化结果必须是真安装根");
        r.AgentLog.Should().Contain("业务层已落 versions/2.0.0");
        // 步骤 9 的失败必须是我们期望的那条，而不是别的崩溃
        r.ExitCode.Should().Be(1, "测试故意让重启步骤找不到启动器");
        r.AgentLog.Should().Contain("未找到 NoSuchLauncher.exe");
    }

    private readonly record struct Collected(int ExitCode, string AgentLog, bool NewExePlaced, bool PluginPlaced,
        bool NestedInsideVersion, string? CurrentPointer, bool AgentLoggedToSandbox);

    /// <summary>跑一次代理并把"断言要用的事实"在读完后就地取走（沙箱随后即删，不留残留）。</summary>
    private static Collected RunAgentAndCollect(string agentPath, int hostPid, string installedVersionDir,
        string stagedDir, string localAppData, string root)
    {
        var (exitCode, stdout, stderr) = RunAgent(agentPath, hostPid, installedVersionDir, stagedDir,
            localAppData, exeName: "NoSuchLauncher.exe");
        // 代理的日志文件是它的对外契约（Write-Host 在子进程重定向下不可靠，不作判据来源）
        var updatesDir = Path.Combine(localAppData, "ForgeSelf", "Updates");
        var logFile = Directory.Exists(updatesDir)
            ? new DirectoryInfo(updatesDir).GetFiles("agent-*.log").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault()
            : null;
        var agentLog = logFile is null
            ? $"（无日志文件；updatesDir={updatesDir} 存在={Directory.Exists(updatesDir)}；" +
              $"pwsh exit={exitCode}；stdout={stdout}；stderr={stderr}）"
            : File.ReadAllText(logFile.FullName, Encoding.UTF8);
        return new Collected(
            exitCode,
            agentLog,
            File.Exists(Path.Combine(root, "versions", "2.0.0", "ForgeSelf.exe")),
            File.Exists(Path.Combine(root, "versions", "2.0.0", "plugins", "SamplePlugin", "plugin.json")),
            Directory.Exists(Path.Combine(installedVersionDir, "versions")),
            File.Exists(Path.Combine(root, "versions", "current"))
                ? File.ReadAllText(Path.Combine(root, "versions", "current")).Trim()
                : null,
            logFile is not null);
    }

    /// <summary>
    /// 取一个"代理自己也看不见"的 PID：代理步骤 1 用 <c>Get-Process -Id</c> 轮询，60s 仍在就
    /// <c>Stop-Process -Force</c>——所以预检必须用<b>同一个 API</b>（PowerShell 的视图），而不是 .NET 的
    /// <c>Process.GetProcessById</c>（实测两者对"刚退出、句柄尚未完全释放"的 PID 判定不一致，
    /// 曾让代理把用例拖到 30s 后静默退出 -1）。预检不过就换一个 PID 重试。
    /// </summary>
    private static int StartAndExitQuickProcess()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var pid = RunOnceAndRelease();
            // 预检必须在句柄释放之后——否则"我们的句柄"就是它还存在的原因
            if (!PowershellSeesProcess(pid)) return pid;
            Thread.Sleep(200);
        }

        throw new InvalidOperationException("连续 5 次拿不到「代理视角已消失」的 PID，跳过代理真跑用例");
    }

    /// <summary>起一个立刻退出的 pwsh，等到它退出并释放句柄，返回它的 PID。</summary>
    private static int RunOnceAndRelease()
    {
        var psi = new ProcessStartInfo("pwsh")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add("exit 0");
        var proc = Process.Start(psi)!;
        proc.WaitForExit(30_000).Should().BeTrue("快速进程应已结束");
        var pid = proc.Id;
        proc.Dispose();
        return pid;
    }

    private static bool PowershellSeesProcess(int pid)
    {
        var psi = new ProcessStartInfo("pwsh")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add($"if (Get-Process -Id {pid} -ErrorAction SilentlyContinue) {{ 'LIVE' }} else {{ 'GONE' }}");
        using var proc = Process.Start(psi)!;
        var text = proc.StandardOutput.ReadToEnd().Trim();
        proc.WaitForExit(30_000).Should().BeTrue("预检应在 30s 内结束");
        return text == "LIVE";
    }

    private static (int ExitCode, string StdOut, string StdErr) RunAgent(string agentPath, int hostPid,
        string installDir, string stagedDir, string localAppData, string exeName)
    {
        Directory.CreateDirectory(localAppData);
        var psi = NewPowershell();
        psi.Environment["LOCALAPPDATA"] = localAppData;
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add(agentPath);
        psi.ArgumentList.Add("-HostPid");
        psi.ArgumentList.Add(hostPid.ToString());
        psi.ArgumentList.Add("-InstallDir");
        psi.ArgumentList.Add(installDir);
        psi.ArgumentList.Add("-StagedDir");
        psi.ArgumentList.Add(stagedDir);
        psi.ArgumentList.Add("-ExeName");
        psi.ArgumentList.Add(exeName);
        return RunExternal(psi, 180_000, "代理应在 180s 内结束");
    }

    private static ProcessStartInfo NewPowershell()
    {
        var psi = new ProcessStartInfo("pwsh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        return psi;
    }

    /// <summary>
    /// 并发抽干两条管道再等退出：<c>WaitForExit</c> 之后再 <c>ReadToEnd</c> 会在输出填满管道缓冲时
    /// 死锁（代理脚本的 robocopy 回显足够填满，实测卡满超时）。
    /// </summary>
    private static (int ExitCode, string StdOut, string StdErr) RunExternal(ProcessStartInfo psi,
        int timeoutMs, string because)
    {
        var outSb = new StringBuilder();
        var errSb = new StringBuilder();
        using var process = Process.Start(psi)!;
        process.OutputDataReceived += (_, e) => { if (e.Data != null) outSb.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) errSb.AppendLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        var finished = process.WaitForExit(timeoutMs);
        if (finished)
        {
            process.WaitForExit();   // 再等一次不带超时：确保异步回调把剩余行 delivered 完
        }
        else
        {
            try { process.Kill(entireProcessTree: true); } catch { /* 尽力而为 */ }
        }
        finished.Should().BeTrue(because);
        return (process.ExitCode, outSb.ToString(), errSb.ToString());
    }

    private static string FindRepositoryFilePath(params string[] relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, Path.Combine(relative));
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException($"未找到仓库文件: {string.Join('/', relative)}");
    }

    private static string FindRepositoryFile(params string[] relative)
        => File.ReadAllText(FindRepositoryFilePath(relative), Encoding.UTF8);

    private static string ExtractFunction(string script, string marker)
    {
        // 脚本随包分发要求 CRLF+BOM（PowerShell 5.1 编码坑），按 LF 切分前先归一换行
        var text = script.Replace("\r\n", "\n");
        var start = text.IndexOf(marker, StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1, $"脚本里应存在 {marker}（缺失说明归一化逻辑被删除）");
        var end = text.IndexOf("\n}\n", start, StringComparison.Ordinal);
        end.Should().BeGreaterThan(-1, "函数体应以独立一行的 } 结束");
        return text[start..(end + 3)];
    }

    private static (int ExitCode, string StdOut, string StdErr) RunPowershell(string script, string[] args)
    {
        var psi = NewPowershell();
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add(script);
        foreach (var a in args) psi.ArgumentList.Add(a);
        return RunExternal(psi, 60_000, "pwsh 探针应在 60s 内结束");
    }
}

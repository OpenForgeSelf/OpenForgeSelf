using FluentAssertions;

namespace ForgeSelf.Api.Tests;

/// <summary>
/// 仓库脚本静态守卫（spec 036 教训）：Windows PowerShell 5.1 把无 BOM 的 UTF-8 .ps1
/// 按 ANSI(GBK) 解码，含中文字符串的脚本会在解析期直接崩（update-agent 曾因此静默失败，
/// 宿主退出后无人换文件重启）。约定：含非 ASCII 文本的 .ps1 必须存为 UTF-8 with BOM。
/// </summary>
public class RepositoryScriptTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "scripts")))
            dir = dir.Parent;
        dir.Should().NotBeNull("测试需能从 bin 目录向上定位仓库根的 scripts/");
        return dir!.FullName;
    }

    public static IEnumerable<object[]> NonAsciiScripts()
    {
        var root = Path.Combine(FindRepoRoot(), "scripts");
        foreach (var file in Directory.EnumerateFiles(root, "*.ps1", SearchOption.AllDirectories))
        {
            var bytes = File.ReadAllBytes(file);
            var text = System.Text.Encoding.UTF8.GetString(bytes);
            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"[^\x00-\x7F]"))
                yield return new object[] { file };
        }
    }

    [Theory]
    [MemberData(nameof(NonAsciiScripts))]
    public void ScriptsWithNonAscii_MustHaveUtf8Bom(string file)
    {
        var bytes = File.ReadAllBytes(file);
        bytes.Length.Should().BeGreaterThan(3);
        bytes[0].Should().Be(0xEF, $"{Path.GetFileName(file)} 含中文但缺 UTF-8 BOM，PS5.1 会按 GBK 解析炸掉");
        bytes[1].Should().Be(0xBB);
        bytes[2].Should().Be(0xBF);
    }

    /// <summary>
    /// git 输出是 UTF-8，而 PowerShell 5.1 捕获子进程 stdout 时按控制台 OEM 码页（本环境 GBK 936）解码
    /// ⇒ 中文 commit 标题在捕获瞬间就被烤成乱码，再写进产物。2026-10-04 实测：RELEASE-NOTES 的更新说明整片乱码，
    /// 宿主只是原样显示（它读的是更新源目录里的 .md）。约定：凡捕获 git log 的脚本，必须在第一次捕获之前
    /// 显式把 [Console]::OutputEncoding 设为 UTF8（make-release-notes.ps1 用 Invoke-GitUtf8 包住，同样算合规）。
    /// </summary>
    [Fact]
    public void GitLogCapturingScripts_MustSwitchConsoleToUtf8First()
    {
        var root = FindRepoRoot();
        const string Marker = "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8";
        var offenders = new List<string>();
        var scanned = 0;

        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "scripts"), "*.ps1", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            var capture = new[] { text.IndexOf("& git log", StringComparison.Ordinal), text.IndexOf("Invoke-GitUtf8 log", StringComparison.Ordinal) }
                .Where(i => i >= 0)
                .DefaultIfEmpty(-1)
                .Min();
            if (capture < 0) continue;

            scanned++;
            var marker = text.IndexOf(Marker, StringComparison.Ordinal);
            if (marker < 0 || marker > capture)
                offenders.Add(Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/') + "（捕获位置 " + capture + "，切码页位置 " + marker + "）");
        }

        scanned.Should().BeGreaterThan(0, "scripts 下确实在捕获 git log 输出，否则本判据是空转");
        offenders.Should().BeEmpty("以下脚本在捕获 git log 前没把控制台输出码页切到 UTF-8，中文提交标题会变乱码写进产物：" + string.Join(" ; ", offenders));
    }

    /// <summary>
    /// 发布布局契约（2026-10-04 输入18）：内置插件必须随版本落在 versions/&lt;ver&gt;/plugins/，
    /// 打包脚本不得再把它外置到安装根、更不得从版本目录删掉——那正是"更新后整台实例 0 插件"的成因
    /// （宿主运行期扫的是业务层旁边的 plugins/，见 AppBuilder 插件根两路扫描）。
    /// </summary>
    [Fact]
    public void PackageRelease_MustKeepBundledPluginsInsideVersionDirectory()
    {
        var root = FindRepoRoot();
        var script = Path.Combine(root, "scripts", "release", "package-release.ps1");
        File.Exists(script).Should().BeTrue("发布组装脚本必须在位");

        var text = File.ReadAllText(script);

        // 反向对照：这两行就是当年把版本目录插件掏空的写法，出现即红
        text.Should().NotContain("Remove-Item (Join-Path $versionDir 'plugins')",
            "不得从 versions/<ver>/ 删除内置插件目录（删了宿主就扫不到插件）");
        text.Should().NotContain("$layoutPlugins",
            "不得把内置插件外置复制到安装根 plugins/（内置插件随版本走，才能随版本回滚）");

        // 正向判据：随版本携带要有显式校验，publish 产出异常时当场失败而不是静默出空包
        text.Should().Contain("versions/<ver>/plugins", "脚本注释与抛错文案要点明内置插件的落位");
        text.Should().Contain("-ceq 'plugins'",
            "产出校验必须按**大小写敏感**判定 plugins 目录在位——Windows 的 Test-Path 大小写不敏感，" +
            "publish 产出的是 Plugins/，用不敏感检查会放行一个运行布局里不该存在的大写目录名（真源 §4-R9）");

        // 目录名规范化：publish 侧源码目录是 Plugins（csproj Content Include），进包必须落成小写 plugins
        text.Should().Contain("-ceq 'Plugins'", "必须显式处理 publish 产出的大写 Plugins 目录名");
        text.Should().Contain("Plugins → plugins", "规范化动作要留日志，便于事后确认包内大小写不是偶然");

        // 阳性对照：本判据扫的不是空文件
        new FileInfo(script).Length.Should().BeGreaterThan(1000);
    }

    /// <summary>
    /// 插件 zip 不得带上宿主共享程序集、它们的 pdb，以及宿主已提供的 Microsoft.Extensions.*。
    /// 2026-11-05 HelloWorld 包曾打进 ForgeSelf.Abstractions.pdb、ForgeSelf.Core.pdb 和
    /// Microsoft.Extensions.DependencyInjection.Abstractions.dll。
    /// </summary>
    [Fact]
    public void PackagePluginScript_ExcludesHostSharedPdbAndFrameworkExtensions()
    {
        var script = Path.Combine(FindRepoRoot(), "scripts", "package-plugin.ps1");
        var text = File.ReadAllText(script);
        text.Should().Contain("function Test-HostSharedPackageFile");
        text.Should().Contain("Microsoft.Extensions.");
        text.Should().Contain("ForgeSelf.*");
        text.Should().Contain(".pdb");
        text.Should().Contain("Package hygiene failed");
    }

    /// <summary>
    /// 2026-11-19：Cloud Agent hooksPath dispatcher 只调用带 +x 的原 hook；
    /// 安装脚本必须 chmod +x 并断言，且 pre-commit 必须调用 feature/回写校验（与 CI 对齐）。
    /// </summary>
    [Fact]
    public void GitHooksInstall_MustChmodAndPreCommitMustCheckFeatureWriteback()
    {
        var root = FindRepoRoot();
        var install = File.ReadAllText(Path.Combine(root, "scripts", "install-git-hooks.ps1"));
        install.Should().Contain("chmod +x", "安装 hook 必须设置可执行位，否则 Cloud dispatcher 静默跳过");
        install.Should().Contain("Test-IsExecutable", "安装后必须断言可执行，防止再出现 644 假安装");
        install.Should().Contain("git-common-dir",
            "安装目标须用 git-common-dir/hooks，避免服从 core.hooksPath 装进 Cursor dispatcher 目录");
        // 允许注释里提到「禁止 git-path hooks」；禁止作为赋值安装目标
        System.Text.RegularExpressions.Regex.IsMatch(install, @"(?m)^\s*\$\w+.*=.*rev-parse --git-path hooks")
            .Should().BeFalse("不得用 --git-path hooks 解析结果作为安装目标（会跟随 core.hooksPath）");

        var preCommit = File.ReadAllText(Path.Combine(root, "scripts", "hooks", "pre-commit"));
        preCommit.Should().Contain("verify-pilot-feature-writeback.ps1",
            "pre-commit 必须跑 feature/回写校验，不能只靠 CI");

        var writeback = Path.Combine(root, "scripts", "verify-pilot-feature-writeback.ps1");
        File.Exists(writeback).Should().BeTrue("共享回写校验脚本必须在位");
        var wb = File.ReadAllText(writeback);
        wb.Should().Contain("$feat-*.md", "须按 feature 短号匹配 docs/02-features/{feature}-*.md");
        wb.Should().Contain("ChangedFilesPath");
    }

    [Fact]
    public void RootDevCommand_MustUseFullStackLauncher()
    {
        var root = FindRepoRoot();
        using var package = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "package.json")));
        var scripts = package.RootElement.GetProperty("scripts");

        scripts.GetProperty("dev").GetString().Should().Be(
            "pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/dev-stack.ps1",
            "根目录 pnpm dev 必须复用已有的前后端整栈启动器");
        scripts.GetProperty("dev:web").GetString().Should().Be("pnpm -C ForgeSelf.Web dev",
            "原前端开发入口必须保留为 dev:web");
        scripts.TryGetProperty("dev:api", out _).Should().BeFalse("旧的 dev:api 入口必须移除");

        var launcher = Path.Combine(root, "scripts", "dev-stack.ps1");
        File.Exists(launcher).Should().BeTrue("根 dev 命令的整栈启动器必须在位");
        var launcherText = File.ReadAllText(launcher);
        launcherText.Should().Contain("Start-Process -FilePath 'dotnet'",
            "整栈启动器必须实际启动宿主后端");
        launcherText.Should().Contain("Start-Process -FilePath 'node'",
            "整栈启动器必须实际启动宿主前端");
        launcherText.Should().Contain("dev-stack.json",
            "整栈启动器须保留既有实例状态管理");
        new FileInfo(launcher).Length.Should().BeGreaterThan(1000,
            "启动脚本阳性对照：守卫不得因空文件或路径误配而空转");
    }

    /// <summary>仓库级守卫（2026-11-21 输入15）：原生更新代理双轨在位 + PC-041 三处同源修复在位。</summary>
    [Fact]
    public void 更新代理双轨随包与PC041三处同源修复必须在位()
    {
        var root = FindRepoRoot();

        // 原生代理工程与发布入口在位（ForgeSelf.UpdateAgent / publish-updateagent.ps1）
        File.Exists(Path.Combine(root, "ForgeSelf.UpdateAgent", "ForgeSelf.UpdateAgent.csproj"))
            .Should().BeTrue("原生 update-agent.exe 工程（2026-11-21 输入15）必须存在");
        File.Exists(Path.Combine(root, "scripts", "release", "publish-updateagent.ps1"))
            .Should().BeTrue("publish-updateagent.ps1 单点发布入口必须存在");

        // ps1 双轨随包：老宿主 StagedUpdateService 只认 ps1，exe 感知是未发布新代码——禁删
        var ps1 = Path.Combine(root, "scripts", "update-agent.ps1");
        File.Exists(ps1).Should().BeTrue("update-agent.ps1 必须继续随包分发（老宿主只认 ps1，删除=存量更新链断裂）");

        // PC-041 三处同源：版本比较补零定宽（发行串第 4 段 10 位时间码 > Int32.MaxValue，禁 [version] 解析）
        foreach (var rel in new[]
                 {
                     "ForgeSelf.UpdateAgent/UpdateAgentCore.cs",
                     "scripts/update-agent.ps1",
                     "scripts/publish-plugin.ps1",
                 })
        {
            var text = File.ReadAllText(Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar)));
            text.Should().Contain("PadLeft(12, '0')",
                $"{rel} 的版本比较必须补零定宽（PC-041 canonical fix），禁 System.Version/[version] 分量解析");
        }
    }

    /// <summary>
    /// PC-043（2026-11-23 输入5）：跨平台脚本的「平台专属短路」必须先于 POSIX 探测。
    /// 实测：Windows + git-bash 机器上 bash 在 PATH，install-git-hooks.ps1 的 Test-IsExecutable
    /// 先走 `bash -lc 'test -x 盘符:\...'`——MSYS 内建 test 不识别反斜杠 Windows 路径 → 恒 false
    /// → hook 被误判不可执行 → postinstall 恒败 → 发布链每个 pnpm install (host-web/plugin-web) 全挂。
    /// 守卫：$IsWindows 短路必须出现在 bash 分支之前（顺序断言，非行为模拟）。
    /// </summary>
    [Fact]
    public void 跨平台脚本平台短路必须先于POSIX探测_安装钩子()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root, "scripts", "install-git-hooks.ps1"));

        var windowsShortCircuit = text.IndexOf("if ($IsWindows) { return $true }", StringComparison.Ordinal);
        windowsShortCircuit.Should().BeGreaterThan(0, "Test-IsExecutable 必须保留 Windows 平台短路（NTFS 无可执行位语义）");

        var bashProbe = text.IndexOf("& bash -lc", StringComparison.Ordinal);
        bashProbe.Should().BeGreaterThan(0, "Test-IsExecutable 的 POSIX 探测分支必须仍在（Linux/Cloud 侧需要）");
        windowsShortCircuit.Should().BeLessThan(bashProbe,
            "Windows 短路必须先于 bash 探测——否则 git-bash 机器上 test -x 收到 Windows 路径恒 false（PC-043 实测发布链全挂）");
    }
}

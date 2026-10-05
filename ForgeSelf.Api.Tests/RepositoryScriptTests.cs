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
}

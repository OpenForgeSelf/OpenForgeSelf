using System.Text;
using ForgeSelf.Api.Plugins.SmartTerminal.Models;
using ForgeSelf.Api.Plugins.SmartTerminal.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.SmartTerminalTests;

/// <summary>
/// PC-036 复现组（输入22 · 2026-11-15）：智能终端实测三缺陷的类级守卫。
/// A) 新会话默认起始目录 `~` 未展开 → 「工作目录不存在」+ 泄露宿主进程路径（2026-11-13 BUG#1 延期债到期）。
/// C) cmd 会话 `cd` 后 cwd 永不更新（包裹命令不采集 cwd，页面目录恒旧）。
/// </summary>
public class StStartDirTests
{
    [Fact]
    public void Resolve_Tilde_ReturnsUserProfile()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var resolved = StStartDir.Resolve("~");

        Assert.Equal(home, resolved);
    }

    [Fact]
    public void Resolve_TildeWithSubpath_CombinesUnderUserProfile()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var resolved = StStartDir.Resolve("~/Documents");

        Assert.Equal(Path.Combine(home, "Documents"), resolved);
    }

    [Fact]
    public void Resolve_NullOrEmpty_ReturnsUserProfile()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        Assert.Equal(home, StStartDir.Resolve(null));
        Assert.Equal(home, StStartDir.Resolve("  "));
    }

    [Fact]
    public void Resolve_NonRootedMissingPath_MessageDoesNotLeakHostProcessCwd()
    {
        // 脱敏回归：相对路径不存在时报错只回显用户输入，不得拼接宿主进程工作目录（BUG#1 泄露面）。
        var ex = Assert.Throws<SmartTerminalException>(() => StStartDir.Resolve("no-such-dir-st-test"));

        Assert.Equal("工作目录不存在: no-such-dir-st-test", ex.Message);
        Assert.DoesNotContain(Environment.CurrentDirectory, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_RootedMissingPath_ThrowsWithFullPath()
    {
        var missing = Path.Combine(Path.GetTempPath(), "st-no-such-" + Guid.NewGuid().ToString("N"));

        var ex = Assert.Throws<SmartTerminalException>(() => StStartDir.Resolve(missing));

        Assert.Contains(missing, ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>C 类复现：真实 cmd 会话里 cd 之后，Run 返回的 Cwd 必须反映切换后的目录（两行采集协议）。</summary>
public class SessionShellCmdCwdTests
{
    [Fact]
    public void Run_AfterCd_ReportsSwitchedCwd()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "st-cwd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp); // 只创建不删除（数据安全铁律：测试不做清理动作）
        var root = Path.GetPathRoot(tmp)!;

        using var shell = SessionShell.StartLocal(
            new ShellProfileDto { Id = "t-cmd", DisplayName = "cmd", Executable = "cmd.exe", Kind = "cmd", Available = true },
            tmp);

        var result = shell.Run("cd " + root, "cmd", TimeSpan.FromSeconds(30));

        Assert.Equal(root.TrimEnd('\\'), result.Cwd.TrimEnd('\\'));
        Assert.NotEqual(tmp.TrimEnd('\\'), result.Cwd.TrimEnd('\\'));
    }
}

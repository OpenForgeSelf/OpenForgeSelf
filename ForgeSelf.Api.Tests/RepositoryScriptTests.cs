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
}

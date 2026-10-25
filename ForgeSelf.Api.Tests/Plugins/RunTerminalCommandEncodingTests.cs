using System.Text;
using System.Text.Json;
using FluentAssertions;
using ForgeSelf.Api.Plugins.AIAgent;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Core;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins;

/// <summary>
/// PC-011 红灯复现（PILOT-073 · 2026-10-22）：run_terminal_command 对子进程输出**固定按 UTF-8 解码**
/// （RunTerminalCommandTool.cs 现状 StandardOutputEncoding=UTF8），子进程按系统 ANSI 码页（GBK/936）
/// 写中文时产出 U+FFFD——中文观察数据不可逆销毁（2026-10-22 fs 实例实测坐实）。
/// 复现与测试环境码页无关：载荷经 -EncodedCommand（base64 UTF-16LE，守卫会解码扫描）进入子进程，
/// 子进程用 .NET API 把确定文本按指定码页编码后**直写 stdout 原始字节**：
///   - GBK 用例：写入 "中文编码测试" 的 936 编码字节（probe 实测 d6d0cec4…，非合法 UTF-8）→ 修复前必红；
///   - UTF-8 用例：同文 UTF-8 字节 → 阳性对照，防「改成永远 ANSI 解码」蒙混过关。
/// 期望串在测试侧以 \u 转义固化，同样不依赖运行环境。
/// </summary>
public class RunTerminalCommandEncodingTests
{
    /// <summary>"中文编码测试"（\u4e2d\u6587\u7f16\u7801\u6d4b\u8bd5）。</summary>
    private const string Chinese = "\u4e2d\u6587\u7f16\u7801\u6d4b\u8bd5";

    [Fact]
    public async Task GbkChildOutput_DecodesBackToChinese_WithoutReplacementChars()
    {
        var stdout = await RunChildEncodingOutputAsync("gbk");

        stdout.Should().Contain(Chinese, "子进程按 GBK 写的中文应被无损还原");
        stdout.Should().NotContain("\uFFFD", "修复前此处必现 U+FFFD（红灯）");
    }

    [Fact]
    public async Task Utf8ChildOutput_DecodesBackToChinese_WithoutReplacementChars()
    {
        var stdout = await RunChildEncodingOutputAsync("utf8");

        stdout.Should().Contain(Chinese, "UTF-8 阳性对照：不能为修 GBK 把 UTF-8 弄坏");
        stdout.Should().NotContain("\uFFFD");
    }

    /// <summary>
    /// 起一个 pwsh 子进程（经被测工具本体），把 <see cref="Chinese"/> 按指定码页编码直写 stdout，
    /// 返回工具 JSON 里的 stdout 字段。
    /// </summary>
    private static async Task<string> RunChildEncodingOutputAsync(string encoding)
    {
        var psEncoding = encoding == "gbk"
            ? "[Text.Encoding]::GetEncoding(936)"
            : "[Text.Encoding]::UTF8";
        var payload =
            "$s=[Console]::OpenStandardOutput(); " +
            "$b=" + psEncoding + ".GetBytes('" + Chinese + "'); " +
            "$s.Write($b,0,$b.Length); $s.Flush()";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(payload));

        // WorkingDirectory 必须真实存在（否则进程起不来=无效红）；进程只写 stdout 不落盘。
        var root = Path.Combine(Path.GetTempPath(), "forgeself-enc-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var tool = CreateTool(root);
            var parameters = JsonSerializer.Serialize(new
            {
                command = $"pwsh -NoProfile -NonInteractive -EncodedCommand \"{encoded}\"",
                cwd = "",
                timeoutSeconds = 30,
            });

            var json = await tool.ExecuteAsync(parameters);
            using var doc = JsonDocument.Parse(json);
            var rootEl = doc.RootElement;

            rootEl.GetProperty("success").GetBoolean().Should().BeTrue($"工具应正常执行（完整返回：{json}）");
            rootEl.GetProperty("exitCode").GetInt32().Should().Be(0);
            return rootEl.GetProperty("stdout").GetString() ?? string.Empty;
        }
        finally
        {
            try { Directory.Delete(root, true); } catch (IOException) { /* 临时目录清理失败不影响断言 */ }
        }
    }

    private static RunTerminalCommandTool CreateTool(string projectRoot)
    {
        var workspace = new Mock<IProjectWorkspaceService>();
        workspace.Setup(w => w.IsProjectSet).Returns(true);
        workspace.Setup(w => w.ProjectRoot).Returns(projectRoot);
        // 工具只调 ResolveSafePath(dto.Cwd)：模拟真实语义——相对路径拼根（"" → 根本身）
        workspace.Setup(w => w.ResolveSafePath(It.IsAny<string>()))
            .Returns<string>(rel => string.IsNullOrWhiteSpace(rel)
                ? projectRoot
                : Path.GetFullPath(Path.Combine(projectRoot, rel.Replace('\\', Path.DirectorySeparatorChar))));

        var ctx = new Mock<IContext>();
        ctx.Setup(c => c.Get<IProjectWorkspaceService>()).Returns(workspace.Object);

        return new RunTerminalCommandTool("aiagent", ctx.Object);
    }
}

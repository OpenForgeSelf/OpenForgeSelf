using System.Diagnostics;
using System.Text.Json;
using ForgeSelf.Api.Plugins.McpCenter.Models;
using ForgeSelf.Api.Plugins.McpCenter.Services.McpClient;
using Xunit.Abstractions;

namespace ForgeSelf.Api.Tests.Plugins.McpCenterTests;

/// <summary>
/// PC-010 红灯锚定：stdio 传输在 Windows 下必须能启动**批处理 shim 形态**的命令（npx.cmd / *.bat）。
/// 现场证据（2026-10-18 用户实例 :51888）：外部 MCP 配 command="npx" → 400
/// 「无法启动外部 MCP 进程 'npx': An error occurred trying to start process 'npx' ... 系统找不到指定的文件」——
/// Windows 的 npx 实为 npx.cmd（npm 批处理 shim），CreateProcess 不按 PATHEXT 解析批处理，
/// UseShellExecute=false + FileName="npx" 必然 ERROR_FILE_NOT_FOUND。
/// 既有集成测试只用 node（node.exe 真实存在），e2e「stdio npx」用例走优雅降级分支——shim 路径零覆盖。
/// </summary>
public sealed class McpStdioWindowsShimTests
{
    private readonly ITestOutputHelper _out;

    public McpStdioWindowsShimTests(ITestOutputHelper @out) => _out = @out;

    /// <summary>真实启动集成锚：Windows + command="npx" 必须完成 initialize + tools/list（修复前=红灯）。
    /// 用 e2e 同款 server-everything（本机 npm 缓存已含，秒级启动，不发网络请求）。</summary>
    [Fact]
    public async Task StdioTransport_WindowsNpxShim_LaunchesAndInitializes()
    {
        if (!OperatingSystem.IsWindows()) return; // shim 解析问题仅存在 Windows
        if (Environment.GetEnvironmentVariable("CI") == "true") return; // CI 无 npm 缓存时 npx 下载不稳定，本机为基准

        var cfg = new McpExternalServerConfig
        {
            Id = "npx-shim",
            Name = "npx shim",
            Transport = "stdio",
            Command = "npx",
            Args = new List<string> { "-y", "@modelcontextprotocol/server-everything", "stdio" }
        };

        var transport = new StdioMcpTransport(cfg);
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            // v2.5.0 客户端合规：initialize 载荷含 clientInfo（canonical 单点），此处验的是「进程能拉起 + 握手通」
            var init = await transport.InitializeAsync(new[] { "2025-06-18" }, cts.Token);
            Assert.Equal(JsonValueKind.Object, init.ValueKind);

            var tools = await transport.ListToolsAsync(cts.Token);
            var count = tools.GetProperty("tools").GetArrayLength();
            _out.WriteLine($"npx shim 启动成功，server-everything 工具数={count}");
            Assert.True(count > 0, "server-everything 应至少暴露 1 个工具");
        }
        finally
        {
            await transport.CloseAsync();
        }
    }

    /// <summary>找不到命令时的报错必须**可行动**（指名 PATH），不得再抛 Win32「系统找不到指定的文件」天书。</summary>
    [Fact]
    public void StdioTransport_UnknownCommand_GivesActionableError()
    {
        if (!OperatingSystem.IsWindows()) return;

        var cfg = new McpExternalServerConfig
        {
            Id = "ghost",
            Name = "Ghost",
            Transport = "stdio",
            Command = "npx",
            // 白名单内的命令 + 一个绝不存在的同名文件目录环境无法构造 PATH 隔离（进程级）——
            // 用白名单外命令验证更早的校验层（Validate），白名单内 NOT-FOUND 路径由 ResolveLaunch 单测覆盖
            Args = new List<string>()
        };
        cfg.Command = "definitely-not-a-real-mcp-command";

        var ex = Assert.Throws<McpClientException>(() => new StdioMcpTransport(cfg));
        _out.WriteLine(ex.Message);
        // 白名单守卫先命中（安全边界不变），报错含可行动提示
        Assert.Contains("白名单", ex.Message);
    }

    /* ── ResolveLaunch 纯函数单测（注入 pathDirs/isWindows/fileExists，跨平台确定性） ── */

    [Fact]
    public void ResolveLaunch_Windows_ShimOnly_UsesShellWrapper()
    {
        var dirs = new[] { @"C:\fake\npm" };
        var (file, wrapper) = StdioMcpTransport.ResolveLaunch(
            "npx", new[] { "-y", "@modelcontextprotocol/server-everything", "stdio" }, dirs, isWindows: true,
            path => path == @"C:\fake\npm\npx.cmd");

        Assert.NotNull(wrapper); // 非 null=走 shell 包装器（不再把 "npx" 直接交给 CreateProcess）
        Assert.Contains(@"C:\fake\npm\npx.cmd", wrapper);
        Assert.Contains("@modelcontextprotocol/server-everything stdio\"", wrapper);
    }

    [Fact]
    public void ResolveLaunch_Windows_PrefersExeOverCmd()
    {
        var dirs = new[] { @"C:\fake\bin", @"C:\fake\npm" };
        var (file, wrapper) = StdioMcpTransport.ResolveLaunch(
            "uvx", Array.Empty<string>(), dirs, isWindows: true,
            path => path is @"C:\fake\bin\uvx.exe" or @"C:\fake\npm\uvx.cmd");

        Assert.Equal(@"C:\fake\bin\uvx.exe", file);
        Assert.Null(wrapper); // 直启分支：保持 ArgumentList 原行为（既有 node 集成测依赖）
    }

    [Fact]
    public void ResolveLaunch_Windows_ShimArguments_HaveDSlashCQuotes_AndSpaceQuoting()
    {
        var dirs = new[] { @"C:\Program Files\npm" };
        var (file, wrapper) = StdioMcpTransport.ResolveLaunch(
            "npx", new[] { "-y", "my tool", "plain" }, dirs, isWindows: true,
            path => path == Path.Combine(@"C:\Program Files\npm", "npx.cmd"));

        Assert.NotNull(wrapper);
        // /d /s /c 外壳 + 内层含空格 token 加引号；完整 shim 路径含空格也被引号包住
        var expected = "/d /s /c \"\"" + Path.Combine(@"C:\Program Files\npm", "npx.cmd") + "\" -y \"my tool\" plain\"";
        Assert.Equal(expected, wrapper);
    }

    [Fact]
    public void ResolveLaunch_Windows_NotAnywhere_ThrowsActionablePathError()
    {
        var dirs = new[] { @"C:\fake\a", @"C:\fake\b" };
        var ex = Assert.Throws<McpClientException>(() =>
            StdioMcpTransport.ResolveLaunch("node", Array.Empty<string>(), dirs, isWindows: true, _ => false));
        Assert.Contains("PATH", ex.Message);
        Assert.Contains("node.exe", ex.Message);
    }

    [Fact]
    public void ResolveLaunch_NonWindows_PassesThroughUnchanged()
    {
        var (file, wrapper) = StdioMcpTransport.ResolveLaunch(
            "npx", new[] { "-y", "pkg" }, new[] { "/usr/local/bin" }, isWindows: false, _ => false);
        Assert.Equal("npx", file);
        Assert.Null(wrapper);
    }

    [Fact]
    public void ResolveLaunch_Windows_RootedShimPath_UsesShellWrapper()
    {
        var (file, wrapper) = StdioMcpTransport.ResolveLaunch(
            @"C:\tools\run-mcp.bat", new[] { "--fast" }, Array.Empty<string>(), isWindows: true,
            path => path == @"C:\tools\run-mcp.bat");
        Assert.NotNull(wrapper);
        Assert.Contains("/d /s /c", wrapper);
        Assert.Contains(@"C:\tools\run-mcp.bat --fast", wrapper);
    }
}

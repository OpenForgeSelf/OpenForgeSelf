using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.McpCenter.Services;
using ForgeSelf.Api.Services;
using ForgeSelf.Core;
using Moq;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.McpCenterTests;

/// <summary>
/// 2026-11-02：run_terminal_command 超时/调用方取消必须终止进程树；
/// 超时文案不再含 "process continues in background"。
/// </summary>
public class RunTerminalCommandTimeoutKillTests
{
    private static RunTerminalCommandTool CreateTool() =>
        new(Mock.Of<IContext>());

    private static string NewTempDir()
    {
        return Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "forgeself-term-kill-" + Guid.NewGuid().ToString("N"))).FullName;
    }

    private static bool IsAlive(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return !p.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static async Task WaitUntilDeadAsync(int pid, TimeSpan budget)
    {
        var deadline = DateTime.UtcNow + budget;
        while (DateTime.UtcNow < deadline)
        {
            if (!IsAlive(pid)) return;
            await Task.Delay(50);
        }
    }

    /// <summary>
    /// 长睡命令：先把 $PID 落到 pid.txt，再 Sleep 60s——超时后断言文件中的 PID 已不在。
    /// </summary>
    private static string LongSleepWritePidCommand(string pidFileName) =>
        $"pwsh -NoProfile -NonInteractive -Command \"$pid | Set-Content -Path {pidFileName} -Encoding ascii; Start-Sleep -Seconds 60\"";

    [Fact]
    public async Task Timeout_KillsProcess_AndErrorDoesNotSayContinuesInBackground()
    {
        var dir = NewTempDir();
        var pidPath = Path.Combine(dir, "pid.txt");
        try
        {
            var json = await CreateTool().ExecuteAsync(JsonSerializer.Serialize(new
            {
                command = LongSleepWritePidCommand("pid.txt"),
                cwd = dir,
                timeoutSeconds = 2,
            }));

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            root.GetProperty("success").GetBoolean().Should().BeFalse();
            root.GetProperty("timedOut").GetBoolean().Should().BeTrue();
            var error = root.GetProperty("error").GetString() ?? string.Empty;
            error.Should().Contain("timeout");
            error.Should().Contain("process terminated");
            error.Should().NotContain("continues in background");

            File.Exists(pidPath).Should().BeTrue("子进程应在被杀前写出 PID");
            var pid = int.Parse(File.ReadAllText(pidPath).Trim());
            await WaitUntilDeadAsync(pid, TimeSpan.FromSeconds(5));
            IsAlive(pid).Should().BeFalse($"超时后 PID {pid} 必须已退出");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task CallerCancel_KillsProcess()
    {
        var dir = NewTempDir();
        var pidPath = Path.Combine(dir, "pid.txt");
        try
        {
            using var cts = new CancellationTokenSource();
            var execTask = CreateTool().ExecuteAsync(
                JsonSerializer.Serialize(new
                {
                    command = LongSleepWritePidCommand("pid.txt"),
                    cwd = dir,
                    timeoutSeconds = 30,
                }),
                cts.Token);

            // 等 PID 文件出现再取消，避免取消抢在 Start 之前。
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (!File.Exists(pidPath) && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50);
            }
            File.Exists(pidPath).Should().BeTrue("取消前应已启动子进程并写出 PID");
            var pid = int.Parse(File.ReadAllText(pidPath).Trim());

            cts.Cancel();
            var json = await execTask;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            root.GetProperty("success").GetBoolean().Should().BeFalse();
            root.GetProperty("cancelled").GetBoolean().Should().BeTrue();
            (root.GetProperty("error").GetString() ?? string.Empty).Should().Contain("cancelled");

            await WaitUntilDeadAsync(pid, TimeSpan.FromSeconds(5));
            IsAlive(pid).Should().BeFalse($"调用方取消后 PID {pid} 必须已退出");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task Timeout_AlsoKillsChildProcess()
    {
        var dir = NewTempDir();
        var parentPidPath = Path.Combine(dir, "parent.txt");
        var childPidPath = Path.Combine(dir, "child.txt");
        var scriptPath = Path.Combine(dir, "spawn-child.ps1");
        try
        {
            // 脚本落盘再 -File 执行，避开嵌套引号被守卫/参数解析误伤。
            await File.WriteAllTextAsync(scriptPath, string.Join(Environment.NewLine, new[]
            {
                "$pid | Set-Content -Path parent.txt -Encoding ascii",
                "$c = Start-Process -FilePath pwsh -ArgumentList '-NoProfile','-NonInteractive','-Command','Start-Sleep -Seconds 60' -PassThru -WindowStyle Hidden",
                "$c.Id | Set-Content -Path child.txt -Encoding ascii",
                "Start-Sleep -Seconds 60",
            }));

            var json = await CreateTool().ExecuteAsync(JsonSerializer.Serialize(new
            {
                command = "pwsh -NoProfile -NonInteractive -File spawn-child.ps1",
                cwd = dir,
                timeoutSeconds = 3,
            }));

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            root.TryGetProperty("timedOut", out var timedOutProp).Should().BeTrue(
                because: "应走超时路径；实际 JSON=" + json);
            timedOutProp.GetBoolean().Should().BeTrue();

            File.Exists(parentPidPath).Should().BeTrue();
            File.Exists(childPidPath).Should().BeTrue();
            var parentPid = int.Parse(File.ReadAllText(parentPidPath).Trim());
            var childPid = int.Parse(File.ReadAllText(childPidPath).Trim());

            await WaitUntilDeadAsync(parentPid, TimeSpan.FromSeconds(5));
            await WaitUntilDeadAsync(childPid, TimeSpan.FromSeconds(5));
            IsAlive(parentPid).Should().BeFalse($"父进程 {parentPid} 应已退出");
            IsAlive(childPid).Should().BeFalse($"子进程 {childPid} 应已被进程树终止");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task ToolRegistry_PassesSignal_AndCancelKillsProcess()
    {
        var dir = NewTempDir();
        var pidPath = Path.Combine(dir, "pid.txt");
        try
        {
            var registry = new ToolRegistry();
            registry.RegisterTool(CreateTool());

            using var cts = new CancellationTokenSource();
            var execTask = registry.ExecuteAsync(new ToolExecution
            {
                CallId = "term-kill-1",
                ToolName = "run_terminal_command",
                ArgsJson = JsonSerializer.Serialize(new
                {
                    command = LongSleepWritePidCommand("pid.txt"),
                    cwd = dir,
                    timeoutSeconds = 30,
                }),
                SessionId = "test"
            }, cts.Token);

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (!File.Exists(pidPath) && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50);
            }
            File.Exists(pidPath).Should().BeTrue();
            var pid = int.Parse(File.ReadAllText(pidPath).Trim());

            cts.Cancel();
            var result = await execTask;
            // 注册表 WhenAny 竞速可能先收口为取消错误，或等工具体返回 cancelled JSON——两种均可，关键是进程已退。
            result.Should().NotBeNull();

            await WaitUntilDeadAsync(pid, TimeSpan.FromSeconds(5));
            IsAlive(pid).Should().BeFalse($"ToolRegistry Signal 取消后 PID {pid} 必须已退出");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}

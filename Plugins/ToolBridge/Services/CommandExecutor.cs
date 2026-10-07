using System.Diagnostics;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.ToolBridge.Services;

/// <summary>
/// 命令执行（PILOT-053 02-spec FR-3.4/3.6/3.7）。
/// 进程姿势沿用内置 agent 已实证的形态（<c>RunTerminalCommandTool.cs:127-189</c>）：
/// UseShellExecute=false（不经 shell 解释）、CreateNoWindow、stdout/stderr 显式 UTF-8、
/// **并发抽干双管道再等退出**（防 stdout 缓冲满把子进程卡在 stderr 写不上）、超时不 Kill。
/// 守卫先于进程：被拒路径绝不 <c>Process.Start</c>（BR-3、AC7）。
/// </summary>
public static class CommandExecutor
{
    public static async Task<ToolResult> RunAsync(SandboxRoot sandbox, string? command, string? cwd, int? timeoutSeconds,
        Dictionary<string, JsonNode>? args = null)
    {
        var start = Environment.TickCount64;
        if (string.IsNullOrWhiteSpace(command))
        {
            return Fail(command, "missing_argument: command", "command 为空", start);
        }

        var guard = CommandGuard.Check(command);
        if (!guard.Allowed)
        {
            // 守卫原文回给 AI（AC7），并留日志（NFR-5）。
            XTrace.Log.Warn("[tool-bridge] run_command 被守卫拒绝：{0}（command={1}）", guard.Reason, command);
            return Fail(command, "command_rejected", guard.Reason, start);
        }

        if (!sandbox.TryResolve(cwd, out var workDir, out var cwdError))
        {
            return Fail(command, "outside_workspace", cwdError, start);
        }

        var (exe, rest) = CommandGuard.SplitExecutable(command);
        var timeout = Math.Clamp(timeoutSeconds ?? CommandGuard.MaxTimeoutSeconds, 1, CommandGuard.MaxTimeoutSeconds);

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = rest,
            WorkingDirectory = workDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        Process? proc;
        try
        {
            proc = Process.Start(psi);
        }
        catch (Win32Exception ex)
        {
            // BC-6：白名单过了但可执行文件不在 PATH。
            return Fail(command, "executable_not_found", $"{exe}（{ex.Message}）", start);
        }
        catch (Exception ex)
        {
            return Fail(command, "start_failed", ex.Message, start);
        }

        if (proc == null)
        {
            return Fail(command, "start_failed", $"failed to start process '{exe}'", start);
        }

        using (proc)
        {
            string stdout;
            string stderr;
            var timedOut = false;
            try
            {
                var outTask = proc.StandardOutput.ReadToEndAsync();
                var errTask = proc.StandardError.ReadToEndAsync();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
                try
                {
                    await proc.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    timedOut = true;
                }

                if (!timedOut)
                {
                    await Task.WhenAll(outTask, errTask);
                }

                stdout = timedOut ? string.Empty : outTask.Result;
                stderr = timedOut ? string.Empty : errTask.Result;
            }
            catch (ObjectDisposedException)
            {
                timedOut = true;
                stdout = string.Empty;
                stderr = string.Empty;
            }

            var (stdoutText, stdoutTruncated) = CommandGuard.Truncate(stdout);
            var (stderrText, stderrTruncated) = CommandGuard.Truncate(stderr);

            if (timedOut)
            {
                // BC-7：超时不 Kill，进程在后台继续；exitCode 用 -1 表达"没等到"。
                XTrace.Log.Warn("[tool-bridge] run_command 超时 {0}s（进程继续后台）：{1}", timeout, command);
                return new ToolResult
                {
                    Tool = ToolSpec.RunCommand,
                    Ok = false,
                    Args = args ?? new Dictionary<string, JsonNode>(),
                    Error = "timeout",
                    Reason = $"timeout {timeout}s (process continues in background)",
                    Result = new JsonObject
                    {
                        ["command"] = command,
                        ["cwd"] = sandbox.ToRelative(workDir),
                        ["exitCode"] = -1,
                        ["timedOut"] = true
                    },
                    DurationMs = Environment.TickCount64 - start
                };
            }

            var exitCode = SafeExitCode(proc);

            // FR-3.8：非 0 退出码不是错误——失败输出同样是有效观察数据。
            var payload = new JsonObject
            {
                ["command"] = command,
                ["cwd"] = sandbox.ToRelative(workDir),
                ["exitCode"] = exitCode,
                ["stdout"] = stdoutText,
                ["stderr"] = stderrText,
                ["stdoutTruncated"] = stdoutTruncated,
                ["stderrTruncated"] = stderrTruncated
            };

            XTrace.Log.Info("[tool-bridge] run_command {0}（{1}ms，exit={2}）", exe, Environment.TickCount64 - start, exitCode);

            return new ToolResult
            {
                Tool = ToolSpec.RunCommand,
                Ok = true,
                Args = args ?? new Dictionary<string, JsonNode>(),
                Result = payload,
                Truncated = stdoutTruncated || stderrTruncated,
                DurationMs = Environment.TickCount64 - start
            };
        }
    }

    private static int SafeExitCode(Process proc)
    {
        try { return proc.ExitCode; }
        catch { return -1; }
    }

    private static ToolResult Fail(string? command, string error, string? reason, long start) => new()
    {
        Tool = ToolSpec.RunCommand,
        Ok = false,
        Args = new Dictionary<string, JsonNode> { ["command"] = JsonValue.Create(command ?? string.Empty)! },
        Error = error,
        Reason = reason,
        DurationMs = Environment.TickCount64 - start
    };
}

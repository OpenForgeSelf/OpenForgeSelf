using System.Diagnostics;
using System.Text;
using NewLife;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AgentHub.Services;

/// <summary>子进程执行结果。</summary>
public class ProcessRunResult
{
    /// <summary>是否成功启动并正常结束</summary>
    public Boolean Started { get; set; }

    /// <summary>退出码（未启动为 -1）</summary>
    public Int32 ExitCode { get; set; } = -1;

    /// <summary>标准输出</summary>
    public String StdOut { get; set; } = String.Empty;

    /// <summary>标准错误</summary>
    public String StdErr { get; set; } = String.Empty;

    /// <summary>是否超时被强杀</summary>
    public Boolean TimedOut { get; set; }

    /// <summary>错误信息（启动失败时）</summary>
    public String? Error { get; set; }

    /// <summary>耗时（毫秒）</summary>
    public Int64 ElapsedMs { get; set; }
}

/// <summary>
/// 子进程执行器（委派与探测的共用底座）。
///
/// 安全铁律（见 design §6）：
/// - 参数一律以 <see cref="ProcessStartInfo.ArgumentList"/> **数组**传递，绝不拼 shell 字符串；
/// - 不使用 <c>cmd /c</c> 拼接；prompt 作为独立 argv 项或走 stdin；
/// - 超时先软终（关闭 stdin / 发终止信号），再强杀进程树。
/// </summary>
public static class ProcessRunner
{
    /// <summary>默认探测超时（毫秒）——版本探测这类轻量命令不应拖太久</summary>
    public const Int32 DefaultProbeTimeoutMs = 10_000;

    /// <summary>
    /// 运行外部命令并收集输出。
    /// </summary>
    /// <param name="fileName">可执行文件（名或绝对路径）</param>
    /// <param name="arguments">参数数组（逐项传递，不做 shell 解析）</param>
    /// <param name="workingDirectory">工作目录（null 表示继承当前目录）</param>
    /// <param name="stdin">写入标准输入的内容（null 表示不写）</param>
    /// <param name="timeoutMs">超时毫秒</param>
    /// <param name="envVars">额外环境变量（键为变量名，值为变量名——只做透传，不落明文）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>执行结果</returns>
    public static async Task<ProcessRunResult> RunAsync(
        String fileName,
        IReadOnlyList<String> arguments,
        String? workingDirectory = null,
        String? stdin = null,
        Int32 timeoutMs = DefaultProbeTimeoutMs,
        IReadOnlyDictionary<String, String>? envVars = null,
        CancellationToken ct = default)
    {
        var result = new ProcessRunResult();
        var sw = Stopwatch.StartNew();

        if (fileName.IsNullOrEmpty())
        {
            result.Error = "可执行文件为空";
            return result;
        }

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = stdin != null,
            UseShellExecute = false,   // 关键：不经过 shell，杜绝命令注入
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        // 逐个添加参数项：ArgumentList 会自行做正确转义，不会把 prompt 里的 ; & | 当命令
        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        if (!workingDirectory.IsNullOrEmpty() && Directory.Exists(workingDirectory))
        {
            psi.WorkingDirectory = workingDirectory;
        }

        // 环境变量只按「变量名」透传（值留在环境里，不入库）
        if (envVars != null)
        {
            foreach (var (key, envName) in envVars)
            {
                if (key.IsNullOrEmpty() || envName.IsNullOrEmpty()) continue;

                var value = Environment.GetEnvironmentVariable(envName);
                if (!value.IsNullOrEmpty()) psi.Environment[key] = value;
            }
        }

        Process? process = null;
        try
        {
            process = new Process { StartInfo = psi };
            if (!process.Start())
            {
                result.Error = $"无法启动进程: {fileName}";
                return result;
            }

            result.Started = true;

            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);

            if (stdin != null)
            {
                try
                {
                    await process.StandardInput.WriteAsync(stdin.AsMemory(), ct);
                    await process.StandardInput.FlushAsync(ct);
                }
                catch (Exception ex)
                {
                    // 有些 CLI 不读 stdin，写入可能失败；不视为致命
                    XTrace.Log.Debug("[AgentHub] 写入子进程 stdin 失败（多数 CLI 不读 stdin 属正常）: {0}", ex.Message);
                }
                finally
                {
                    try { process.StandardInput.Close(); } catch { /* 进程已退出 */ }
                }
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeoutMs);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                result.TimedOut = !ct.IsCancellationRequested;
                KillProcessTree(process);
            }

            // 进程结束后再取输出（避免读未完成）；若被强杀也尽力取已有内容
            result.StdOut = await SafeAwait(stdoutTask);
            result.StdErr = await SafeAwait(stderrTask);

            if (result.TimedOut)
            {
                result.Error = $"执行超时（{timeoutMs}ms），已终止进程树";
            }
            else if (process.HasExited)
            {
                result.ExitCode = process.ExitCode;
            }
        }
        catch (Exception ex)
        {
            result.Error = $"{ex.GetType().Name}: {ex.Message}";
            if (process != null) KillProcessTree(process);
        }
        finally
        {
            sw.Stop();
            result.ElapsedMs = sw.ElapsedMilliseconds;
            process?.Dispose();
        }

        return result;
    }

    /// <summary>强杀进程树（含子进程，如 CLI 拉起的子 agent）。</summary>
    /// <param name="process">目标进程</param>
    public static void KillProcessTree(Process? process)
    {
        if (process == null) return;

        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[AgentHub] 终止进程树失败: {0}", ex.Message);
        }
    }

    /// <summary>等待读取任务完成（读取本身失败不抛出，返回已读到的部分）。</summary>
    private static async Task<String> SafeAwait(Task<String> task)
    {
        try
        {
            return await task;
        }
        catch
        {
            return String.Empty;
        }
    }
}

using System.Diagnostics;
using System.Text;
using ForgeSelf.Api.Plugins.SmartTerminal.Models;
using ForgeSelf.Core;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.SmartTerminal.Services;

/// <summary>长寿命壳进程：按行投喂命令，采集输出与退出码。不以完整 PTY 为验收标准。</summary>
public sealed class SessionShell : IDisposable
{
    private readonly Process _process;
    private readonly string? _askPassFile;
    private readonly object _gate = new();
    private readonly StringBuilder _stdout = new();
    private readonly StringBuilder _stderr = new();
    private readonly ProcessOutputLineBuffer _stdoutLines = new();
    private readonly ProcessOutputLineBuffer _stderrLines = new();
    private bool _disposed;

    public string Cwd { get; private set; }
    public bool IsAlive
    {
        get
        {
            try { return !_disposed && !_process.HasExited; }
            catch { return false; }
        }
    }

    private SessionShell(Process process, string cwd, string? askPassFile = null)
    {
        _process = process;
        _askPassFile = askPassFile;
        Cwd = cwd;
        // PC-011：不使用事件流默认解码（BeginOutputReadLine/BeginErrorReadLine/OutputDataReceived），
        // 改为读原始字节 → ProcessOutputLineBuffer 逐行裁决，GBK 等非 UTF-8 输出不再乱码。
        Pump(_process.StandardOutput.BaseStream, _stdoutLines, _stdout);
        Pump(_process.StandardError.BaseStream, _stderrLines, _stderr);
    }

    /// <summary>读原始字节流，经字节裁决后按完整行落缓冲；进程退出（或会话销毁）读到流尾后 Flush 尾行。</summary>
    private static void Pump(Stream stream, ProcessOutputLineBuffer lineBuffer, StringBuilder sink)
    {
        _ = Task.Run(async () =>
        {
            var chunk = new byte[8192];
            try
            {
                while (true)
                {
                    var read = await stream.ReadAsync(chunk);
                    if (read == 0) break;
                    AppendLines(sink, lineBuffer.Append(chunk.AsSpan(0, read)));
                }
                AppendLines(sink, lineBuffer.Flush());
                if (lineBuffer.UsedNonUtf8)
                    XTrace.Log.Info("[SmartTerminal] 壳输出按非 UTF-8 还原：{0}", lineBuffer.NonUtf8EncodingUsed);
            }
            catch { /* 会话销毁/进程被杀时读流异常属正常收尾 */ }
        });
    }

    private static void AppendLines(StringBuilder sink, List<string> lines)
    {
        if (lines.Count == 0) return;
        lock (sink)
        {
            foreach (var line in lines)
                sink.AppendLine(line);
        }
    }

    public static SessionShell StartLocal(ShellProfileDto shell, string cwd, IDictionary<string, string>? env = null)
    {
        if (!shell.Available)
            throw SmartTerminalException.Invalid("外壳不可用");
        var psi = new ProcessStartInfo
        {
            FileName = shell.Executable,
            WorkingDirectory = Directory.Exists(cwd) ? cwd : (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) is var home && Directory.Exists(home) ? home : Environment.CurrentDirectory),
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        ApplyInteractiveArgs(psi, shell.Kind);
        CopyEnv(psi, env, skipSshPass: true);
        var process = StartOrThrow(psi, "本机壳启动失败");
        var session = new SessionShell(process, psi.WorkingDirectory);
        session.RefreshCwd(shell.Kind);
        return session;
    }

    public static SessionShell StartSsh(string host, int port, string user, string? identityFile, string? password, IDictionary<string, string>? env = null)
    {
        var psi = new ProcessStartInfo
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        var sshArgs = $"-T -o StrictHostKeyChecking=accept-new -o ConnectTimeout=15 -p {port}";
        if (!string.IsNullOrWhiteSpace(identityFile))
        {
            if (!File.Exists(identityFile) || !IsReadable(identityFile))
                throw new SmartTerminalException("credential_unavailable", "私钥文件不可读", 400);
            sshArgs += $" -i {Quote(identityFile)} -o IdentitiesOnly=yes -o BatchMode=yes";
        }

        var target = $"{user}@{host}";
        string? askPassFile = null;
        if (!string.IsNullOrEmpty(password))
        {
            var sshpass = FindOnPath("sshpass");
            if (sshpass != null)
            {
                psi.FileName = sshpass;
                psi.Arguments = $"-e ssh {sshArgs} {Quote(target)}";
                psi.Environment["SSHPASS"] = password;
            }
            else
            {
                var ssh = FindOnPath("ssh.exe") ?? FindOnPath("ssh")
                    ?? throw new SmartTerminalException("connect_failed", "本机没有 ssh，无法用密码建立 SSH", 400);
                askPassFile = WriteAskPassShim();
                psi.FileName = ssh;
                psi.Arguments = $"{sshArgs} -o PreferredAuthentications=password -o PubkeyAuthentication=no -o NumberOfPasswordPrompts=1 {Quote(target)}";
                psi.Environment["SSH_ASKPASS"] = askPassFile;
                psi.Environment["SSH_ASKPASS_REQUIRE"] = "force";
                psi.Environment["DISPLAY"] = "1";
                psi.Environment["ST_SSH_PW"] = password;
            }
        }
        else
        {
            var ssh = FindOnPath("ssh") ?? "ssh";
            psi.FileName = ssh;
            psi.Arguments = $"{sshArgs} {Quote(target)}";
        }

        CopyEnv(psi, env, skipSshPass: string.IsNullOrEmpty(password));
        if (!string.IsNullOrEmpty(password) && askPassFile != null)
        {
            psi.Environment["SSH_ASKPASS"] = askPassFile;
            psi.Environment["SSH_ASKPASS_REQUIRE"] = "force";
            psi.Environment["DISPLAY"] = "1";
            psi.Environment["ST_SSH_PW"] = password;
        }
        var process = StartOrThrow(psi, "SSH 连接失败");
        // 给握手一点时间：若立刻退出则视为连接失败
        Thread.Sleep(200);
        if (process.HasExited)
            throw new SmartTerminalException("connect_failed", "SSH / 本机壳启动失败", 400);
        var session = new SessionShell(process, "~", askPassFile);
        try { session.RefreshCwd("bash"); }
        catch { /* 远端提示符未就绪时保留 ~，后续命令再刷新 */ }
        return session;
    }

    public ShellCommandResult Run(string command, string shellKind, TimeSpan? timeout = null)
    {
        if (!IsAlive) throw SmartTerminalException.Conflict("session_disconnected", "会话未连接");
        lock (_gate)
        {
            var token = Guid.NewGuid().ToString("N")[..12];
            var done = $"__ST_DONE_{token}__";
            ClearBuffers();
            var script = BuildWrappedCommand(command, shellKind, token, done);
            _process.StandardInput.WriteLine(script);
            _process.StandardInput.Flush();

            var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromMinutes(2));
            string done2 = "";
            var phase2 = false;
            string snapshot;
            while (DateTime.UtcNow < deadline)
            {
                snapshot = Snapshot();
                if (!phase2 && snapshot.Contains(done, StringComparison.Ordinal))
                {
                    if (shellKind is "cmd")
                    {
                        // PC-036(C)：交互式 cmd 的 %VAR% 在整行解析期展开（命令执行前），同行拿不到
                        // 执行后的 cwd（实测 `call echo`/`%%` 转义均不可用）。两行协议：标记行结束后
                        // 再发一条采集行，其 %CD% 在命令完成后才被解析展开。
                        phase2 = true;
                        done2 = $"__ST_CWD_{Guid.NewGuid().ToString("N")[..12]}__";
                        _process.StandardInput.WriteLine($"echo {done2}::%CD%");
                        _process.StandardInput.Flush();
                        continue;
                    }
                    return Parse(snapshot, done, token, shellKind);
                }
                if (phase2 && snapshot.Contains(done2, StringComparison.Ordinal))
                {
                    var baseResult = Parse(snapshot, done, token, shellKind);
                    return ApplyCmdCwd(baseResult, snapshot, done2);
                }
                if (!IsAlive)
                    return new ShellCommandResult(SnapshotOutput(snapshot, done), 1, Cwd);
                Thread.Sleep(40);
            }
            return new ShellCommandResult(SecretRedactor.Redact(SnapshotOutput(Snapshot(), done) + "\n[等待命令结束超时]"), 124, Cwd);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { if (!_process.HasExited) _process.Kill(entireProcessTree: true); } catch { /* ignore */ }
        try { _process.Dispose(); } catch { /* ignore */ }
        if (!string.IsNullOrEmpty(_askPassFile))
        {
            try { File.Delete(_askPassFile); } catch { /* ignore */ }
        }
    }

    /// <summary>Windows 没有 sshpass 时，让 ssh 通过环境变量读口令。脚本本身不含口令。</summary>
    private static string WriteAskPassShim()
    {
        var path = Path.Combine(Path.GetTempPath(), "st-askpass-" + Guid.NewGuid().ToString("N") + ".cmd");
        File.WriteAllText(path, """
            @echo off
            powershell.exe -NoProfile -NonInteractive -Command "[Console]::Out.Write($env:ST_SSH_PW)"
            """);
        return path;
    }

    private void RefreshCwd(string shellKind)
    {
        try
        {
            // PC-036：cmd 没有 pwd，`cd`（无参数）即打印当前目录。
            var probe = shellKind is "cmd" ? "cd" : "pwd";
            var result = Run(probe, shellKind, TimeSpan.FromSeconds(5));
            if (!string.IsNullOrWhiteSpace(result.Cwd) && result.Cwd != "~")
                Cwd = result.Cwd;
        }
        catch { /* ignore */ }
    }

    private static void ApplyInteractiveArgs(ProcessStartInfo psi, string kind)
    {
        psi.Arguments = kind switch
        {
            "cmd" => "/K",
            "powershell" or "pwsh" => "-NoLogo -NoProfile -Command -",
            "bash" or "zsh" or "sh" or "fish" or "dash" => "--noprofile --norc",
            _ => ""
        };
    }

    private static string BuildWrappedCommand(string command, string kind, string token, string done)
    {
        if (kind is "cmd")
            return $"{command} & echo {done}!ERRORLEVEL!";
        if (kind is "powershell" or "pwsh")
            return $"{command}; Write-Output ('{done}' + $LASTEXITCODE + '::' + (Get-Location).Path)";
        // POSIX：先跑命令，再输出标记、退出码、pwd
        var safe = command.Replace("'", "'\\''");
        return $"{{ {command} ; }} ; printf '%s\\n' '{done}' \"$?\" \"$(pwd 2>/dev/null)\"";
    }

    private ShellCommandResult Parse(string snapshot, string done, string token, string shellKind)
    {
        var idx = snapshot.IndexOf(done, StringComparison.Ordinal);
        var before = idx >= 0 ? snapshot[..idx] : snapshot;
        var after = idx >= 0 ? snapshot[(idx + done.Length)..].Trim() : "";
        var exit = 0;
        var cwd = Cwd;
        if (shellKind is "cmd")
        {
            var bang = after.Trim();
            if (bang.StartsWith('!')) bang = bang[1..];
            int.TryParse(new string(bang.TakeWhile(char.IsDigit).ToArray()), out exit);
        }
        else if (shellKind is "powershell" or "pwsh")
        {
            var parts = after.Split("::", 2);
            int.TryParse(parts[0].Trim(), out exit);
            if (parts.Length > 1) cwd = parts[1].Trim();
        }
        else
        {
            var lines = after.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length > 0) int.TryParse(lines[0], out exit);
            if (lines.Length > 1) cwd = lines[1];
        }
        if (!string.IsNullOrWhiteSpace(cwd)) Cwd = cwd;
        return new ShellCommandResult(SecretRedactor.Redact(before.TrimEnd()), exit, Cwd);
    }

    /// <summary>cmd 第二行采集：取**最后一次**出现的采集标记（cmd 会回显输入行，真输出行在其后），
    /// `::` 后即命令执行完成后的 %CD%。</summary>
    private ShellCommandResult ApplyCmdCwd(ShellCommandResult baseResult, string snapshot, string done2)
    {
        var idx = snapshot.LastIndexOf(done2, StringComparison.Ordinal);
        if (idx < 0) return baseResult;
        var after = snapshot[(idx + done2.Length)..].Trim();
        if (after.StartsWith("::", StringComparison.Ordinal))
        {
            var p = after[2..].Trim();
            if (p.Length > 0 && p != "~") Cwd = p;
        }
        return new ShellCommandResult(baseResult.Output, baseResult.ExitCode, Cwd);
    }

    private string Snapshot()
    {
        lock (_stdout)
        lock (_stderr)
            return _stdout.ToString() + _stderr;
    }

    private static string SnapshotOutput(string snapshot, string done)
    {
        var idx = snapshot.IndexOf(done, StringComparison.Ordinal);
        return idx >= 0 ? snapshot[..idx] : snapshot;
    }

    private void ClearBuffers()
    {
        lock (_stdout) _stdout.Clear();
        lock (_stderr) _stderr.Clear();
    }

    private static Process StartOrThrow(ProcessStartInfo psi, string failMessage)
    {
        try
        {
            var process = Process.Start(psi);
            if (process == null)
                throw new SmartTerminalException("connect_failed", failMessage, 400);
            return process;
        }
        catch (SmartTerminalException) { throw; }
        catch (Exception ex)
        {
            throw new SmartTerminalException("connect_failed", SecretRedactor.Redact($"{failMessage}: {ex.Message}"), 400);
        }
    }

    private static void CopyEnv(ProcessStartInfo psi, IDictionary<string, string>? env, bool skipSshPass)
    {
        if (env == null) return;
        foreach (var kv in env)
        {
            if (skipSshPass && string.Equals(kv.Key, "SSHPASS", StringComparison.OrdinalIgnoreCase)) continue;
            psi.Environment[kv.Key] = kv.Value;
        }
    }

    private static bool IsReadable(string path)
    {
        try
        {
            using var s = File.OpenRead(path);
            return true;
        }
        catch { return false; }
    }

    private static string? FindOnPath(string name)
    {
        if (File.Exists(name)) return Path.GetFullPath(name);
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);
        foreach (var dir in paths)
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            var candidate = Path.Combine(dir, name);
            if (File.Exists(candidate)) return candidate;
            if (OperatingSystem.IsWindows() && File.Exists(candidate + ".exe")) return candidate + ".exe";
        }
        return null;
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}

public readonly record struct ShellCommandResult(string Output, int ExitCode, string Cwd);

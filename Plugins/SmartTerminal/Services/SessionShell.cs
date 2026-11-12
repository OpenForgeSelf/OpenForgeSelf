using System.Diagnostics;
using System.Text;
using ForgeSelf.Api.Plugins.SmartTerminal.Models;

namespace ForgeSelf.Api.Plugins.SmartTerminal.Services;

/// <summary>长寿命壳进程：按行投喂命令，采集输出与退出码。不以完整 PTY 为验收标准。</summary>
public sealed class SessionShell : IDisposable
{
    private readonly Process _process;
    private readonly object _gate = new();
    private readonly StringBuilder _stdout = new();
    private readonly StringBuilder _stderr = new();
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

    private SessionShell(Process process, string cwd)
    {
        _process = process;
        Cwd = cwd;
        _process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (_stdout) _stdout.AppendLine(e.Data); };
        _process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (_stderr) _stderr.AppendLine(e.Data); };
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
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
        if (!string.IsNullOrEmpty(password))
        {
            var sshpass = FindOnPath("sshpass");
            if (sshpass == null)
                throw new SmartTerminalException("connect_failed", "本机没有 sshpass，无法用交互密码建立 SSH", 400);
            psi.FileName = sshpass;
            psi.Arguments = $"-e ssh {sshArgs} {Quote(target)}";
            psi.Environment["SSHPASS"] = password;
        }
        else
        {
            var ssh = FindOnPath("ssh") ?? "ssh";
            psi.FileName = ssh;
            psi.Arguments = $"{sshArgs} {Quote(target)}";
        }

        CopyEnv(psi, env, skipSshPass: string.IsNullOrEmpty(password));
        var process = StartOrThrow(psi, "SSH 连接失败");
        // 给握手一点时间：若立刻退出则视为连接失败
        Thread.Sleep(200);
        if (process.HasExited)
            throw new SmartTerminalException("connect_failed", "SSH / 本机壳启动失败", 400);
        var session = new SessionShell(process, "~");
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
            string snapshot;
            while (DateTime.UtcNow < deadline)
            {
                snapshot = Snapshot();
                if (snapshot.Contains(done, StringComparison.Ordinal))
                    return Parse(snapshot, done, token, shellKind);
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
    }

    private void RefreshCwd(string shellKind)
    {
        try
        {
            var result = Run("pwd", shellKind, TimeSpan.FromSeconds(5));
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

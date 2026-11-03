using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using ForgeSelf.Api.Plugins.SmartTerminal.Models;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.SmartTerminal.Services;

public interface ITerminalSessionService
{
    IReadOnlyList<ShellProfileDto> ListShells();
    TerminalSessionDto CreateSession(CreateSessionRequest request);
    IReadOnlyList<TerminalSessionDto> ListSessions();
    TerminalSessionDto? GetSession(string sessionId);
    void SetPermissionMode(string sessionId, PermissionMode mode);
    PermissionMode GetGlobalPermissionMode();
    void SetGlobalPermissionMode(PermissionMode mode);
    RunCommandResponse ProposeOrRun(string sessionId, string command, bool autoApproveIfAllowed = true);
    PendingApprovalDto? GetPendingApproval(string approvalId);
    IReadOnlyList<PendingApprovalDto> ListPendingApprovals(string? sessionId = null);
    RunCommandResponse ResolveApproval(string approvalId, bool approve, string? note = null);
    OutputSnapshotDto GetOutput(string sessionId, int afterIndex = 0);
    IReadOnlyList<TerminalCommandRecordDto> ListCommands(string sessionId);
    bool CloseSession(string sessionId);
    RunCommandResponse Rerun(string sessionId, string commandId);
    IReadOnlyList<AuditEventDto> QueryAudit(string? sessionId, string? kind, string? agentId, int take, int skip);
}

public sealed class TerminalSessionService : ITerminalSessionService
{
    private readonly IShellProfileDetector _shells;
    private readonly IPermissionClassifier _classifier;
    private readonly IAuditLogStore _audit;
    private readonly ConcurrentDictionary<string, SessionState> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, PendingState> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _modeLock = new();
    private PermissionMode _globalMode = PermissionMode.AskWrite;

    public TerminalSessionService(
        IShellProfileDetector shells,
        IPermissionClassifier classifier,
        IAuditLogStore audit)
    {
        _shells = shells;
        _classifier = classifier;
        _audit = audit;
    }

    public IReadOnlyList<ShellProfileDto> ListShells() => _shells.ListProfiles();

    public PermissionMode GetGlobalPermissionMode()
    {
        lock (_modeLock) return _globalMode;
    }

    public void SetGlobalPermissionMode(PermissionMode mode)
    {
        lock (_modeLock) _globalMode = mode;
        _classifier.SetMode(mode);
        _audit.Append(new AuditEventDto
        {
            EventId = NewId("evt"),
            Time = DateTimeOffset.UtcNow,
            Kind = "permission_mode",
            Decision = mode.ToString(),
            Detail = "global"
        });
    }

    public TerminalSessionDto CreateSession(CreateSessionRequest request)
    {
        var shells = _shells.ListProfiles();
        var preferred = request.ShellId;
        var shell = !string.IsNullOrWhiteSpace(preferred)
            ? _shells.Find(preferred!)
            : shells.FirstOrDefault(s => s.Available && s.Id is "pwsh" or "powershell" or "cmd" or "bash")
              ?? shells.FirstOrDefault(s => s.Available);

        if (shell == null || !shell.Available)
        {
            throw new InvalidOperationException("没有可用的终端外壳。Windows 请确认 cmd/powershell/pwsh 在 PATH；非 Windows 请安装 bash/pwsh。");
        }

        var cwd = string.IsNullOrWhiteSpace(request.Cwd)
            ? Environment.CurrentDirectory
            : Path.GetFullPath(request.Cwd);
        if (!Directory.Exists(cwd))
        {
            throw new DirectoryNotFoundException($"工作目录不存在: {cwd}");
        }

        var mode = request.PermissionMode ?? GetGlobalPermissionMode();
        var session = new SessionState
        {
            SessionId = NewId("sess"),
            Title = string.IsNullOrWhiteSpace(request.Title) ? $"终端 · {shell.DisplayName}" : request.Title!.Trim(),
            Shell = shell,
            Cwd = cwd,
            PermissionMode = mode,
            AgentId = request.AgentId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _sessions[session.SessionId] = session;

        _audit.Append(new AuditEventDto
        {
            EventId = NewId("evt"),
            Time = DateTimeOffset.UtcNow,
            Kind = "session_create",
            SessionId = session.SessionId,
            AgentId = session.AgentId,
            ShellId = shell.Id,
            Cwd = cwd,
            Detail = session.Title
        });

        return ToDto(session);
    }

    public IReadOnlyList<TerminalSessionDto> ListSessions()
        => _sessions.Values.OrderByDescending(s => s.UpdatedAt).Select(ToDto).ToList();

    public TerminalSessionDto? GetSession(string sessionId)
        => _sessions.TryGetValue(sessionId, out var s) ? ToDto(s) : null;

    public void SetPermissionMode(string sessionId, PermissionMode mode)
    {
        var s = Require(sessionId);
        s.PermissionMode = mode;
        s.UpdatedAt = DateTimeOffset.UtcNow;
        _audit.Append(new AuditEventDto
        {
            EventId = NewId("evt"),
            Time = DateTimeOffset.UtcNow,
            Kind = "permission_mode",
            SessionId = sessionId,
            AgentId = s.AgentId,
            Decision = mode.ToString()
        });
    }

    public RunCommandResponse ProposeOrRun(string sessionId, string command, bool autoApproveIfAllowed = true)
    {
        var s = Require(sessionId);
        if (s.Closed) throw new InvalidOperationException("会话已关闭");
        if (string.IsNullOrWhiteSpace(command)) throw new ArgumentException("command 不能为空");

        var redacted = SecretRedactor.Redact(command);
        var classify = _classifier.Classify(command, s.PermissionMode);
        var cmd = new CommandState
        {
            CommandId = NewId("cmd"),
            SessionId = sessionId,
            RawCommand = command,
            CommandRedacted = redacted,
            Risk = classify.Risk,
            Decision = classify.Decision,
            MatchedRule = classify.MatchedRule,
            Status = "pending",
            CreatedAt = DateTimeOffset.UtcNow
        };
        s.Commands.Add(cmd);
        s.UpdatedAt = DateTimeOffset.UtcNow;

        _audit.Append(new AuditEventDto
        {
            EventId = NewId("evt"),
            Time = DateTimeOffset.UtcNow,
            Kind = "classify",
            SessionId = sessionId,
            AgentId = s.AgentId,
            ShellId = s.Shell.Id,
            Cwd = s.Cwd,
            CommandRedacted = redacted,
            Decision = $"{classify.Decision}/{classify.Risk}",
            MatchedRule = classify.MatchedRule,
            Detail = classify.Reason
        });

        if (classify.Decision == ApprovalDecision.Deny)
        {
            cmd.Status = "denied";
            cmd.FinishedAt = DateTimeOffset.UtcNow;
            AppendSystem(s, $"拒绝执行：{classify.Reason}");
            _audit.Append(DeniedEvent(s, cmd, "policy_deny"));
            return new RunCommandResponse
            {
                SessionId = sessionId,
                CommandId = cmd.CommandId,
                Status = cmd.Status,
                Classify = classify,
                Message = classify.Reason
            };
        }

        if (classify.Decision == ApprovalDecision.Ask && autoApproveIfAllowed)
        {
            var approvalId = NewId("appr");
            cmd.Status = "awaiting_approval";
            cmd.ApprovalId = approvalId;
            s.Status = "awaiting_approval";
            var pending = new PendingState
            {
                Approval = new PendingApprovalDto
                {
                    ApprovalId = approvalId,
                    SessionId = sessionId,
                    CommandId = cmd.CommandId,
                    CommandRedacted = redacted,
                    Risk = classify.Risk,
                    MatchedRule = classify.MatchedRule,
                    Reason = classify.Reason,
                    Mode = s.PermissionMode,
                    CreatedAt = DateTimeOffset.UtcNow
                },
                Command = cmd,
                Session = s
            };
            _pending[approvalId] = pending;
            AppendSystem(s, $"等待审批（{classify.Risk} / {classify.MatchedRule}）：{redacted}");
            _audit.Append(new AuditEventDto
            {
                EventId = NewId("evt"),
                Time = DateTimeOffset.UtcNow,
                Kind = "approval_request",
                SessionId = sessionId,
                AgentId = s.AgentId,
                ShellId = s.Shell.Id,
                Cwd = s.Cwd,
                CommandRedacted = redacted,
                Decision = "Ask",
                MatchedRule = classify.MatchedRule,
                Detail = approvalId
            });
            return new RunCommandResponse
            {
                SessionId = sessionId,
                CommandId = cmd.CommandId,
                Status = cmd.Status,
                ApprovalId = approvalId,
                Classify = classify,
                Message = "需要人工审批后才能执行"
            };
        }

        StartExecution(s, cmd);
        return new RunCommandResponse
        {
            SessionId = sessionId,
            CommandId = cmd.CommandId,
            Status = cmd.Status,
            Classify = classify,
            Message = "已开始执行"
        };
    }

    public PendingApprovalDto? GetPendingApproval(string approvalId)
        => _pending.TryGetValue(approvalId, out var p) ? p.Approval : null;

    public IReadOnlyList<PendingApprovalDto> ListPendingApprovals(string? sessionId = null)
    {
        var q = _pending.Values.Select(p => p.Approval);
        if (!string.IsNullOrWhiteSpace(sessionId))
            q = q.Where(a => string.Equals(a.SessionId, sessionId, StringComparison.OrdinalIgnoreCase));
        return q.OrderByDescending(a => a.CreatedAt).ToList();
    }

    public RunCommandResponse ResolveApproval(string approvalId, bool approve, string? note = null)
    {
        if (!_pending.TryRemove(approvalId, out var pending))
            throw new KeyNotFoundException("审批项不存在或已处理");

        var s = pending.Session;
        var cmd = pending.Command;
        _audit.Append(new AuditEventDto
        {
            EventId = NewId("evt"),
            Time = DateTimeOffset.UtcNow,
            Kind = approve ? "approval_grant" : "approval_deny",
            SessionId = s.SessionId,
            AgentId = s.AgentId,
            ShellId = s.Shell.Id,
            Cwd = s.Cwd,
            CommandRedacted = cmd.CommandRedacted,
            Decision = approve ? "Allow" : "Deny",
            MatchedRule = cmd.MatchedRule,
            Detail = note
        });

        if (!approve)
        {
            cmd.Status = "denied";
            cmd.FinishedAt = DateTimeOffset.UtcNow;
            s.Status = "idle";
            s.UpdatedAt = DateTimeOffset.UtcNow;
            AppendSystem(s, $"用户拒绝：{cmd.CommandRedacted}");
            return new RunCommandResponse
            {
                SessionId = s.SessionId,
                CommandId = cmd.CommandId,
                Status = cmd.Status,
                ApprovalId = approvalId,
                Message = "已拒绝"
            };
        }

        cmd.Decision = ApprovalDecision.Allow;
        StartExecution(s, cmd);
        return new RunCommandResponse
        {
            SessionId = s.SessionId,
            CommandId = cmd.CommandId,
            Status = cmd.Status,
            ApprovalId = approvalId,
            Message = "已批准并开始执行"
        };
    }

    public OutputSnapshotDto GetOutput(string sessionId, int afterIndex = 0)
    {
        var s = Require(sessionId);
        var chunks = s.Chunks.Where(c => c.Index >= afterIndex).ToList();
        var next = s.Chunks.Count == 0 ? 0 : s.Chunks.Max(c => c.Index) + 1;
        return new OutputSnapshotDto
        {
            SessionId = sessionId,
            ActiveCommandId = s.ActiveCommandId,
            Status = s.Status,
            Chunks = chunks,
            NextIndex = next
        };
    }

    public IReadOnlyList<TerminalCommandRecordDto> ListCommands(string sessionId)
    {
        var s = Require(sessionId);
        return s.Commands.Select(c => new TerminalCommandRecordDto
        {
            CommandId = c.CommandId,
            SessionId = c.SessionId,
            CommandRedacted = c.CommandRedacted,
            Risk = c.Risk,
            Decision = c.Decision,
            MatchedRule = c.MatchedRule,
            Status = c.Status,
            ExitCode = c.ExitCode,
            CreatedAt = c.CreatedAt,
            FinishedAt = c.FinishedAt,
            ApprovalId = c.ApprovalId
        }).Reverse().ToList();
    }

    public bool CloseSession(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var s)) return false;
        s.Closed = true;
        s.Status = "closed";
        s.UpdatedAt = DateTimeOffset.UtcNow;
        try { s.ActiveProcess?.Kill(entireProcessTree: true); } catch { /* ignore */ }
        _audit.Append(new AuditEventDto
        {
            EventId = NewId("evt"),
            Time = DateTimeOffset.UtcNow,
            Kind = "session_close",
            SessionId = sessionId,
            AgentId = s.AgentId,
            ShellId = s.Shell.Id,
            Cwd = s.Cwd
        });
        return true;
    }

    public RunCommandResponse Rerun(string sessionId, string commandId)
    {
        var s = Require(sessionId);
        var prev = s.Commands.FirstOrDefault(c => c.CommandId == commandId)
                   ?? throw new KeyNotFoundException("命令记录不存在");
        return ProposeOrRun(sessionId, prev.RawCommand);
    }

    public IReadOnlyList<AuditEventDto> QueryAudit(string? sessionId, string? kind, string? agentId, int take, int skip)
        => _audit.Query(sessionId, kind, agentId, take, skip);

    private void StartExecution(SessionState s, CommandState cmd)
    {
        cmd.Status = "running";
        s.Status = "running";
        s.ActiveCommandId = cmd.CommandId;
        s.UpdatedAt = DateTimeOffset.UtcNow;
        AppendSystem(s, $"$ {cmd.CommandRedacted}");

        _ = Task.Run(() => ExecuteAsync(s, cmd));
    }

    private async Task ExecuteAsync(SessionState s, CommandState cmd)
    {
        var sw = Stopwatch.StartNew();
        Process? process = null;
        try
        {
            var (fileName, arguments) = BuildStartInfo(s.Shell, cmd.RawCommand);
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = s.Cwd,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            s.ActiveProcess = process;

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                AppendChunk(s, "stdout", SecretRedactor.Redact(e.Data));
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                AppendChunk(s, "stderr", SecretRedactor.Redact(e.Data));
            };

            if (!process.Start())
            {
                throw new InvalidOperationException("进程启动失败");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                cmd.Status = "failed";
                cmd.ExitCode = -1;
                AppendSystem(s, "执行超时（10 分钟），已终止进程树");
            }

            if (cmd.Status != "failed")
            {
                cmd.ExitCode = process.HasExited ? process.ExitCode : -1;
                cmd.Status = cmd.ExitCode == 0 ? "completed" : "failed";
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[SmartTerminal] 执行失败: {0}", ex.Message);
            cmd.Status = "failed";
            cmd.ExitCode = -1;
            AppendSystem(s, "执行错误: " + ex.Message);
        }
        finally
        {
            sw.Stop();
            cmd.FinishedAt = DateTimeOffset.UtcNow;
            s.ActiveCommandId = null;
            s.ActiveProcess = null;
            s.Status = s.Closed ? "closed" : "idle";
            s.UpdatedAt = DateTimeOffset.UtcNow;
            _audit.Append(new AuditEventDto
            {
                EventId = NewId("evt"),
                Time = DateTimeOffset.UtcNow,
                Kind = "command_result",
                SessionId = s.SessionId,
                AgentId = s.AgentId,
                ShellId = s.Shell.Id,
                Cwd = s.Cwd,
                CommandRedacted = cmd.CommandRedacted,
                Decision = cmd.Decision.ToString(),
                MatchedRule = cmd.MatchedRule,
                ExitCode = cmd.ExitCode,
                Detail = $"status={cmd.Status}; elapsedMs={sw.ElapsedMilliseconds}"
            });
            process?.Dispose();
        }
    }

    private static (string fileName, string arguments) BuildStartInfo(ShellProfileDto shell, string command)
    {
        // 对 Windows 壳：把整段命令交给外壳；对 wt-profile：尽量用其 executable + template。
        var quoted = QuoteForShell(command, shell.Kind);
        var args = shell.ArgumentsTemplate.Replace("{command}", quoted, StringComparison.Ordinal);
        return (shell.Executable, args);
    }

    private static string QuoteForShell(string command, string kind)
    {
        if (kind is "cmd")
        {
            // cmd /c 需要整体引住含空格与特殊符的命令
            return "\"" + command.Replace("\"", "\\\"") + "\"";
        }
        // pwsh / powershell / bash：用单引号包一层并转义内部单引号
        return "'" + command.Replace("'", "''") + "'";
    }

    private void AppendSystem(SessionState s, string text) => AppendChunk(s, "system", text);

    private static void AppendChunk(SessionState s, string stream, string text)
    {
        lock (s.ChunkLock)
        {
            var idx = s.Chunks.Count;
            s.Chunks.Add(new TerminalOutputChunkDto
            {
                Index = idx,
                Stream = stream,
                Text = text,
                At = DateTimeOffset.UtcNow
            });
        }
    }

    private SessionState Require(string sessionId)
        => _sessions.TryGetValue(sessionId, out var s)
            ? s
            : throw new KeyNotFoundException($"会话不存在: {sessionId}");

    private static TerminalSessionDto ToDto(SessionState s) => new()
    {
        SessionId = s.SessionId,
        Title = s.Title,
        ShellId = s.Shell.Id,
        ShellDisplayName = s.Shell.DisplayName,
        Cwd = s.Cwd,
        PermissionMode = s.PermissionMode,
        Status = s.Status,
        AgentId = s.AgentId,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt,
        CommandCount = s.Commands.Count
    };

    private static AuditEventDto DeniedEvent(SessionState s, CommandState cmd, string detail) => new()
    {
        EventId = NewId("evt"),
        Time = DateTimeOffset.UtcNow,
        Kind = "command_result",
        SessionId = s.SessionId,
        AgentId = s.AgentId,
        ShellId = s.Shell.Id,
        Cwd = s.Cwd,
        CommandRedacted = cmd.CommandRedacted,
        Decision = "Deny",
        MatchedRule = cmd.MatchedRule,
        ExitCode = null,
        Detail = detail
    };

    private static string NewId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    private sealed class SessionState
    {
        public string SessionId { get; init; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public ShellProfileDto Shell { get; init; } = new();
        public string Cwd { get; set; } = string.Empty;
        public PermissionMode PermissionMode { get; set; } = PermissionMode.AskWrite;
        public string? AgentId { get; init; }
        public string Status { get; set; } = "idle";
        public bool Closed { get; set; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string? ActiveCommandId { get; set; }
        public Process? ActiveProcess { get; set; }
        public List<CommandState> Commands { get; } = new();
        public List<TerminalOutputChunkDto> Chunks { get; } = new();
        public object ChunkLock { get; } = new();
    }

    private sealed class CommandState
    {
        public string CommandId { get; init; } = string.Empty;
        public string SessionId { get; init; } = string.Empty;
        public string RawCommand { get; init; } = string.Empty;
        public string CommandRedacted { get; init; } = string.Empty;
        public CommandRiskClass Risk { get; set; }
        public ApprovalDecision Decision { get; set; }
        public string MatchedRule { get; set; } = string.Empty;
        public string Status { get; set; } = "pending";
        public int? ExitCode { get; set; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? FinishedAt { get; set; }
        public string? ApprovalId { get; set; }
    }

    private sealed class PendingState
    {
        public PendingApprovalDto Approval { get; init; } = new();
        public CommandState Command { get; init; } = new();
        public SessionState Session { get; init; } = new();
    }
}

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
    TerminalSessionDto RenameSession(string sessionId, string title);
    RunCommandResponse Rerun(string sessionId, string commandId);
    LaunchTerminalResponse Launch(LaunchTerminalRequest request);
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
        ShellProfileDto? shell;
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            shell = _shells.Find(preferred!);
        }
        else
        {
            var defaultId = _shells.PreferDefaultShellId();
            shell = !string.IsNullOrWhiteSpace(defaultId)
                ? _shells.Find(defaultId!)
                : shells.FirstOrDefault(s => s.Available);
        }

        if (shell == null || !shell.Available)
        {
            throw new InvalidOperationException(
                "没有可用的终端外壳。Windows 请确认 cmd/powershell/pwsh 在 PATH；Linux/macOS 请确认 bash/zsh/sh 可用。");
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
        s.SessionEnv.Clear();
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

    public TerminalSessionDto RenameSession(string sessionId, string title)
    {
        var s = Require(sessionId);
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("标题不能为空");
        var next = title.Trim();
        if (next.Length > 80) next = next[..80];
        var prev = s.Title;
        s.Title = next;
        s.UpdatedAt = DateTimeOffset.UtcNow;
        _audit.Append(new AuditEventDto
        {
            EventId = NewId("evt"),
            Time = DateTimeOffset.UtcNow,
            Kind = "session_rename",
            SessionId = sessionId,
            AgentId = s.AgentId,
            Detail = $"'{SecretRedactor.Redact(prev)}' → '{SecretRedactor.Redact(next)}'"
        });
        return ToDto(s);
    }

    public LaunchTerminalResponse Launch(LaunchTerminalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        // 密码只留在局部变量，尽快从 request 剥离，避免后续日志/序列化带出
        var password = request.Password;
        request.Password = null;
        var passwordProvided = !string.IsNullOrEmpty(password);

        var type = NormalizeLaunchType(string.IsNullOrWhiteSpace(request.Profile) ? request.Type : request.Profile);
        if (type is "ssh" or "wsl-ssh")
        {
            if (string.IsNullOrWhiteSpace(request.Host))
                throw new ArgumentException("SSH 启动需要 host");
            if (string.IsNullOrWhiteSpace(request.User))
                throw new ArgumentException("SSH 启动需要 user");
        }

        var auth = (request.Auth ?? (passwordProvided ? "sshpass" : "interactive")).Trim().ToLowerInvariant();
        if (auth is "password") auth = "sshpass";

        // S5：interactive（或缺省且无密）且未提供 password → 不创建会话，由前端先收密
        if (type is "ssh" or "wsl-ssh")
        {
            if (auth == "interactive" && !passwordProvided)
                throw new InteractivePasswordRequiredException();
            if (auth == "sshpass" && !passwordProvided)
                throw new InvalidOperationException("auth=sshpass 需要 password");
        }

        // Windows + 密码 SSH：默认走 WSL sshpass（可用 type=wsl-ssh 强制）
        var isWindows = OperatingSystem.IsWindows();
        if (type == "ssh" && passwordProvided && auth == "sshpass" && isWindows)
            type = "wsl-ssh";

        var title = string.IsNullOrWhiteSpace(request.Title)
            ? type switch
            {
                "ssh" or "wsl-ssh" => $"SSH · {request.User}@{request.Host}",
                _ => null
            }
            : request.Title;

        // SSH 启动优先 bash（Linux）或 wsl 场景下仍建一个 bash/本地壳用于后续本地命令；
        // 真正的 ssh 连接用 bootstrap 直启进程。
        var shellId = request.ShellId;
        if (type is "ssh" or "wsl-ssh")
            shellId ??= isWindows ? null : "bash";

        var sessionDto = CreateSession(new CreateSessionRequest
        {
            Title = title,
            ShellId = shellId,
            Cwd = request.Cwd,
            PermissionMode = request.PermissionMode,
            AgentId = request.AgentId
        });
        var s = Require(sessionDto.SessionId);
        s.LaunchType = type;
        s.RemoteHost = request.Host;
        s.RemoteUser = request.User;
        ApplySessionEnv(s, request.Env);

        var envAudit = SecretRedactor.RedactEnvForAudit(s.SessionEnv.Count > 0 ? s.SessionEnv : null);
        _audit.Append(new AuditEventDto
        {
            EventId = NewId("evt"),
            Time = DateTimeOffset.UtcNow,
            Kind = "launch",
            SessionId = s.SessionId,
            AgentId = s.AgentId,
            ShellId = s.Shell.Id,
            Cwd = s.Cwd,
            Detail = $"profile={(string.IsNullOrWhiteSpace(request.Profile) ? type : request.Profile)}; type={type}; auth={auth}; host={request.Host}; user={request.User}; passwordProvided={passwordProvided}"
                     + (string.IsNullOrEmpty(envAudit) ? "" : $"; env=[{envAudit}]")
        });

        var results = new List<RunCommandResponse>();
        string? warning = null;
        if (passwordProvided)
        {
            warning = "请求携带了密码（可能来自 URL）。密码仅注入进程环境 SSHPASS，不写 JSONL；地址栏应已脱敏。产品上更推荐密钥登录或一次性口令。";
            AppendSystem(s, "⚠ 启动凭证含密码：已做脱敏处理，密码不落审计原文。建议改用 SSH 密钥。");
        }

        if (type is "ssh" or "wsl-ssh")
        {
            var (bootCmd, env) = BuildSshBootstrap(type, auth, request, password);
            password = null; // 不再持有明文
            env ??= new Dictionary<string, string>(StringComparer.Ordinal);
            MergeEnvSkippingSshPass(env, request.Env);
            results.Add(RunBootstrap(s, bootCmd, env, redactedLabel: BuildSshRedactedLabel(type, auth, request)));
        }
        else
        {
            password = null;
        }

        foreach (var line in ExpandLaunchCommands(request))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            results.Add(ProposeOrRun(s.SessionId, line, autoApproveIfAllowed: true));
        }

        return new LaunchTerminalResponse
        {
            Session = ToDto(s),
            CommandResults = results,
            LaunchType = type,
            Profile = string.IsNullOrWhiteSpace(request.Profile) ? type : request.Profile,
            Message = $"已启动 {type} 会话",
            PasswordProvided = passwordProvided,
            Warning = warning,
            NeedsInteractivePassword = false
        };
    }

    private RunCommandResponse RunBootstrap(SessionState s, string command, Dictionary<string, string> ephemeralEnv, string redactedLabel)
    {
        var cmd = new CommandState
        {
            CommandId = NewId("cmd"),
            SessionId = s.SessionId,
            RawCommand = command,
            CommandRedacted = SecretRedactor.Redact(redactedLabel),
            Risk = CommandRiskClass.HighRisk,
            Decision = ApprovalDecision.Allow,
            MatchedRule = "launch:bootstrap",
            Status = "pending",
            CreatedAt = DateTimeOffset.UtcNow,
            EphemeralEnv = ephemeralEnv
        };
        s.Commands.Add(cmd);
        _audit.Append(new AuditEventDto
        {
            EventId = NewId("evt"),
            Time = DateTimeOffset.UtcNow,
            Kind = "launch_connect",
            SessionId = s.SessionId,
            AgentId = s.AgentId,
            ShellId = s.Shell.Id,
            Cwd = s.Cwd,
            CommandRedacted = cmd.CommandRedacted,
            Decision = "Allow",
            MatchedRule = "launch:bootstrap",
            Detail = "URL/API 启动引导（密码仅环境变量）"
        });
        StartExecution(s, cmd);
        return new RunCommandResponse
        {
            SessionId = s.SessionId,
            CommandId = cmd.CommandId,
            Status = cmd.Status,
            Message = "SSH 引导已开始"
        };
    }

    private static string NormalizeLaunchType(string? type)
    {
        var t = (type ?? "local").Trim().ToLowerInvariant();
        if (t.StartsWith("custom:", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "本里程碑不支持 custom profile（custom:<id>）；仅支持 local / ssh / wsl-ssh。请改用已实现的 profile，勿静默降级为 local。");
        }
        return t switch
        {
            "local" or "shell" or "bash" => "local",
            "ssh" => "ssh",
            "wsl-ssh" or "wslssh" or "wsl_ssh" => "wsl-ssh",
            _ => t
        };
    }

    /// <summary>
    /// Steps 优先：有结构化 Steps 时只展开 Steps（前端常同时带已扁平的 commands，避免重复执行）；
    /// 无 Steps 时回退扁平 Commands。
    /// </summary>
    private static List<string> ExpandLaunchCommands(LaunchTerminalRequest request)
    {
        var lines = new List<string>();
        if (request.Steps is { Count: > 0 })
        {
            foreach (var step in request.Steps)
            {
                var flat = FlattenLaunchStep(step);
                if (!string.IsNullOrWhiteSpace(flat))
                    lines.Add(flat);
            }
            return lines;
        }
        if (request.Commands != null)
        {
            foreach (var c in request.Commands)
            {
                if (string.IsNullOrWhiteSpace(c)) continue;
                lines.Add(c);
            }
        }
        return lines;
    }

    private static string FlattenLaunchStep(LaunchStep? step)
    {
        if (step == null) return string.Empty;
        var cmd = (step.Command ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(cmd)) return string.Empty;
        if (step.Args == null || step.Args.Count == 0) return cmd;
        var parts = new List<string> { cmd };
        foreach (var a in step.Args)
        {
            if (a == null) continue;
            parts.Add(QuoteArgPosix(a));
        }
        return string.Join(' ', parts);
    }

    private static string QuoteArgPosix(string arg)
    {
        // 无空白与危险元字符时可裸传；否则单引号包裹
        if (arg.Length > 0
            && !arg.Any(c => char.IsWhiteSpace(c) || ";&|$`\"'<>()".Contains(c)))
            return arg;
        return "'" + arg.Replace("'", "'\''") + "'";
    }

    private static void ApplySessionEnv(SessionState s, Dictionary<string, string>? env)
    {
        s.SessionEnv.Clear();
        if (env == null) return;
        foreach (var kv in env)
        {
            if (string.IsNullOrEmpty(kv.Key)) continue;
            if (string.Equals(kv.Key, "SSHPASS", StringComparison.OrdinalIgnoreCase)) continue;
            s.SessionEnv[kv.Key] = kv.Value ?? "";
        }
    }

    private static void MergeEnvSkippingSshPass(Dictionary<string, string> target, Dictionary<string, string>? env)
    {
        if (env == null) return;
        foreach (var kv in env)
        {
            if (string.IsNullOrEmpty(kv.Key)) continue;
            if (string.Equals(kv.Key, "SSHPASS", StringComparison.OrdinalIgnoreCase)) continue;
            target[kv.Key] = kv.Value ?? "";
        }
    }

    private static string BuildSshRedactedLabel(string type, string auth, LaunchTerminalRequest request)
    {
        var port = request.Port is > 0 and not 22 ? $" -p {request.Port}" : "";
        var key = auth == "key" && !string.IsNullOrWhiteSpace(request.IdentityFile)
            ? $" -i {request.IdentityFile}"
            : "";
        var prefix = type == "wsl-ssh" ? "wsl sshpass -e ssh" : auth == "sshpass" ? "sshpass -e ssh" : "ssh";
        if (auth == "key") prefix = type == "wsl-ssh" ? "wsl ssh" : "ssh";
        return $"{prefix}{port}{key} {request.User}@***HOST***";
    }

    private static (string command, Dictionary<string, string>? env) BuildSshBootstrap(
        string type, string auth, LaunchTerminalRequest request, string? password)
    {
        var port = request.Port is > 0 ? request.Port.Value : 22;
        var host = request.Host!.Trim();
        var user = request.User!.Trim();
        var accept = request.AcceptNewHostKey ? "accept-new" : "ask";
        var sshOpts = $"-o StrictHostKeyChecking={accept} -o NumberOfPasswordPrompts=1 -p {port}";
        if (auth == "key" && !string.IsNullOrWhiteSpace(request.IdentityFile))
            sshOpts += $" -i {EscapePosix(request.IdentityFile!)}";

        var remote = $"{EscapePosix(user)}@{EscapePosix(host)}";
        var env = new Dictionary<string, string>(StringComparer.Ordinal);

        if (type == "wsl-ssh")
        {
            // Windows：wsl.exe + sshpass -e；通过 WSLENV 传入 SSHPASS
            if (auth == "sshpass")
            {
                if (string.IsNullOrEmpty(password))
                    throw new InvalidOperationException("auth=sshpass 需要 password");
                env["SSHPASS"] = password;
                env["WSLENV"] = AppendWslEnv(Environment.GetEnvironmentVariable("WSLENV"), "SSHPASS/w");
                // 整段交给本机默认壳执行（cmd/pwsh/bash）；命令本身不含密码
                return ($"wsl.exe -e sshpass -e ssh {sshOpts} {remote}", env);
            }
            return ($"wsl.exe -e ssh {sshOpts} {remote}", null);
        }

        // Linux/macOS 原生
        if (auth == "sshpass")
        {
            if (string.IsNullOrEmpty(password))
                throw new InvalidOperationException("auth=sshpass 需要 password");
            env["SSHPASS"] = password;
            return ($"sshpass -e ssh {sshOpts} {remote}", env);
        }

        return ($"ssh {sshOpts} {remote}", null);
    }

    private static string AppendWslEnv(string? existing, string flag)
    {
        if (string.IsNullOrWhiteSpace(existing)) return flag;
        if (existing.Split(':').Any(p => p.StartsWith("SSHPASS", StringComparison.OrdinalIgnoreCase)))
            return existing!;
        return existing!.TrimEnd(':') + ":" + flag;
    }

    private static string EscapePosix(string value)
    {
        // 仅用于拼进已由壳二次引用的引导命令；去掉危险元字符
        if (value.Any(c => char.IsWhiteSpace(c) || ";&|$`\\\"'<>".Contains(c)))
            throw new ArgumentException($"SSH 参数含非法字符: {SecretRedactor.Redact(value)}");
        return value;
    }

    public RunCommandResponse Rerun(string sessionId, string commandId)
    {
        var s = Require(sessionId);
        var prev = s.Commands.FirstOrDefault(c => c.CommandId == commandId)
                   ?? throw new KeyNotFoundException("命令记录不存在");
        // 重跑不携带历史短暂环境（密码不二次落盘）
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
            // 会话级 env（local/后续命令）；禁止用调用方覆盖已有 SSHPASS（仅 ephemeral 可设）
            foreach (var kv in s.SessionEnv)
            {
                if (string.Equals(kv.Key, "SSHPASS", StringComparison.OrdinalIgnoreCase)) continue;
                psi.Environment[kv.Key] = kv.Value;
            }
            if (cmd.EphemeralEnv != null)
            {
                foreach (var kv in cmd.EphemeralEnv)
                    psi.Environment[kv.Key] = kv.Value;
            }

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
            // 短暂凭证用后即焚：不在会话状态中保留密码
            cmd.EphemeralEnv?.Clear();
            cmd.EphemeralEnv = null;
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

        if (kind is "bash" or "zsh" or "sh" or "fish" or "other")
        {
            // POSIX：单引号包裹；内部单引号用 '\'' 跳出
            return "'" + command.Replace("'", "'\\''") + "'";
        }

        // pwsh / powershell / wt-profile：PowerShell 单引号转义为 ''
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

    private static TerminalSessionDto ToDto(SessionState s)
    {
        var processAlive = false;
        try
        {
            var p = s.ActiveProcess;
            processAlive = p != null && !p.HasExited;
        }
        catch
        {
            processAlive = false;
        }

        var reconnectable = !s.Closed;
        string? hint;
        if (!reconnectable)
            hint = "会话已关闭，请重新 launch / 新建。";
        else if (processAlive)
            hint = "会话可重连拉 output；活动进程仍在。";
        else
            hint = "会话对象仍在，可继续拉 output；活动进程可能已退出。缺密 SSH 需重新走交互密码后再 launch。";

        return new TerminalSessionDto
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
            CommandCount = s.Commands.Count,
            LaunchType = s.LaunchType,
            RemoteHost = s.RemoteHost,
            RemoteUser = s.RemoteUser,
            ProcessAlive = processAlive,
            Reconnectable = reconnectable,
            ReconnectHint = hint
        };
    }

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
        public string LaunchType { get; set; } = "local";
        public string? RemoteHost { get; set; }
        public string? RemoteUser { get; set; }
        /// <summary>注入后续命令进程的会话环境（不含 SSHPASS / 密码）。</summary>
        public Dictionary<string, string> SessionEnv { get; } = new(StringComparer.Ordinal);
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
        /// <summary>仅执行期注入的环境变量（如 SSHPASS）；结束后清空，不进审计。</summary>
        public Dictionary<string, string>? EphemeralEnv { get; set; }
    }

    private sealed class PendingState
    {
        public PendingApprovalDto Approval { get; init; } = new();
        public CommandState Command { get; init; } = new();
        public SessionState Session { get; init; } = new();
    }
}

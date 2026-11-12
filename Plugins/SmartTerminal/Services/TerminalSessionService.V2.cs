using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.SmartTerminal.Models;
using ApprovalDto = ForgeSelf.Api.Plugins.SmartTerminal.Models.ApprovalDto;

namespace ForgeSelf.Api.Plugins.SmartTerminal.Services;

public interface ISmartTerminalV2
{
    MetaDto GetMeta();
    SessionDto OpenSession(CreateSessionRequest request, string source, bool isAgent);
    IReadOnlyList<SessionDto> ListOpenSessions();
    SessionDto GetOpenSession(string id);
    SessionDto Disconnect(string id);
    SessionDto Reconnect(string id, ReconnectRequest? request, string source, bool isAgent);
    SessionDto CloseV2(string id);
    SessionDto PatchTitle(string id, string? title);
    void SetSessionPermissionMode(string id, PermissionMode mode);
    SubmitCommandResponse SubmitCommand(string sessionId, SubmitCommandRequest request, string source);
    BlockListDto ListBlocks(string sessionId, int afterSeq);
    IReadOnlyList<BlockDto> ListCommandHistory(string sessionId);
    IReadOnlyList<ApprovalDto> ListApprovalsV2(string? sessionId, string? status);
    SubmitCommandResponse ResolveApprovalV2(string approvalId, string decision, string source, bool isAgent);
    ClassifyPreviewResponse PreviewClassify(string command, PermissionMode? mode);
    IReadOnlyList<ProfileDto> ListProfiles(string? kind, string? q);
    ProfileDto GetProfile(string id);
    ProfileDto CreateProfile(UpsertProfileRequest request, string source);
    ProfileDto UpdateProfile(string id, UpsertProfileRequest request);
    bool DeleteProfile(string id, bool deleteSecret);
    IReadOnlyList<SecretDto> ListSecrets();
    SecretDto CreateSecret(CreateSecretRequest request);
    bool DeleteSecret(string id);
    IReadOnlyList<SnippetDto> ListSnippets();
    SnippetDto CreateSnippet(UpsertSnippetRequest request);
    SnippetDto UpdateSnippet(string id, UpsertSnippetRequest request);
    bool DeleteSnippet(string id);
    ChatThreadDto GetChat(string sessionId);
    Task<ChatMessageDto> SendChatAsync(string sessionId, SendChatRequest request, CancellationToken ct);
    AuditPageDto QueryAuditV2(string? sessionId, string? kind, string? q, int page, int pageSize);
}

public sealed partial class TerminalSessionService
{
    private readonly ConcurrentDictionary<string, V2Approval> _v2Approvals = new(StringComparer.OrdinalIgnoreCase);

    public MetaDto GetMeta() => new()
    {
        PluginId = "smart-terminal",
        Version = "2.0.0",
        PermissionDefault = "AskWrite",
        Features = new MetaFeaturesDto { Split = false, Sftp = false, PtyAcceptance = false }
    };

    public SessionDto OpenSession(CreateSessionRequest request, string source, bool isAgent)
    {
        ArgumentNullException.ThrowIfNull(request);
        var password = request.Password;
        request.Password = null;
        if (isAgent) password = null; // Agent 不得携带密码；需交互密时走 409
        source = string.IsNullOrWhiteSpace(source) ? "ui" : source;

        ProfileDto? profile = null;
        if (!string.IsNullOrWhiteSpace(request.ProfileId))
        {
            profile = _catalog.GetProfile(request.ProfileId!)
                      ?? throw SmartTerminalException.NotFound("配置不存在");
        }

        var kind = NormalizeKind(request.Kind ?? profile?.Kind ?? "local");
        var host = profile?.Host ?? request.Host;
        var port = profile?.Port ?? request.Port ?? 22;
        var user = profile?.User ?? request.User;
        var secretId = profile?.SecretId ?? request.SecretId;
        var identityFile = FirstNonEmpty(request.IdentityFile, profile?.IdentityFile);
        var auth = NormalizeAuth(request.Auth ?? profile?.Auth, secretId, identityFile, password);
        var cwdHint = FirstNonEmpty(request.Cwd, profile?.Cwd);
        var shellId = FirstNonEmpty(request.ShellId, profile?.ShellId);

        if (kind == "ssh")
        {
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user))
                throw SmartTerminalException.Invalid("SSH 需要 host 和 user");
            if (auth == "key")
            {
                if (string.IsNullOrWhiteSpace(secretId) && string.IsNullOrWhiteSpace(identityFile))
                    throw new SmartTerminalException("credential_required", "新建 SSH 选私钥须指定已有凭据句柄或本机 identityFile", 400);
            }
            else
            {
                if (string.IsNullOrEmpty(password))
                    throw SmartTerminalException.PasswordRequired();
            }
        }

        var shell = ResolveShell(shellId, kind);
        var session = new SessionState
        {
            SessionId = PublicId("s"),
            Title = FirstNonEmpty(request.Title, kind == "ssh" ? $"{user}@{host}" : $"{shell.DisplayName} · local")!,
            Shell = shell,
            Cwd = kind == "local" ? ResolveLocalCwd(cwdHint) : (cwdHint ?? "~"),
            PermissionMode = request.PermissionMode ?? GetGlobalPermissionMode(),
            AgentId = request.AgentId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Status = "connecting",
            LaunchType = kind,
            Kind = kind,
            RemoteHost = host,
            RemoteUser = user,
            Port = kind == "ssh" ? port : null,
            Auth = kind == "ssh" ? auth : null,
            PasswordProvided = !string.IsNullOrEmpty(password),
            Source = source,
            ProfileId = profile?.Id,
            SecretId = secretId,
            IdentityFile = identityFile
        };

        try
        {
            session.LiveShell = kind == "ssh"
                ? StartSshShell(session, password)
                : SessionShell.StartLocal(shell, session.Cwd, session.SessionEnv.Count > 0 ? session.SessionEnv : null);
            if (session.LiveShell != null && !string.IsNullOrWhiteSpace(session.LiveShell.Cwd))
                session.Cwd = session.LiveShell.Cwd;
        }
        catch
        {
            password = null;
            DeleteTempKey(session);
            throw;
        }
        finally
        {
            password = null;
        }

        session.PasswordProvided = false; // 用完即焚，响应不得带回
        session.Status = "connected";
        session.Prompt = BuildPrompt(session);
        session.UpdatedAt = DateTimeOffset.UtcNow;
        AppendBlock(session, "system", "connected", "已连接", 0, "done", "system");
        _sessions[session.SessionId] = session;
        AuditV2("session_open", session, null, $"{kind};auth={auth}", source);
        return ToSessionDto(session, includeBlocks: true);
    }

    public IReadOnlyList<SessionDto> ListOpenSessions()
        => _sessions.Values.Where(s => !s.Closed).OrderByDescending(s => s.UpdatedAt).Select(s => ToSessionDto(s)).ToList();

    public SessionDto GetOpenSession(string id)
        => ToSessionDto(RequireOpen(id), includeBlocks: false);

    public SessionDto Disconnect(string id)
    {
        var s = RequireOpen(id);
        TearDownShell(s);
        s.Status = "disconnected";
        s.UpdatedAt = DateTimeOffset.UtcNow;
        s.Prompt = BuildPrompt(s);
        AppendBlock(s, "system", "disconnect", "已断开", 0, "done", "system");
        AuditV2("session_disconnect", s, null, null, s.Source);
        return ToSessionDto(s);
    }

    public SessionDto Reconnect(string id, ReconnectRequest? request, string source, bool isAgent)
    {
        var s = RequireOpen(id);
        var password = isAgent ? null : request?.Password;
        if (s.LiveShell is { IsAlive: true })
            return ToSessionDto(s);

        if (s.Kind == "ssh")
        {
            var auth = NormalizeAuth(s.Auth, s.SecretId, s.IdentityFile, password);
            if (auth == "key")
            {
                if (string.IsNullOrWhiteSpace(s.SecretId) && string.IsNullOrWhiteSpace(s.IdentityFile))
                    throw new SmartTerminalException("credential_required", "重连需要凭据句柄或本机 identityFile", 400);
            }
            else if (string.IsNullOrEmpty(password))
            {
                throw SmartTerminalException.PasswordRequired("SSH 重连需要密码");
            }
        }

        try
        {
            s.LiveShell = s.Kind == "ssh"
                ? StartSshShell(s, password)
                : SessionShell.StartLocal(s.Shell, s.Cwd, s.SessionEnv.Count > 0 ? s.SessionEnv : null);
            if (s.LiveShell != null && !string.IsNullOrWhiteSpace(s.LiveShell.Cwd))
                s.Cwd = s.LiveShell.Cwd;
        }
        finally
        {
            password = null;
        }

        s.Status = "connected";
        s.PasswordProvided = false;
        s.UpdatedAt = DateTimeOffset.UtcNow;
        s.Prompt = BuildPrompt(s);
        AppendBlock(s, "system", "reconnect", "已重新连接", 0, "done", "system");
        AuditV2("session_reconnect", s, null, null, source);
        return ToSessionDto(s);
    }

    public SessionDto CloseV2(string id)
    {
        if (!_sessions.TryGetValue(id, out var s))
            throw SmartTerminalException.NotFound("会话不存在");
        if (!s.Closed)
            CloseSession(id);
        return ToSessionDto(s);
    }

    public SessionDto PatchTitle(string id, string? title)
    {
        RequireOpen(id);
        return ToSessionDto(Require(RenameSession(id, title ?? string.Empty).SessionId));
    }

    public void SetSessionPermissionMode(string id, PermissionMode mode) => SetPermissionMode(id, mode);

    public SubmitCommandResponse SubmitCommand(string sessionId, SubmitCommandRequest request, string source)
    {
        var s = RequireOpen(sessionId);
        if (s.Status != "connected" || s.LiveShell is not { IsAlive: true })
            throw SmartTerminalException.Conflict("session_disconnected", "会话未连接");
        var command = request.Command ?? string.Empty;
        if (string.IsNullOrWhiteSpace(command))
            throw SmartTerminalException.Invalid("command 不能为空");

        lock (s.CommandLock)
        {
            if (s.CommandBusy || s.Blocks.Any(b => b.Status == "running"))
                throw SmartTerminalException.Conflict("command_busy", "当前会话已有命令在执行");
        }

        var clientSource = source == "agent"
            ? "agent"
            : NormalizeCommandSource(request.Source, source);
        var classify = _classifier.Classify(command, s.PermissionMode);
        var risk = StRiskNames.Risk(classify.Risk);
        var decision = StRiskNames.Decision(classify.Decision);
        var redacted = SecretRedactor.Redact(command);

        if (classify.Decision == ApprovalDecision.Deny)
        {
            var block = AppendBlock(s, "command", redacted, classify.Reason, null, "rejected", clientSource, command);
            AuditV2("command_deny", s, redacted, classify.Reason, clientSource);
            throw new SmartTerminalException("rejected_hard", classify.Reason, 400, new Dictionary<string, object?> { ["risk"] = risk, ["block"] = ToBlockDto(block) });
        }

        if (classify.Decision == ApprovalDecision.Ask)
        {
            var block = AppendBlock(s, "command", redacted, "", null, "awaiting_approval", clientSource, command);
            var approvalId = PublicId("ap");
            var now = DateTimeOffset.UtcNow;
            var approval = new ApprovalDto
            {
                Id = approvalId,
                SessionId = s.SessionId,
                Command = redacted,
                Risk = risk,
                Mode = StRiskNames.Mode(s.PermissionMode),
                Status = "pending",
                Source = clientSource,
                CreatedAt = now,
                ExpiresAt = now.AddMinutes(30)
            };
            _v2Approvals[approvalId] = new V2Approval
            {
                Dto = approval,
                Session = s,
                Block = block,
                RawCommand = command,
                ExpiresAt = approval.ExpiresAt!.Value
            };
            AuditV2("approval_ask", s, redacted, classify.MatchedRule, clientSource);
            return new SubmitCommandResponse
            {
                Status = "awaiting_approval",
                Decision = decision,
                Risk = risk,
                Block = ToBlockDto(block),
                Approval = approval
            };
        }

        var running = AppendBlock(s, "command", redacted, "", null, "running", clientSource, command);
        s.CommandBusy = true;
        s.Status = "connected";
        AuditV2("command_run", s, redacted, classify.MatchedRule, clientSource);
        StartBlockExecution(s, running);
        return new SubmitCommandResponse
        {
            Status = running.Status == "done" ? "done" : "running",
            Decision = decision,
            Risk = risk,
            Block = ToBlockDto(running)
        };
    }

    public BlockListDto ListBlocks(string sessionId, int afterSeq)
    {
        var s = RequireOpen(sessionId);
        var items = s.Blocks.Where(b => b.Seq > afterSeq).OrderBy(b => b.Seq).Select(ToBlockDto).ToList();
        return new BlockListDto { Prompt = s.Prompt, Status = SessionStatus(s), Items = items };
    }

    public IReadOnlyList<BlockDto> ListCommandHistory(string sessionId)
        => RequireOpen(sessionId).Blocks.Where(b => b.Kind == "command").OrderBy(b => b.Seq).Select(ToBlockDto).ToList();

    public IReadOnlyList<ApprovalDto> ListApprovalsV2(string? sessionId, string? status)
    {
        ExpireApprovals();
        IEnumerable<V2Approval> rows = _v2Approvals.Values;
        if (!string.IsNullOrWhiteSpace(sessionId))
            rows = rows.Where(a => a.Session.SessionId == sessionId);
        if (!string.IsNullOrWhiteSpace(status))
            rows = rows.Where(a => string.Equals(a.Dto.Status, status, StringComparison.OrdinalIgnoreCase));
        return rows.OrderByDescending(a => a.Dto.CreatedAt).Select(a => a.Dto).ToList();
    }

    public SubmitCommandResponse ResolveApprovalV2(string approvalId, string decision, string source, bool isAgent)
    {
        if (isAgent)
            throw SmartTerminalException.Forbidden("Agent 不得自行批准或拒绝审批");
        ExpireApprovals();
        if (!_v2Approvals.TryRemove(approvalId, out var pending))
            throw SmartTerminalException.NotFound("审批项不存在或已处理");
        if (DateTimeOffset.UtcNow > pending.ExpiresAt)
        {
            pending.Block.Status = "expired";
            pending.Block.EndedAt = DateTimeOffset.UtcNow;
            throw SmartTerminalException.Conflict("approval_expired", "审批已过期，请重新提交命令");
        }

        var approve = decision.Equals("approve", StringComparison.OrdinalIgnoreCase)
                      || decision.Equals("allow", StringComparison.OrdinalIgnoreCase);
        var reject = decision.Equals("reject", StringComparison.OrdinalIgnoreCase)
                     || decision.Equals("deny", StringComparison.OrdinalIgnoreCase);
        if (!approve && !reject)
            throw SmartTerminalException.Invalid("decision 必须是 approve 或 reject");

        var s = pending.Session;
        var block = pending.Block;
        // 批准不得改写命令
        if (!approve)
        {
            block.Status = "rejected";
            block.EndedAt = DateTimeOffset.UtcNow;
            block.Output = "用户已拒绝";
            s.UpdatedAt = DateTimeOffset.UtcNow;
            AuditV2("approval_deny", s, block.Command, null, source);
            return new SubmitCommandResponse
            {
                Status = "rejected",
                Decision = "deny",
                Risk = pending.Dto.Risk,
                Block = ToBlockDto(block),
                Approval = pending.Dto
            };
        }

        block.Status = "running";
        block.Output = "";
        s.CommandBusy = true;
        AuditV2("approval_grant", s, block.Command, null, source);
        StartBlockExecution(s, block);
        return new SubmitCommandResponse
        {
            Status = block.Status == "done" ? "done" : "running",
            Decision = "allow",
            Risk = pending.Dto.Risk,
            Block = ToBlockDto(block)
        };
    }

    public ClassifyPreviewResponse PreviewClassify(string command, PermissionMode? mode)
    {
        if (string.IsNullOrWhiteSpace(command))
            throw SmartTerminalException.Invalid("command 不能为空");
        var result = _classifier.Classify(command, mode ?? GetGlobalPermissionMode());
        return new ClassifyPreviewResponse
        {
            Risk = StRiskNames.Risk(result.Risk),
            Decision = StRiskNames.Decision(result.Decision),
            Reason = result.Reason,
            Mode = StRiskNames.Mode(mode ?? GetGlobalPermissionMode())
        };
    }

    public IReadOnlyList<ProfileDto> ListProfiles(string? kind, string? q) => _catalog.ListProfiles(kind, q);
    public ProfileDto GetProfile(string id) => _catalog.GetProfile(id) ?? throw SmartTerminalException.NotFound("配置不存在");

    public ProfileDto CreateProfile(UpsertProfileRequest request, string source)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!string.IsNullOrEmpty(request.Password))
            throw new SmartTerminalException("password_not_persistable", "密码不得写入已保存配置", 400);
        var kind = NormalizeKind(request.Kind ?? "local");
        request.Kind = kind;
        if (kind == "ssh")
        {
            if (string.IsNullOrWhiteSpace(request.Host) || string.IsNullOrWhiteSpace(request.User))
                throw SmartTerminalException.Invalid("SSH 配置需要 host 和 user");
            var auth = NormalizeAuth(request.Auth, request.SecretId, request.IdentityFile, password: null);
            request.Auth = auth;
            if (auth == "key" && string.IsNullOrWhiteSpace(request.SecretId) && string.IsNullOrWhiteSpace(request.IdentityFile))
                throw new SmartTerminalException("credential_required", "新建 SSH 选私钥须指定已有凭据句柄或本机 identityFile", 400);
        }
        if (string.IsNullOrWhiteSpace(request.Name))
            request.Name = kind == "ssh" ? $"{request.User}@{request.Host}" : "本机";
        return _catalog.CreateProfile(request, source);
    }

    public ProfileDto UpdateProfile(string id, UpsertProfileRequest request)
    {
        if (!string.IsNullOrEmpty(request.Password))
            throw new SmartTerminalException("password_not_persistable", "密码不得写入已保存配置", 400);
        return _catalog.UpdateProfile(id, request);
    }

    public bool DeleteProfile(string id, bool deleteSecret) => _catalog.DeleteProfile(id, deleteSecret);

    public IReadOnlyList<SecretDto> ListSecrets() => _catalog.ListSecrets();

    public SecretDto CreateSecret(CreateSecretRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Material))
            throw SmartTerminalException.Invalid("material 不能为空");
        var secrets = _host?.TrySecrets() ?? throw SmartTerminalException.HostUnavailable("宿主凭据加密模块不可用");
        string cipher;
        try { cipher = secrets.Encrypt(request.Material); }
        catch (Exception ex) { throw SmartTerminalException.HostUnavailable("加密失败: " + ex.Message); }
        var fingerprint = Fingerprint(request.Material);
        var kind = string.IsNullOrWhiteSpace(request.Kind) ? "ssh-private-key" : request.Kind!.Trim();
        return _catalog.CreateSecret(kind, request.Label, cipher, fingerprint);
    }

    public bool DeleteSecret(string id) => _catalog.DeleteSecret(id);

    public IReadOnlyList<SnippetDto> ListSnippets() => _catalog.ListSnippets();
    public SnippetDto CreateSnippet(UpsertSnippetRequest request) => _catalog.CreateSnippet(request);
    public SnippetDto UpdateSnippet(string id, UpsertSnippetRequest request) => _catalog.UpdateSnippet(id, request);
    public bool DeleteSnippet(string id) => _catalog.DeleteSnippet(id);

    public ChatThreadDto GetChat(string sessionId)
        => new() { Messages = RequireOpen(sessionId).Chat.ToList() };

    public async Task<ChatMessageDto> SendChatAsync(string sessionId, SendChatRequest request, CancellationToken ct)
    {
        var s = RequireOpen(sessionId);
        if (string.IsNullOrWhiteSpace(request.Content))
            throw SmartTerminalException.Invalid("content 不能为空");
        var chat = _host?.TryChat() ?? throw SmartTerminalException.HostUnavailable("宿主对话模块不可用，会话不受影响");
        var user = new ChatMessageDto
        {
            Id = PublicId("m"),
            SessionId = s.SessionId,
            Role = "user",
            Content = request.Content.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        s.Chat.Add(user);

        ChatCompletionResult result;
        try
        {
            result = await chat.CompleteAsync(new ChatCompletionRequest
            {
                SessionId = "st-chat-" + s.SessionId,
                Message = BuildChatPrompt(s, request.Content)
            }, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw SmartTerminalException.HostUnavailable("助手调用失败: " + SecretRedactor.Redact(ex.Message));
        }

        if (!result.Success)
            throw SmartTerminalException.HostUnavailable(result.Error ?? "助手调用失败");

        var content = SecretRedactor.Redact(result.Content ?? string.Empty);
        var assistant = new ChatMessageDto
        {
            Id = PublicId("m"),
            SessionId = s.SessionId,
            Role = "assistant",
            Content = content,
            SuggestedCommand = ExtractSuggestedCommand(content),
            CreatedAt = DateTimeOffset.UtcNow
        };
        s.Chat.Add(assistant);
        s.UpdatedAt = DateTimeOffset.UtcNow;
        AuditV2("chat", s, assistant.SuggestedCommand, null, "ui");
        return assistant;
    }

    public AuditPageDto QueryAuditV2(string? sessionId, string? kind, string? q, int page, int pageSize)
        => _catalog.QueryAudit(sessionId, kind, q, page <= 0 ? 1 : page, pageSize <= 0 ? 50 : pageSize);

    private static void DeleteTempKey(SessionState s)
    {
        var path = s.TempKeyFile;
        s.TempKeyFile = null;
        if (string.IsNullOrWhiteSpace(path)) return;
        try { File.Delete(path); } catch { /* ignore */ }
    }

    private SessionShell StartSshShell(SessionState s, string? password)
    {
        var identity = s.IdentityFile;
        if (!string.IsNullOrWhiteSpace(s.SecretId) && string.IsNullOrWhiteSpace(identity))
        {
            var secrets = _host?.TrySecrets() ?? throw SmartTerminalException.HostUnavailable("宿主凭据加密模块不可用");
            var material = _catalog.GetSecretMaterial(s.SecretId!);
            if (material == null)
                throw new SmartTerminalException("credential_unavailable", "凭据句柄不存在", 400);
            if (!secrets.TryDecrypt(material.Cipher, out var plain) || string.IsNullOrEmpty(plain))
                throw new SmartTerminalException("credential_unavailable", "凭据无法解密", 400);
            identity = WriteTempKey(plain);
            s.TempKeyFile = identity;
        }

        return SessionShell.StartSsh(s.RemoteHost!, s.Port ?? 22, s.RemoteUser!, identity, password, s.SessionEnv.Count > 0 ? s.SessionEnv : null);
    }

    private static string WriteTempKey(string material)
    {
        var path = Path.Combine(Path.GetTempPath(), "st-key-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(path, material);
        if (!OperatingSystem.IsWindows())
        {
            try { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
            catch { /* best-effort */ }
        }
        return path;
    }

    private ShellProfileDto ResolveShell(string? shellId, string kind)
    {
        ShellProfileDto? shell = null;
        if (!string.IsNullOrWhiteSpace(shellId))
            shell = _shells.Find(shellId!);
        if (shell == null)
        {
            var def = _shells.PreferDefaultShellId();
            shell = !string.IsNullOrWhiteSpace(def) ? _shells.Find(def!) : _shells.ListProfiles().FirstOrDefault(x => x.Available);
        }
        if (shell == null || !shell.Available)
            throw new SmartTerminalException("shell_unavailable", "没有可用的终端外壳", 400);
        return shell;
    }

    private SessionState RequireOpen(string id)
    {
        if (!_sessions.TryGetValue(id, out var s) || s.Closed)
            throw SmartTerminalException.NotFound("会话不存在");
        return s;
    }

    private void StartBlockExecution(SessionState s, BlockState block)
    {
        // 短命令同步跑完，方便单测；超时仍标 running 由轮询取结果会过于复杂，因此一律同步等待壳返回。
        try
        {
            if (s.LiveShell is not { IsAlive: true })
                throw SmartTerminalException.Conflict("session_disconnected", "会话未连接");
            block.StartedAt = DateTimeOffset.UtcNow;
            var result = s.LiveShell.Run(block.RawCommand ?? block.Command ?? "", s.Shell.Kind);
            block.Output = SecretRedactor.Redact(result.Output);
            block.ExitCode = result.ExitCode;
            block.Status = "done";
            block.EndedAt = DateTimeOffset.UtcNow;
            if (!string.IsNullOrWhiteSpace(result.Cwd))
                s.Cwd = result.Cwd;
            s.Prompt = BuildPrompt(s);
        }
        catch (SmartTerminalException ex)
        {
            block.Output = SecretRedactor.Redact(ex.Message);
            block.Status = "error";
            block.ExitCode = 1;
            block.EndedAt = DateTimeOffset.UtcNow;
            if (ex.Code == "session_disconnected")
                s.Status = "disconnected";
        }
        catch (Exception ex)
        {
            block.Output = SecretRedactor.Redact(ex.Message);
            block.Status = "error";
            block.ExitCode = 1;
            block.EndedAt = DateTimeOffset.UtcNow;
        }
        finally
        {
            s.CommandBusy = false;
            s.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private BlockState AppendBlock(SessionState s, string kind, string? command, string output, int? exit, string status, string source, string? raw = null)
    {
        var block = new BlockState
        {
            Id = PublicId("b"),
            Seq = s.NextSeq++,
            Kind = kind,
            Command = command,
            RawCommand = raw,
            Output = output ?? "",
            ExitCode = exit,
            Status = status,
            StartedAt = DateTimeOffset.UtcNow,
            EndedAt = status is "done" or "rejected" or "error" ? DateTimeOffset.UtcNow : null,
            Source = source
        };
        s.Blocks.Add(block);
        s.UpdatedAt = DateTimeOffset.UtcNow;
        s.Prompt = BuildPrompt(s);
        return block;
    }

    private static BlockDto ToBlockDto(BlockState b) => new()
    {
        Id = b.Id,
        Seq = b.Seq,
        Kind = b.Kind,
        Command = b.Command,
        Output = b.Output,
        ExitCode = b.ExitCode,
        Status = b.Status,
        StartedAt = b.StartedAt,
        EndedAt = b.EndedAt,
        Source = b.Source
    };

    private static SessionDto ToSessionDto(SessionState s, bool includeBlocks = false) => new()
    {
        Id = s.SessionId,
        SessionId = s.SessionId,
        Title = s.Title,
        ProfileId = s.ProfileId,
        Kind = s.Kind,
        ShellId = s.Shell.Id,
        Cwd = s.Cwd,
        Status = SessionStatus(s),
        Host = s.RemoteHost,
        Port = s.Port,
        User = s.RemoteUser,
        Auth = s.Auth,
        PermissionMode = s.PermissionMode,
        ProcessAlive = s.LiveShell is { IsAlive: true },
        PasswordProvided = false,
        Prompt = string.IsNullOrWhiteSpace(s.Prompt) ? BuildPrompt(s) : s.Prompt,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt,
        Source = s.Source,
        Blocks = includeBlocks ? s.Blocks.Select(ToBlockDto).ToList() : null
    };

    private static string SessionStatus(SessionState s)
    {
        if (s.Closed) return "closed";
        if (s.Status == "disconnected" || s.LiveShell is not { IsAlive: true })
            return s.Closed ? "closed" : (s.Status == "connecting" ? "connecting" : (s.LiveShell is { IsAlive: true } ? "connected" : (s.Status == "connected" && s.LiveShell == null ? "disconnected" : (string.IsNullOrEmpty(s.Status) || s.Status == "idle" ? "disconnected" : s.Status))));
        return "connected";
    }

    private static string BuildPrompt(SessionState s)
        => s.Kind == "ssh" && !string.IsNullOrWhiteSpace(s.RemoteUser)
            ? $"{s.RemoteUser}@{s.RemoteHost}:{s.Cwd}$ "
            : $"{s.Cwd} $ ";

    private void AuditV2(string kind, SessionState s, string? command, string? detail, string source)
    {
        try
        {
            _catalog.AppendAudit(kind, s.SessionId, s.ProfileId, source, s.AgentId, command, detail);
        }
        catch { /* 审计失败不得阻断主路径 */ }
        try
        {
            _audit.Append(new AuditEventDto
            {
                EventId = NewId("evt"),
                Time = DateTimeOffset.UtcNow,
                Kind = kind,
                SessionId = s.SessionId,
                AgentId = s.AgentId,
                ShellId = s.Shell.Id,
                Cwd = s.Cwd,
                CommandRedacted = command,
                Detail = detail
            });
        }
        catch { /* ignore */ }
    }

    private void ExpireApprovals()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kv in _v2Approvals)
        {
            if (now <= kv.Value.ExpiresAt) continue;
            if (_v2Approvals.TryRemove(kv.Key, out var pending))
            {
                pending.Block.Status = "expired";
                pending.Block.EndedAt = now;
            }
        }
    }

    private void TearDownShell(SessionState s)
    {
        try { s.LiveShell?.Dispose(); } catch { /* ignore */ }
        s.LiveShell = null;
        try { s.ActiveProcess?.Kill(entireProcessTree: true); } catch { /* ignore */ }
        s.ActiveProcess = null;
        DeleteTempKey(s);
        s.CommandBusy = false;
    }

    private static string NormalizeKind(string? kind)
        => string.Equals(kind, "ssh", StringComparison.OrdinalIgnoreCase) ? "ssh" : "local";

    private static string NormalizeAuth(string? auth, string? secretId, string? identityFile, string? password)
    {
        if (!string.IsNullOrWhiteSpace(auth))
        {
            var a = auth.Trim().ToLowerInvariant();
            if (a is "key" or "privatekey" or "identity") return "key";
            if (a is "interactive" or "password" or "sshpass") return "interactive";
        }
        if (!string.IsNullOrWhiteSpace(secretId) || !string.IsNullOrWhiteSpace(identityFile))
            return "key";
        return "interactive";
    }

    private static string NormalizeCommandSource(string? client, string server)
    {
        var c = (client ?? "").Trim().ToLowerInvariant();
        if (c is "prompt" or "snippet" or "assistant" or "ui") return c;
        return string.IsNullOrWhiteSpace(server) ? "ui" : server;
    }

    private static string ResolveLocalCwd(string? cwd)
    {
        var path = string.IsNullOrWhiteSpace(cwd)
            ? (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) is var home && Directory.Exists(home) ? home : Environment.CurrentDirectory)
            : Path.GetFullPath(cwd);
        if (!Directory.Exists(path))
            throw new SmartTerminalException("invalid_request", "工作目录不存在: " + path, 400);
        return path;
    }

    private static string PublicId(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static string Fingerprint(string material)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    private static string BuildChatPrompt(SessionState s, string userText)
    {
        var ctx = $"当前会话 kind={s.Kind} cwd={s.Cwd} host={s.RemoteHost}。把可执行建议放在 ```bash 代码块里。不要直接执行。用户说：\n{userText}";
        return ctx;
    }

    private static string? ExtractSuggestedCommand(string content)
    {
        var fence = Regex.Match(content, "```(?:bash|sh|zsh|shell)?\\s*\\n([\\s\\S]*?)```", RegexOptions.IgnoreCase);
        if (fence.Success)
        {
            var line = fence.Groups[1].Value.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0 && !l.StartsWith('#'));
            return string.IsNullOrWhiteSpace(line) ? null : SecretRedactor.Redact(line);
        }
        var promptLine = Regex.Match(content, "^\\s*\\$\\s+(.+)$", RegexOptions.Multiline);
        return promptLine.Success ? SecretRedactor.Redact(promptLine.Groups[1].Value.Trim()) : null;
    }
}

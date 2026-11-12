namespace ForgeSelf.Api.Plugins.SmartTerminal.Models;

public sealed class SessionDto
{
    public string Id { get; init; } = string.Empty;
    /// <summary>兼容旧字段，值与 <see cref="Id"/> 相同。</summary>
    public string SessionId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? ProfileId { get; init; }
    public string Kind { get; init; } = "local";
    public string? ShellId { get; init; }
    public string Cwd { get; init; } = string.Empty;
    public string Status { get; init; } = "connecting";
    public string? Host { get; init; }
    public int? Port { get; init; }
    public string? User { get; init; }
    public string? Auth { get; init; }
    public PermissionMode PermissionMode { get; init; } = PermissionMode.AskWrite;
    public bool ProcessAlive { get; init; }
    public bool PasswordProvided { get; init; }
    public string Prompt { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public string Source { get; init; } = "ui";
    public IReadOnlyList<BlockDto>? Blocks { get; init; }
}

public sealed class BlockDto
{
    public string Id { get; init; } = string.Empty;
    public int Seq { get; init; }
    public string Kind { get; init; } = "command";
    public string? Command { get; init; }
    public string Output { get; init; } = string.Empty;
    public int? ExitCode { get; init; }
    public string Status { get; init; } = "done";
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? EndedAt { get; init; }
    public string Source { get; init; } = "system";
}

public sealed class SubmitCommandRequest
{
    public string Command { get; set; } = string.Empty;
    /// <summary>客户端声明仅作审计辅助，不能绕过闸门。Agent 入口由服务端记 source=agent。</summary>
    public string? Source { get; set; }
}

public sealed class SubmitCommandResponse
{
    public string Status { get; init; } = string.Empty;
    public string Decision { get; init; } = string.Empty;
    public string Risk { get; init; } = string.Empty;
    public BlockDto? Block { get; init; }
    public ApprovalDto? Approval { get; init; }
}

public sealed class ApprovalDto
{
    public string Id { get; init; } = string.Empty;
    public string SessionId { get; init; } = string.Empty;
    public string Command { get; init; } = string.Empty;
    public string Risk { get; init; } = string.Empty;
    public string Mode { get; init; } = "AskWrite";
    public string Status { get; init; } = "pending";
    public string Source { get; init; } = "ui";
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}

public sealed class ResolveApprovalRequest
{
    public string Decision { get; set; } = string.Empty;
}

public sealed class BlockListDto
{
    public string Prompt { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public IReadOnlyList<BlockDto> Items { get; init; } = Array.Empty<BlockDto>();
}

public sealed class ProfileDto
{
    public string Id { get; init; } = string.Empty;
    public string Kind { get; init; } = "local";
    public string Name { get; init; } = string.Empty;
    public string? ShellId { get; init; }
    public string? Cwd { get; init; }
    public string? Host { get; init; }
    public int? Port { get; init; }
    public string? User { get; init; }
    public string? Auth { get; init; }
    public string? SecretId { get; init; }
    public string? IdentityFile { get; init; }
    public string Source { get; init; } = "ui";
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class UpsertProfileRequest
{
    public string? Kind { get; set; }
    public string? Name { get; set; }
    public string? ShellId { get; set; }
    public string? Cwd { get; set; }
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? User { get; set; }
    public string? Auth { get; set; }
    public string? SecretId { get; set; }
    public string? IdentityFile { get; set; }
    public string? Password { get; set; }
}

public sealed class SecretDto
{
    public string Id { get; init; } = string.Empty;
    public string Kind { get; init; } = "ssh-private-key";
    public string? Fingerprint { get; init; }
    public string? Label { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class CreateSecretRequest
{
    public string? Kind { get; set; }
    public string? Label { get; set; }
    public string? Material { get; set; }
    public string? Passphrase { get; set; }
}

public sealed class SnippetDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Command { get; init; } = string.Empty;
}

public sealed class UpsertSnippetRequest
{
    public string? Name { get; set; }
    public string? Command { get; set; }
}

public sealed class ChatMessageDto
{
    public string Id { get; init; } = string.Empty;
    public string SessionId { get; init; } = string.Empty;
    public string Role { get; init; } = "user";
    public string Content { get; init; } = string.Empty;
    public string? SuggestedCommand { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class ChatThreadDto
{
    public IReadOnlyList<ChatMessageDto> Messages { get; init; } = Array.Empty<ChatMessageDto>();
    public string? Model { get; init; }
}

public sealed class SendChatRequest
{
    public string Content { get; set; } = string.Empty;
    public string? BlockId { get; set; }
}

public sealed class ClassifyPreviewResponse
{
    public string Risk { get; init; } = "read";
    public string Decision { get; init; } = "allow";
    public string Reason { get; init; } = string.Empty;
    public string Mode { get; init; } = "AskWrite";
}

public sealed class ReconnectRequest
{
    public string? Password { get; set; }
}

public sealed class PatchSessionRequest
{
    public string? Title { get; set; }
}

public sealed class AuditPageDto
{
    public IReadOnlyList<AuditRowDto> Items { get; init; } = Array.Empty<AuditRowDto>();
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed class AuditRowDto
{
    public long Id { get; init; }
    public string Kind { get; init; } = string.Empty;
    public string? SessionId { get; init; }
    public string? ProfileId { get; init; }
    public string? Source { get; init; }
    public string? Actor { get; init; }
    public string? CommandRedacted { get; init; }
    public string? DetailJson { get; init; }
    public DateTimeOffset CreateTime { get; init; }
}

public sealed class MetaDto
{
    public string PluginId { get; init; } = "smart-terminal";
    public string Version { get; init; } = "2.0.0";
    public string PermissionDefault { get; init; } = "AskWrite";
    public MetaFeaturesDto Features { get; init; } = new();
}

public sealed class MetaFeaturesDto
{
    public bool Split { get; init; }
    public bool Sftp { get; init; }
    public bool PtyAcceptance { get; init; }
}

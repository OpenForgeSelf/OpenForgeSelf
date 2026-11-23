using ForgeSelf.Api.Entities;

namespace ForgeSelf.Api.Services.Vault;

/// <summary>
/// 凭据保险库 DTO 与请求模型（PILOT-086）。
/// <b>明文红线</b>：响应 DTO 只投影元数据——<see cref="SecretMetaDto"/> 不含 value、
/// WrappedDek/Nonce/Ciphertext/Tag 等任何信封材料字段。
/// </summary>

// ───────────────────────── 响应 DTO ─────────────────────────

/// <summary>凭据元数据（列表/详情响应体；绝不携带明文与密文材料）</summary>
public sealed record SecretMetaDto
{
    public long Id { get; init; }
    public string RefId { get; init; } = "";
    /// <summary>SecretRef JSON 串（配置落盘形态：{"source":"vault","id":"sec_…","version":"current"}）</summary>
    public string Ref { get; init; } = "";
    public string Name { get; init; } = "";
    public string SecretType { get; init; } = "";
    public string Provider { get; init; } = "";
    public string OwnerType { get; init; } = "";
    public string OwnerId { get; init; } = "";
    public string MetadataJson { get; init; } = "";
    public string Status { get; init; } = "";
    public DateTime ExpiresAt { get; init; }
    public int RotationVersion { get; init; }
    public int GrantCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>授权（Grant）视图</summary>
public sealed record GrantDto
{
    public long Id { get; init; }
    public long SecretId { get; init; }
    public string SubjectType { get; init; } = "";
    public string SubjectId { get; init; } = "";
    public string Operation { get; init; } = "";
    public string TargetConstraint { get; init; } = "";
    public DateTime ValidFrom { get; init; }
    public DateTime ExpiresAt { get; init; }
    public DateTime RevokedAt { get; init; }
    public string CreatedBy { get; init; } = "";
    public DateTime CreatedAt { get; init; }
}

/// <summary>审计事件视图（全部为脱敏字段）</summary>
public sealed record AuditEventDto
{
    public long Id { get; init; }
    public long SecretId { get; init; }
    public long GrantId { get; init; }
    public string Actor { get; init; } = "";
    public string SubjectType { get; init; } = "";
    public string SubjectId { get; init; } = "";
    public string Action { get; init; } = "";
    public string Target { get; init; } = "";
    public string Result { get; init; } = "";
    public string ReasonCode { get; init; } = "";
    public string CorrelationId { get; init; } = "";
    public DateTime CreatedAt { get; init; }
}

// ───────────────────────── 请求模型 ─────────────────────────

/// <summary>创建凭据请求（value 明文仅出现在请求体当次）</summary>
public sealed class CreateSecretRequest
{
    public string Name { get; set; } = "";
    /// <summary>ApiKey | Token | Password | PrivateKey | Custom</summary>
    public string SecretType { get; set; } = "";
    public string Provider { get; set; } = "";
    /// <summary>默认 host</summary>
    public string OwnerType { get; set; } = "";
    /// <summary>默认 host</summary>
    public string OwnerId { get; set; } = "";
    /// <summary>元数据 JSON（严禁放机密值），可空</summary>
    public string MetadataJson { get; set; } = "";
    /// <summary>明文凭据值（≤10000 字符）</summary>
    public string Value { get; set; } = "";
    /// <summary>过期时间；缺省 = 不过期（DateTime.MinValue）</summary>
    public DateTime ExpiresAt { get; set; } = DateTime.MinValue;
}

/// <summary>更新凭据元数据请求（空串 = 不改该字段）</summary>
public sealed class UpdateSecretRequest
{
    public string Name { get; set; } = "";
    public string Provider { get; set; } = "";
    public string MetadataJson { get; set; } = "";
    /// <summary>过期时间（DateTime.MinValue = 不过期）；null = 不改</summary>
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>轮换凭据值请求</summary>
public sealed class SetSecretValueRequest
{
    /// <summary>新明文值（≤10000 字符）</summary>
    public string Value { get; set; } = "";
}

/// <summary>创建授权请求</summary>
public sealed class CreateGrantRequest
{
    /// <summary>host | agent | plugin | automation</summary>
    public string SubjectType { get; set; } = "";
    public string SubjectId { get; set; } = "";
    /// <summary>use | test-connection | *</summary>
    public string Operation { get; set; } = "use";
    /// <summary>空 或 * = 不限目标；否则全等匹配</summary>
    public string TargetConstraint { get; set; } = "";
    /// <summary>生效时间；缺省 = 立即</summary>
    public DateTime ValidFrom { get; set; } = DateTime.MinValue;
    /// <summary>过期时间；缺省 = 不过期</summary>
    public DateTime ExpiresAt { get; set; } = DateTime.MinValue;
}

// ───────────────────────── 使用上下文与裁决 ─────────────────────────

/// <summary>宿主内部使用凭据的上下文（GetValueForHost 专用）</summary>
public sealed record UseContext
{
    /// <summary>发起方（审计用，如 host:llm-adapter）</summary>
    public string Actor { get; init; } = "host";
    /// <summary>使用主体类型（host | agent | plugin | automation）</summary>
    public string SubjectType { get; init; } = "host";
    /// <summary>使用主体标识</summary>
    public string SubjectId { get; init; } = "host";
    /// <summary>操作（use | test-connection | …）</summary>
    public string Operation { get; init; } = "use";
    /// <summary>目标（域名/端点约束值，可空）</summary>
    public string Target { get; init; } = "";
    /// <summary>调用链关联ID（可空）</summary>
    public string CorrelationId { get; init; } = "";
}

/// <summary>授权裁决（默认拒绝）</summary>
public sealed record GrantDecision(bool Allowed, long GrantId, string ReasonCode);

/// <summary>凭据业务拒绝（吊销/过期/授权错位等），控制器转为 success:false 外壳</summary>
public sealed class VaultOperationException(string message, string reasonCode) : Exception(message)
{
    /// <summary>机器可读原因码（如 secret_revoked / grant_mismatch）</summary>
    public string ReasonCode { get; } = reasonCode;
}

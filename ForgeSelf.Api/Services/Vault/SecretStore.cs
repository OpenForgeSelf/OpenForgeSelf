using System.Security.Cryptography;
using ForgeSelf.Api.Entities;
using ForgeSelf.Api.Security;
using ForgeSelf.Abstractions;
using NewLife.Log;

namespace ForgeSelf.Api.Services.Vault;

/// <summary>
/// 凭据保险库存储服务（PILOT-086；方案 §5/§6）。
/// </summary>
/// <remarks>
/// <para><b>明文红线</b>：明文只在 Create/SetSecretValue 的请求当次与 <see cref="GetValueForHost"/>
/// 的返回当次出现；任何响应 DTO、日志、审计都不含明文或信封材料。</para>
/// <para><b>Fail Closed</b>：吊销/过期/无授权/解密失败一律拒绝并落审计，绝不降级。</para>
/// </remarks>
public class SecretStore : ISecretStore
{
    public const string StatusActive = "active";
    public const string StatusRevoked = "revoked";

    /// <summary>合法凭据类型</summary>
    public static readonly string[] AllowedSecretTypes = ["ApiKey", "Token", "Password", "PrivateKey", "Custom"];

    public const int MaxValueLength = 10_000;
    public const int MaxNameLength = 100;
    public const int MaxMetadataLength = 2_000;
    public const int MaxAuditLimit = 500;

    /// <summary>审计动作常量</summary>
    public const string ActionCreate = "create";
    public const string ActionUpdateValue = "update_value";
    public const string ActionUpdate = "update";
    public const string ActionRevoke = "revoke";
    public const string ActionDelete = "delete";
    public const string ActionGrantCreated = "grant_created";
    public const string ActionGrantRevoked = "grant_revoked";
    public const string ActionUseAllowed = "use_allowed";
    public const string ActionUseDenied = "use_denied";

    private readonly ISecretKeyProvider _keyProvider;
    private readonly ICredentialBroker _broker;

    public SecretStore(ISecretKeyProvider keyProvider, ICredentialBroker broker)
    {
        _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
    }

    // ───────────────────────── 创建 / 查询 ─────────────────────────

    /// <inheritdoc />
    public SecretMetaDto Create(CreateSecretRequest request, string actor)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateValue(request.Value);
        ValidateMeta(request.Name, request.SecretType, request.MetadataJson);

        var secret = new StoredSecret
        {
            RefId = SecretRef.New().Id,
            Name = request.Name.Trim(),
            SecretType = request.SecretType.Trim(),
            Provider = BlankToNull(request.Provider),
            OwnerType = string.IsNullOrWhiteSpace(request.OwnerType) ? "host" : request.OwnerType.Trim(),
            OwnerId = string.IsNullOrWhiteSpace(request.OwnerId) ? "host" : request.OwnerId.Trim(),
            MetadataJson = BlankToNull(request.MetadataJson),
            FormatVersion = EnvelopeSecretCrypto.FormatVersion,
            RotationVersion = 1,
            Status = StatusActive,
            ExpiresAt = NormalizeNever(request.ExpiresAt),
            RevokedAt = DateTime.MinValue,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
        };
        ApplyEnvelope(secret, request.Value);

        secret.Insert();
        AddAudit(secret.Id, 0, actor, null, null, ActionCreate, null, "allowed", null, null);
        return ToMeta(secret);
    }

    /// <inheritdoc />
    public List<SecretMetaDto> List() =>
        StoredSecret.FindAll(StoredSecret._.Id > 0).OrderBy(e => e.Id).Select(ToMeta).ToList();

    /// <inheritdoc />
    public SecretMetaDto? Get(string idOrRef) =>
        Resolve(idOrRef) is { } s ? ToMeta(s) : null;

    // ───────────────────────── 元数据维护 ─────────────────────────

    /// <inheritdoc />
    public SecretMetaDto? Update(string idOrRef, UpdateSecretRequest request, string actor)
    {
        ArgumentNullException.ThrowIfNull(request);
        var secret = Resolve(idOrRef);
        if (secret == null) return null;

        var changed = false;
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            if (request.Name.Trim().Length > MaxNameLength)
                throw new VaultOperationException($"名称超长（>{MaxNameLength} 字符）", "name_too_long");
            secret.Name = request.Name.Trim();
            changed = true;
        }
        if (!string.IsNullOrWhiteSpace(request.Provider))
        {
            secret.Provider = request.Provider.Trim();
            changed = true;
        }
        if (!string.IsNullOrWhiteSpace(request.MetadataJson))
        {
            ValidateMetadata(request.MetadataJson);
            secret.MetadataJson = request.MetadataJson;
            changed = true;
        }
        if (request.ExpiresAt.HasValue)
        {
            secret.ExpiresAt = NormalizeNever(request.ExpiresAt.Value);
            changed = true;
        }

        if (changed)
        {
            secret.UpdatedAt = DateTime.Now;
            secret.Save();
            AddAudit(secret.Id, 0, actor, null, null, ActionUpdate, null, "allowed", null, null);
        }
        return ToMeta(secret);
    }

    /// <inheritdoc />
    public SecretMetaDto? SetSecretValue(string idOrRef, string newValue, string actor)
    {
        ValidateValue(newValue);
        var secret = Resolve(idOrRef) ?? throw new VaultOperationException("凭据不存在", "secret_not_found");
        EnsureUsable(secret);

        // 新 DEK + 新 Nonce，轮值版本 +1（AAD 绑定 RotationVersion → 旧轮材料对新 AAD 必失败）
        secret.RotationVersion += 1;
        ApplyEnvelope(secret, newValue);
        secret.UpdatedAt = DateTime.Now;
        secret.Save();

        AddAudit(secret.Id, 0, actor, null, null, ActionUpdateValue, null, "allowed", null, null);
        return ToMeta(secret);
    }

    // ───────────────────────── 吊销 / 删除 ─────────────────────────

    /// <inheritdoc />
    public SecretMetaDto? Revoke(string idOrRef, string actor)
    {
        var secret = Resolve(idOrRef);
        if (secret == null) return null;

        secret.Status = StatusRevoked;
        if (secret.RevokedAt == DateTime.MinValue) secret.RevokedAt = DateTime.Now;
        secret.UpdatedAt = DateTime.Now;
        secret.Save();

        AddAudit(secret.Id, 0, actor, null, null, ActionRevoke, null, "allowed", null, null);
        return ToMeta(secret);
    }

    /// <inheritdoc />
    public bool Delete(string idOrRef, string actor)
    {
        var secret = Resolve(idOrRef);
        if (secret == null) return false;

        // 先吊销全部有效授权，再删行（审计保留）
        foreach (var grant in CredentialGrant.FindAllBySecretId(secret.Id))
        {
            if (grant.RevokedAt != DateTime.MinValue) continue;
            grant.RevokedAt = DateTime.Now;
            grant.Save();
            AddAudit(secret.Id, grant.Id, actor, grant.SubjectType, grant.SubjectId, ActionGrantRevoked, null, "allowed", "secret_deleted", null);
        }

        secret.Delete();
        AddAudit(secret.Id, 0, actor, null, null, ActionDelete, null, "allowed", null, null);
        return true;
    }

    // ───────────────────────── 授权（Grant） ─────────────────────────

    /// <inheritdoc />
    public List<GrantDto> ListGrants(string idOrRef) =>
        Resolve(idOrRef) is { } s
            ? CredentialGrant.FindAllBySecretId(s.Id).Select(ToGrantDto).ToList()
            : [];

    /// <inheritdoc />
    public GrantDto? CreateGrant(string idOrRef, CreateGrantRequest request, string actor)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.SubjectType))
            throw new VaultOperationException("SubjectType 不能为空", "grant_invalid");
        if (string.IsNullOrWhiteSpace(request.SubjectId))
            throw new VaultOperationException("SubjectId 不能为空", "grant_invalid");
        if (string.IsNullOrWhiteSpace(request.Operation))
            throw new VaultOperationException("Operation 不能为空", "grant_invalid");

        var secret = Resolve(idOrRef) ?? throw new VaultOperationException("凭据不存在", "secret_not_found");
        if (secret.Status != StatusActive)
            throw new VaultOperationException("凭据已吊销，禁止授权", "secret_revoked");

        var grant = new CredentialGrant
        {
            SecretId = secret.Id,
            SubjectType = request.SubjectType.Trim(),
            SubjectId = request.SubjectId.Trim(),
            Operation = request.Operation.Trim(),
            TargetConstraint = BlankToNull(request.TargetConstraint),
            ValidFrom = NormalizeNever(request.ValidFrom),
            ExpiresAt = NormalizeNever(request.ExpiresAt),
            RevokedAt = DateTime.MinValue,
            CreatedBy = actor,
            CreatedAt = DateTime.Now,
        };
        grant.Insert();

        AddAudit(secret.Id, grant.Id, actor, grant.SubjectType, grant.SubjectId, ActionGrantCreated, grant.TargetConstraint, "allowed", null, null);
        return ToGrantDto(grant);
    }

    /// <inheritdoc />
    public bool RevokeGrant(string idOrRef, long grantId, string actor)
    {
        var secret = Resolve(idOrRef) ?? throw new VaultOperationException("凭据不存在", "secret_not_found");
        var grant = CredentialGrant.FindById(grantId)
            ?? throw new VaultOperationException("授权不存在", "grant_not_found");
        if (grant.SecretId != secret.Id)
            throw new VaultOperationException("授权不属于该凭据", "grant_mismatch");

        if (grant.RevokedAt == DateTime.MinValue)
        {
            grant.RevokedAt = DateTime.Now;
            grant.Save();
            AddAudit(secret.Id, grant.Id, actor, grant.SubjectType, grant.SubjectId, ActionGrantRevoked, null, "allowed", null, null);
        }
        return true;
    }

    // ───────────────────────── 审计 ─────────────────────────

    /// <inheritdoc />
    public List<AuditEventDto> GetAudit(long? secretId, int limit)
    {
        if (limit <= 0) limit = 100;
        if (limit > MaxAuditLimit) limit = MaxAuditLimit;
        return SecretAuditEvent.FindAllRecent(secretId ?? 0, limit).Select(ToAuditDto).ToList();
    }

    /// <summary>写审计事件（全部字段脱敏；禁止明文/密文进入参数）</summary>
    private void AddAudit(long secretId, long grantId, string? actor, string? subjectType, string? subjectId,
        string action, string? target, string result, string? reasonCode, string? correlationId)
    {
        try
        {
            new SecretAuditEvent
            {
                SecretId = secretId,
                GrantId = grantId,
                Actor = (actor ?? "unknown").Clamp(64),
                SubjectType = subjectType.Clamp(20),
                SubjectId = subjectId.Clamp(64),
                Action = action.Clamp(30),
                Target = target.Clamp(200),
                Result = result.Clamp(10),
                ReasonCode = reasonCode.Clamp(40),
                CorrelationId = correlationId.Clamp(64),
                CreatedAt = DateTime.Now,
            }.Insert();
        }
        catch (Exception ex)
        {
            // 审计写失败不阻断主流程，但必须留痕（NewLife 日志，不含敏感材料）
            XTrace.Log.Error("凭据审计写入失败: {0}", ex.Message);
        }
    }

    // ───────────────────────── 明文唯一出口 ─────────────────────────

    /// <inheritdoc />
    public (string? Value, GrantDecision Decision) GetValueForHost(string idOrRef, UseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var secret = Resolve(idOrRef);
        if (secret == null)
        {
            var d = new GrantDecision(false, 0, "secret_not_found");
            AddAudit(0, 0, context.Actor, context.SubjectType, context.SubjectId, ActionUseDenied, context.Target, "denied", d.ReasonCode, context.CorrelationId);
            return (null, d);
        }

        var decision = _broker.Authorize(secret, context);
        if (!decision.Allowed)
        {
            AddAudit(secret.Id, decision.GrantId, context.Actor, context.SubjectType, context.SubjectId, ActionUseDenied, context.Target, "denied", decision.ReasonCode, context.CorrelationId);
            return (null, decision);
        }

        try
        {
            var aad = EnvelopeSecretCrypto.BuildAad(secret.RefId, secret.OwnerType, secret.OwnerId, secret.RotationVersion);
            var payload = new EnvelopePayload
            {
                KeyId = secret.KeyId,
                WrappedDek = secret.WrappedDek,
                WrapNonce = secret.WrapNonce,
                WrapTag = secret.WrapTag,
                Nonce = secret.Nonce,
                Ciphertext = secret.Ciphertext,
                Tag = secret.Tag,
                FormatVersion = secret.FormatVersion,
            };
            var value = EnvelopeSecretCrypto.Decrypt(payload, aad, _keyProvider.GetCurrentKey().Key);
            AddAudit(secret.Id, decision.GrantId, context.Actor, context.SubjectType, context.SubjectId, ActionUseAllowed, context.Target, "allowed", null, context.CorrelationId);
            return (value, decision);
        }
        catch (CryptographicException ex)
        {
            // Fail Closed：解密失败（篡改/KEK 缺失）→ 拒绝 + 审计 error，不区分失败细节
            XTrace.Log.Error("凭据解密失败（refId 已脱敏）: {0}", ex.Message);
            var d = new GrantDecision(false, decision.GrantId, "decrypt_failed");
            AddAudit(secret.Id, decision.GrantId, context.Actor, context.SubjectType, context.SubjectId, ActionUseDenied, context.Target, "error", d.ReasonCode, context.CorrelationId);
            return (null, d);
        }
    }

    // ───────────────────────── 内部工具 ─────────────────────────

    /// <summary>解析数字 Id 或 sec_ 引用为实体；均不匹配返回 null</summary>
    internal StoredSecret? Resolve(string? idOrRef)
    {
        if (string.IsNullOrWhiteSpace(idOrRef)) return null;
        if (idOrRef.StartsWith(SecretRef.IdPrefix, StringComparison.Ordinal)) return StoredSecret.FindByRefId(idOrRef);
        return long.TryParse(idOrRef, out var id) ? StoredSecret.FindById(id) : null;
    }

    private void ApplyEnvelope(StoredSecret secret, string value)
    {
        var aad = EnvelopeSecretCrypto.BuildAad(secret.RefId, secret.OwnerType, secret.OwnerId, secret.RotationVersion);
        var payload = EnvelopeSecretCrypto.Encrypt(value, aad, _keyProvider.GetCurrentKey());
        secret.FormatVersion = payload.FormatVersion;
        secret.KeyId = payload.KeyId;
        secret.WrappedDek = payload.WrappedDek;
        secret.WrapNonce = payload.WrapNonce;
        secret.WrapTag = payload.WrapTag;
        secret.Nonce = payload.Nonce;
        secret.Ciphertext = payload.Ciphertext;
        secret.Tag = payload.Tag;
    }

    /// <summary>吊销/过期校验（Fail Closed）</summary>
    private static void EnsureUsable(StoredSecret secret)
    {
        if (secret.Status != StatusActive)
            throw new VaultOperationException("凭据已吊销", "secret_revoked");
        if (secret.ExpiresAt != DateTime.MinValue && secret.ExpiresAt < DateTime.Now)
            throw new VaultOperationException("凭据已过期", "secret_expired");
    }

    private static void ValidateValue(string? value)
    {
        if (string.IsNullOrEmpty(value)) throw new VaultOperationException("明文值不能为空", "value_empty");
        if (value.Length > MaxValueLength) throw new VaultOperationException($"明文超长（>{MaxValueLength} 字符）", "value_too_long");
    }

    private static void ValidateMeta(string? name, string? secretType, string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new VaultOperationException("名称不能为空", "name_empty");
        if (name.Trim().Length > MaxNameLength) throw new VaultOperationException($"名称超长（>{MaxNameLength} 字符）", "name_too_long");
        if (string.IsNullOrWhiteSpace(secretType) || !AllowedSecretTypes.Contains(secretType.Trim()))
            throw new VaultOperationException($"凭据类型非法（允许：{string.Join("/", AllowedSecretTypes)}）", "secret_type_invalid");
        ValidateMetadata(metadataJson);
    }

    private static void ValidateMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson)) return;
        if (metadataJson.Length > MaxMetadataLength) throw new VaultOperationException($"元数据超长（>{MaxMetadataLength} 字符）", "metadata_too_long");
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(metadataJson);
        }
        catch (System.Text.Json.JsonException)
        {
            throw new VaultOperationException("元数据必须是合法 JSON", "metadata_invalid");
        }
    }

    private static string? BlankToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime NormalizeNever(DateTime value) =>
        value == DateTime.MinValue ? DateTime.MinValue : value;

    private static SecretMetaDto ToMeta(StoredSecret s) => new()
    {
        Id = s.Id,
        RefId = s.RefId,
        Ref = new SecretRef { Id = s.RefId }.ToJson(),
        Name = s.Name,
        SecretType = s.SecretType,
        Provider = s.Provider ?? "",
        OwnerType = s.OwnerType,
        OwnerId = s.OwnerId,
        MetadataJson = s.MetadataJson ?? "",
        Status = s.Status,
        ExpiresAt = s.ExpiresAt,
        RotationVersion = s.RotationVersion,
        GrantCount = (int)CredentialGrant.FindCount(CredentialGrant._.SecretId == s.Id & CredentialGrant._.RevokedAt == DateTime.MinValue),
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt,
    };

    private static GrantDto ToGrantDto(CredentialGrant g) => new()
    {
        Id = g.Id,
        SecretId = g.SecretId,
        SubjectType = g.SubjectType,
        SubjectId = g.SubjectId,
        Operation = g.Operation,
        TargetConstraint = g.TargetConstraint ?? "",
        ValidFrom = g.ValidFrom,
        ExpiresAt = g.ExpiresAt,
        RevokedAt = g.RevokedAt,
        CreatedBy = g.CreatedBy,
        CreatedAt = g.CreatedAt,
    };

    private static AuditEventDto ToAuditDto(SecretAuditEvent e) => new()
    {
        Id = e.Id,
        SecretId = e.SecretId,
        GrantId = e.GrantId,
        Actor = e.Actor,
        SubjectType = e.SubjectType ?? "",
        SubjectId = e.SubjectId ?? "",
        Action = e.Action,
        Target = e.Target ?? "",
        Result = e.Result,
        ReasonCode = e.ReasonCode ?? "",
        CorrelationId = e.CorrelationId ?? "",
        CreatedAt = e.CreatedAt,
    };
}

/// <summary>字符串截断（防审计字段溢出）</summary>
internal static class AuditStringExtensions
{
    public static string? Clamp(this string? value, int maxLength) =>
        string.IsNullOrEmpty(value) ? value : value.Length <= maxLength ? value : value[..maxLength];
}

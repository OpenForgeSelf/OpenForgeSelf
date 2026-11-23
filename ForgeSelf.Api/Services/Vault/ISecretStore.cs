using ForgeSelf.Api.Entities;

namespace ForgeSelf.Api.Services.Vault;

/// <summary>
/// 凭据保险库存储服务契约（PILOT-086，方案 <c>docs/08-security/credential-vault-design.md</c> §6）。
/// 仅宿主内部使用（管理面控制器 + 宿主可信代码），<b>不向插件 seed</b>——
/// 插件用凭据属后续批次的 Adapter 面，必须经授权（Grant）通道，不得直接读明文。
/// </summary>
public interface ISecretStore
{
    /// <summary>创建凭据（value 明文只在请求当次出现；响应只返回元数据 + SecretRef）</summary>
    SecretMetaDto Create(CreateSecretRequest request, string actor);

    /// <summary>列出全部凭据元数据（无明文无密文材料）</summary>
    List<SecretMetaDto> List();

    /// <summary>按数字 Id 或 sec_ 引用取元数据；不存在返回 null</summary>
    SecretMetaDto? Get(string idOrRef);

    /// <summary>更新元数据（Name/Provider/MetadataJson/ExpiresAt）；不存在返回 null</summary>
    SecretMetaDto? Update(string idOrRef, UpdateSecretRequest request, string actor);

    /// <summary>轮换凭据值：重新生成 DEK + Nonce，RotationVersion+1；已吊销/过期抛 <see cref="VaultOperationException"/></summary>
    SecretMetaDto? SetSecretValue(string idOrRef, string newValue, string actor);

    /// <summary>吊销凭据（立即对所有使用 Fail Closed）；不存在返回 null</summary>
    SecretMetaDto? Revoke(string idOrRef, string actor);

    /// <summary>删除凭据：先吊销全部 Grant 再删行（审计保留）；不存在返回 false</summary>
    bool Delete(string idOrRef, string actor);

    /// <summary>列出某凭据的授权</summary>
    List<GrantDto> ListGrants(string idOrRef);

    /// <summary>创建授权（默认拒绝模型的例外面）；凭据不存在返回 null</summary>
    GrantDto? CreateGrant(string idOrRef, CreateGrantRequest request, string actor);

    /// <summary>撤销授权（立即生效）；授权不存在或不属于该凭据抛 <see cref="VaultOperationException"/></summary>
    bool RevokeGrant(string idOrRef, long grantId, string actor);

    /// <summary>查询审计事件（脱敏字段），新→旧，limit 上限 500</summary>
    List<AuditEventDto> GetAudit(long? secretId, int limit);

    /// <summary>
    /// <b>明文唯一出口</b>（宿主可信代码专用）：经 <see cref="ICredentialBroker"/> 授权校验 → 解密 → 审计。
    /// 拒绝/失败时 Value=null 且 Decision 携带原因码；不设任何 HTTP 明文端点。
    /// </summary>
    (string? Value, GrantDecision Decision) GetValueForHost(string idOrRef, UseContext context);
}

/// <summary>
/// 授权校验器契约（方案 §6：默认拒绝；撤销立即生效，无缓存）。
/// </summary>
public interface ICredentialBroker
{
    /// <summary>校验 <paramref name="context"/> 是否被授权使用 <paramref name="secret"/>（审计由 SecretStore 统一落）</summary>
    GrantDecision Authorize(StoredSecret secret, UseContext context);
}

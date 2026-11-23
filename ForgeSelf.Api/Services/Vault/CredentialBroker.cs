using ForgeSelf.Api.Entities;

namespace ForgeSelf.Api.Services.Vault;

/// <summary>
/// 授权校验器（方案 §6，PILOT-086）：默认拒绝矩阵。
/// </summary>
/// <remarks>
/// 规则（任一不满足即拒绝）：
/// ① Secret 状态 active 且未过期；
/// ② 存在 Grant：SubjectType/SubjectId 精确（Grant 侧可为 *）、Operation 精确（可为 *）、
///    Target 约束满足（空/* = 不限，否则大小写不敏感全等）、Grant 未吊销且在有效期内。
/// 无缓存：每次校验直读库，撤销立即生效。
/// </remarks>
public class CredentialBroker : ICredentialBroker
{
    /// <summary>Secret 状态：active</summary>
    public const string StatusActive = SecretStore.StatusActive;

    /// <inheritdoc />
    public GrantDecision Authorize(StoredSecret secret, UseContext context)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(context);

        var now = DateTime.Now;

        // ① Secret 本身可用性（Fail Closed）
        if (!string.Equals(secret.Status, StatusActive, StringComparison.Ordinal))
            return new GrantDecision(false, 0, "secret_revoked");
        if (secret.ExpiresAt != DateTime.MinValue && secret.ExpiresAt < now)
            return new GrantDecision(false, 0, "secret_expired");

        // ② Grant 矩阵（默认拒绝）
        foreach (var grant in CredentialGrant.FindAllBySecretId(secret.Id))
        {
            if (grant.RevokedAt != DateTime.MinValue && grant.RevokedAt <= now) continue;
            if (grant.ValidFrom != DateTime.MinValue && grant.ValidFrom > now) continue;
            if (grant.ExpiresAt != DateTime.MinValue && grant.ExpiresAt <= now) continue;

            if (!Match(grant.SubjectType, context.SubjectType)) continue;
            if (!Match(grant.SubjectId, context.SubjectId)) continue;
            if (!Match(grant.Operation, context.Operation)) continue;
            if (!MatchTarget(grant.TargetConstraint, context.Target)) continue;

            return new GrantDecision(true, grant.Id, "granted");
        }

        return new GrantDecision(false, 0, "no_grant");
    }

    /// <summary>Grant 侧精确匹配（Grant 为 * 时通配；上下文侧必须精确给出）</summary>
    private static bool Match(string? grantValue, string? contextValue)
    {
        if (string.IsNullOrWhiteSpace(contextValue)) return false;
        if (string.IsNullOrWhiteSpace(grantValue)) return false;
        return grantValue == "*" || string.Equals(grantValue, contextValue, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>目标约束：空/* = 不限；否则与上下文目标大小写不敏感全等</summary>
    private static bool MatchTarget(string? constraint, string? target)
    {
        if (string.IsNullOrWhiteSpace(constraint) || constraint == "*") return true;
        if (string.IsNullOrWhiteSpace(target)) return false;
        return string.Equals(constraint, target, StringComparison.OrdinalIgnoreCase);
    }
}

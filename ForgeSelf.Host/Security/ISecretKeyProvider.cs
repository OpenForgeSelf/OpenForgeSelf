namespace ForgeSelf.Api.Security;

/// <summary>
/// 凭据保险库主密钥（KEK）材料快照。
/// <see cref="Key"/> 仅为进程内瞬态使用，调用方不得落盘、落日志或进响应。
/// </summary>
public sealed record VaultKeyMaterial
{
    /// <summary>KEK 标识（kek1- + 8 位指纹 hex），随密文一起落库，用于轮换时识别包封密钥</summary>
    public required string KeyId { get; init; }

    /// <summary>32 字节主密钥（KEK）</summary>
    public required byte[] Key { get; init; }

    /// <summary>保护形态：dpapi（Windows）| protected-file（非 Windows 0600 文件）</summary>
    public required string ProtectionMode { get; init; }
}

/// <summary>
/// 凭据保险库主密钥（KEK）提供器契约（方案 <c>docs/08-security/credential-vault-design.md</c> §5）。
/// </summary>
/// <remarks>
/// 与既有 <see cref="IMachineKeyProvider"/>（机器派生、用于存量 CBC 加密）刻意分开：
/// Vault 主密钥必须是<b>随机生成</b>（不从产品名/机器名/固定串构造），按平台保护持久化，
/// 且与 SQLite 数据库文件分离——仅复制数据库无法解密任何 Vault 凭据。
/// </remarks>
public interface ISecretKeyProvider
{
    /// <summary>取得当前 KEK 材料（进程内缓存；每次调用返回同一逻辑密钥）</summary>
    VaultKeyMaterial GetCurrentKey();
}

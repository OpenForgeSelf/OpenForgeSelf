using System.Security.Cryptography;
using System.Text;

namespace ForgeSelf.Api.Security;

/// <summary>
/// 单条凭据的信封加密载荷（全部 base64 字段，一一对应 <c>StoredSecret</c> 的密文列）。
/// 本 record 只进数据库与进程内存，<b>禁止</b>进入任何 HTTP 响应、日志或审计。
/// </summary>
public sealed record EnvelopePayload
{
    /// <summary>包封 KEK 的 KeyId</summary>
    public required string KeyId { get; init; }

    /// <summary>被 KEK 包封的 DEK（AES-GCM 密文，base64）</summary>
    public required string WrappedDek { get; init; }

    /// <summary>DEK 包封 nonce（base64）</summary>
    public required string WrapNonce { get; init; }

    /// <summary>DEK 包封 tag（base64）</summary>
    public required string WrapTag { get; init; }

    /// <summary>数据加密 nonce（base64）</summary>
    public required string Nonce { get; init; }

    /// <summary>数据密文（base64）</summary>
    public required string Ciphertext { get; init; }

    /// <summary>数据加密 tag（base64）</summary>
    public required string Tag { get; init; }

    /// <summary>信封格式版本（ev1）</summary>
    public required string FormatVersion { get; init; }
}

/// <summary>
/// AES-256-GCM 信封加密（方案 <c>docs/08-security/credential-vault-design.md</c> §5）：
/// 每条凭据值随机 256-bit DEK → DEK 加密数据（AAD 绑定引用/所有者/轮值版本）→ KEK 包封 DEK。
/// </summary>
/// <remarks>
/// <para>与存量 <see cref="AesSecretEncryptionService"/>（CBC v1/v2）完全独立，互不读解——
/// Vault 不覆盖旧格式，存量体系不感知新格式。</para>
/// <para><b>Fail Closed</b>：数据密文、nonce、tag、WrappedDek、wrapNonce、wrapTag、AAD 任一被篡改，
/// 或 KEK 不匹配 → 解密失败抛 <see cref="CryptographicException"/>（错误信息不区分失败细节、不含材料）。</para>
/// </remarks>
public static class EnvelopeSecretCrypto
{
    /// <summary>信封格式版本（写入侧唯一；未来格式演进换版本号，读取按版本分发）</summary>
    public const string FormatVersion = "ev1";

    private const int DekSizeBytes = 32;
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    /// <summary>加密明文为信封载荷。AAD 必须与解密时逐字节一致（见 <see cref="BuildAad"/>）。</summary>
    public static EnvelopePayload Encrypt(string plainText, string aad, VaultKeyMaterial kek)
    {
        ArgumentException.ThrowIfNullOrEmpty(plainText);
        ArgumentException.ThrowIfNullOrEmpty(aad);
        ArgumentNullException.ThrowIfNull(kek);
        if (kek.Key.Length != DekSizeBytes)
            throw new CryptographicException("KEK 长度异常，拒绝加密");

        var dek = RandomNumberGenerator.GetBytes(DekSizeBytes);
        try
        {
            // 1) DEK 加密数据（独立 nonce + AAD 绑定）
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
            var ciphertext = new byte[plainBytes.Length];
            var tag = new byte[TagSizeBytes];
            using (var dataGcm = new AesGcm(dek, TagSizeBytes))
            {
                dataGcm.Encrypt(nonce, plainBytes, ciphertext, tag, Encoding.UTF8.GetBytes(aad));
            }

            // 2) KEK 包封 DEK（独立 wrapNonce，防 KEK 下 nonce 重用；wrap AAD 绑定 KeyId）
            var wrapNonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
            var wrappedDek = new byte[dek.Length];
            var wrapTag = new byte[TagSizeBytes];
            using (var wrapGcm = new AesGcm(kek.Key, TagSizeBytes))
            {
                wrapGcm.Encrypt(wrapNonce, dek, wrappedDek, wrapTag, Encoding.UTF8.GetBytes(BuildWrapAad(kek.KeyId)));
            }

            return new EnvelopePayload
            {
                KeyId = kek.KeyId,
                WrappedDek = ToB64(wrappedDek),
                WrapNonce = ToB64(wrapNonce),
                WrapTag = ToB64(wrapTag),
                Nonce = ToB64(nonce),
                Ciphertext = ToB64(ciphertext),
                Tag = ToB64(tag),
                FormatVersion = FormatVersion,
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    /// <summary>
    /// 解密信封载荷。任何材料被篡改、AAD 不一致或 KEK 不匹配 → <see cref="CryptographicException"/>。
    /// </summary>
    public static string Decrypt(EnvelopePayload payload, string aad, byte[] kek)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrEmpty(aad);
        ArgumentNullException.ThrowIfNull(kek);
        if (kek.Length != DekSizeBytes)
            throw new CryptographicException("KEK 长度异常，拒绝解密");

        try
        {
            // 1) KEK 解封 DEK（wrap AAD 绑定载荷里的 KeyId）
            var dek = new byte[DekSizeBytes];
            try
            {
                using var wrapGcm = new AesGcm(kek, TagSizeBytes);
                wrapGcm.Decrypt(
                    FromB64(payload.WrapNonce),
                    FromB64(payload.WrappedDek),
                    FromB64(payload.WrapTag),
                    dek,
                    Encoding.UTF8.GetBytes(BuildWrapAad(payload.KeyId)));
            }
            catch (CryptographicException)
            {
                throw; // 统一在出口收敛，避免中途区分失败细节
            }

            try
            {
                // 2) DEK 解密数据（AAD 必须与加密时一致）
                var plainBytes = new byte[FromB64(payload.Ciphertext).Length];
                using var dataGcm = new AesGcm(dek, TagSizeBytes);
                dataGcm.Decrypt(
                    FromB64(payload.Nonce),
                    FromB64(payload.Ciphertext),
                    FromB64(payload.Tag),
                    plainBytes,
                    Encoding.UTF8.GetBytes(aad));
                return Encoding.UTF8.GetString(plainBytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(dek);
            }
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
        {
            // 统一失败面：篡改/密钥不匹配/材料损坏一律同一错误，不泄露细节
            throw new CryptographicException("凭据解密失败（密文被篡改、密钥不匹配或材料损坏）", ex);
        }
    }

    /// <summary>
    /// 数据 AAD：绑定格式版本、引用 Id、所有者与轮值版本——
    /// 行内任一绑定字段被改，解密必失败（方案 §5.3「AAD 绑定 SecretId、OwnerId、CipherVersion 和数据版本」）。
    /// </summary>
    public static string BuildAad(string refId, string ownerType, string ownerId, int rotationVersion) =>
        $"{FormatVersion}|{refId}|{ownerType}|{ownerId}|{rotationVersion}";

    /// <summary>DEK 包封 AAD：绑定格式版本与包封 KEK 的 KeyId</summary>
    public static string BuildWrapAad(string keyId) =>
        $"{FormatVersion}|wrap|{keyId}";

    private static string ToB64(byte[] bytes) => Convert.ToBase64String(bytes);

    private static byte[] FromB64(string value)
    {
        if (string.IsNullOrEmpty(value)) throw new FormatException("信封材料字段为空");
        return Convert.FromBase64String(value);
    }
}

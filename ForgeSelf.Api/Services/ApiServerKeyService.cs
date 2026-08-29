using System.Security.Cryptography;
using NewLife.Log;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Security;

namespace ForgeSelf.Api.Services;

/// <summary>
/// API 服务器密钥管理服务。
/// 密钥加密后存储在 ForgeSetting 配置中，不再使用数据库表。
/// </summary>
public class ApiServerKeyService
{
    private readonly ISecretEncryptionService _encryption;
    private readonly object _lock = new();

    public ApiServerKeyService(ISecretEncryptionService encryption)
    {
        _encryption = encryption;
    }

    /// <summary>
    /// 启动时幂等播种：ForgeSetting.ApiToken 空则生成并加密存储。
    /// </summary>
    public void EnsureApiKeySeeded()
    {
        if (!string.IsNullOrEmpty(ForgeSetting.Current.ApiToken)) return;

        var plainKey = GenerateKey();
        var cipher = _encryption.Encrypt(plainKey);

        ForgeSetting.Current.ApiToken = cipher;
        ForgeSetting.Current.Save();

        XTrace.Log.Info("API 服务器密钥已自动生成并加密存储");
    }

    /// <summary>
    /// 从 ForgeSetting 获取加密密钥的密文。
    /// </summary>
    public string? GetActiveKeyCipher() => ForgeSetting.Current.ApiToken;

    /// <summary>
    /// 从 ForgeSetting 获取明文密钥。
    /// 若密文为空或解密失败，自动生成并更新密钥。
    /// </summary>
    public string? GetActiveKeyPlain()
    {
        var cipher = ForgeSetting.Current.ApiToken;
        if (!string.IsNullOrEmpty(cipher))
        {
            try { return _encryption.Decrypt(cipher); } catch { }
        }

        // 密文为空或解密失败 → 自动生成并更新
        return AutoGenerateAndSave();
    }

    /// <summary>生成新密钥，加密保存到 ForgeSetting，返回明文</summary>
    private string AutoGenerateAndSave()
    {
        var plainKey = GenerateKey();
        var cipher = _encryption.Encrypt(plainKey);
        ForgeSetting.Current.ApiToken = cipher;
        ForgeSetting.Current.Save();
        XTrace.Log.Info("API 服务器密钥已自动重新生成并存储");
        return plainKey;
    }

    /// <summary>
    /// 获取当前密钥的掩码展示。
    /// </summary>
    public string GetActiveKeyMasked()
    {
        var plain = GetActiveKeyPlain();
        if (string.IsNullOrEmpty(plain)) return string.Empty;
        return _encryption.Mask(plain);
    }

    /// <summary>
    /// 重新生成密钥，旧密钥立即失效。
    /// </summary>
    public ApiServerKeyRegenerateResult Regenerate()
    {
        lock (_lock)
        {
            var plainKey = GenerateKey();
            var cipher = _encryption.Encrypt(plainKey);

            ForgeSetting.Current.ApiToken = cipher;
            ForgeSetting.Current.Save();

            return new ApiServerKeyRegenerateResult
            {
                PlainKey = plainKey,
                MaskedKey = _encryption.Mask(plainKey),
                AuthHeader = $"Authorization: Bearer {plainKey}",
            };
        }
    }

    /// <summary>生成密码学随机 sk-... 密钥（16字节 → 32位hex）</summary>
    private static string GenerateKey()
    {
        var bytes = new byte[16];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        var suffix = Convert.ToHexString(bytes).ToLowerInvariant();
        return $"sk-{suffix}";
    }
}

/// <summary>重新生成密钥的结果</summary>
public class ApiServerKeyRegenerateResult
{
    public string PlainKey { get; set; } = "";
    public string MaskedKey { get; set; } = "";
    public string AuthHeader { get; set; } = "";
}

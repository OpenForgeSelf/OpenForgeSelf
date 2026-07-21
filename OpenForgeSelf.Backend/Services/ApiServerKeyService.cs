using System.Security.Cryptography;
using NewLife.Log;
using OpenForgeSelf.Backend.Entities;
using OpenForgeSelf.Backend.Security;

namespace OpenForgeSelf.Backend.Services;

/// <summary>
/// API 服务器密钥管理服务。
/// 负责密钥自动生成（启动播种）、读取、轮换。
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
    /// 启动时幂等播种：表空则生成并加密存储一条活跃密钥。
    /// MUST 在 InitializeXCodeDatabase 之后执行。
    /// </summary>
    public void EnsureApiKeySeeded()
    {
        // 幂等：已有记录则跳过
        var exists = ApiServerKey.FindCount(ApiServerKey._.Id > 0) > 0;
        if (exists) return;

        var plainKey = GenerateKey();
        var cipher = _encryption.Encrypt(plainKey);

        var entity = new ApiServerKey
        {
            KeyCipher = cipher,
            IsActive = true,
            CreateTime = DateTime.UtcNow,
            UpdateTime = DateTime.UtcNow,
        };
        entity.Insert();

        XTrace.Log.Info("API 服务器密钥已自动生成并加密存储");
    }

    /// <summary>
    /// 获取当前活跃密钥的密文。
    /// 若无活跃密钥返回 null。
    /// </summary>
    public string? GetActiveKeyCipher()
    {
        var active = ApiServerKey.Find(ApiServerKey._.IsActive == true);
        return active?.KeyCipher;
    }

    /// <summary>
    /// 获取当前活跃密钥的明文。
    /// 若无活跃密钥返回 null。
    /// </summary>
    public string? GetActiveKeyPlain()
    {
        var cipher = GetActiveKeyCipher();
        if (string.IsNullOrEmpty(cipher)) return null;
        try
        {
            return _encryption.Decrypt(cipher);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 获取当前活跃密钥的掩码展示。
    /// </summary>
    public string GetActiveKeyMasked()
    {
        var plain = GetActiveKeyPlain();
        if (string.IsNullOrEmpty(plain)) return string.Empty;
        return _encryption.Mask(plain);
    }

    /// <summary>
    /// 重新生成密钥：旧密钥立即失效，新密钥加密存储。
    /// 并发防护使用简单锁，防止多次快速轮换导致旧密钥残留。
    /// </summary>
    public ApiServerKeyRegenerateResult Regenerate()
    {
        lock (_lock)
        {
            var plainKey = GenerateKey();
            var cipher = _encryption.Encrypt(plainKey);

            // 轮换策略：原地更新活跃行的 KeyCipher（见 data-model.md 决策）
            var active = ApiServerKey.Find(ApiServerKey._.IsActive == true);
            if (active != null)
            {
                // 更新现有行的密钥密文与更新时间
                active.KeyCipher = cipher;
                active.UpdateTime = DateTime.UtcNow;
                active.Update();
            }
            else
            {
                // 无活跃行则新建
                active = new ApiServerKey
                {
                    KeyCipher = cipher,
                    IsActive = true,
                    CreateTime = DateTime.UtcNow,
                    UpdateTime = DateTime.UtcNow,
                };
                active.Insert();
            }

            return new ApiServerKeyRegenerateResult
            {
                PlainKey = plainKey,
                MaskedKey = _encryption.Mask(plainKey),
                AuthHeader = $"Authorization: Bearer {plainKey}",
            };
        }
    }

    /// <summary>生成密码学随机 sk-... 密钥</summary>
    private static string GenerateKey()
    {
        var bytes = new byte[32];
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

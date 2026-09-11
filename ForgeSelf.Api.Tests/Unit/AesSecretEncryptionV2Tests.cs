using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using ForgeSelf.Api.Security;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// 密文版本化（v1 → v2）测试。
/// </summary>
/// <remarks>
/// 两个关键回归防线：
/// <list type="number">
/// <item><see cref="Decrypt_旧v1硬编码密文_仍能解开"/>：旧硬编码密钥一旦被移除或 v1 回退链一旦断裂立即变红。</item>
/// <item><see cref="Decrypt_v1_错误候选偶然通过padding校验时_必须返回真实明文"/>：
/// 错误密钥偶然通过 PKCS7 padding 的概率约 1/256，若实现「第一个不抛异常就返回」，
/// 就会把乱码当明文返回（并在迁移路径上覆盖原值造成静默数据丢失）。</item>
/// </list>
/// </remarks>
public class AesSecretEncryptionV2Tests
{
    /// <summary>
    /// 用旧硬编码兼容键 <c>Sha256("ForgeSelf-AIProvider-Default-Encryption-Key")</c> 预生成的 v1 密文。
    /// 固定常量，不随环境变化，保证测试断言的是「老数据可读」这一产品契约。
    /// </summary>
    public const string LegacyV1Cipher =
        "AQIDBAUGBwgJCgsMDQ4PENBCeTVMh1ehopHRiC2FtCmTxHTvRlWmhzaFZZ0VlOrekdP/EskRoHvNjkF5qosW/Q==";

    /// <summary><see cref="LegacyV1Cipher"/> 对应的明文</summary>
    public const string LegacyPlainKey = "sk-00000000000000000000000000000001";

    /// <summary>旧硬编码密钥源（与 AesSecretEncryptionService 内的兼容键常量一致）</summary>
    private const string LegacyKeySource = "ForgeSelf-AIProvider-Default-Encryption-Key";

    /// <summary>测试用当前密钥源（经 Encryption:Key 注入，固定为已知串以便自造密文）</summary>
    private const string CurrentKeySource = "unit-test-current-key";

    /// <summary>固定种子：让「随机搜索 padding 碰撞」的构造过程可复现</summary>
    private const int SearchSeed = 20260910;

    /// <summary>搜索上限：期望约 256 次命中，给足冗余</summary>
    private const int MaxSearchAttempts = 20000;

    private static AesSecretEncryptionService CreateDefaultService()
        => new(new ConfigurationBuilder().Build(), new MachineKeyProvider());

    /// <summary>构造一个「当前密钥固定为已知串」的加密服务，便于在测试里自造两侧密文</summary>
    private static AesSecretEncryptionService CreateServiceWithKey(string keySource)
        => new(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:Key"] = keySource,
            }).Build(),
            new MachineKeyProvider());

    #region 基础行为

    [Fact]
    public void Encrypt_空配置_输出v2前缀密文()
    {
        var service = CreateDefaultService();

        var cipher = service.Encrypt("sk-plain-value");

        cipher.Should().StartWith(AesSecretEncryptionService.V2Prefix, "写路径永远只产 v2");
    }

    [Fact]
    public void Encrypt_Decrypt_往返一致()
    {
        var service = CreateDefaultService();

        var cipher = service.Encrypt(LegacyPlainKey);

        service.Decrypt(cipher).Should().Be(LegacyPlainKey);
    }

    [Fact]
    public void Decrypt_旧v1硬编码密文_仍能解开()
    {
        var service = CreateDefaultService();

        LegacyV1Cipher.Should().NotStartWith(AesSecretEncryptionService.V2Prefix);
        service.Decrypt(LegacyV1Cipher).Should().Be(LegacyPlainKey);
    }

    [Fact]
    public void Decrypt_v1重封装为v2后_仍然可读()
    {
        var service = CreateDefaultService();

        var plain = service.Decrypt(LegacyV1Cipher);
        var rewrapped = service.Encrypt(plain);

        rewrapped.Should().StartWith(AesSecretEncryptionService.V2Prefix);
        service.Decrypt(rewrapped).Should().Be(LegacyPlainKey);
    }

    [Fact]
    public void TryDecrypt_另一机器密钥加密的v2密文_返回false()
    {
        var other = CreateServiceWithKey("another-machine-secret");
        var foreignCipher = other.Encrypt(LegacyPlainKey);

        var service = CreateDefaultService();

        service.TryDecrypt(foreignCipher, out var plain).Should().BeFalse();
        plain.Should().BeEmpty();
    }

    [Fact]
    public void TryDecrypt_损坏的v1密文_返回false()
    {
        var service = CreateDefaultService();
        var corrupted = Convert.ToBase64String(new byte[64]);

        service.TryDecrypt(corrupted, out var plain).Should().BeFalse();
        plain.Should().BeEmpty();
    }

    [Fact]
    public void TryDecrypt_旧v1密文_返回true且明文正确()
    {
        var service = CreateDefaultService();

        service.TryDecrypt(LegacyV1Cipher, out var plain).Should().BeTrue();
        plain.Should().Be(LegacyPlainKey);
    }

    [Fact]
    public void TryDecrypt_空密文_返回false不抛异常()
    {
        var service = CreateDefaultService();

        service.TryDecrypt("", out var plain).Should().BeFalse();
        plain.Should().BeEmpty();
    }

    [Fact]
    public void Mask_保留前3后4_中间遮盖()
    {
        var service = CreateDefaultService();

        service.Mask("sk-00000000000000000000000000000001").Should().Be("sk-****0001");
        service.Mask("").Should().BeEmpty();
    }

    #endregion

    #region v1 padding 误判（HIGH 缺陷回归）

    [Fact]
    public void Decrypt_v1_错误候选偶然通过padding校验时_必须返回真实明文()
    {
        var service = CreateServiceWithKey(CurrentKeySource);
        var crafted = FindPaddingCollisionCipher(LegacyPlainKey, CurrentKeySource, out var wrongCandidatePlain);

        // 前置条件自证：当前密钥确实「padding 合法但解出乱码」→ 本用例真的落在 bug 触发点上
        wrongCandidatePlain.Should().NotBe(LegacyPlainKey);
        IsPlausiblePlaintext(wrongCandidatePlain).Should().BeFalse(
            "错误密钥解出的应是乱码（含控制字符或 U+FFFD），否则用例构造无效");

        // 修复前：第一个候选不抛异常即返回 → 返回乱码（RED）
        // 修复后：乱码被合理性判据拒绝，回退到真正的旧兼容键 → 返回真实明文（GREEN）
        service.Decrypt(crafted).Should().Be(LegacyPlainKey);
    }

    [Fact]
    public void Decrypt_v1_只采信合理明文_绝不返回乱码()
    {
        var service = CreateServiceWithKey(CurrentKeySource);
        var crafted = FindPaddingCollisionCipher(LegacyPlainKey, CurrentKeySource, out var wrongCandidatePlain);

        var result = service.Decrypt(crafted);

        result.Should().Be(LegacyPlainKey);
        result.Should().NotBe(wrongCandidatePlain, "绝不把错误候选解出的乱码当明文返回");
        IsPlausiblePlaintext(result).Should().BeTrue();
    }

    [Fact]
    public void Decrypt_v1_任何候选都解不出合理明文_抛CryptographicException()
    {
        var service = CreateServiceWithKey(CurrentKeySource);

        var random = new Random(SearchSeed + 1);
        var payload = new byte[64];
        random.NextBytes(payload);
        var cipher = Convert.ToBase64String(payload);

        var act = () => service.Decrypt(cipher);

        act.Should().Throw<CryptographicException>();
    }

    #endregion

    #region 测试辅助（供 SecretMigrationServiceTests 复用）

    /// <summary>
    /// 构造一个 v1 密文：用旧兼容键加密 <paramref name="truePlain"/>，
    /// 但用 <paramref name="currentKeySource"/> 派生的当前密钥解它时 padding 恰好也合法（概率约 1/256）。
    /// </summary>
    /// <param name="truePlain">真实明文</param>
    /// <param name="currentKeySource">当前密钥源（Encryption:Key 的值）</param>
    /// <param name="wrongCandidatePlain">当前密钥侧解出的乱码</param>
    /// <returns>base64(IV ‖ 密文) 形式的 v1 密文</returns>
    internal static string FindPaddingCollisionCipher(string truePlain, string currentKeySource, out string wrongCandidatePlain)
    {
        var legacyKey = Sha256(LegacyKeySource);
        var currentKey = Sha256(currentKeySource);
        var random = new Random(SearchSeed);

        for (var attempt = 0; attempt < MaxSearchAttempts; attempt++)
        {
            var iv = new byte[16];
            random.NextBytes(iv);

            var cipher = EncryptWithKey(truePlain, legacyKey, iv);

            if (!TryDecryptRaw(cipher, currentKey, out var garbage)) continue;          // padding 不合法 → 不构成碰撞
            if (IsPlausiblePlaintext(garbage)) continue;                              // 乱码碰巧合理 → 换一个，保证用例语义单一

            wrongCandidatePlain = garbage;
            return cipher;
        }

        throw new InvalidOperationException($"未能在 {MaxSearchAttempts} 次内构造出 padding 碰撞密文");
    }

    /// <summary>调用 <see cref="AesSecretEncryptionService"/> 的明文合理性判据（internal，经反射访问）</summary>
    internal static bool IsPlausiblePlaintext(string s)
    {
        var method = typeof(AesSecretEncryptionService)
            .GetMethod("IsPlausiblePlaintext", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull("明文合理性判据必须存在");
        return (bool)method!.Invoke(null, new object?[] { s })!;
    }

    /// <summary>用指定密钥与 IV 做 AES-256-CBC-PKCS7 加密，输出 base64(IV ‖ 密文)</summary>
    private static string EncryptWithKey(string plain, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        var bytes = Encoding.UTF8.GetBytes(plain);
        using var encryptor = aes.CreateEncryptor();
        var cipher = encryptor.TransformFinalBlock(bytes, 0, bytes.Length);

        var buffer = new byte[iv.Length + cipher.Length];
        iv.CopyTo(buffer, 0);
        cipher.CopyTo(buffer, iv.Length);
        return Convert.ToBase64String(buffer);
    }

    /// <summary>用指定密钥裸解一次（不做多候选回退），padding 不合法时返回 false</summary>
    private static bool TryDecryptRaw(string cipherText, byte[] key, out string plain)
    {
        plain = string.Empty;
        try
        {
            var data = Convert.FromBase64String(cipherText);
            using var aes = Aes.Create();
            aes.Key = key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            var iv = new byte[16];
            Array.Copy(data, 0, iv, 0, iv.Length);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream(data, iv.Length, data.Length - iv.Length);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            plain = sr.ReadToEnd();
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static byte[] Sha256(string source) => SHA256.HashData(Encoding.UTF8.GetBytes(source));

    #endregion
}

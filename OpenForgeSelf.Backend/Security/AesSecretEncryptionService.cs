using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace OpenForgeSelf.Backend.Security;

/// <summary>
/// 基于 AES-256-CBC 的密钥加解密实现。
/// 密钥来源优先级：配置 Encryption:Key > 环境变量 OPENFORGE_ENCRYPTION_KEY > 内置固定密钥（仅开发默认，生产须覆盖）。
/// 密文格式：base64(IV[16字节] + 密文)，每次加密使用随机 IV，解密时从密文前缀还原。
/// </summary>
public class AesSecretEncryptionService : ISecretEncryptionService
{
    private readonly byte[] _key;

    public AesSecretEncryptionService(IConfiguration configuration)
    {
        _key = ResolveKey(configuration);
    }

    /// <summary>加密明文，返回 base64 密文（含前缀 IV）</summary>
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        using var ms = new MemoryStream();
        // 写入 IV 前缀，便于解密时还原
        ms.Write(aes.IV, 0, aes.IV.Length);
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs))
        {
            sw.Write(plainText);
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    /// <summary>解密密文，返回原始明文</summary>
    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;

        var data = Convert.FromBase64String(cipherText);
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        // 从密文前缀读取 IV
        var ivLength = aes.BlockSize / 8;
        var iv = new byte[ivLength];
        Array.Copy(data, 0, iv, 0, ivLength);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream(data, ivLength, data.Length - ivLength);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);
        {
            return sr.ReadToEnd();
        }
    }

    /// <summary>生成掩码展示，如 sk-****1234（保留前缀 3 位与末尾 4 位，中间以 **** 遮盖）</summary>
    public string Mask(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;

        const int head = 3;
        const int tail = 4;
        if (plainText.Length <= head + tail) return "****" + plainText[^Math.Min(tail, plainText.Length)..];

        return plainText[..head] + "****" + plainText[^tail..];
    }

    /// <summary>解析加密密钥：配置/环境变量优先，均未设置时回退内置固定密钥（开发默认）</summary>
    private static byte[] ResolveKey(IConfiguration configuration)
    {
        var keySetting = configuration["Encryption:Key"];
        if (!string.IsNullOrWhiteSpace(keySetting)) return DeriveKey(keySetting);

        var envKey = Environment.GetEnvironmentVariable("OPENFORGE_ENCRYPTION_KEY");
        if (!string.IsNullOrWhiteSpace(envKey)) return DeriveKey(envKey);

        // 开发默认固定密钥；生产环境务必通过 Encryption:Key 或环境变量覆盖，避免密钥硬编码泄露
        return DeriveKey("OpenForgeSelf-AIProvider-Default-Encryption-Key");
    }

    /// <summary>将任意长度密钥源派生为固定 32 字节 AES 密钥</summary>
    private static byte[] DeriveKey(string source)
    {
        using var sha = SHA256.Create();
        return sha.ComputeHash(Encoding.UTF8.GetBytes(source));
    }
}

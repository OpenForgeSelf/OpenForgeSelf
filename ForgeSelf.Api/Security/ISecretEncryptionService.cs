namespace ForgeSelf.Api.Security;

/// <summary>
/// 密钥加解密服务。用于敏感凭证（如 AI Provider 的 ApiKey）落库加密与读取解密，以及前端展示掩码。
/// </summary>
public interface ISecretEncryptionService
{
    /// <summary>加密明文，返回可安全存储的密文（base64 字符串）</summary>
    string Encrypt(string plainText);

    /// <summary>解密密文，返回原始明文</summary>
    string Decrypt(string cipherText);

    /// <summary>生成掩码展示，如 sk-****1234（保留前缀与末尾若干字符，中间以 **** 遮盖）</summary>
    string Mask(string plainText);
}

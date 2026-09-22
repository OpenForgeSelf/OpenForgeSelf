namespace ForgeSelf.Abstractions;

/// <summary>
/// 密钥加解密服务契约（宿主实现，插件共用）。用于敏感凭证（如 AI Provider 的 ApiKey、API 子密钥、IM 网关密钥）落库加密与读取解密，以及前端展示掩码。
/// 宿主实现：<c>ForgeSelf.Api.Security.AesSecretEncryptionService</c>（AES-256-CBC，密文版本化）。
/// 插件经 <c>IContext.Get&lt;ISecretEncryptionService&gt;()</c> 获取——与 <see cref="IChatCompletion"/> 同构。
/// </summary>
/// <remarks>
/// 密文具备版本化前缀：<c>v2:</c> 前缀表示「当前密钥加密」；无前缀（v1）表示历史密文，
/// 解密时按候选密钥链回退，保证老数据永久可读。
/// </remarks>
public interface ISecretEncryptionService
{
    /// <summary>加密明文，返回可安全存储的密文（带 <c>v2:</c> 前缀的 base64 字符串）</summary>
    string Encrypt(string plainText);

    /// <summary>解密密文，返回原始明文；无法解密时抛 <see cref="System.Security.Cryptography.CryptographicException"/></summary>
    string Decrypt(string cipherText);

    /// <summary>
    /// 尝试解密密文，失败返回 false 而不抛异常。
    /// 供启动期的密文迁移扫描使用——迁移绝不能因单条坏数据中断，也不该用异常流控制流程。
    /// </summary>
    /// <param name="cipherText">密文</param>
    /// <param name="plainText">解密成功时的明文；失败时为 <see cref="string.Empty"/></param>
    /// <returns>是否解密成功</returns>
    bool TryDecrypt(string cipherText, out string plainText);

    /// <summary>生成掩码展示，如 sk-****1234（保留前缀与末尾若干字符，中间以 **** 遮盖）</summary>
    string Mask(string plainText);
}

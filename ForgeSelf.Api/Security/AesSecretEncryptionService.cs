using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using ForgeSelf.Abstractions;
using NewLife.Log;

namespace ForgeSelf.Api.Security;

/// <summary>
/// 基于 AES-256-CBC 的密钥加解密实现（密文版本化）。
/// </summary>
/// <remarks>
/// <para><b>密钥来源优先级</b>：配置 <c>Encryption:Key</c> &gt; 环境变量 <c>FORGESELF_ENCRYPTION_KEY</c> &gt; 机器派生密钥。
/// 第三优先级由 <see cref="IMachineKeyProvider"/> 提供（PBKDF2 派生于本机 MachineGuid），
/// 使默认安装下的密文「出本机即不可解」，彻底替代原先写死在代码里的固定密钥。</para>
/// <para><b>密文版本化（读兼容永久保留，写只出 v2）</b>：</para>
/// <list type="bullet">
/// <item>v2（新）：<c>"v2:" + base64(IV[16] + 密文)</c>，用当前密钥加密与解密，失败即抛。</item>
/// <item>v1（旧）：<c>base64(IV[16] + 密文)</c>，无前缀。解密时按「当前密钥 → 旧硬编码兼容键」依次尝试，
/// 保证升级前落库/落配置文件的全部老密文（尤其是 AIProvider.ApiKey）<b>永久可读</b>。</item>
/// </list>
/// 旧硬编码串 <c>ForgeSelf-AIProvider-Default-Encryption-Key</c> 已降级为「仅用于解 v1 旧密文的兼容键」，
/// <b>永不再用于加密任何新数据</b>，这是老数据可读的唯一机制，不得删除。
/// </remarks>
public class AesSecretEncryptionService : ISecretEncryptionService
{
    /// <summary>v2 密文前缀（3 字节 ASCII），用于区分新旧密文</summary>
    public const string V2Prefix = "v2:";

    /// <summary>
    /// 旧硬编码密钥源。<b>仅解密兼容用途</b>：历史上未配置任何密钥的实例用它加密过数据，
    /// 保留它才能解开这些 v1 密文。绝不可再用于加密新数据。
    /// </summary>
    private const string LegacyKeySource = "ForgeSelf-AIProvider-Default-Encryption-Key";

    /// <summary>当前密钥（配置 &gt; 环境变量 &gt; 机器派生）</summary>
    private readonly byte[] _key;

    /// <summary>旧硬编码兼容键，仅参与 v1 密文的解密回退</summary>
    private readonly byte[] _legacyKey;

    public AesSecretEncryptionService(IConfiguration configuration, IMachineKeyProvider machineKeyProvider)
    {
        _key = ResolveKey(configuration, machineKeyProvider);
        _legacyKey = DeriveKey(LegacyKeySource);
    }

    /// <summary>加密明文，返回带 <c>v2:</c> 前缀的 base64 密文（每次随机 IV）</summary>
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;

        // 写路径永远只产 v2：明文→密文长度不变，仅多 3 个字符前缀
        return V2Prefix + EncryptCore(plainText, _key);
    }

    /// <summary>
    /// 解密密文。v2 只用当前密钥解（失败即抛）；v1 遍历全部候选密钥，按「明文合理性」筛选后决策。
    /// </summary>
    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;

        if (cipherText.StartsWith(V2Prefix, StringComparison.Ordinal))
        {
            // v2：不存在「其它机器」的可能，解不开就是真解不开，不回退
            return DecryptCore(cipherText[V2Prefix.Length..], _key);
        }

        // v1 旧密文：候选 1 覆盖装过 Encryption:Key/环境变量的历史实例，候选 2 覆盖默认安装实例。
        //
        // ⚠️ 这里**不能**「第一个不抛异常就返回」：错误密钥偶然通过 PKCS7 padding 校验的概率约 1/256，
        // 一旦命中就会把乱码当成明文返回，而不再回退到真正正确的候选。曾因此导致迁移路径
        // 把原明文用乱码覆盖写回（静默数据丢失）。故必须遍历全部候选 + 明文合理性过滤 + 不猜。
        var plausible = new List<string>();
        foreach (var candidate in GetV1CandidateKeys())
        {
            string plain;
            try
            {
                plain = DecryptCore(cipherText, candidate);
            }
            catch (CryptographicException)
            {
                // padding 不合法 → 该候选必然不对，换下一个（padding 校验失败仍是有效的否定判据）
                continue;
            }

            // padding 合法只是必要条件：还要排除「错误密钥偶然通过 padding」产生的乱码
            if (IsPlausiblePlaintext(plain)) plausible.Add(plain);
        }

        // 0 个 → 解不开；1 个 → 采信；>1 个 → 无法区分，拒绝猜测
        if (plausible.Count == 1) return plausible[0];

        if (plausible.Count > 1)
            throw new CryptographicException("v1 密文存在多个候选密钥且均解出合理明文，拒绝猜测");

        throw new CryptographicException("密文无法在本机解密（可能为其它机器加密或已损坏）");
    }

    /// <summary>
    /// 判断解密结果是否是「像样的明文」——用于在 v1 多候选回退中排除「错误密钥偶然通过 padding 校验」的乱码。
    /// </summary>
    /// <remarks>
    /// <para>错误密钥偶然通过 PKCS7 padding 校验的概率约 <b>1/256</b>；而随机字节经 UTF-8 解码
    /// 几乎必然产生替换字符（<c>U+FFFD</c>）或控制字符，故本判据把误判概率压到可忽略量级。</para>
    /// <para><b>刻意不校验密钥格式</b>：<c>AIProvider.ApiKey</c> 是用户填入的第三方密钥，
    /// 可能是 <c>AIza…</c>、<c>sk-ant-…</c>，甚至本地随意串，只能做「文本合理性」判断，
    /// 不能要求 <c>sk-</c> 前缀，否则会造成合法老密文被判为不可解。</para>
    /// </remarks>
    /// <param name="s">候选明文</param>
    /// <returns>是否可采信</returns>
    internal static bool IsPlausiblePlaintext(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;

        foreach (var ch in s)
        {
            // U+FFFD：StreamReader 的 UTF-8 解码替换字符，乱码几乎必然出现
            if (ch == '\uFFFD') return false;

            // 除常用空白外的控制字符：正常密钥串不会出现
            if (char.IsControl(ch) && ch != '\t' && ch != '\r' && ch != '\n') return false;
        }

        return true;
    }

    /// <inheritdoc />
    public bool TryDecrypt(string cipherText, out string plainText)
    {
        plainText = string.Empty;
        if (string.IsNullOrEmpty(cipherText)) return false;

        try
        {
            var result = Decrypt(cipherText);
            if (string.IsNullOrEmpty(result)) return false;

            plainText = result;
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Debug("密文解密失败（迁移跳过）: {0}", ex.Message);
            return false;
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

    /// <summary>
    /// 取得 v1 密文的候选解密密钥：当前密钥 + 旧硬编码兼容键（去重）。
    /// 顺序不可颠倒——先试当前密钥，性能更好（覆盖历史实例的概率也更高）。
    /// 注意：候选会被<b>全部</b>尝试而非「命中即返回」，因为单个候选的 padding 校验结果不足以采信
    /// （错误密钥偶然通过 padding 的概率约 1/256，详见 <see cref="Decrypt"/> 内注释）。
    /// </summary>
    private IEnumerable<byte[]> GetV1CandidateKeys()
    {
        yield return _key;

        if (!_legacyKey.AsSpan().SequenceEqual(_key)) yield return _legacyKey;
    }

    /// <summary>
    /// 解析当前加密密钥：配置 &gt; 环境变量 &gt; 机器派生密钥。
    /// 机器派生只在真正需要时才触发（PBKDF2 210k 迭代约 100~200ms）。
    /// </summary>
    private static byte[] ResolveKey(IConfiguration configuration, IMachineKeyProvider machineKeyProvider)
    {
        var keySetting = configuration?["Encryption:Key"];
        if (!string.IsNullOrWhiteSpace(keySetting)) return DeriveKey(keySetting);

        var envKey = Environment.GetEnvironmentVariable("FORGESELF_ENCRYPTION_KEY");
        if (!string.IsNullOrWhiteSpace(envKey)) return DeriveKey(envKey);

        // 逃生舱末端：本机派生（出本机即不可解）
        return machineKeyProvider.GetMachineKey();
    }

    /// <summary>
    /// 核心加密：随机 IV + AES-256-CBC-PKCS7，输出 base64(IV ‖ 密文)。
    /// </summary>
    private static string EncryptCore(string plainText, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV();

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

    /// <summary>
    /// 核心解密：从密文前缀还原 IV，再用给定密钥做 AES-256-CBC-PKCS7 解密。
    /// 密钥错误时由框架的 padding 校验抛出 <see cref="CryptographicException"/>。
    /// </summary>
    private static string DecryptCore(string payload, byte[] key)
    {
        var data = Convert.FromBase64String(payload);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        var ivLength = aes.BlockSize / 8;
        // 只有 IV 没有密文载荷：不是合法密文，明确抛错（避免错误密钥解出空串被误判为成功）
        if (data.Length <= ivLength) throw new CryptographicException("密文长度不足，缺少加密载荷");

        var iv = new byte[ivLength];
        Array.Copy(data, 0, iv, 0, ivLength);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream(data, ivLength, data.Length - ivLength);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);
        return sr.ReadToEnd();
    }

    /// <summary>将任意长度密钥源派生为固定 32 字节 AES 密钥</summary>
    private static byte[] DeriveKey(string source)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(source));
    }
}

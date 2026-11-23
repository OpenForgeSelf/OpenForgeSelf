using System.Security.Cryptography;
using NewLife.Log;

namespace ForgeSelf.Api.Security;

/// <summary>
/// 凭据保险库 KEK 提供器（随机主密钥 + 平台保护持久化）。
/// </summary>
/// <remarks>
/// <para><b>密钥来源</b>：首次使用时 <see cref="RandomNumberGenerator"/> 生成 32 字节随机密钥，
/// 不从产品名、机器名或任何固定字符串构造（与 <see cref="MachineKeyProvider"/> 的机器派生路线刻意不同，
/// 满足方案 §5「机器 ID 不能作为唯一高熵秘密」）。</para>
/// <para><b>保护形态</b>：</para>
/// <list type="bullet">
/// <item>Windows：DPAPI（<c>DataProtectionScope.LocalMachine</c> + 应用常量熵）保护后落
/// <c>{keyDir}/vault-kek.v1.bin</c>。选 LocalMachine 而非 CurrentUser：宿主会以桌面交互与
/// Windows 服务（LocalSystem）两种形态运行，CurrentUser 会让「服务模式读不到桌面形态写的 KEK」——
/// 与 IMachineKeyProvider 排除用户 SID 是同一教训；应用常量熵仅做域分隔，非秘密。</item>
/// <item>非 Windows：0600 文件保存 + 启动 WARN（方案 §12 已把 Secret Service/systemd credentials
/// 列为未决风险，后续批次接入；此处 Fail Safe 到「显式告知的文件权限保护」，不静默、不回退明文常量）。</item>
/// </list>
/// <para><b>Fail Closed</b>：KEK 文件缺失→生成；文件损坏/解封失败/长度异常→抛异常，
/// 绝不降级为明文常量或源码默认密钥。</para>
/// </remarks>
public class VaultKeyProvider : ISecretKeyProvider
{
    /// <summary>KEK 文件名（与 SQLite 数据库文件分目录，默认 {数据根}/keys/）</summary>
    public const string KeyFileName = "vault-kek.v1.bin";

    /// <summary>KEK 长度（32 字节 = AES-256）</summary>
    private const int KeySizeBytes = 32;

    /// <summary>DPAPI 附加熵（应用常量，非秘密，仅域分隔——防止同机其它产品用 DPAPI 产出可互换的密文）</summary>
    private static readonly byte[] DpapiEntropy = "ForgeSelf.CredentialVault.v1.DpapiEntropy"u8.ToArray();

    /// <summary>保护形态常量</summary>
    public const string ModeDpapi = "dpapi";
    public const string ModeProtectedFile = "protected-file";

    private readonly string _keyFilePath;
    private readonly Lazy<VaultKeyMaterial> _material;

    /// <param name="keyDirectory">KEK 存放目录（必须与数据库目录分离；AppBuilder 传 {数据根}/keys）</param>
    public VaultKeyProvider(string keyDirectory)
    {
        if (string.IsNullOrWhiteSpace(keyDirectory))
            throw new ArgumentException("KEK 目录不能为空", nameof(keyDirectory));

        _keyFilePath = Path.Combine(keyDirectory, KeyFileName);
        _material = new Lazy<VaultKeyMaterial>(
            LoadOrCreate, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc />
    public VaultKeyMaterial GetCurrentKey() => _material.Value;

    private VaultKeyMaterial LoadOrCreate()
    {
        var dir = Path.GetDirectoryName(_keyFilePath);
        if (string.IsNullOrEmpty(dir)) throw new CryptographicException("KEK 文件路径异常：" + _keyFilePath);
        Directory.CreateDirectory(dir);

        byte[] key;
        string mode;

        if (OperatingSystem.IsWindows())
        {
            mode = ModeDpapi;
            if (File.Exists(_keyFilePath))
            {
                key = ProtectedData.Unprotect(File.ReadAllBytes(_keyFilePath), DpapiEntropy, DataProtectionScope.LocalMachine);
            }
            else
            {
                key = CreateKey();
                File.WriteAllBytes(_keyFilePath, ProtectedData.Protect(key, DpapiEntropy, DataProtectionScope.LocalMachine));
            }
        }
        else
        {
            mode = ModeProtectedFile;
            if (File.Exists(_keyFilePath))
            {
                key = File.ReadAllBytes(_keyFilePath);
            }
            else
            {
                key = CreateKey();
                File.WriteAllBytes(_keyFilePath, key);
            }

            TryRestrictUnixPermissions(_keyFilePath);
            XTrace.Log.Warn("凭据保险库：非 Windows 环境，KEK 以 0600 文件保护（{0}）；建议后续接入 Secret Service / systemd credentials", _keyFilePath);
        }

        if (key.Length != KeySizeBytes)
            throw new CryptographicException($"KEK 文件长度异常（{key.Length} != {KeySizeBytes}），Fail Closed：{Path.GetFileName(_keyFilePath)}");

        XTrace.Log.Info("凭据保险库 KEK 就绪（KeyId={0}，形态={1}）", BuildKeyId(key), mode);
        return new VaultKeyMaterial { KeyId = BuildKeyId(key), Key = key, ProtectionMode = mode };
    }

    /// <summary>KeyId = kek1- + SHA256(密钥) 前 8 位小写 hex（随密文落库，轮换后可识别旧包封密钥）</summary>
    internal static string BuildKeyId(byte[] key) =>
        "kek1-" + Convert.ToHexString(SHA256.HashData(key))[..8].ToLowerInvariant();

    private static byte[] CreateKey() => RandomNumberGenerator.GetBytes(KeySizeBytes);

    private static void TryRestrictUnixPermissions(string path)
    {
        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception ex)
        {
            // 权限收紧失败不阻断（部分文件系统不支持），但必须留痕
            XTrace.Log.Warn("凭据保险库：KEK 文件权限收紧失败（请人工检查 {0}）: {1}", path, ex.Message);
        }
    }
}

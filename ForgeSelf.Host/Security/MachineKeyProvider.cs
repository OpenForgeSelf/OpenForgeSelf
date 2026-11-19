using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using NewLife.Log;

namespace ForgeSelf.Api.Security;

/// <summary>
/// 机器派生密钥提供器实现。
/// 熵源按序取第一个可用值：Windows 注册表 MachineGuid → /etc/machine-id → /var/lib/dbus/machine-id → 计算机名（兜底并 WARN）。
/// 派生算法固定为 PBKDF2-HMAC-SHA256：密码 = "ForgeSelf|" + 熵源，盐为应用固定常量，迭代 210000 次，输出 32 字节。
/// </summary>
/// <remarks>
/// 盐是固定常量而非秘密，作用仅在于「域分隔」，避免与采用同样熵源的其它产品派生出相同密钥。
/// 迭代次数取 OWASP 对 PBKDF2-HMAC-SHA256 的推荐下限，210k 次约耗时 100~200ms，
/// 故用 <see cref="Lazy{T}"/> 缓存，整个进程只付一次代价。
/// </remarks>
public class MachineKeyProvider : IMachineKeyProvider
{
    /// <summary>熵字符串前缀，避免与其它同熵源产品的派生结果撞车</summary>
    private const string EntropyPrefix = "ForgeSelf|";

    /// <summary>PBKDF2 盐（应用固定常量，非秘密，仅做域分隔）</summary>
    private const string Pbkdf2Salt = "ForgeSelf.SecretEncryption.v2.MachineBound";

    /// <summary>PBKDF2 迭代次数（OWASP 对 PBKDF2-HMAC-SHA256 的推荐下限）</summary>
    private const int Pbkdf2Iterations = 210_000;

    /// <summary>派生输出长度（32 字节 = AES-256 密钥）</summary>
    private const int KeySizeBytes = 32;

    /// <summary>Windows 机器标识注册表路径（HKLM，机器级；LocalSystem 与用户会话读到同一值）</summary>
    private const string MachineGuidRegistryPath = @"SOFTWARE\Microsoft\Cryptography";

    /// <summary>Windows 机器标识注册表值名</summary>
    private const string MachineGuidValueName = "MachineGuid";

    /// <summary>Linux 机器标识文件（systemd 新发行版）</summary>
    private const string MachineIdFileEtc = "/etc/machine-id";

    /// <summary>Linux 机器标识文件（dbus，老发行版回退）</summary>
    private const string MachineIdFileDbus = "/var/lib/dbus/machine-id";

    /// <summary>指纹长度（SHA256 前 8 位 hex）</summary>
    private const int FingerprintLength = 8;

    private readonly Lazy<byte[]> _key;

    public MachineKeyProvider()
    {
        _key = new Lazy<byte[]>(() =>
        {
            var key = Derive(CollectEntropy());
            XTrace.Log.Info("机器派生密钥已就绪（指纹={0}）", ComputeFingerprint(key));
            return key;
        }, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc />
    public byte[] GetMachineKey()
    {
        // 返回副本，避免调用方修改内部缓存的密钥
        return (byte[])_key.Value.Clone();
    }

    /// <inheritdoc />
    public string GetKeyFingerprint() => ComputeFingerprint(_key.Value);

    /// <summary>
    /// 采集机器熵源：按优先级取第一个可用值，全部不可用时回退计算机名并记 WARN。
    /// 任一来源读取失败（非 Windows、权限不足、键值缺失、文件不存在）一律静默降级，不抛异常。
    /// </summary>
    /// <returns>熵字符串（已加 "ForgeSelf|" 前缀）</returns>
    private static string CollectEntropy()
    {
        var value = TryReadWindowsMachineGuid();

        if (string.IsNullOrWhiteSpace(value)) value = TryReadMachineIdFile(MachineIdFileEtc);
        if (string.IsNullOrWhiteSpace(value)) value = TryReadMachineIdFile(MachineIdFileDbus);

        if (string.IsNullOrWhiteSpace(value))
        {
            // 最后兜底：计算机名稳定性弱于前三者（改名/加域会变），故明确告警
            value = Environment.MachineName;
            XTrace.Log.Warn("机器密钥：未能读取稳定的机器标识，已回退到计算机名（稳定性较弱，改名后旧密文将不可解）");
        }

        return EntropyPrefix + value;
    }

    /// <summary>
    /// 读取 Windows 机器标识（HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid）。
    /// 非 Windows 平台或读取失败一律返回 null，由调用方降级。
    /// </summary>
    private static string? TryReadWindowsMachineGuid()
    {
        // 用平台守卫替代 [SupportedOSPlatform]，避免非 Windows 构建的 CA1416 告警
        if (!OperatingSystem.IsWindows()) return null;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(MachineGuidRegistryPath);
            return key?.GetValue(MachineGuidValueName) as string;
        }
        catch (Exception ex)
        {
            XTrace.Log.Debug("机器密钥：读取 MachineGuid 失败，降级到下一熵源: {0}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// 读取 Linux 机器标识文件首行。文件不存在或不可读时返回 null。
    /// </summary>
    private static string? TryReadMachineIdFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;

            var text = File.ReadAllText(path).Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }
        catch (Exception ex)
        {
            XTrace.Log.Debug("机器密钥：读取 {0} 失败，降级到下一熵源: {1}", path, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// 用 PBKDF2-HMAC-SHA256 把熵字符串派生为 32 字节密钥。
    /// </summary>
    /// <param name="entropy">熵字符串（调用方已加前缀）</param>
    /// <returns>32 字节密钥</returns>
    private static byte[] Derive(string entropy)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(entropy),
            Encoding.UTF8.GetBytes(Pbkdf2Salt),
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            KeySizeBytes);
    }

    /// <summary>
    /// 计算密钥指纹：<c>SHA256(密钥)</c> 的前 8 位小写 hex。
    /// 仅用于日志，不泄露密钥本体。
    /// </summary>
    private static string ComputeFingerprint(byte[] key)
    {
        var hash = SHA256.HashData(key);
        return Convert.ToHexString(hash)[..FingerprintLength].ToLowerInvariant();
    }
}

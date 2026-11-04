using System.Security.Cryptography;
using System.Text;

namespace ForgeSelf.Api.Plugins.DbAdmin.Services;

/// <summary>敏感字段加解密契约（后续可替换为宿主 vault 实现）</summary>
public interface ISecretProtector
{
    String? Protect(String? plain);
    String? Unprotect(String? cipher);
}

/// <summary>
/// 密钥来源决策（docs/05 §3，已写回文档）：插件数据目录下本机密钥文件 <c>dbadmin.key</c>
/// （32 字节随机，首次使用生成；Unix 置 0600），与库文件同目录但<b>不入库</b>。
/// 算法 AES-256-GCM；密文格式 <c>v1:</c> + Base64(nonce[12] | tag[16] | cipher)。
/// 不用宿主 DataProtection：插件子容器拿不到宿主 IDataProtectionProvider（铁律12c），且其密钥环
/// 默认落用户 profile，跨数据根迁移/便携模式下会失效。
/// 风险：密钥文件与库同机同目录，只防「单独拷走 .db / 备份泄漏」，不防本机同用户进程。丢失密钥文件 =
/// 已存密码不可解（界面提示重新输入密码，不会抛死）。
/// </summary>
public sealed class FileKeySecretProtector : ISecretProtector
{
    public const String KeyFileName = "dbadmin.key";
    private const String Prefix = "v1:";
    private readonly String _keyPath;
    private readonly Lock _lock = new();
    private Byte[]? _key;

    public FileKeySecretProtector(String dataDirectory)
    {
        if (String.IsNullOrWhiteSpace(dataDirectory)) throw new ArgumentException("数据目录不能为空", nameof(dataDirectory));
        _keyPath = Path.Combine(dataDirectory, KeyFileName);
    }

    /// <summary>密钥文件路径（诊断用）</summary>
    public String KeyPath => _keyPath;

    private Byte[] GetKey()
    {
        if (_key != null) return _key;
        lock (_lock)
        {
            if (_key != null) return _key;
            if (File.Exists(_keyPath))
            {
                var raw = File.ReadAllBytes(_keyPath);
                if (raw.Length != 32) throw new CryptographicException("DbAdmin 密钥文件损坏（长度不是 32 字节）");
                _key = raw;
                return _key;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(_keyPath)!);
            var key = RandomNumberGenerator.GetBytes(32);
            // CreateNew：并发进程抢先写入时不覆盖
            try
            {
                using (var fs = new FileStream(_keyPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    fs.Write(key);
                if (!OperatingSystem.IsWindows())
                {
                    try { File.SetUnixFileMode(_keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite); } catch { /* 尽力而为 */ }
                }
                _key = key;
            }
            catch (IOException) when (File.Exists(_keyPath))
            {
                _key = File.ReadAllBytes(_keyPath);
            }
            return _key;
        }
    }

    public String? Protect(String? plain)
    {
        if (String.IsNullOrEmpty(plain)) return null;
        var key = GetKey();
        var nonce = RandomNumberGenerator.GetBytes(12);
        var data = Encoding.UTF8.GetBytes(plain);
        var cipher = new Byte[data.Length];
        var tag = new Byte[16];
        using (var aes = new AesGcm(key, 16)) aes.Encrypt(nonce, data, cipher, tag);
        var buf = new Byte[12 + 16 + cipher.Length];
        nonce.CopyTo(buf, 0);
        tag.CopyTo(buf, 12);
        cipher.CopyTo(buf, 28);
        return Prefix + Convert.ToBase64String(buf);
    }

    public String? Unprotect(String? cipherText)
    {
        if (String.IsNullOrEmpty(cipherText)) return null;
        if (!cipherText.StartsWith(Prefix, StringComparison.Ordinal)) throw new CryptographicException("未知的密文格式");
        var buf = Convert.FromBase64String(cipherText[Prefix.Length..]);
        if (buf.Length < 28) throw new CryptographicException("密文长度不足");
        var plain = new Byte[buf.Length - 28];
        using (var aes = new AesGcm(GetKey(), 16))
            aes.Decrypt(buf.AsSpan(0, 12), buf.AsSpan(28), buf.AsSpan(12, 16), plain);
        return Encoding.UTF8.GetString(plain);
    }
}

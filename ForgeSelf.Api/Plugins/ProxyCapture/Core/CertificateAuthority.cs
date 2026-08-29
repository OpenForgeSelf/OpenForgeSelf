using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.ProxyCapture.Core;

/// <summary>
/// 自签 CA + 按 SNI 动态签发叶子证书，用于 HTTPS MITM 解密。
/// CA 私钥仅存于本地数据目录，需用户手动安装到「受信任的根证书颁发机构」以解密 HTTPS。
/// </summary>
public class CertificateAuthority
{
    // 本地抓包专用 CA 密码（仅用于本机持久化 pfx，非安全敏感）。
    private const string CaPassword = "ForgeSelf-ProxyCapture-CA";

    private readonly string _caPfxPath;
    private readonly string _caCerPath;
    private X509Certificate2? _caCert;
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, (X509Certificate2 Cert, DateTime Expiry)> _leafCache = new();

    public CertificateAuthority(string dataDirectory)
    {
        _caPfxPath = Path.Combine(dataDirectory, "ca.pfx");
        _caCerPath = Path.Combine(dataDirectory, "ca.cer");
    }

    /// <summary>CA 公钥证书（.cer）路径，供前端下载安装。</summary>
    public string CaCerPath => _caCerPath;

    public X509Certificate2 GetCaCertificate()
    {
        EnsureCa();
        return _caCert!;
    }

    private void EnsureCa()
    {
        if (_caCert != null) return;
        lock (_lock)
        {
            if (_caCert != null) return;

            if (File.Exists(_caPfxPath))
            {
                _caCert = new X509Certificate2(
                    _caPfxPath, CaPassword,
                    X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
                return;
            }

            // 生成自签 CA
            using var rsa = RSA.Create(2048);
            var req = new CertificateRequest(
                "CN=ForgeSelf ProxyCapture CA, O=ForgeSelf, C=CN",
                rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            req.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            req.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

            var cert = req.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));

            // CreateSelfSigned 返回的证书已含私钥，直接导出 pfx（勿再 CopyWithPrivateKey，会抛「证书已关联私钥」）
            var pfx = cert.Export(X509ContentType.Pfx, CaPassword);
            File.WriteAllBytes(_caPfxPath, pfx);
            File.WriteAllBytes(_caCerPath, cert.RawData);

            _caCert = new X509Certificate2(
                _caPfxPath, CaPassword,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
            XTrace.Log.Info("[ProxyCapture] 已生成自签 CA 证书: {0}", _caPfxPath);
        }
    }

    /// <summary>
    /// 为指定主机名签发叶子证书（带私钥，可直接用于 SslStream 服务端认证）。结果按主机缓存 1 小时。
    /// </summary>
    public X509Certificate2 GetLeafCertificate(string host)
    {
        if (string.IsNullOrWhiteSpace(host)) host = "localhost";
        host = host.TrimEnd('.');
        var now = DateTime.UtcNow;
        if (_leafCache.TryGetValue(host, out var cached) && cached.Expiry > now)
        {
            return cached.Cert;
        }

        EnsureCa();
        lock (_lock)
        {
            if (_leafCache.TryGetValue(host, out var cached2) && cached2.Expiry > now)
            {
                return cached2.Cert;
            }

            using var rsa = RSA.Create(2048);
            var req = new CertificateRequest(
                $"CN={host}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            req.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
            req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));

            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName(host);
            if (IPAddress.TryParse(host, out var ip)) sanBuilder.AddIpAddress(ip);
            req.CertificateExtensions.Add(sanBuilder.Build());
            req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

            var leaf = req.Create(_caCert!, now.AddDays(-1), now.AddYears(2), Guid.NewGuid().ToByteArray());
            var withKey = leaf.CopyWithPrivateKey(rsa);
            var cert = new X509Certificate2(
                withKey.Export(X509ContentType.Pfx), "",
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);

            _leafCache[host] = (cert, now.AddHours(1));
            return cert;
        }
    }
}

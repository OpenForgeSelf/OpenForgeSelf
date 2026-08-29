using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using NewLife.Log;
using ForgeSelf.Api.Plugins.ProxyCapture.Data.Entities;

namespace ForgeSelf.Api.Plugins.ProxyCapture.Core;

/// <summary>
/// HTTPS MITM 处理器：在入站侧终结 TLS（按 SNI 用自签 CA 签发叶子证书），
/// 解密后复用 HttpCaptureHandler 抓取内层 HTTP；出站再以真实 TLS 连接目标服务器。
/// </summary>
public class HttpsMitmHandler : IProtocolHandler
{
    private readonly CertificateAuthority _ca;

    public HttpsMitmHandler(CertificateAuthority ca)
    {
        _ca = ca;
    }

    public bool CanHandle(ProtocolDetection d) => d.IsTls;

    public async Task HandleAsync(Stream inbound, TcpClient client, ListenerConfig cfg, ProtocolDetection detection, CancellationToken ct)
    {
        SslStream? ssl = null;
        try
        {
            ssl = new SslStream(inbound, false);
            await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                // 客户端发来 SNI 时按主机名签发对应叶子证书
                ServerCertificateSelectionCallback = (_, host) =>
                    _ca.GetLeafCertificate(host ?? cfg.TargetHost ?? "localhost"),
                ClientCertificateRequired = false,
                EnabledSslProtocols = SslProtocols.None
            });

            // 内层为已解密的 HTTP，交给统一处理逻辑
            await HttpCaptureHandler.ProcessAsync(ssl, client, cfg, true, ct);
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[ProxyCapture] HTTPS 握手/处理失败: {0}", ex.Message);
        }
        finally
        {
            try { ssl?.Close(); } catch { }
            try { inbound.Close(); } catch { }
            try { client.Close(); } catch { }
        }
    }
}

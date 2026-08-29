using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NewLife.Log;
using ForgeSelf.Api.Plugins.ProxyCapture.Data.Entities;

namespace ForgeSelf.Api.Plugins.ProxyCapture.Core;

/// <summary>
/// 原始 TCP 透传兜底处理器：用于非 HTTP/HTTPS 的流量（如 MQTT、自定义 TCP）。
/// 配置目标则双向透传并记录字节数；未配置则抓取首段原始字节（hex 预览）后关闭。
/// 预留：后续可派生 MqttHandler 等做协议级解析。
/// </summary>
public class RawTunnelHandler : IProtocolHandler
{
    // 兜底处理器必须最后注册：CanHandle 恒真
    public bool CanHandle(ProtocolDetection d) => true;

    public async Task HandleAsync(Stream inbound, TcpClient client, ListenerConfig cfg, ProtocolDetection detection, CancellationToken ct)
    {
        var record = CaptureEngine.Instance.NewRecord(cfg, "Tcp", client);
        var ns = client.GetStream();
        try
        {
            // 限时读取首段原始字节用于预览（避免无数据时长时间阻塞）
            ns.ReadTimeout = 2000;
            var prefix = detection.FirstByte;
            using var ms = new MemoryStream();
            ms.Write(prefix, 0, prefix.Length);
            var buf = new byte[4096];
            try
            {
                while (ms.Length < 64 * 1024)
                {
                    var n = await inbound.ReadAsync(buf.AsMemory(0, (int)Math.Min(buf.Length, 64 * 1024 - ms.Length)), ct);
                    if (n == 0) break;
                    ms.Write(buf, 0, n);
                }
            }
            catch (IOException)
            {
                // 读超时或连接断开：使用已抓到的预览
            }

            var preview = ms.ToArray();
            record.RawPreview = ToHexPreview(preview, 256);
            record.RequestBytes = preview.Length;

            if (!string.IsNullOrEmpty(cfg.TargetHost) && cfg.TargetPort > 0)
            {
                // ── 透传到目标 ──
                record.Forwarded = true;
                record.Target = $"{cfg.TargetHost}:{cfg.TargetPort}";
                ns.ReadTimeout = 0; // 转发阶段不限时
                using var targetClient = new TcpClient();
                await targetClient.ConnectAsync(cfg.TargetHost, cfg.TargetPort);
                var targetStream = targetClient.GetStream();
                await targetStream.WriteAsync(preview, ct); // 先把预览字节发往目标
                var (c2t, t2c) = await StreamTunnel.PipeAsync(inbound, targetStream, ct);
                record.RequestBytes += c2t;
                record.ResponseBytes = t2c;
            }
            else
            {
                // ── 仅抓包 ──
                record.Forwarded = false;
            }
        }
        catch (Exception ex)
        {
            record.ErrorMessage = ex.Message;
            XTrace.Log.Warn("[ProxyCapture] 原始 TCP 处理异常: {0}", ex.Message);
        }
        finally
        {
            try { CaptureEngine.Instance.SaveRecord(record); } catch { }
            try { inbound.Close(); } catch { }
            try { client.Close(); } catch { }
        }
    }

    private static string ToHexPreview(byte[] data, int maxBytes)
    {
        if (data == null || data.Length == 0) return string.Empty;
        var take = Math.Min(data.Length, maxBytes);
        var sb = new StringBuilder();
        for (var i = 0; i < take; i++)
        {
            if (i > 0 && i % 16 == 0) sb.Append('\n');
            sb.Append(data[i].ToString("X2"));
            sb.Append(' ');
        }

        if (data.Length > take) sb.Append($"... ({data.Length} bytes total)");
        return sb.ToString();
    }
}

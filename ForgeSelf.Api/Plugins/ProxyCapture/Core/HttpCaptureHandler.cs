using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NewLife.Log;
using ForgeSelf.Api.Plugins.ProxyCapture.Data;
using ForgeSelf.Api.Plugins.ProxyCapture.Data.Entities;

namespace ForgeSelf.Api.Plugins.ProxyCapture.Core;

/// <summary>
/// HTTP 抓取 / 转发处理器（同时被 HttpsMitmHandler 复用：TLS 终结后的内层也是 HTTP）。
/// 支持「配置目标则原样转发、未配置则仅抓包并返回 200 占位」。
/// </summary>
public class HttpCaptureHandler : IProtocolHandler
{
    public bool CanHandle(ProtocolDetection d) => d.IsHttp;

    public Task HandleAsync(Stream inbound, TcpClient client, ListenerConfig cfg, ProtocolDetection detection, CancellationToken ct)
    {
        return ProcessAsync(inbound, client, cfg, detection.IsTls, ct);
    }

    /// <summary>
    /// 处理一条 HTTP 流（明文或已解密的 TLS）。inbound 同时用于向客户端回写响应。
    /// </summary>
    public static async Task ProcessAsync(Stream inbound, TcpClient client, ListenerConfig cfg, bool isTls, CancellationToken ct)
    {
        var record = CaptureEngine.Instance.NewRecord(cfg, isTls ? "Https" : "Http", client);
        try
        {
            var request = await HttpReader.ReadMessageAsync(inbound, ct);

            // 解析请求行：METHOD URL HTTP/1.1
            var parts = request.StartLine.Split(' ');
            record.Method = parts.Length > 0 ? parts[0] : null;
            record.Url = parts.Length > 1 ? parts[1] : null;
            record.HttpVersion = parts.Length > 2 ? parts[2] : null;
            record.RequestHeaders = HeadersToJson(request.Headers);
            record.RequestBody = TryDecode(request.Body);
            record.RequestBytes = request.HeaderBytes.Length + request.Body.Length;

            if (!string.IsNullOrEmpty(cfg.TargetHost) && cfg.TargetPort > 0)
            {
                // ── 原样转发到目标 ──
                record.Forwarded = true;
                record.Target = $"{cfg.TargetHost}:{cfg.TargetPort}";
                using var targetClient = new TcpClient();
                await targetClient.ConnectAsync(cfg.TargetHost, cfg.TargetPort);
                var targetStream = targetClient.GetStream();
                Stream outStream = targetStream;
                if (isTls)
                {
                    // 出站也走 TLS（命中真实 HTTPS 服务器），SNI 取 Host 头或目标主机
                    var ssl = new SslStream(targetStream, false);
                    await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                    {
                        TargetHost = ExtractHost(request.Headers) ?? cfg.TargetHost
                    });
                    outStream = ssl;
                }

                // 转发原始请求字节（头部 + 体），保持 Host 等头不变
                await outStream.WriteAsync(request.HeaderBytes, ct);
                await outStream.WriteAsync(request.Body, ct);
                await outStream.FlushAsync(ct);

                // 读取并回写响应
                var response = await HttpReader.ReadMessageAsync(outStream, ct);
                record.StatusCode = ParseStatusCode(response.StartLine) ?? 0;
                record.ResponseHeaders = HeadersToJson(response.Headers);
                record.ResponseBody = TryDecode(response.Body);
                record.ResponseBytes = response.HeaderBytes.Length + response.Body.Length;

                await inbound.WriteAsync(response.HeaderBytes, ct);
                await inbound.WriteAsync(response.Body, ct);
                await inbound.FlushAsync(ct);
            }
            else
            {
                // ── 仅抓包、不转发：返回 200 占位 ──
                record.Forwarded = false;
                record.StatusCode = 200;
                var bodyText = "ForgeSelf ProxyCapture: request captured (no target configured).";
                var bodyBytes = Encoding.UTF8.GetBytes(bodyText);
                var header = Encoding.ASCII.GetBytes(
                    $"HTTP/1.1 200 OK\r\n" +
                    $"Content-Type: text/plain; charset=utf-8\r\n" +
                    $"Content-Length: {bodyBytes.Length}\r\n" +
                    $"Connection: close\r\n\r\n");
                await inbound.WriteAsync(header, ct);
                await inbound.WriteAsync(bodyBytes, ct);
                await inbound.FlushAsync(ct);
                record.ResponseBody = "[captured, no forward] " + bodyText;
                record.ResponseBytes = header.Length + bodyBytes.Length;
            }
        }
        catch (Exception ex)
        {
            record.ErrorMessage = ex.Message;
            XTrace.Log.Warn("[ProxyCapture] HTTP 处理异常: {0}", ex.Message);
        }
        finally
        {
            try { CaptureEngine.Instance.SaveRecord(record); } catch { }
            try { inbound.Close(); } catch { }
            try { client.Close(); } catch { }
        }
    }

    private static string? ExtractHost(Dictionary<string, string> headers)
    {
        if (headers.TryGetValue("Host", out var host) && !string.IsNullOrWhiteSpace(host))
        {
            return host.Split(':')[0];
        }

        return null;
    }

    private static int? ParseStatusCode(string statusLine)
    {
        // HTTP/1.1 200 OK
        var parts = statusLine.Split(' ');
        if (parts.Length >= 2 && int.TryParse(parts[1], out var code)) return code;
        return null;
    }

    internal static string HeadersToJson(Dictionary<string, string> headers)
    {
        return JsonSerializer.Serialize(headers, new JsonSerializerOptions { WriteIndented = false });
    }

    /// <summary>尝试将字节解码为文本；疑似二进制或过大则返回说明字符串或 null。</summary>
    internal static string? TryDecode(byte[] data)
    {
        if (data == null || data.Length == 0) return null;
        if (data.Length > 512 * 1024) return $"[body too large: {data.Length} bytes, omitted]";

        // 前 8KB 含空字符则判定为二进制
        var probe = Math.Min(data.Length, 8192);
        for (var i = 0; i < probe; i++)
        {
            if (data[i] == 0) return $"[binary body: {data.Length} bytes]";
        }

        try { return Encoding.UTF8.GetString(data); }
        catch { return null; }
    }
}

using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ForgeSelf.Api.Plugins.ProxyCapture.Core;

/// <summary>一条 HTTP 报文（请求或响应）的解析结果。</summary>
public class HttpMessage
{
    /// <summary>请求行（METHOD URL HTTP/1.1）或状态行（HTTP/1.1 200 OK）。</summary>
    public string StartLine { get; set; } = string.Empty;

    public Dictionary<string, string> Headers { get; set; } = new();

    public byte[] HeaderBytes { get; set; } = Array.Empty<byte>();

    public byte[] Body { get; set; } = Array.Empty<byte>();
}

/// <summary>从流中读取一条完整 HTTP 报文（头部 + 依据 Content-Length / chunked 的 Body）。</summary>
public static class HttpReader
{
    public static async Task<HttpMessage> ReadMessageAsync(Stream stream, CancellationToken ct)
    {
        // 读取头部直到出现 \r\n\r\n（逐字节扫描结尾）
        using var ms = new MemoryStream();
        var buf = new byte[1];
        var prev3 = -1;
        var prev2 = -1;
        var prev1 = -1;
        while (true)
        {
            var n = await stream.ReadAsync(buf, 0, 1, ct);
            if (n == 0) break;
            var b = buf[0];
            ms.WriteByte(b);
            if (prev3 == 13 && prev2 == 10 && prev1 == 13 && b == 10) break; // \r\n\r\n
            prev3 = prev2;
            prev2 = prev1;
            prev1 = b;
        }

        var headerBytes = ms.ToArray();
        var headerText = Encoding.ASCII.GetString(headerBytes);
        var msg = new HttpMessage { HeaderBytes = headerBytes };

        var lines = headerText.Split("\r\n");
        msg.StartLine = lines.Length > 0 ? lines[0] : headerText;
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrEmpty(line)) continue;
            var sep = line.IndexOf(':');
            if (sep <= 0) continue;
            var key = line.Substring(0, sep).Trim();
            var value = line.Substring(sep + 1).Trim();
            msg.Headers[key] = value;
        }

        msg.Body = await ReadBodyAsync(stream, msg.Headers, ct);
        return msg;
    }

    private static async Task<byte[]> ReadBodyAsync(Stream stream, Dictionary<string, string> headers, CancellationToken ct)
    {
        if (headers.TryGetValue("Content-Length", out var clStr) &&
            int.TryParse(clStr, out var cl) && cl > 0)
        {
            var body = new byte[cl];
            var read = 0;
            while (read < cl)
            {
                var n = await stream.ReadAsync(body.AsMemory(read, cl - read), ct);
                if (n == 0) break;
                read += n;
            }

            if (read < cl) Array.Resize(ref body, read);
            return body;
        }

        if (headers.TryGetValue("Transfer-Encoding", out var te) &&
            te.Contains("chunked", StringComparison.OrdinalIgnoreCase))
        {
            using var outMs = new MemoryStream();
            while (true)
            {
                var sizeLine = await ReadLineAsync(stream, ct);
                if (sizeLine == null) break;
                var sizeStr = sizeLine.Trim().Split(';')[0];
                if (!int.TryParse(sizeStr, System.Globalization.NumberStyles.HexNumber, null, out var size)) break;
                if (size == 0)
                {
                    await ReadLineAsync(stream, ct); // 拖尾 CRLF
                    break;
                }

                var chunk = new byte[size];
                var got = 0;
                while (got < size)
                {
                    var n = await stream.ReadAsync(chunk.AsMemory(got, size - got), ct);
                    if (n == 0) break;
                    got += n;
                }

                outMs.Write(chunk, 0, got);
                await ReadLineAsync(stream, ct); // chunk 后的 CRLF
            }

            return outMs.ToArray();
        }

        return Array.Empty<byte>();
    }

    private static async Task<string?> ReadLineAsync(Stream stream, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        var buf = new byte[1];
        while (true)
        {
            var n = await stream.ReadAsync(buf, 0, 1, ct);
            if (n == 0)
            {
                if (ms.Length == 0) return null;
                break;
            }

            var b = buf[0];
            if (b == 10) break;
            if (b == 13) continue;
            ms.WriteByte(b);
        }

        return Encoding.ASCII.GetString(ms.ToArray());
    }
}

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ForgeSelf.Api.Plugins.ProxyCapture.Core;

/// <summary>双向字节流透传工具（用于 TCP 隧道与原始协议兜底）。</summary>
public static class StreamTunnel
{
    /// <summary>
    /// 双向拷贝 a↔b。任意一端结束或取消即终止另一端（避免单边挂起）。
    /// 返回 (a→b 字节数, b→a 字节数)。
    /// </summary>
    public static async Task<(long, long)> PipeAsync(Stream a, Stream b, CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var tA = CopyAsync(a, b, linked.Token);
        var tB = CopyAsync(b, a, linked.Token);

        await Task.WhenAny(tA, tB);
        linked.Cancel(); // 任意一端结束即终止另一端

        long x = 0;
        long y = 0;
        try { x = await tA; } catch { }
        try { y = await tB; } catch { }
        return (x, y);
    }

    public static async Task<long> CopyAsync(Stream src, Stream dst, CancellationToken ct)
    {
        var buf = new byte[8192];
        long total = 0;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var n = await src.ReadAsync(buf.AsMemory(0, buf.Length), ct);
                if (n == 0) break;
                await dst.WriteAsync(buf.AsMemory(0, n), ct);
                total += n;
            }
        }
        catch (Exception)
        {
            // 连接断开或被取消：返回已拷贝字节数
        }

        return total;
    }
}

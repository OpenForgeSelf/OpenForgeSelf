using System.Net.Sockets;
using OpenForgeSelf.Backend.Plugins.ProxyCapture.Data.Entities;

namespace OpenForgeSelf.Backend.Plugins.ProxyCapture.Core;

/// <summary>协议嗅探结果，供各协议处理器判断是否可处理。</summary>
public class ProtocolDetection
{
    /// <summary>已读出的首个字节（还原流用）。</summary>
    public byte[] FirstByte { get; set; } = Array.Empty<byte>();

    /// <summary>是否为 TLS（首字节 0x16）。</summary>
    public bool IsTls { get; set; }

    /// <summary>是否疑似 HTTP（首字节为大写方法字母）。</summary>
    public bool IsHttp { get; set; }
}

/// <summary>
/// 协议处理器扩展点。
/// 现有实现：HttpsMitmHandler（TLS→MITM 解密）、HttpCaptureHandler（HTTP 抓取/转发）、RawTunnelHandler（TCP 透传）。
/// 后续可新增 MqttHandler / UdpHandler 等以扩展更多协议。
/// </summary>
public interface IProtocolHandler
{
    /// <summary>是否可处理该检测结果。</summary>
    bool CanHandle(ProtocolDetection detection);

    /// <summary>处理一条客户端连接。inbound 为已还原（含首字节）的入站流。</summary>
    Task HandleAsync(Stream inbound, TcpClient client, ListenerConfig cfg, ProtocolDetection detection, CancellationToken ct);
}

/// <summary>监听器运行时状态。</summary>
public class ActiveListener
{
    public TcpListener Listener { get; set; } = null!;
    public CancellationTokenSource Cts { get; set; } = null!;
    public ListenerConfig Config { get; set; } = null!;
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenForgeSelf.Backend.Plugins.ProxyCapture.Data.Entities;

/// <summary>
/// 抓包会话记录：一条被监听到的请求（含完整请求/响应信息）。
/// </summary>
[Table("CaptureSession")]
public class CaptureSession
{
    [Key]
    public long Id { get; set; }

    public int ListenerId { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>协议：Http / Https / Tcp / Udp。</summary>
    [MaxLength(16)]
    public string Protocol { get; set; } = "Http";

    [MaxLength(64)]
    public string ClientIp { get; set; } = string.Empty;

    [MaxLength(64)]
    public string LocalEndpoint { get; set; } = string.Empty;

    /// <summary>目标（转发到的地址:端口），未转发为空。</summary>
    [MaxLength(256)]
    public string? Target { get; set; }

    [MaxLength(16)]
    public string? Method { get; set; }

    /// <summary>请求 URL / 路径。</summary>
    public string? Url { get; set; }

    [MaxLength(16)]
    public string? HttpVersion { get; set; }

    /// <summary>请求头（JSON 字符串）。</summary>
    public string? RequestHeaders { get; set; }

    /// <summary>请求体（文本；非文本或过大时为空，见 RawPreview）。</summary>
    public string? RequestBody { get; set; }

    public int? StatusCode { get; set; }

    public string? ResponseHeaders { get; set; }

    public string? ResponseBody { get; set; }

    public long RequestBytes { get; set; }

    public long ResponseBytes { get; set; }

    public long DurationMs { get; set; }

    public bool Forwarded { get; set; }

    /// <summary>非 HTTP/HTTPS 或无法文本化的原始字节预览（hex）。</summary>
    public string? RawPreview { get; set; }

    public string? ErrorMessage { get; set; }
}

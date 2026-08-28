using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenForgeSelf.Backend.Plugins.ProxyCapture.Data.Entities;

/// <summary>
/// 监听器配置：监听地址/端口 + 可选目标地址/端口。
/// 目标为空表示「仅抓包、不转发」。
/// </summary>
[Table("ListenerConfig")]
public class ListenerConfig
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    /// <summary>监听地址，如 0.0.0.0 / 127.0.0.1。</summary>
    [Required]
    [MaxLength(64)]
    public string ListenAddress { get; set; } = "0.0.0.0";

    public int ListenPort { get; set; }

    /// <summary>目标主机（可选）。为空表示仅抓包不转发。</summary>
    [MaxLength(256)]
    public string? TargetHost { get; set; }

    /// <summary>目标端口（可选）。</summary>
    public int? TargetPort { get; set; }

    public bool Enabled { get; set; } = true;

    [MaxLength(512)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

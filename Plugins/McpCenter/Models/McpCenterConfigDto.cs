namespace ForgeSelf.Api.Plugins.McpCenter.Models;

/// <summary>网关配置视图（GET api/mcp-center/config）：令牌永不回显明文。</summary>
public class McpCenterConfigDto
{
    public int Port { get; set; }

    public string ListenHost { get; set; } = string.Empty;

    public string ListenUrl { get; set; } = string.Empty;

    /// <summary>是否已设置令牌（true 时客户端需带 Authorization: Bearer &lt;token&gt;）。</summary>
    public bool HasToken { get; set; }

    /// <summary>令牌掩码（如 ••••abcd）；未设置则为空串。</summary>
    public string TokenMasked { get; set; } = string.Empty;

    public bool IsRunning { get; set; }

    /// <summary>会话模式（v2.4.0）：true=无状态（每请求独立，重启不断会话）；false=有状态（initialize 签发 Mcp-Session-Id）。</summary>
    public bool Stateless { get; set; }

    public string Version { get; set; } = string.Empty;
}

/// <summary>网关配置更新（PUT api/mcp-center/config）：token 传空串=清除鉴权、不传=保留原值；各字段不传=不变。</summary>
public class McpCenterConfigUpdateDto
{
    public int? Port { get; set; }

    public string? ListenHost { get; set; }

    public string? Token { get; set; }

    /// <summary>会话模式（v2.4.0）：不传=不变；保存后热重启立即生效。</summary>
    public bool? Stateless { get; set; }
}

namespace ForgeSelf.Api.Plugins.McpCenter.Models;

/// <summary>
/// 外部工具调用请求体（v2.3.0 工具测试台）：POST api/mcp-center/servers/{id}/tools/invoke。
/// Tool 用**外部工具原生名**（tools/list 的 name，不是 mcp.&lt;id&gt;.&lt;name&gt;）；
/// ArgumentsJson 为空时按 {} 调用。
/// </summary>
public sealed class McpToolInvokeRequest
{
    /// <summary>外部工具原生名。</summary>
    public string Tool { get; set; } = string.Empty;

    /// <summary>参数 JSON 字符串（可为空）。</summary>
    public string? ArgumentsJson { get; set; }
}

/// <summary>
/// 会话层工具调用结果（v2.3.0）：在既有 CallToolAsync 的「文本」之外保留 isError 与原始 JSON，
/// 供工具测试台同时展示人类可读文本与机器可读原文。
/// </summary>
public sealed class McpToolCallOutcome
{
    /// <summary>远端 MCP 声明的 isError。</summary>
    public bool IsError { get; set; }

    /// <summary>text content 拼接结果（非 text 项原样 JSON 透传）。</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>tools/call 返回的 result 原文 JSON。</summary>
    public string RawJson { get; set; } = string.Empty;
}

/// <summary>
/// 工具调用端点的响应体（v2.3.0）：Ok = 调用本身成功且远端未声明 isError。
/// </summary>
public sealed class McpToolInvokeResult
{
    public string ServerId { get; set; } = string.Empty;

    public string Tool { get; set; } = string.Empty;

    /// <summary>调用成功（未发生错误且远端 isError=false）。</summary>
    public bool Ok { get; set; }

    /// <summary>远端 MCP 声明的 isError（Ok=false 时的细分原因）。</summary>
    public bool IsError { get; set; }

    /// <summary>人类可读结果文本。</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>原始 JSON（tools/call result 原文）。</summary>
    public string RawJson { get; set; } = string.Empty;

    /// <summary>调用耗时（毫秒）。</summary>
    public long ElapsedMs { get; set; }
}

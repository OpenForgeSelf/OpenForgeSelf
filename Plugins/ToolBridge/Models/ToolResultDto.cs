namespace ForgeSelf.Api.Plugins.ToolBridge.Models;

/// <summary>单条调用的执行观察（02-spec Output 节）。业务失败也照常回传给 AI，HTTP 不表达。</summary>
public sealed class ToolResult
{
    public string Tool { get; init; } = string.Empty;

    /// <summary>true=这条调用被执行了（含非 0 退出码，FR-3.8）；false=没能执行。</summary>
    public bool Ok { get; init; }

    /// <summary>AI 原文里写的工具名（未归一）。</summary>
    public string RawName { get; init; } = string.Empty;

    public Dictionary<string, JsonNode> Args { get; init; } = new();

    /// <summary>成功负载（文件内容 / 目录项 / exitCode+stdout+stderr 原文）。</summary>
    public JsonNode? Result { get; init; }

    /// <summary>机器可读错误码：outside_workspace / command_rejected / not_found / is_a_directory / missing_argument / unknown_tool …</summary>
    public string? Error { get; init; }

    /// <summary>人类/AI 可读的原因原文（守卫拒绝时是守卫原文，AC7）。</summary>
    public string? Reason { get; init; }

    public bool Truncated { get; init; }

    /// <summary>内容超限时的原始字节数（FR-4.2）。</summary>
    public long? OriginalBytes { get; init; }

    public long DurationMs { get; init; }
}

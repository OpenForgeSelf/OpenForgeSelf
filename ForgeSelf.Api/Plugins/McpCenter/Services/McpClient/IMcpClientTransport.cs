using System.Text.Json;

namespace ForgeSelf.Api.Plugins.McpCenter.Services.McpClient;

/// <summary>MCP 客户端错误（传输/协议层失败统一异常，携带面向用户的错误消息）。</summary>
public sealed class McpClientException : Exception
{
    public McpClientException(string message) : base(message) { }
    public McpClientException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// MCP 客户端传输抽象（v2.1.0）：会话与路由不感知传输格式差异。
/// 三个实现：StdioMcpTransport（stdio 帧）/ StreamableHttpMcpTransport（单端点 POST+SSE）/ LegacySseMcpTransport（双端点）。
/// 统一语义：方法返回 JSON-RPC 响应的 result 元素；协议/传输错误抛 McpClientException。
/// </summary>
public interface IMcpClientTransport
{
    /// <summary>发起 initialize 握手（客户端声明的协议版本列表，服务端协商）。返回 result（含 protocolVersion/capabilities/serverInfo）。</summary>
    Task<JsonElement> InitializeAsync(IReadOnlyList<string> protocolVersions, CancellationToken ct);

    /// <summary>tools/list，返回 result（{ tools: [...] }）。</summary>
    Task<JsonElement> ListToolsAsync(CancellationToken ct);

    /// <summary>tools/call，返回 result（{ content: [...], isError? }）。argumentsJson 为目标工具参数 JSON 原文。</summary>
    Task<JsonElement> CallToolAsync(string name, string argumentsJson, CancellationToken ct);

    /// <summary>ping，返回 result（空对象）。</summary>
    Task<JsonElement> PingAsync(CancellationToken ct);

    /// <summary>关闭连接（杀进程/断流/取消挂起请求）。幂等。</summary>
    Task CloseAsync();

    /// <summary>日志描述（不含密钥）。</summary>
    string Describe();
}

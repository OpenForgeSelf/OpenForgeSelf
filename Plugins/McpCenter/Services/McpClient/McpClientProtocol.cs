namespace ForgeSelf.Api.Plugins.McpCenter.Services.McpClient;

using System.Text.Json;

/// <summary>
/// MCP 客户端协议常量与 initialize 载荷单点构造（v2.5.0，PC-008 canonical fix）。
/// 规范依据（2025-06-18）：InitializeRequest.params.clientInfo{name,version} **必填**——
/// 缺失时严格服务器（如 DeepWiki）直接回 -32602 Invalid request parameters，连接/测试全灭。
/// 三个传输（stdio / streamable-http / http-sse）的 InitializeAsync 一律经
/// <see cref="BuildInitializeParams"/> 构造载荷，禁止各自手写匿名对象
/// （源码守卫 McpClientProtocolGuardTests 钉住该不变量）。
/// </summary>
public static class McpClientProtocol
{
    /// <summary>initialize 握手完成后客户端 MUST 发送的无 id 通知。</summary>
    public const string InitializedNotificationMethod = "notifications/initialized";

    /// <summary>Streamable HTTP 会话头（服务器在 initialize 响应返回时须回传）。</summary>
    public const string SessionIdHeader = "Mcp-Session-Id";

    /// <summary>Streamable HTTP 协议版本头（协商完成后后续请求须携带）。</summary>
    public const string ProtocolVersionHeader = "Mcp-Protocol-Version";

    /// <summary>客户端标识名（面向外部 MCP 服务器，不暴露宿主内部信息）。</summary>
    public const string ClientInfoName = "OpenForgeSelf-McpCenter";

    /// <summary>客户端版本：取本插件程序集版本（与 plugin.json 版本同源，避免手写腐化）。</summary>
    public static string ClientInfoVersion { get; } =
        typeof(McpClientProtocol).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    /// <summary>构造 initialize 请求 params：protocolVersion + capabilities + clientInfo（规范必填三件）。</summary>
    public static object BuildInitializeParams(string protocolVersion) => new
    {
        protocolVersion,
        capabilities = new { },
        clientInfo = new
        {
            name = ClientInfoName,
            version = ClientInfoVersion
        }
    };

    /// <summary>
    /// 序列化 JSON-RPC 请求（三传输 canonical 单点，v2.5.1 PC-010）：
    /// <paramref name="prms"/> 为 null 时**省略 params 键**——规范里 params 可选，
    /// 而现场实测 FastMCP 系服务器（server-everything 探针坐实）收到 <c>"params":null</c> 后完全沉默
    /// （无 result 无 error），客户端只能干等 30s 超时；省略键则正常响应。
    /// </summary>
    public static string SerializeRequest(int id, string method, object? prms) => prms is null
        ? JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method })
        : JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method, @params = prms });
}

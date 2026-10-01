using System.Text;
using System.Text.Json;
using ForgeSelf.Api.Plugins.McpCenter.Models;

namespace ForgeSelf.Api.Plugins.McpCenter.Services.McpClient;

/// <summary>
/// 外部 MCP 会话（v2.1.0）：在传输层之上提供握手、工具清单快照、工具调用与状态。
/// 协议版本协商：客户端声明支持列表，服务端返回所选版本（会话保持该版本语义）。
/// </summary>
public sealed class McpClientSession : IAsyncDisposable
{
    /// <summary>客户端支持的 MCP 协议版本（标准版本列表，偏好顺序：2.0 最前）。</summary>
    public static readonly string[] SupportedProtocolVersions = { "2025-11-25", "2025-06-18", "2025-03-26", "2024-11-05" };

    private readonly McpExternalServerConfig _config;
    private readonly IMcpClientTransport _transport;
    private readonly List<McpExternalToolDto> _tools = new();
    private readonly object _toolsLock = new();

    public McpClientSession(McpExternalServerConfig config, IMcpClientTransport transport)
    {
        _config = config;
        _transport = transport;
    }

    public string ServerId => _config.Id;
    public string ProtocolVersion { get; private set; } = string.Empty;
    public string ServerInfoName { get; private set; } = string.Empty;
    public int ToolCount { get { lock (_toolsLock) return _tools.Count; } }

    /// <summary>建立连接：initialize 握手 → tools/list 快照。</summary>
    public async Task ConnectAsync(CancellationToken ct)
    {
        var result = await _transport.InitializeAsync(SupportedProtocolVersions, ct);
        ProtocolVersion = result.TryGetProperty("protocolVersion", out var pv) && pv.ValueKind == JsonValueKind.String
            ? pv.GetString()!
            : string.Empty;
        ServerInfoName = result.TryGetProperty("serverInfo", out var si) && si.TryGetProperty("name", out var n)
            ? n.GetString() ?? string.Empty
            : string.Empty;
        await RefreshToolsAsync(ct);
    }

    /// <summary>重新拉取 tools/list 快照。</summary>
    public async Task RefreshToolsAsync(CancellationToken ct)
    {
        var result = await _transport.ListToolsAsync(ct);
        var fresh = new List<McpExternalToolDto>();
        if (result.TryGetProperty("tools", out var tools) && tools.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in tools.EnumerateArray())
            {
                var name = t.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString()! : string.Empty;
                if (string.IsNullOrWhiteSpace(name)) continue;
                fresh.Add(new McpExternalToolDto
                {
                    Name = name,
                    FullName = $"mcp.{_config.Id}.{name}",
                    Description = t.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString()! : string.Empty,
                    InputSchemaJson = t.TryGetProperty("inputSchema", out var s) ? s.GetRawText() : string.Empty
                });
            }
        }
        lock (_toolsLock)
        {
            _tools.Clear();
            _tools.AddRange(fresh);
        }
    }

    /// <summary>工具清单（含完整转发名 mcp.&lt;id&gt;.&lt;name&gt;）。</summary>
    public IReadOnlyList<McpExternalToolDto> GetTools()
    {
        lock (_toolsLock) return _tools.ToList();
    }

    /// <summary>
    /// 调用外部工具，返回给 MCP 客户端的文本负载：
    /// text content 拼接；结构化 content 逐项 JSON 透传；isError 时包 {success:false,error:...}。
    /// </summary>
    public async Task<string> CallToolAsync(string name, string argumentsJson, CancellationToken ct)
    {
        var result = await _transport.CallToolAsync(name, argumentsJson, ct);
        var isError = result.TryGetProperty("isError", out var ie) && ie.ValueKind == JsonValueKind.True;

        if (result.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var item in content.EnumerateArray())
            {
                if (item.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String && type.GetString() == "text"
                    && item.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
                {
                    sb.Append(textProp.GetString());
                }
                else
                {
                    sb.Append(item.GetRawText());
                }
            }
            var text = sb.ToString();
            if (isError)
            {
                return JsonSerializer.Serialize(new { success = false, error = string.IsNullOrWhiteSpace(text) ? "外部工具执行失败" : text });
            }
            return string.IsNullOrWhiteSpace(text) ? "{}" : text;
        }

        var raw = result.GetRawText();
        return isError ? JsonSerializer.Serialize(new { success = false, error = raw }) : raw;
    }

    public Task PingAsync(CancellationToken ct) => _transport.PingAsync(ct);

    public async ValueTask DisposeAsync()
    {
        try { await _transport.CloseAsync(); }
        catch (Exception) { /* 关闭失败不抛出 */ }
    }
}

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
                    ServerId = _config.Id,
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
    /// 语义由 McpClientSessionTests 钉住，v2.3.0 起改为委托 Inspect（与 CallToolDetailedAsync 共用一处提取逻辑）。
    /// </summary>
    public async Task<string> CallToolAsync(string name, string argumentsJson, CancellationToken ct)
    {
        var view = Inspect(await _transport.CallToolAsync(name, argumentsJson, ct));
        if (view.IsError)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = string.IsNullOrWhiteSpace(view.Text) ? "外部工具执行失败" : view.Text
            });
        }
        // content 数组存在但拼不出文本 → 沿用既有 "{}"；无 content 字段 → 原样透传 result 原文。
        return view.HasContent ? (string.IsNullOrWhiteSpace(view.Text) ? "{}" : view.Text) : view.RawJson;
    }

    /// <summary>
    /// 调用外部工具并返回完整结果（v2.3.0 工具测试台）：在文本之外保留 isError 与原始 JSON。
    /// </summary>
    public async Task<McpToolCallOutcome> CallToolDetailedAsync(string name, string argumentsJson, CancellationToken ct)
    {
        var view = Inspect(await _transport.CallToolAsync(name, argumentsJson, ct));
        return new McpToolCallOutcome
        {
            IsError = view.IsError,
            Text = view.Text,
            RawJson = view.RawJson
        };
    }

    /// <summary>tools/call 结果的统一提取（唯一一处解析逻辑，供两个入口共用）。</summary>
    private static (bool IsError, bool HasContent, string Text, string RawJson) Inspect(JsonElement result)
    {
        var isError = result.TryGetProperty("isError", out var ie) && ie.ValueKind == JsonValueKind.True;
        var raw = result.GetRawText();

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
            return (isError, true, sb.ToString(), raw);
        }

        // 无 content 字段：文本为空，原文即 result 本身（既有语义：结构化结果原样透传）
        return (isError, false, string.Empty, raw);
    }

    public Task PingAsync(CancellationToken ct) => _transport.PingAsync(ct);

    public async ValueTask DisposeAsync()
    {
        try { await _transport.CloseAsync(); }
        catch (Exception) { /* 关闭失败不抛出 */ }
    }
}

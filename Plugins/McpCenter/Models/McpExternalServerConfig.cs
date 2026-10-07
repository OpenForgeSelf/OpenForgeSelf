namespace ForgeSelf.Api.Plugins.McpCenter.Models;

/// <summary>
/// 外部 MCP 服务器连接配置（v2.1.0 外部 MCP 客户端接入）。
/// 持久化于 {插件数据根}/external-servers.json（与网关自身 config.json 分离）。
/// 传输类型：stdio / streamable-http / http-sse（旧版双端点）。
/// 密钥字段（Headers/Env 值）仅在内存与本地文件中保留明文；API 返回一律脱敏。
/// </summary>
public sealed class McpExternalServerConfig
{
    /// <summary>服务器标识（kebab-case，唯一；外部工具命名空间前缀 mcp.&lt;id&gt;.&lt;工具&gt; 用）。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>展示名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>启用开关（连接时生效）。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>传输类型：stdio / streamable-http / http-sse。</summary>
    public string Transport { get; set; } = "streamable-http";

    /// <summary>服务器地址（仅 streamable-http / http-sse 使用；http/https 白名单）。</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>请求头（Authorization 等；仅两种 HTTP 传输使用）。</summary>
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>可执行文件（仅 stdio 使用；白名单：npx/node/python/python3/uvx/uv/dotnet）。</summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>启动参数（仅 stdio 使用）。</summary>
    public List<string> Args { get; set; } = new();

    /// <summary>附加环境变量（仅 stdio 使用）。</summary>
    public Dictionary<string, string> Env { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// 外部服务器运行状态视图（API GET 返回；配置字段已脱敏）。
/// </summary>
public sealed class McpExternalServerStateDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public string Transport { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    /// <summary>请求头脱敏视图（值掩码 ****尾4）。</summary>
    public Dictionary<string, string> HeadersMasked { get; set; } = new();

    public string Command { get; set; } = string.Empty;

    public IReadOnlyList<string> Args { get; set; } = Array.Empty<string>();

    /// <summary>环境变量脱敏视图。</summary>
    public Dictionary<string, string> EnvMasked { get; set; } = new();

    /// <summary>是否已连接（会话已建立）。</summary>
    public bool Connected { get; set; }

    /// <summary>工具数（连接后 tools/list 快照）。</summary>
    public int ToolCount { get; set; }

    /// <summary>协商到的 MCP 协议版本。</summary>
    public string ProtocolVersion { get; set; } = string.Empty;

    /// <summary>服务器标识（initialize serverInfo.name）。</summary>
    public string ServerInfoName { get; set; } = string.Empty;

    /// <summary>最近一次错误（无则空）。</summary>
    public string LastError { get; set; } = string.Empty;
}

/// <summary>
/// 外部服务器工具清单项（GET /api/mcp-center/servers/{id}/tools）。
/// FullName 即 universal_tool 的 tool 参数（mcp.&lt;id&gt;.&lt;name&gt;）。
/// </summary>
public sealed class McpExternalToolDto
{
    /// <summary>完整转发名：mcp.&lt;服务器id&gt;.&lt;工具名&gt;。</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>所属外部服务器 id（v2.3.0：工具测试台按服务器发起调用时用）。</summary>
    public string ServerId { get; set; } = string.Empty;

    /// <summary>外部工具原生名（tools/list 返回）。</summary>
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>输入 schema 原文 JSON（无则空）。</summary>
    public string InputSchemaJson { get; set; } = string.Empty;
}

/// <summary>
/// 外部服务器新增/更新请求体（POST/PUT /api/mcp-center/servers）。
/// </summary>
public sealed class McpExternalServerUpsertDto
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public bool? Enabled { get; set; }

    public string? Transport { get; set; }

    public string? Url { get; set; }

    public Dictionary<string, string>? Headers { get; set; }

    public string? Command { get; set; }

    public List<string>? Args { get; set; }

    public Dictionary<string, string>? Env { get; set; }
}

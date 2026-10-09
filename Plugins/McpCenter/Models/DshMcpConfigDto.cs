using System.Text.Json;

namespace ForgeSelf.Api.Plugins.McpCenter.Models;

/// <summary>dsh 的一个 MCP 客户端条目（对应 cordis.patch.yml 里 name = @deepseek-ai/dsh-mcp-client 的 insert 行）。</summary>
public class DshMcpServerDto
{
    /// <summary>条目 id（补丁行 id，默认 mcp-forgeself）。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>暴露给 dsh 的 serverName。</summary>
    public string ServerName { get; set; } = string.Empty;

    /// <summary>传输类型：streamable-http / http-sse / stdio。</summary>
    public string Transport { get; set; } = string.Empty;

    /// <summary>http 系传输的地址。</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>stdio 传输的命令。</summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>stdio 传输的参数。</summary>
    public List<string> Args { get; set; } = new();

    /// <summary>http 传输的自定义请求头。</summary>
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>是否启用（= !补丁行的 disabled）。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>所在行号（列表顺序，便于定位，1 基）。</summary>
    public int Line { get; set; }
}

/// <summary>dsh MCP 配置总览（GET api/mcp-center/dsh）。</summary>
public class DshMcpConfigDto
{
    public string Profile { get; set; } = "desktop";
    public string ConfigPath { get; set; } = string.Empty;
    public bool ConfigExists { get; set; }

    /// <summary>默认写入地址 = 当前网关地址 + /mcp（供界面预填）。</summary>
    public string DefaultUrl { get; set; } = string.Empty;

    // ── 写入目标条目字段（DshMcpConfigWriter.GetStatus/Upsert 与前端 dsh 面板共用）──
    // Dto 重构为「配置总览」形状后一度漏掉这几个字段，导致 Writer 编译不过（CS0117/CS1061）。
    // 这里按 Writer 与前端 types/dsh.ts 的既有用法补回，纯新增、不改变既有语义。
    /// <summary>本次写入的目标条目 id（补丁行 id，默认 mcp-forgeself）。</summary>
    public string EntryId { get; set; } = string.Empty;

    /// <summary>写入条目暴露给 dsh 的 serverName。</summary>
    public string ServerName { get; set; } = string.Empty;

    /// <summary>写入条目的传输类型（默认 streamable-http）。</summary>
    public string Transport { get; set; } = string.Empty;

    /// <summary>目标条目当前已配置的 url（未配置时回落到 DefaultUrl）。</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>目标条目是否已存在于 cordis.patch.yml。</summary>
    public bool EntryExists { get; set; }

    /// <summary>当前 dsh 已配置的全部 MCP 客户端条目。</summary>
    public List<DshMcpServerDto> Servers { get; set; } = new();

    /// <summary>重复的条目 id（同一 id 出现多次，需要去重）。</summary>
    public List<string> DuplicateIds { get; set; } = new();

    public string? LastError { get; set; }
}

/// <summary>新增/编辑 dsh MCP 条目（POST/PUT api/mcp-center/dsh/servers）。</summary>
public class DshMcpServerUpsertDto
{
    /// <summary>条目 id（新增可省，默认由 serverName 派生为 mcp-&lt;name&gt;；编辑时不可改）。</summary>
    public string? Id { get; set; }

    public string? ServerName { get; set; }

    /// <summary>streamable-http / http-sse / stdio（默认 streamable-http）。</summary>
    public string? Transport { get; set; }

    /// <summary>http 系传输的地址。</summary>
    public string? Url { get; set; }

    /// <summary>stdio 传输的命令。</summary>
    public string? Command { get; set; }

    /// <summary>stdio 参数：空格分隔，或一行一个（两种都接受）。</summary>
    public string? Args { get; set; }

    /// <summary>自定义请求头（每行 Key: Value）。</summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>是否启用（默认 true → 不写 disabled 行）。</summary>
    public bool? Enabled { get; set; }
}

/// <summary>快捷写入 ForgeSelf 地址（POST api/mcp-center/dsh，保留旧版兼容）。</summary>
public class DshMcpConfigWriteDto
{
    public string? Profile { get; set; }
    public string? ServerId { get; set; }
    public string? ServerName { get; set; }
    public string? Url { get; set; }
}

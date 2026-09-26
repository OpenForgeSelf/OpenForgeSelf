using System.Text;
using System.Text.Json;
using ForgeSelf.Api.Plugins.McpCenter.Models;
using ForgeSelf.Api.Plugins.McpCenter.Services.McpClient;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.McpCenter.Services;

/// <summary>
/// 外部 MCP 服务器配置存储（v2.1.0）：external-servers.json 读写。
/// 与网关自身 config.json 分离（本文件管「外部连接」，config.json 管「自身监听」）。
/// 原子写（临时文件 + 改名）；校验传输类型/URL scheme/stdio command 白名单。
/// 文件保留密钥明文（本地工具，用户自己的配置）；API 返回由 Manager 脱敏。
/// </summary>
public sealed class ExternalServersStore
{
    public const string FileName = "external-servers.json";

    /// <summary>stdio 可执行文件白名单（与 StdioMcpTransport.AllowedCommands 一致）。</summary>
    public static readonly string[] AllowedCommands = StdioMcpTransport.AllowedCommands;

    private readonly string _filePath;
    private readonly object _lock = new();

    public ExternalServersStore(string dataDir)
    {
        _filePath = Path.Combine(dataDir, FileName);
    }

    public string FilePath => _filePath;

    public List<McpExternalServerConfig> Load()
    {
        if (!File.Exists(_filePath)) return new();
        try
        {
            var json = File.ReadAllText(_filePath, Encoding.UTF8);
            return JsonSerializer.Deserialize<List<McpExternalServerConfig>>(json)
                ?? new List<McpExternalServerConfig>();
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[McpCenter] 读取 external-servers.json 失败（按空清单处理）: {0}", ex.Message);
            return new();
        }
    }

    public void Save(List<McpExternalServerConfig> configs)
    {
        lock (_lock)
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(configs, new JsonSerializerOptions { WriteIndented = true });
            var tmp = _filePath + ".tmp";
            File.WriteAllText(tmp, json, new UTF8Encoding(false));
            File.Move(tmp, _filePath, overwrite: true);
        }
    }

    /// <summary>校验并归一化一条配置（新增/更新共用）。返回修正后的配置；不合法抛 McpClientException。</summary>
    public static McpExternalServerConfig ValidateAndNormalize(McpExternalServerConfig cfg)
    {
        var id = (cfg.Id ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(id) || id.Contains(' ') || id.Contains('.'))
            throw new McpClientException("服务器 id 必填且须为小写 kebab-case（不含空格与点，点保留给工具命名空间）");
        if (string.IsNullOrWhiteSpace(cfg.Name))
            throw new McpClientException("服务器名称必填");

        var transport = (cfg.Transport ?? string.Empty).Trim().ToLowerInvariant();
        if (transport is not ("stdio" or "streamable-http" or "http-sse"))
            throw new McpClientException($"传输类型不支持: {cfg.Transport}（支持 stdio / streamable-http / http-sse）");

        var normalized = new McpExternalServerConfig
        {
            Id = id,
            Name = cfg.Name.Trim(),
            Enabled = cfg.Enabled,
            Transport = transport,
            Url = cfg.Url?.Trim() ?? string.Empty,
            Command = cfg.Command?.Trim() ?? string.Empty,
            Args = cfg.Args?.Where(a => !string.IsNullOrWhiteSpace(a)).ToList() ?? new(),
            Headers = cfg.Headers?.Where(kv => !string.IsNullOrWhiteSpace(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase) ?? new(),
            Env = cfg.Env?.Where(kv => !string.IsNullOrWhiteSpace(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value ?? string.Empty, StringComparer.Ordinal) ?? new()
        };

        if (transport is "streamable-http" or "http-sse")
        {
            if (string.IsNullOrWhiteSpace(normalized.Url))
                throw new McpClientException($"{transport} 传输必须配置 url");
            if (!Uri.TryCreate(normalized.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                throw new McpClientException("外部 MCP 地址仅支持 http/https");
        }

        if (transport == "stdio")
        {
            if (string.IsNullOrWhiteSpace(normalized.Command))
                throw new McpClientException("stdio 传输必须配置 command");
            if (!AllowedCommands.Contains(normalized.Command, StringComparer.OrdinalIgnoreCase))
                throw new McpClientException($"stdio command 不在白名单: {normalized.Command}（允许: {string.Join("/", AllowedCommands)}）");
        }

        return normalized;
    }
}

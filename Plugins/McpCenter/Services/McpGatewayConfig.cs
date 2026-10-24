using System.Text.Json;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.McpCenter.Services;

/// <summary>
/// MCP 网关配置：三级优先序（环境变量 → 插件数据根 config.json → 内置默认）。
/// 环境变量：FORGESELF_MCP_GATEWAY_PORT / _HOST / _TOKEN / _STATELESS / _TIMEOUT_MS。
/// e2e 多宿主实例并存时靠环境变量错开端口，生产靠 config.json 固化。
/// Stateless（v2.4.0）：会话模式开关，默认无状态（根治宿主/网关重启后旧 Mcp-Session-Id 吃 404/-32001，
/// 见 034-mcp-center.md「有状态会话语义」）；页面/配置切回有状态无需发版。
/// ExecutionTimeoutMs（v2.5.2）：外部 MCP 执行请求超时（毫秒），默认 5 分钟；对保存后新建/重连的连接生效，
/// 合法域 1s~1h（防「永不超时」挂死）；连接建立阶段子超时（帧协商/endpoint 发现）不受其约束。
/// </summary>
public sealed class McpGatewayConfig
{
    public const string DefaultHost = "127.0.0.1";
    public const int DefaultPort = 18890;
    public const string DefaultToken = "";
    public const bool DefaultStateless = true;

    /// <summary>外部 MCP 执行请求超时默认值（v2.5.2）：5 分钟——30s 对 DeepWiki/Playwright/npx 冷启动类慢工具偏短（用户 2026-10-18 拍板）。</summary>
    public const int DefaultExecutionTimeoutMs = 300000;

    /// <summary>合法域下限 1 秒：拒绝 0/负值造成「请求即死」。</summary>
    public const int MinExecutionTimeoutMs = 1000;

    /// <summary>合法域上限 1 小时：拒绝「永不超时」挂死线程/连接。</summary>
    public const int MaxExecutionTimeoutMs = 3600000;

    public const string EnvPort = "FORGESELF_MCP_GATEWAY_PORT";
    public const string EnvHost = "FORGESELF_MCP_GATEWAY_HOST";
    public const string EnvToken = "FORGESELF_MCP_GATEWAY_TOKEN";
    public const string EnvStateless = "FORGESELF_MCP_GATEWAY_STATELESS";
    public const string EnvExecutionTimeoutMs = "FORGESELF_MCP_GATEWAY_TIMEOUT_MS";

    private const string ConfigFileName = "config.json";

    public int Port { get; set; } = DefaultPort;
    public string ListenHost { get; set; } = DefaultHost;
    public string Token { get; set; } = DefaultToken;

    /// <summary>允许浏览器跨域访问 /mcp 的来源；空列表默认拒绝所有跨域来源。</summary>
    public List<string> AllowedOrigins { get; set; } = [];

    /// <summary>true=无状态（每请求独立，不发 session id）；false=有状态（initialize 签发 Mcp-Session-Id）。</summary>
    public bool Stateless { get; set; } = DefaultStateless;

    /// <summary>外部 MCP 执行请求超时（毫秒，v2.5.2）：驱动三传输 tools/call、tools/list 等请求的取消预算与 HttpClient 超时。</summary>
    public int ExecutionTimeoutMs { get; set; } = DefaultExecutionTimeoutMs;

    public string ListenUrl => $"http://{ListenHost}:{Port}";

    /// <summary>从插件数据根目录加载配置；文件缺失时写入默认值（幂等，可安全反复调用）。</summary>
    public static McpGatewayConfig Load(string dataDirectory)
    {
        var cfg = new McpGatewayConfig();
        var configPath = Path.Combine(dataDirectory, ConfigFileName);

        try
        {
            if (File.Exists(configPath))
            {
                // 大小写不敏感：config.json 属用户可手改文件，小写/驼峰键都应被识别（实测踩坑：手写
                // {"port": ...} 因默认大小写敏感被忽略 → 绑定回退默认端口 18890）。
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var fromFile = JsonSerializer.Deserialize<McpGatewayConfig>(File.ReadAllText(configPath), options);
                if (fromFile != null)
                {
                    cfg.Port = fromFile.Port;
                    cfg.ListenHost = string.IsNullOrWhiteSpace(fromFile.ListenHost) ? DefaultHost : fromFile.ListenHost;
                    cfg.Token = fromFile.Token ?? DefaultToken;
                    cfg.AllowedOrigins = NormalizeAllowedOrigins(fromFile.AllowedOrigins, rejectInvalid: false);
                    // 旧版 config.json 无 stateless 键 ⇒ 反序列化保持默认 true（升级即自动转无状态，设计拍板 S1）
                    cfg.Stateless = fromFile.Stateless;
                    // 旧版 config.json 无 executionTimeoutMs 键 ⇒ 反序列化为 0，隐式保持默认 300000（升级兼容不记警）；
                    // 显式写了越界值（非 0）⇒ 回落默认并记警（不抛，与 Load 容错风格一致）
                    if (IsValidExecutionTimeout(fromFile.ExecutionTimeoutMs))
                    {
                        cfg.ExecutionTimeoutMs = fromFile.ExecutionTimeoutMs;
                    }
                    else if (fromFile.ExecutionTimeoutMs != 0)
                    {
                        XTrace.Log.Warn("[McpCenter] config.json 的 executionTimeoutMs={0} 越界（合法 {1}~{2}ms），回落默认 {3}ms",
                            fromFile.ExecutionTimeoutMs, MinExecutionTimeoutMs, MaxExecutionTimeoutMs, DefaultExecutionTimeoutMs);
                    }
                }
            }
            else
            {
                Directory.CreateDirectory(dataDirectory);
                File.WriteAllText(configPath, JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));
                XTrace.Log.Info("[McpCenter] 已生成默认配置: {0}", configPath);
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("[McpCenter] 读取配置失败，使用默认值: {0}", ex.Message);
        }

        // 环境变量优先于 config.json（e2e / 多实例并存场景）
        var envPort = Environment.GetEnvironmentVariable(EnvPort);
        if (int.TryParse(envPort, out var port) && port is >= 1024 and <= 65535)
        {
            cfg.Port = port;
        }

        var envHost = Environment.GetEnvironmentVariable(EnvHost);
        if (!string.IsNullOrWhiteSpace(envHost))
        {
            cfg.ListenHost = envHost;
        }

        var envToken = Environment.GetEnvironmentVariable(EnvToken);
        if (envToken != null)
        {
            cfg.Token = envToken;
        }

        var envStateless = Environment.GetEnvironmentVariable(EnvStateless);
        if (!string.IsNullOrWhiteSpace(envStateless))
        {
            // 接受 "1/0"、"true/false"（大小写不敏感）；无法识别则保持低一级优先序的值
            if (envStateless.Equals("1", StringComparison.Ordinal) || envStateless.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                cfg.Stateless = true;
            }
            else if (envStateless.Equals("0", StringComparison.Ordinal) || envStateless.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                cfg.Stateless = false;
            }
        }

        var envTimeout = Environment.GetEnvironmentVariable(EnvExecutionTimeoutMs);
        if (!string.IsNullOrWhiteSpace(envTimeout))
        {
            if (int.TryParse(envTimeout, out var timeoutMs) && IsValidExecutionTimeout(timeoutMs))
            {
                cfg.ExecutionTimeoutMs = timeoutMs;
            }
            else
            {
                XTrace.Log.Warn("[McpCenter] 环境变量 {0}={1} 非法（须为 {2}~{3} 的整数毫秒），保持 {4}ms",
                    EnvExecutionTimeoutMs, envTimeout, MinExecutionTimeoutMs, MaxExecutionTimeoutMs, cfg.ExecutionTimeoutMs);
            }
        }

        return cfg;
    }

    /// <summary>执行超时合法域判据（PUT 校验与 Load 回落共用单点）：1 秒 ~ 1 小时。</summary>
    public static bool IsValidExecutionTimeout(int ms) => ms is >= MinExecutionTimeoutMs and <= MaxExecutionTimeoutMs;

    /// <summary>规范化并验证 HTTP(S) Origin；仅接受 scheme + authority，不含路径、查询或片段。</summary>
    public static bool TryNormalizeOrigin(string? value, out string normalized)
    {
        normalized = string.Empty;
        var candidate = value?.Trim();
        if (string.IsNullOrEmpty(candidate) || !Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if ((uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.UserInfo) || uri.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        normalized = uri.GetLeftPart(UriPartial.Authority);
        return true;
    }

    /// <summary>配置更新严格拒绝无效 Origin；旧/手改配置加载时丢弃无效项并记日志。</summary>
    public static List<string> NormalizeAllowedOrigins(IEnumerable<string>? origins, bool rejectInvalid = true)
    {
        var result = new List<string>();
        foreach (var item in origins ?? [])
        {
            if (!TryNormalizeOrigin(item, out var origin))
            {
                if (rejectInvalid)
                {
                    throw new ArgumentException($"无效的跨域来源：{item}。请输入完整的 http:// 或 https:// Origin，不含路径。");
                }

                XTrace.Log.Warn("[McpCenter] 忽略无效的 MCP 网关允许来源配置: {0}", item);
                continue;
            }

            if (!result.Contains(origin, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(origin);
            }
        }

        return result;
    }

    public bool IsOriginAllowed(string origin)
    {
        if (!TryNormalizeOrigin(origin, out var normalized))
        {
            return false;
        }

        return AllowedOrigins.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }
}

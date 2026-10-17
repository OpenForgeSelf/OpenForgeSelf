using System.Text.Json;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.McpCenter.Services;

/// <summary>
/// MCP 网关配置：三级优先序（环境变量 → 插件数据根 config.json → 内置默认）。
/// 环境变量：FORGESELF_MCP_GATEWAY_PORT / _HOST / _TOKEN / _STATELESS。
/// e2e 多宿主实例并存时靠环境变量错开端口，生产靠 config.json 固化。
/// Stateless（v2.4.0）：会话模式开关，默认无状态（根治宿主/网关重启后旧 Mcp-Session-Id 吃 404/-32001，
/// 见 034-mcp-center.md「有状态会话语义」）；页面/配置切回有状态无需发版。
/// </summary>
public sealed class McpGatewayConfig
{
    public const string DefaultHost = "127.0.0.1";
    public const int DefaultPort = 18890;
    public const string DefaultToken = "";
    public const bool DefaultStateless = true;

    public const string EnvPort = "FORGESELF_MCP_GATEWAY_PORT";
    public const string EnvHost = "FORGESELF_MCP_GATEWAY_HOST";
    public const string EnvToken = "FORGESELF_MCP_GATEWAY_TOKEN";
    public const string EnvStateless = "FORGESELF_MCP_GATEWAY_STATELESS";

    private const string ConfigFileName = "config.json";

    public int Port { get; set; } = DefaultPort;
    public string ListenHost { get; set; } = DefaultHost;
    public string Token { get; set; } = DefaultToken;

    /// <summary>true=无状态（每请求独立，不发 session id）；false=有状态（initialize 签发 Mcp-Session-Id）。</summary>
    public bool Stateless { get; set; } = DefaultStateless;

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
                    // 旧版 config.json 无 stateless 键 ⇒ 反序列化保持默认 true（升级即自动转无状态，设计拍板 S1）
                    cfg.Stateless = fromFile.Stateless;
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

        return cfg;
    }
}

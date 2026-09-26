using System.Text.Json;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.McpCenter.Services;

/// <summary>
/// MCP 网关配置：三级优先序（环境变量 → 插件数据根 config.json → 内置默认）。
/// 环境变量：FORGESELF_MCP_GATEWAY_PORT / FORGESELF_MCP_GATEWAY_HOST / FORGESELF_MCP_GATEWAY_TOKEN。
/// e2e 多宿主实例并存时靠环境变量错开端口，生产靠 config.json 固化。
/// </summary>
public sealed class McpGatewayConfig
{
    public const string DefaultHost = "127.0.0.1";
    public const int DefaultPort = 18889;
    public const string DefaultToken = "";

    public const string EnvPort = "FORGESELF_MCP_GATEWAY_PORT";
    public const string EnvHost = "FORGESELF_MCP_GATEWAY_HOST";
    public const string EnvToken = "FORGESELF_MCP_GATEWAY_TOKEN";

    private const string ConfigFileName = "config.json";

    public int Port { get; set; } = DefaultPort;
    public string ListenHost { get; set; } = DefaultHost;
    public string Token { get; set; } = DefaultToken;

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
                // {"port": ...} 因默认大小写敏感被忽略 → 绑定回退默认端口 18889）。
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var fromFile = JsonSerializer.Deserialize<McpGatewayConfig>(File.ReadAllText(configPath), options);
                if (fromFile != null)
                {
                    cfg.Port = fromFile.Port;
                    cfg.ListenHost = string.IsNullOrWhiteSpace(fromFile.ListenHost) ? DefaultHost : fromFile.ListenHost;
                    cfg.Token = fromFile.Token ?? DefaultToken;
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

        return cfg;
    }
}

using System.Text.Json;
using ForgeSelf.Api.Plugins.McpCenter.Models;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.McpCenter.Services;

/// <summary>
/// MCP 中心运行时单例：持有配置（可变）、网关服务器、数据根目录；
/// 供网关配置 API（api/mcp-center/config）查询与更新。
/// 更新 = 改配置单例 → 写回 config.json → 热重启内置服务器（Stop→Start，新配置生效）；失败回滚旧配置。
/// 配置键与 v1.0.0 一致（config.json 的 port/listenHost/token；环境变量 FORGESELF_MCP_GATEWAY_* 兼容）。
/// </summary>
public sealed class McpCenterRuntime
{
    private const string ConfigFileName = "config.json";

    private readonly string _dataDir;
    private readonly McpGatewayConfig _config;
    private readonly McpGatewayServer _server;
    private readonly string _version;

    public McpCenterRuntime(string dataDir, McpGatewayConfig config, McpGatewayServer server, string version)
    {
        _dataDir = dataDir;
        _config = config;
        _server = server;
        _version = version;
    }

    /// <summary>当前配置视图（令牌脱敏）。</summary>
    public McpCenterConfigDto GetInfo()
    {
        var token = _config.Token ?? "";
        return new McpCenterConfigDto
        {
            Port = _config.Port,
            ListenHost = _config.ListenHost,
            ListenUrl = _config.ListenUrl,
            HasToken = !string.IsNullOrEmpty(token),
            TokenMasked = MaskToken(token),
            IsRunning = _server.IsRunning,
            Version = _version
        };
    }

    /// <summary>
    /// 应用配置更新：校验 → 更新单例 → 写 config.json → 热重启服务器；失败回滚旧配置并抛出异常。
    /// 端口/监听地址/令牌任一可省略（不传=不变）；token 传空串=清除鉴权。
    /// </summary>
    public async Task<McpCenterConfigDto> ApplyUpdateAsync(McpCenterConfigUpdateDto update)
    {
        if (update == null)
        {
            throw new ArgumentException("请求体不能为空");
        }

        var oldPort = _config.Port;
        var oldHost = _config.ListenHost;
        var oldToken = _config.Token;

        try
        {
            if (update.Port is { } port)
            {
                if (port is < 1024 or > 65535)
                {
                    throw new ArgumentException($"端口必须介于 1024-65535（当前 {port}）");
                }
                _config.Port = port;
            }

            if (!string.IsNullOrWhiteSpace(update.ListenHost))
            {
                _config.ListenHost = update.ListenHost.Trim();
            }

            if (update.Token != null)
            {
                _config.Token = update.Token.Trim();
            }

            await PersistAsync();
            await _server.StopAsync();
            await _server.StartAsync();

            XTrace.Log.Info("[McpCenter] 网关配置已更新并热重启: {0}", _config.ListenUrl);
            return GetInfo();
        }
        catch (Exception ex)
        {
            _config.Port = oldPort;
            _config.ListenHost = oldHost;
            _config.Token = oldToken;
            try
            {
                await _server.StopAsync();
                await _server.StartAsync();
            }
            catch (Exception rollbackEx)
            {
                XTrace.Log.Error("[McpCenter] 配置回滚后重启失败: {0}", rollbackEx.Message);
            }

            XTrace.Log.Error("[McpCenter] 网关配置更新失败，已回滚: {0}", ex.Message);
            throw;
        }
    }

    private async Task PersistAsync()
    {
        var path = Path.Combine(_dataDir, ConfigFileName);
        try
        {
            Directory.CreateDirectory(_dataDir);
            var json = JsonSerializer.Serialize(new
            {
                port = _config.Port,
                listenHost = _config.ListenHost,
                token = _config.Token
            }, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(path, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"写回 config.json 失败: {ex.Message}", ex);
        }
    }

    private static string MaskToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return "";
        }
        return token.Length <= 4 ? "••••" : "••••" + token[^4..];
    }
}

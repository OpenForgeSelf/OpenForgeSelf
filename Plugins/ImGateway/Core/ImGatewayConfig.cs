using System.Text.Json;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.ImGateway.Core;

/// <summary>
/// 企业微信「智能机器人」配置（长连接形态，v2.0.0 起唯一通道）。
/// 不需要 CorpId/AgentId/Token/EncodingAESKey，也不需要 access_token 中控——
/// 长连接口令由 BotId+Secret 订阅后直接收发，无加解密。
/// 获取方式：企微管理后台 → 智能机器人 → 开启「API 模式」→ 选「长连接」→ 得到 BotID 与 Secret；
/// 也可经扫码授权（POST /api/im-gateway/wecom/scan-auth）自动回填。
/// </summary>
public class WeComConfig
{
    /// <summary>是否启用该通道。</summary>
    public bool Enabled { get; set; }

    /// <summary>绑定到哪个 Agent（空=默认，由 AIAgent 决定）。</summary>
    public string? BoundAgentId { get; set; }

    /// <summary>绑定聊天模型（形如 provider:upstreamModelId；空=默认）。</summary>
    public string? BoundChatModelId { get; set; }

    /// <summary>智能机器人 BotID（长连接身份标识）。</summary>
    public string BotId { get; set; } = string.Empty;

    /// <summary>长连接专用密钥 Secret（勿与自建应用 Secret 混淆）。</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>企微 CLI 可执行文件路径（扫码授权用；空则按环境变量 WECOM_CLI_PATH → 常见路径自动探测）。</summary>
    public string? CliPath { get; set; }
}

/// <summary>插件总配置（v2.0.0：仅企微长连接一通道，持久化为加密落库）。</summary>
public class ImGatewayConfig
{
    /// <summary>企微通道配置。</summary>
    public WeComConfig WeCom { get; set; } = new();
}

/// <summary>配置存储契约（解耦具体持久化方式）。</summary>
public interface IConfigStore
{
    ImGatewayConfig Load();
    void Save(ImGatewayConfig config);
}

/// <summary>
/// 基于 JSON 文件的配置存储（v1.1.0 遗留，v2.0.0 由 DbConfigStore 取代后仅作迁移读取源）。
/// 路径：~/.forgeself/plugins/im-gateway/config.json。
/// </summary>
public class FileConfigStore : IConfigStore
{
    private static readonly object Lock = new();
    private readonly string _filePath;

    /// <summary>读写共用的 JSON 选项：camelCase 命名（Save 与 Load 必须同一套，否则契约不一致）。</summary>
    private static readonly JsonSerializerOptions StoreOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>对外暴露读写共用选项（供测试复用，确保序列化契约一致、可回归）。</summary>
    public static JsonSerializerOptions JsonOptions => StoreOptions;

    public FileConfigStore()
    {
        var dir = ResolvePluginDataDir();
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "config.json");
    }

    /// <summary>
    /// 解析插件数据目录：优先环境变量 <c>FORGESelf_PLUGIN_CONFIG_DIR</c>（e2e 等隔离场景注入临时目录，
    /// 避免多个宿主实例共享用户目录下的 config.json/sessions.json 导致非确定性与互相污染）；
    /// 未设置时回退默认 <c>~/.forgeself/plugins/im-gateway</c>（真实运行态，向后兼容；Windows 目录大小写不敏感）。
    /// </summary>
    internal static string ResolvePluginDataDir()
    {
        var overrideDir = Environment.GetEnvironmentVariable("FORGESelf_PLUGIN_CONFIG_DIR");
        if (!string.IsNullOrWhiteSpace(overrideDir))
            return overrideDir;
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".forgeself", "plugins", "im-gateway");
    }

    public ImGatewayConfig Load()
    {
        lock (Lock)
        {
            if (!File.Exists(_filePath)) return new ImGatewayConfig();
            try
            {
                var json = File.ReadAllText(_filePath);
                var cfg = JsonSerializer.Deserialize<ImGatewayConfig>(json, StoreOptions);
                return cfg ?? new ImGatewayConfig();
            }
            catch (Exception ex)
            {
                // 反序列化失败严禁静默吞掉（历史曾因此把"落盘成功但读回空"误判为正常，
                // 掩盖了 Save/Load 契约不一致的真实 bug）。记录日志便于回归暴露，
                // 仍返回空配置避免接口 500。
                XTrace.Log.Error($"[ImGateway] 读取配置失败，返回空配置：{ex.Message}", ex);
                return new ImGatewayConfig();
            }
        }
    }

    public void Save(ImGatewayConfig config)
    {
        lock (Lock)
        {
            var json = JsonSerializer.Serialize(config, StoreOptions);
            File.WriteAllText(_filePath, json);
        }
    }
}

/// <summary>会话映射 + 消息去重存储（文件型，密钥为「通道:会话:用户」→ Agent 会话 id）。</summary>
public class SessionMappingStore
{
    private static readonly object Lock = new();
    private readonly string _filePath;

    private class StoreModel
    {
        public Dictionary<string, MappingEntry> Mappings { get; set; } = new();
        public Dictionary<string, List<string>> Processed { get; set; } = new();
    }

    private class MappingEntry
    {
        public string SessionId { get; set; } = string.Empty;
        public string? AgentId { get; set; }
        public string? ModelId { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public SessionMappingStore()
    {
        var dir = FileConfigStore.ResolvePluginDataDir();
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "sessions.json");
    }

    private StoreModel Read()
    {
        if (!File.Exists(_filePath)) return new StoreModel();
        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<StoreModel>(json) ?? new StoreModel();
        }
        catch
        {
            return new StoreModel();
        }
    }

    private void Write(StoreModel m)
    {
        var json = JsonSerializer.Serialize(m, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }

    /// <summary>是否已处理过该消息（幂等去重）。</summary>
    public bool IsProcessed(string channelType, string msgId)
    {
        if (string.IsNullOrEmpty(msgId)) return false;
        lock (Lock)
        {
            var m = Read();
            return m.Processed.TryGetValue(channelType, out var list) && list.Contains(msgId);
        }
    }

    /// <summary>登记已处理消息（每个通道保留最近 500 条）。</summary>
    public void MarkProcessed(string channelType, string msgId)
    {
        if (string.IsNullOrEmpty(msgId)) return;
        lock (Lock)
        {
            var m = Read();
            if (!m.Processed.TryGetValue(channelType, out var list))
            {
                list = new List<string>();
                m.Processed[channelType] = list;
            }
            list.Add(msgId);
            if (list.Count > 500) list.RemoveRange(0, list.Count - 500);
            Write(m);
        }
    }

    /// <summary>按「通道:会话:用户」取或建 Agent 会话映射。</summary>
    public (string SessionId, string? AgentId, string? ModelId) GetOrCreateSession(
        string channelType, string conversationId, string platformUserId, string? boundAgentId, string? boundModelId)
    {
        var key = $"{channelType}:{conversationId}:{platformUserId}";
        lock (Lock)
        {
            var m = Read();
            if (m.Mappings.TryGetValue(key, out var entry))
            {
                entry.UpdatedAt = DateTime.Now;
                Write(m);
                return (entry.SessionId, entry.AgentId, entry.ModelId);
            }
            var sessionId = Guid.NewGuid().ToString("N");
            m.Mappings[key] = new MappingEntry
            {
                SessionId = sessionId,
                AgentId = boundAgentId,
                ModelId = boundModelId,
                UpdatedAt = DateTime.Now
            };
            Write(m);
            return (sessionId, boundAgentId, boundModelId);
        }
    }
}

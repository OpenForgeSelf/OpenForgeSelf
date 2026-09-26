using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.ImGateway.Core;
using ForgeSelf.Api.Plugins.ImGateway.Entities;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.ImGateway.Services;

/// <summary>
/// 基于 SQLite（XCode 实体表 ImGatewayChannel）的配置存储（v2.0.0）。
/// Secret 以密文列 <see cref="ImGatewayChannel.SecretCipher"/> 落库（经宿主 <see cref="ISecretEncryptionService"/>
/// AES-256 加密，密钥由宿主配置/环境变量/机器派生决定），BotId 等非敏感字段明文。
/// 唯一性/存在性判断一律直查数据库（plugin-development 铁律 §四-11：不复用实体缓存）。
/// 加密服务为<b>运行期惰性解析</b>：Apply 阶段宿主接缝（ProvideHostServices）尚未 seed，
/// 而 Save/Load 发生在请求时（接缝已就绪），故构造只收解析函数。
/// </summary>
public class DbConfigStore : IConfigStore
{
    private readonly Func<ISecretEncryptionService?> _encProvider;
    private static readonly ISecretEncryptionService Fallback = new NullSecretEncryptionService();

    public DbConfigStore(Func<ISecretEncryptionService?> encProvider)
    {
        _encProvider = encProvider;
    }

    private ISecretEncryptionService Enc
    {
        get
        {
            var enc = _encProvider();
            if (enc != null) return enc;
            XTrace.Log.Warn("[ImGateway] ISecretEncryptionService 不可用（宿主接缝未 seed），Secret 将以明文兜底入库");
            return Fallback;
        }
    }

    /// <summary>读取配置：表内取最新一行（单行配置），无行则返回空配置。</summary>
    public ImGatewayConfig Load()
    {
        var cfg = new ImGatewayConfig();
        var row = ImGatewayChannel.FindAll()
            .OrderByDescending(r => r.Id)
            .FirstOrDefault();
        if (row == null) return cfg;

        cfg.WeCom = new WeComConfig
        {
            Enabled = row.Enabled,
            BoundAgentId = row.BoundAgentId,
            BoundChatModelId = row.BoundChatModelId,
            BotId = row.BotId ?? string.Empty,
            Secret = ResolveSecret(row.SecretCipher),
            CliPath = row.CliPath,
        };
        return cfg;
    }

    /// <summary>保存配置：Secret 加密后写入密文列（写路径永远走 v2 密文，不落明文）。</summary>
    public void Save(ImGatewayConfig config)
    {
        var row = ImGatewayChannel.FindAll()
            .OrderByDescending(r => r.Id)
            .FirstOrDefault() ?? new ImGatewayChannel();

        row.Enabled = config.WeCom.Enabled;
        row.BoundAgentId = config.WeCom.BoundAgentId;
        row.BoundChatModelId = config.WeCom.BoundChatModelId;
        row.BotId = config.WeCom.BotId;
        row.SecretCipher = string.IsNullOrEmpty(config.WeCom.Secret)
            ? string.Empty
            : Enc.Encrypt(config.WeCom.Secret);
        row.CliPath = config.WeCom.CliPath;
        row.UpdatedAt = DateTime.Now;
        row.Save();
    }

    /// <summary>解密密文列；解不开（密钥不匹配/损坏）时返回空串并记日志，不抛——用户可重填 Secret。</summary>
    private string ResolveSecret(string? cipher)
    {
        if (string.IsNullOrEmpty(cipher)) return string.Empty;
        if (Enc.TryDecrypt(cipher, out var plain)) return plain;
        XTrace.Log.Warn("[ImGateway] Secret 密文解密失败（密钥不匹配或已损坏），返回空值待重填");
        return string.Empty;
    }

    /// <summary>加密服务缺失时的明文兜底（仅在宿主接缝异常时命中；生产正常路径不会使用）。</summary>
    private sealed class NullSecretEncryptionService : ISecretEncryptionService
    {
        public string Encrypt(string plainText) => plainText;
        public string Decrypt(string cipherText) => cipherText;
        public bool TryDecrypt(string cipherText, out string plainText) { plainText = cipherText; return true; }
        public string Mask(string plainText) => plainText;
    }
}

/// <summary>
/// 基于 SQLite（XCode 实体表 ImGatewaySession / ImGatewayProcessedMsg）的会话映射与消息去重存储（v2.0.0）。
/// 方法签名与 v1.1.0 的 SessionMappingStore 保持一致（网关路由只依赖本实现）。
/// </summary>
public class DbSessionStore
{
    /// <summary>是否已处理过该消息（幂等去重，直查数据库）。</summary>
    public bool IsProcessed(string channelType, string msgId)
    {
        if (string.IsNullOrEmpty(msgId)) return false;
        return ImGatewayProcessedMsg.FindCount(ImGatewayProcessedMsg._.ChannelType == channelType
                                               & ImGatewayProcessedMsg._.MsgId == msgId) > 0;
    }

    /// <summary>登记已处理消息（唯一索引 ChannelType+MsgId 防并发重复；已存在则忽略）。</summary>
    public void MarkProcessed(string channelType, string msgId)
    {
        if (string.IsNullOrEmpty(msgId)) return;
        var row = new ImGatewayProcessedMsg
        {
            ChannelType = channelType,
            MsgId = msgId,
            ProcessedAt = DateTime.Now,
        };
        try
        {
            row.Save();
        }
        catch (Exception ex)
        {
            // 唯一索引冲突 = 并发重复登记，幂等忽略
            XTrace.Log.Debug("[ImGateway] 去重登记冲突（并发重复）忽略: {0}", ex.Message);
        }
    }

    /// <summary>按「通道:会话:用户」取或建 Agent 会话映射（唯一索引 ChannelKey 保证稳定映射）。</summary>
    public (string SessionId, string? AgentId, string? ModelId) GetOrCreateSession(
        string channelType, string conversationId, string platformUserId, string? boundAgentId, string? boundModelId)
    {
        var key = $"{channelType}:{conversationId}:{platformUserId}";
        var row = ImGatewaySession.Find(ImGatewaySession._.ChannelKey == key);
        if (row != null)
        {
            row.UpdatedAt = DateTime.Now;
            row.Save();
            return (row.SessionId, row.AgentId, row.ModelId);
        }

        var sessionId = Guid.NewGuid().ToString("N");
        row = new ImGatewaySession
        {
            ChannelKey = key,
            SessionId = sessionId,
            AgentId = boundAgentId,
            ModelId = boundModelId,
            UpdatedAt = DateTime.Now,
        };
        try
        {
            row.Save();
        }
        catch (Exception ex)
        {
            // 唯一索引冲突 = 并发双建，读回既有映射
            XTrace.Log.Debug("[ImGateway] 会话映射并发冲突，读回既有: {0}", ex.Message);
            var existing = ImGatewaySession.Find(ImGatewaySession._.ChannelKey == key);
            if (existing != null) return (existing.SessionId, existing.AgentId, existing.ModelId);
        }
        return (sessionId, boundAgentId, boundModelId);
    }
}

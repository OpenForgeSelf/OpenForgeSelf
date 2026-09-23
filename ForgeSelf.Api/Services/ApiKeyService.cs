using ForgeSelf.Abstractions;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using NewLife.Log;
using ForgeSelf.Api.Entities;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Security;

namespace ForgeSelf.Api.Services;

/// <summary>
/// API 子密钥服务：子密钥的增删改查 + 认证判定。
/// </summary>
/// <remarks>
/// <para><b>认证判定下沉</b>：核心判定逻辑放在 <see cref="ResolveByToken"/> 而非
/// <c>ApiKeyAuthenticationHandler</c> 内部——Handler 依赖 HTTP 上下文难以单测，而本项目
/// 禁止用 <c>WebApplicationFactory</c> 覆盖连接串，必须让判定脱离 HTTP 才能被 xUnit 覆盖。</para>
/// <para><b>明文红线</b>：明文只在 <c>Create</c>/<c>Roll</c> 的返回值里出现一次；
/// 列表投影（<see cref="ApiKeyItemDto"/>）<b>永不</b>携带明文、<c>KeyCipher</c> 或 <c>KeyHash</c>。</para>
/// </remarks>
public class ApiKeyService
{
    /// <summary>子密钥数量软上限（R16）</summary>
    public const int MaxKeyCount = 50;

    /// <summary>子密钥明文前缀</summary>
    private const string KeyPrefix = "sk-";

    /// <summary>认证方式：命中子密钥</summary>
    public const string AuthMethodApiKeyEntry = "ApiKeyEntry";

    /// <summary>认证方式：命中主密钥</summary>
    public const string AuthMethodBearerApiKey = "BearerApiKey";

    /// <summary>主密钥在身份信息里的固定名称与 ID（主密钥不落子密钥表，故 ID 记 0）</summary>
    public const string MasterKeyName = "主密钥";

    /// <summary>同一密钥 LastUsedAt 的写入节流窗口（60 秒内最多写一次）</summary>
    private static readonly TimeSpan LastUsedThrottleWindow = TimeSpan.FromSeconds(60);

    /// <summary>LastUsedAt 节流表：密钥 ID → 上次写库时间。进程级静态（跨 DI 作用域共享节流效果）</summary>
    private static readonly ConcurrentDictionary<Int64, DateTime> LastUsedThrottle = new();

    private readonly ISecretEncryptionService _encryption;

    public ApiKeyService(ISecretEncryptionService encryption)
    {
        _encryption = encryption;
    }

    #region 查询 / 创建 / 更新 / 启停 / 轮换 / 删除

    /// <summary>
    /// 列出全部子密钥（掩码投影，不含任何明文与密文材料）。
    /// </summary>
    public IList<ApiKeyItemDto> List()
    {
        var entities = ApiKeyCredential.FindAll();
        return entities.OrderBy(e => e.Id).Select(ToDto).ToList();
    }

    /// <summary>
    /// 创建子密钥。明文只在本次返回中出现一次。
    /// </summary>
    /// <param name="req">创建请求（名称为空时自动生成「密钥 N」）</param>
    /// <returns>含明文的创建结果；已达数量上限时返回 null</returns>
    public ApiKeyPlainResultDto? Create(CreateApiKeyRequest req)
    {
        var count = ApiKeyCredential.FindCount();
        if (count >= MaxKeyCount) return null;

        var plain = GeneratePlainKey();
        var entity = new ApiKeyCredential
        {
            Name = string.IsNullOrWhiteSpace(req.Name) ? $"密钥 {count + 1}" : req.Name.Trim(),
            Remark = req.Remark ?? "",
            KeyCipher = _encryption.Encrypt(plain),
            KeyHash = Sha256Hex(plain),
            KeyPrefix = KeyPrefix,
            Enabled = true,
            ExpiresAt = req.ExpiresAt ?? DateTime.MinValue,
            LastUsedAt = DateTime.MinValue,
        };
        entity.Insert();

        return new ApiKeyPlainResultDto
        {
            Item = ToDto(entity),
            PlainKey = plain,
            AuthHeader = $"Authorization: Bearer {plain}",
        };
    }

    /// <summary>
    /// 更新子密钥的名称 / 备注 / 过期时间。
    /// </summary>
    /// <returns>更新后的 DTO；条目不存在返回 null</returns>
    public ApiKeyItemDto? Update(Int64 id, UpdateApiKeyRequest req)
    {
        var entity = ApiKeyCredential.FindById(id);
        if (entity == null) return null;

        if (!string.IsNullOrWhiteSpace(req.Name)) entity.Name = req.Name.Trim();
        if (req.Remark != null) entity.Remark = req.Remark;
        if (req.ExpiresAt.HasValue) entity.ExpiresAt = req.ExpiresAt.Value;

        entity.Save();
        return ToDto(entity);
    }

    /// <summary>
    /// 启停子密钥。停用后该密钥立即失效（且不会回退到主密钥）。
    /// </summary>
    /// <returns>更新后的 DTO；条目不存在返回 null</returns>
    public ApiKeyItemDto? Toggle(Int64 id, Boolean enabled)
    {
        var entity = ApiKeyCredential.FindById(id);
        if (entity == null) return null;

        entity.Enabled = enabled;
        entity.Save();
        return ToDto(entity);
    }

    /// <summary>
    /// 轮换子密钥：换新明文，旧值立即失效。新明文只在本次返回中出现一次。
    /// </summary>
    /// <returns>含新明文的结果；条目不存在返回 null</returns>
    public ApiKeyPlainResultDto? Roll(Int64 id)
    {
        var entity = ApiKeyCredential.FindById(id);
        if (entity == null) return null;

        var plain = GeneratePlainKey();
        entity.KeyCipher = _encryption.Encrypt(plain);
        entity.KeyHash = Sha256Hex(plain);
        entity.Save();

        LastUsedThrottle.TryRemove(entity.Id, out _);

        return new ApiKeyPlainResultDto
        {
            Item = ToDto(entity),
            PlainKey = plain,
            AuthHeader = $"Authorization: Bearer {plain}",
        };
    }

    /// <summary>
    /// 硬删除子密钥，立即失效。
    /// </summary>
    /// <returns>是否删除成功（条目不存在返回 false）</returns>
    public Boolean Delete(Int64 id)
    {
        var entity = ApiKeyCredential.FindById(id);
        if (entity == null) return false;

        entity.Delete();
        LastUsedThrottle.TryRemove(id, out _);
        return true;
    }

    #endregion

    #region 认证判定

    /// <summary>
    /// 按令牌解析身份：先查子密钥（按 SHA-256 摘要 O(1) 定位），未命中再回退主密钥。
    /// </summary>
    /// <remarks>
    /// <b>子密钥命中即终态</b>：命中但已停用 / 已过期时<b>直接返回 null，不回退主密钥</b>，
    /// 否则「吊销子密钥」形同虚设。
    /// </remarks>
    /// <param name="token">待校验的令牌明文</param>
    /// <returns>身份信息；校验失败返回 null（调用方应返回 401）</returns>
    public ApiKeyIdentity? ResolveByToken(string token)
    {
        if (string.IsNullOrEmpty(token)) return null;

        // 1. 子密钥：按摘要索引 O(1) 定位，绝不逐条解密比对
        ApiKeyCredential? entity = null;
        try
        {
            entity = ApiKeyCredential.FindByKeyHash(Sha256Hex(token));
        }
        catch (Exception ex)
        {
            // 表缺失 / 数据库异常时不能让认证 500；记日志后继续走主密钥回退
            XTrace.Log.Warn("查询子密钥失败，回退主密钥判定: {0}", ex.Message);
        }

        if (entity != null)
        {
            if (!entity.Enabled)
            {
                XTrace.Log.Debug("子密钥[{0}] 已停用，拒绝认证（不回退主密钥）", entity.Id);
                return null;
            }

            if (entity.IsExpired)
            {
                XTrace.Log.Debug("子密钥[{0}] 已过期，拒绝认证（不回退主密钥）", entity.Id);
                return null;
            }

            UpdateLastUsedThrottled(entity);

            return new ApiKeyIdentity
            {
                KeyId = entity.Id,
                KeyName = entity.Name,
                AuthMethod = AuthMethodApiKeyEntry,
            };
        }

        // 2. 回退主密钥
        return ResolveMasterKey(token);
    }

    /// <summary>
    /// 回退判定主密钥：解密 <see cref="ForgeSetting.ApiToken"/> 后定长比较。
    /// </summary>
    private ApiKeyIdentity? ResolveMasterKey(string token)
    {
        var cipher = ForgeSetting.Current.ApiToken;
        if (string.IsNullOrEmpty(cipher)) return null;

        if (!_encryption.TryDecrypt(cipher, out var plain) || string.IsNullOrEmpty(plain)) return null;

        // 定长比较，消除字符串比较的时序侧信道
        var ok = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(token),
            Encoding.UTF8.GetBytes(plain));

        return ok
            ? new ApiKeyIdentity { KeyId = 0, KeyName = MasterKeyName, AuthMethod = AuthMethodBearerApiKey }
            : null;
    }

    #endregion

    #region 内部辅助

    /// <summary>
    /// 实体 → 列表 DTO 投影。只做掩码，绝不输出明文 / 密文 / 摘要。
    /// 解密失败（换机器等）时置 <c>CanDecrypt=false</c>，由 UI 提示「请重新生成」。
    /// </summary>
    private ApiKeyItemDto ToDto(ApiKeyCredential entity)
    {
        var canDecrypt = _encryption.TryDecrypt(entity.KeyCipher, out var plain);
        return new ApiKeyItemDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Remark = entity.Remark,
            MaskedKey = canDecrypt ? _encryption.Mask(plain) : "",
            CanDecrypt = canDecrypt,
            Enabled = entity.Enabled,
            IsExpired = entity.IsExpired,
            ExpiresAt = ToNullable(entity.ExpiresAt),
            LastUsedAt = ToNullable(entity.LastUsedAt),
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }

    /// <summary>
    /// 节流写入最后使用时间：同一密钥 60 秒内最多写一次。
    /// 采用节流内同步写（而非后台 Task.Run）：最坏 60 秒一次单行 UPDATE，开销可忽略，
    /// 且彻底规避后台线程碰 XCode 实体缓存 / 连接串的竞态。
    /// </summary>
    private static void UpdateLastUsedThrottled(ApiKeyCredential entity)
    {
        var now = DateTime.Now;
        if (LastUsedThrottle.TryGetValue(entity.Id, out var last) && now - last < LastUsedThrottleWindow) return;

        LastUsedThrottle[entity.Id] = now;
        try
        {
            entity.LastUsedAt = now;
            entity.Save();
        }
        catch (Exception ex)
        {
            // 审计字段写失败不应影响认证本身
            XTrace.Log.Debug("更新子密钥[{0}] 最后使用时间失败: {1}", entity.Id, ex.Message);
        }
    }

    /// <summary>生成密码学随机明文密钥：<c>sk-</c> + 32 位小写 hex（16 字节 CSPRNG）</summary>
    private static string GeneratePlainKey()
    {
        var bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);
        return KeyPrefix + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>计算 UTF-8 明文的 SHA-256 小写 hex</summary>
    private static string Sha256Hex(string plain)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plain))).ToLowerInvariant();
    }

    /// <summary>XCode 用 MinValue 表示「未设置」，出网前转为 null</summary>
    private static DateTime? ToNullable(DateTime value)
    {
        return value <= DateTime.MinValue ? null : value;
    }

    #endregion
}

/// <summary>认证通过后的身份信息（由 <see cref="ApiKeyService.ResolveByToken"/> 产出）</summary>
public class ApiKeyIdentity
{
    /// <summary>密钥 ID；主密钥固定为 0</summary>
    public Int64 KeyId { get; set; }

    /// <summary>密钥名称；主密钥为「主密钥」</summary>
    public string KeyName { get; set; } = "";

    /// <summary>认证方式：<c>ApiKeyEntry</c>（子密钥）或 <c>BearerApiKey</c>（主密钥）</summary>
    public string AuthMethod { get; set; } = "";
}

/// <summary>列表 / 更新返回的密钥条目。<b>永不携带明文、密文或摘要</b></summary>
public class ApiKeyItemDto
{
    public Int64 Id { get; set; }
    public string Name { get; set; } = "";
    public string Remark { get; set; } = "";

    /// <summary>掩码展示，如 <c>sk-****1234</c>；解密失败时为 <see cref="string.Empty"/></summary>
    public string MaskedKey { get; set; } = "";

    /// <summary>是否可在本机解密；false 时 UI 提示「无法在本机解密，请重新生成」</summary>
    public Boolean CanDecrypt { get; set; } = true;

    public Boolean Enabled { get; set; }
    public Boolean IsExpired { get; set; }

    /// <summary>过期时间；null 表示永不过期</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>最后使用时间；null 表示从未使用</summary>
    public DateTime? LastUsedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>创建 / 轮换的返回：<b>唯一一次</b>出现明文</summary>
public class ApiKeyPlainResultDto
{
    public ApiKeyItemDto Item { get; set; } = new();
    public string PlainKey { get; set; } = "";
    public string AuthHeader { get; set; } = "";
}

/// <summary>创建子密钥请求</summary>
public class CreateApiKeyRequest
{
    public string Name { get; set; } = "";
    public string? Remark { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>更新子密钥请求（重命名 / 备注 / 过期时间）</summary>
public class UpdateApiKeyRequest
{
    public string Name { get; set; } = "";
    public string? Remark { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>启停子密钥请求</summary>
public class ToggleApiKeyRequest
{
    public Boolean Enabled { get; set; }
}

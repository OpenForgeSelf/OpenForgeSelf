using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using OpenForgeSelf.Backend.Entities;
using OpenForgeSelf.Backend.Security;
using OpenForgeSelf.Backend.Services.AI;
using OpenForgeSelf.Backend.Services.AI.Models;
using OpenForgeSelf.Backend.Services.AI.Providers;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Services;

/// <summary>
/// 供应商模型记录服务：负责从上游拉取模型并 upsert 持久化（保留未返回记录与用户可编辑字段）、
/// 按供应商分组查询、启用/禁用开关、用户可编辑字段（别名/能力标签）更新、以及供应商级联清理。
/// 不接触任何凭证（ApiKey 仅经仓储解密后在内存中用于上游请求）。
/// </summary>
public interface IAIModelService
{
    /// <summary>从上游拉取某供应商模型并 upsert 持久化，返回统计与模型列表</summary>
    Task<FetchModelsResult> FetchForProviderAsync(long providerId, CancellationToken cancellationToken = default);

    /// <summary>按供应商分组查询模型（providerId 为空=全部；enabledOnly=true 仅已启用）</summary>
    List<AIModelGroupDto> GetGroups(long? providerId = null, bool enabledOnly = false);

    /// <summary>启用/禁用指定模型</summary>
    AIModelDto? SetEnabled(long id, bool enabled);

    /// <summary>更新用户可编辑字段（仅 alias/capabilities/maxContext；锁定字段忽略）</summary>
    AIModelDto? UpdateEditable(long id, string? alias, List<string>? capabilities, int? maxContext);

    /// <summary>删除某供应商下的全部模型（供应商级联清理用）</summary>
    void DeleteByProvider(long providerId);
}

public class AIModelService : IAIModelService
{
    private readonly IAIProviderRepository _providerRepo;
    private readonly ISecretEncryptionService _encryption;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogService _log;

    public AIModelService(
        IAIProviderRepository providerRepo,
        ISecretEncryptionService encryption,
        IHttpClientFactory httpClientFactory,
        ILogService log)
    {
        _providerRepo = providerRepo;
        _encryption = encryption;
        _httpClientFactory = httpClientFactory;
        _log = log;
    }

    /// <inheritdoc/>
    public async Task<FetchModelsResult> FetchForProviderAsync(long providerId, CancellationToken cancellationToken = default)
    {
        var provider = _providerRepo.GetById(providerId);
        if (provider == null)
            throw new ArgumentException($"供应商不存在：{providerId}", nameof(providerId));

        _log.Info("开始拉取模型: ProviderId={0}, Name={1}, Endpoint={2}", providerId, provider.Name, provider.Endpoint);

        // 解密 ApiKey 为明文（仓储返回密文，遵循调用方按需解密约定），构造运行期配置
        var config = ToConfig(provider);

        var httpClient = _httpClientFactory.CreateClient();
        var upstream = new OpenAICompatibleProvider(config, httpClient);

        List<ModelInfo> upstreamModels;
        try
        {
            upstreamModels = await upstream.GetModelsAsync(cancellationToken);
            _log.Info("上游 GetModelsAsync 返回 {0} 个模型", upstreamModels.Count);
        }
        catch (Exception ex)
        {
            _log.Error("上游 GetModelsAsync 抛出异常: {0}", ex.ToString());
            throw;
        }

        var kept = 0;
        var added = 0;
        var updated = 0;
        var now = DateTime.Now;

        // 拉取前已存在集合（用于计算"库中有但本次未返回"的保留数量）
        var beforeIds = AIModel.FindAllByProviderId(providerId)
            .ToDictionary(m => m.UpstreamModelId, StringComparer.OrdinalIgnoreCase);

        var upserted = new List<AIModelDto>();
        var returnedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mi in upstreamModels)
        {
            if (string.IsNullOrWhiteSpace(mi.Id)) continue;
            returnedKeys.Add(mi.Id);

            var existing = AIModel.FindByProviderIdAndUpstreamModelId(providerId, mi.Id);
            if (existing == null)
            {
                // 新增：默认启用
                var entity = new AIModel
                {
                    ProviderId = providerId,
                    ProviderName = provider.Name,
                    UpstreamModelId = mi.Id,
                    ChatModelId = $"{provider.Name}:{mi.Id}",
                    Owner = mi.Owner,
                    Enabled = true,
                    LastSyncTime = now,
                    CreateTime = now,
                    UpdateTime = now
                };
                entity.Insert();
                upserted.Add(ToDto(entity));
                added++;
            }
            else
            {
                // 更新：仅刷新元数据，保留用户的 Enabled/Alias/Capabilities
                existing.Owner = mi.Owner;
                existing.LastSyncTime = now;
                existing.UpdateTime = now;
                existing.Save();
                upserted.Add(ToDto(existing));
                updated++;
            }
        }

        // 库中有但本次上游未返回的，予以保留（不删除）；计入 kept
        kept = beforeIds.Count - returnedKeys.Count(k => beforeIds.ContainsKey(k));
        if (kept < 0) kept = 0;

        XTrace.Log.Info("[{0}] 拉取模型完成：上游返回 {1}，新增 {2}，更新 {3}，保留 {4}",
            provider.Name, upstreamModels.Count, added, updated, kept);

        return new FetchModelsResult
        {
            ProviderId = providerId,
            ProviderName = provider.Name,
            Fetched = upstreamModels.Count,
            Added = added,
            Updated = updated,
            Kept = kept,
            Models = upserted
        };
    }

    /// <inheritdoc/>
    public List<AIModelGroupDto> GetGroups(long? providerId = null, bool enabledOnly = false)
    {
        var providers = _providerRepo.GetAll()
            .Where(p => providerId == null || p.Id == providerId)
            .OrderBy(p => p.Name)
            .ToList();

        var groups = new List<AIModelGroupDto>();
        foreach (var p in providers)
        {
            var models = AIModel.FindAllByProviderId(p.Id)
                .Where(m => !enabledOnly || m.Enabled)
                .OrderBy(m => m.UpstreamModelId)
                .Select(ToDto)
                .ToList();

            groups.Add(new AIModelGroupDto
            {
                ProviderId = p.Id,
                ProviderName = p.Name,
                Models = models
            });
        }

        return groups;
    }

    /// <inheritdoc/>
    public AIModelDto? SetEnabled(long id, bool enabled)
    {
        var entity = AIModel.FindById(id);
        if (entity == null) return null;

        entity.Enabled = enabled;
        entity.UpdateTime = DateTime.Now;
        entity.Save();
        return ToDto(entity);
    }

    /// <inheritdoc/>
    public AIModelDto? UpdateEditable(long id, string? alias, List<string>? capabilities, int? maxContext)
    {
        var entity = AIModel.FindById(id);
        if (entity == null) return null;

        // 仅接受可编辑字段；ProviderName / UpstreamModelId / ChatModelId 锁定，忽略其修改
        if (alias != null) entity.Alias = alias;
        if (capabilities != null)
            entity.Capabilities = string.Join(",", capabilities.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()));
        if (maxContext != null && maxContext >= 0)
            entity.MaxContext = maxContext.Value;

        entity.UpdateTime = DateTime.Now;
        entity.Save();
        return ToDto(entity);
    }

    /// <inheritdoc/>
    public void DeleteByProvider(long providerId)
    {
        foreach (var m in AIModel.FindAllByProviderId(providerId))
            m.Delete();
    }

    #region 映射

    private AIModelDto ToDto(AIModel m) => new()
    {
        Id = m.Id,
        ProviderId = m.ProviderId,
        ProviderName = m.ProviderName,
        UpstreamModelId = m.UpstreamModelId,
        ChatModelId = m.ChatModelId,
        Alias = string.IsNullOrEmpty(m.Alias) ? null : m.Alias,
        Capabilities = string.IsNullOrEmpty(m.Capabilities)
            ? new List<string>()
            : m.Capabilities.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
        Enabled = m.Enabled,
        MaxContext = m.MaxContext,
        Owner = string.IsNullOrEmpty(m.Owner) ? null : m.Owner,
        LastSyncTime = m.LastSyncTime,
        CreateTime = m.CreateTime,
        UpdateTime = m.UpdateTime
    };

    private AIProviderConfig ToConfig(AIProvider e) => new()
    {
        Name = e.Name,
        ProviderType = Enum.TryParse<AIProviderType>(e.ProviderType, true, out var t) ? t : AIProviderType.OpenAI,
        Endpoint = e.Endpoint,
        ApiKey = _encryption.Decrypt(e.ApiKey),
        SupportedModels = string.IsNullOrEmpty(e.SupportedModels)
            ? new List<string>()
            : e.SupportedModels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
        IsDefault = e.IsDefault,
        TimeoutSeconds = e.TimeoutSeconds > 0 ? e.TimeoutSeconds : 120,
        VisionModel = string.IsNullOrEmpty(e.VisionModel) ? null : e.VisionModel,
        EnableMultimodal = e.EnableMultimodal,
        VisionPromptTemplate = string.IsNullOrEmpty(e.VisionPromptTemplate) ? null : e.VisionPromptTemplate
    };

    #endregion
}

#region DTO

/// <summary>拉取模型结果统计</summary>
public class FetchModelsResult
{
    public long ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public int Fetched { get; set; }
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Kept { get; set; }
    public List<AIModelDto> Models { get; set; } = new();
}

/// <summary>模型分组（按供应商）</summary>
public class AIModelGroupDto
{
    public long ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public List<AIModelDto> Models { get; set; } = new();
}

/// <summary>模型记录响应（仅元数据，绝不含凭证）</summary>
public class AIModelDto
{
    public long Id { get; set; }
    public long ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string UpstreamModelId { get; set; } = string.Empty;
    public string ChatModelId { get; set; } = string.Empty;
    public string? Alias { get; set; }
    public List<string> Capabilities { get; set; } = new();
    public int MaxContext { get; set; }
    public bool Enabled { get; set; }
    public string? Owner { get; set; }
    public DateTime LastSyncTime { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
}

#endregion

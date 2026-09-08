using System.Net.Http;
using ForgeSelf.Abstractions;
using Microsoft.Extensions.Http;
using ForgeSelf.Api.Entities;
using ForgeSelf.Api.Services.AI.Providers;
using NewLife.Log;

namespace ForgeSelf.Api.Services.AI;

public class AIProviderRegistry : IAIProviderRegistry
{
    // 注意：非 readonly，重载时原子替换引用，保证进行中请求读到完整旧集合
    private List<IAIProvider> _providers = new();
    private readonly object _reloadLock = new();

    public AIProviderRegistry()
    {
    }

    public void RegisterProvider(IAIProvider provider)
    {
        if (_providers.Any(p => p.ProviderName.Equals(provider.ProviderName, StringComparison.OrdinalIgnoreCase)))
        {
            XTrace.Log.Warn("AI 提供者 {0} 已注册，跳过", provider.ProviderName);
            return;
        }

        _providers.Add(provider);
        XTrace.Log.Info("已注册 AI 提供者: {0} (类型: {1}, 模型数: {2})",
            provider.ProviderName, provider.ProviderType, provider.SupportedModels.Count);
    }

    /// <summary>
    /// 从配置列表原子重载全部提供方。为每个配置经 <see cref="IHttpClientFactory"/> 构造
    /// <see cref="OpenAICompatibleProvider"/>，加锁后引用替换内部集合，进行中请求不受影响。
    /// 无 Endpoint 的配置不生效（与现有 ConfigurationService 行为一致）。
    /// </summary>
    /// <param name="httpClientFactory">HTTP 客户端工厂</param>
    /// <param name="configs">运行期提供方配置（ApiKey 为明文）</param>
    public Task ReloadAsync(IHttpClientFactory httpClientFactory, IReadOnlyList<AIProviderConfig> configs)
    {
        if (httpClientFactory == null) throw new System.ArgumentNullException(nameof(httpClientFactory));
        if (configs == null) throw new System.ArgumentNullException(nameof(configs));

        // 先构建新集合，再原子替换，避免重载过程中集合处于半初始化状态
        var next = new List<IAIProvider>(configs.Count);
        foreach (var cfg in configs)
        {
            if (string.IsNullOrWhiteSpace(cfg.Endpoint)) continue;
            var httpClient = httpClientFactory.CreateClient();
            next.Add(new OpenAICompatibleProvider(cfg, httpClient));
        }

        lock (_reloadLock)
        {
            _providers = next;
        }

        XTrace.Log.Info("AI 提供方注册表已重载，当前提供方数：{0}", _providers.Count);
        return Task.CompletedTask;
    }

    public IAIProvider? GetProviderByName(string name)
    {
        return _providers.FirstOrDefault(p =>
            p.ProviderName.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 按项目聊天模型 id（格式 <c>提供商:原始模型id</c>）定位提供方。
    /// 取首个 ':' 之前的部分作为供应商名匹配，之后部分用于诊断。
    /// 与 <see cref="GetProviderByModel"/> 并存：聊天接口可用此解析 <c>提供商:原始模型id</c> 格式。
    /// 若该模型记录存在且被禁用（AIModel.Enabled=false），按 FR-013 不参与路由，返回 null。
    /// </summary>
    public IAIProvider? GetProviderByChatModelId(string chatModelId)
    {
        if (string.IsNullOrWhiteSpace(chatModelId)) return GetDefaultProvider();

        var idx = chatModelId.IndexOf(':');
        if (idx <= 0) return GetProviderByModel(chatModelId);

        var providerName = chatModelId[..idx];
        var upstreamModelId = chatModelId[(idx + 1)..];
        var provider = GetProviderByName(providerName);
        if (provider == null) return GetProviderByModel(chatModelId);

        // FR-013：模型被禁用则不参与对话路由
        if (!IsModelEnabled(providerName, upstreamModelId)) return null;

        return provider;
    }

    /// <summary>
    /// 判断某上游模型是否参与路由：库中存在且禁用 -> false；
    /// 库中没有记录（走旧 SupportedModels 配置）或已启用 -> true（保持既有行为）。
    /// </summary>
    private static bool IsModelEnabled(string providerName, string upstreamModelId)
    {
        if (string.IsNullOrWhiteSpace(providerName) || string.IsNullOrWhiteSpace(upstreamModelId))
            return true;

        // 经 AIModel 实体查询（兼容小表实体缓存，避免热路径频繁打库）
        var model = AIModel.FindByProviderNameAndUpstreamModelId(providerName, upstreamModelId);
        // 无记录 -> 视为启用（兼容未拉取场景）；有记录则按其 Enabled 决定
        return model == null || model.Enabled;
    }

    public IAIProvider? GetProviderByModel(string modelName)
    {
        // 支持 "提供商:上游模型id" 前缀显式指定提供方（与 GetProviderByChatModelId 一致）：
        // 多模态视觉模型、Anthropic/Responses 等直接传模型名时也能按前缀路由。
        if (!string.IsNullOrWhiteSpace(modelName))
        {
            var idx = modelName.IndexOf(':');
            if (idx > 0)
            {
                var providerName = modelName[..idx];
                var upstreamModelId = modelName[(idx + 1)..];
                var byName = GetProviderByName(providerName);
                if (byName != null)
                {
                    // FR-013：模型被禁用则不参与路由
                    if (IsModelEnabled(providerName, upstreamModelId))
                        return byName;
                    return null;
                }
            }
        }

        var exactMatch = _providers.FirstOrDefault(p =>
            p.SupportedModels.Any(m => m.Equals(modelName, StringComparison.OrdinalIgnoreCase)));

        if (exactMatch != null)
            return exactMatch;

        var prefixMatch = _providers.FirstOrDefault(p =>
            p.SupportedModels.Any(m => modelName.StartsWith(m, StringComparison.OrdinalIgnoreCase)));

        if (prefixMatch != null)
            return prefixMatch;

        if (modelName == "*" || string.IsNullOrWhiteSpace(modelName))
            return GetDefaultProvider();

        return GetDefaultProvider();
    }

    public IAIProvider? GetDefaultProvider()
    {
        return _providers.FirstOrDefault(p => p.IsDefault) ?? _providers.FirstOrDefault();
    }

    public List<IAIProvider> GetAllProviders()
    {
        return _providers.ToList();
    }

    public async Task<List<ModelInfo>> GetAllModelsAsync(CancellationToken cancellationToken = default)
    {
        var allModels = new List<ModelInfo>();
        foreach (var provider in _providers)
        {
            try
            {
                var models = await provider.GetModelsAsync(cancellationToken);
                allModels.AddRange(models);
            }
            catch (Exception ex)
            {
                XTrace.Log.Error("获取提供者 {0} 的模型列表失败: {1}", provider.ProviderName, ex.Message);
            }
        }
        return allModels;
    }

    public ModelInfo? GetModelById(string modelId)
    {
        foreach (var provider in _providers)
        {
            var models = provider.SupportedModels;
            if (models.Any(m => m.Equals(modelId, StringComparison.OrdinalIgnoreCase)))
            {
                return new ModelInfo
                {
                    Id = modelId,
                    Name = modelId,
                    Owner = provider.ProviderName,
                    Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    ProviderName = provider.ProviderName
                };
            }
        }
        return null;
    }
}

using OpenForgeSelf.Backend.Services.AI.Models;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Services.AI;

public class AIProviderRegistry
{
    private readonly List<IAIProvider> _providers = new();

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

    public IAIProvider? GetProviderByName(string name)
    {
        return _providers.FirstOrDefault(p =>
            p.ProviderName.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public IAIProvider? GetProviderByModel(string modelName)
    {
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

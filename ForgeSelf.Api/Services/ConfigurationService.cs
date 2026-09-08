using ForgeSelf.Api.Models;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Services.AI;
using Microsoft.Extensions.Configuration;
using NewLife.Log;

namespace ForgeSelf.Api.Services;

/// <summary>
/// 配置服务实现
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly IConfiguration _configuration;
    private readonly AIConfig _aiConfig;
    private readonly List<AIProviderConfig> _providerConfigs;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="configuration">配置对象</param>
    public ConfigurationService(IConfiguration configuration)
    {
        _configuration = configuration;
        _aiConfig = LoadAIConfig();
        _providerConfigs = LoadProviderConfigs();
        XTrace.Log.Info("配置服务初始化完成，API端点：{0}", _aiConfig.ApiEndpoint);
        XTrace.Log.Info("已加载 {0} 个 AI 提供者配置", _providerConfigs.Count);
    }

    /// <summary>
    /// 加载AI配置
    /// </summary>
    /// <returns>AI配置</returns>
    private AIConfig LoadAIConfig()
    {
        var aiConfig = new AIConfig();

        var aiSection = _configuration.GetSection("AI");
        if (aiSection.Exists())
        {
            aiConfig.ApiEndpoint = aiSection["ApiEndpoint"] ?? string.Empty;
            aiConfig.ApiKey = aiSection["ApiKey"] ?? string.Empty;
            aiConfig.ModelName = aiSection["ModelName"] ?? string.Empty;
        }
        else
        {
            XTrace.Log.Warn("未找到AI配置节，使用默认值");
        }

        return aiConfig;
    }

    private List<AIProviderConfig> LoadProviderConfigs()
    {
        var providers = new List<AIProviderConfig>();

        var providersSection = _configuration.GetSection("AI:Providers");
        if (providersSection.Exists() && providersSection.GetChildren().Any())
        {
            foreach (var providerSection in providersSection.GetChildren())
            {
                var config = new AIProviderConfig
                {
                    Name = providerSection["Name"] ?? providerSection.Key,
                    Endpoint = providerSection["Endpoint"] ?? string.Empty,
                    ApiKey = providerSection["ApiKey"] ?? string.Empty,
                    IsDefault = bool.TryParse(providerSection["IsDefault"], out var isDefault) && isDefault,
                    TimeoutSeconds = int.TryParse(providerSection["TimeoutSeconds"], out var timeout) ? timeout : 120,
                    VisionModel = providerSection["VisionModel"],
                    EnableMultimodal = bool.TryParse(providerSection["EnableMultimodal"], out var enableMulti) && enableMulti,
                    VisionPromptTemplate = providerSection["VisionPromptTemplate"]
                };

                var providerTypeStr = providerSection["ProviderType"] ?? "OpenAI";
                config.ProviderType = Enum.TryParse<AIProviderType>(providerTypeStr, true, out var type)
                    ? type
                    : AIProviderType.OpenAI;

                var modelsStr = providerSection["SupportedModels"];
                if (!string.IsNullOrEmpty(modelsStr))
                {
                    config.SupportedModels = modelsStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(m => m.Trim())
                        .ToList();
                }

                if (!string.IsNullOrEmpty(config.Endpoint))
                {
                    providers.Add(config);
                }
            }
        }

        if (providers.Count == 0 && !string.IsNullOrEmpty(_aiConfig.ApiEndpoint))
        {
            var defaultProvider = new AIProviderConfig
            {
                Name = "default",
                ProviderType = AIProviderType.OpenAI,
                Endpoint = _aiConfig.ApiEndpoint,
                ApiKey = _aiConfig.ApiKey,
                SupportedModels = string.IsNullOrEmpty(_aiConfig.ModelName)
                    ? new List<string>()
                    : new List<string> { _aiConfig.ModelName },
                IsDefault = true
            };
            providers.Add(defaultProvider);
            XTrace.Log.Info("使用单 AI 配置作为默认提供者: {0}", defaultProvider.Name);
        }

        if (providers.Count > 0 && !providers.Any(p => p.IsDefault))
        {
            providers[0].IsDefault = true;
        }

        return providers;
    }

    /// <summary>
    /// 获取AI配置
    /// </summary>
    /// <returns>AI配置</returns>
    public AIConfig GetAIConfig()
    {
        return _aiConfig;
    }

    /// <summary>
    /// 获取AI提供者配置列表
    /// </summary>
    /// <returns>AI提供者配置列表</returns>
    public List<AIProviderConfig> GetAIProviderConfigs()
    {
        return _providerConfigs;
    }
}
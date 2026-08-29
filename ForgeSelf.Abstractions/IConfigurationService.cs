namespace ForgeSelf.Abstractions;

/// <summary>
/// 配置服务接口
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// 获取AI配置
    /// </summary>
    /// <returns>AI配置</returns>
    AIConfig GetAIConfig();

    /// <summary>
    /// 获取AI提供者配置列表
    /// </summary>
    /// <returns>AI提供者配置列表</returns>
    List<AIProviderConfig> GetAIProviderConfigs();
}

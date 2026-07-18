namespace OpenForgeSelf.Backend.Models;

/// <summary>
/// AI模型配置
/// </summary>
public class AIConfig
{
    /// <summary>
    /// API端点地址
    /// </summary>
    public string ApiEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// API密钥
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// 模型名称
    /// </summary>
    public string ModelName { get; set; } = string.Empty;
}
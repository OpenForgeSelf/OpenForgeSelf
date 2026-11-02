namespace ForgeSelf.Abstractions;

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

/// <summary>
/// AI提供者类型
/// </summary>
public enum AIProviderType
{
    OpenAI,

    /// <summary>
    /// OpenAI 新格式（Responses API，POST {base}/v1/responses）。
    /// 出站经 OpenAIResponsesProvider（openai-dotnet SDK 适配），协议按供应商显式声明（BR-1）。
    /// </summary>
    OpenAIResponses,
    Anthropic,
    Custom,

    /// <summary>
    /// Ollama。出站走 OpenAI 兼容端点（默认 http://localhost:11434/v1/chat/completions），不使用原生 /api/chat。
    /// </summary>
    Ollama,

    /// <summary>
    /// LM Studio。出站走 OpenAI 兼容端点（默认 http://localhost:1234/v1/chat/completions）。
    /// </summary>
    LMStudio
}

/// <summary>模型服务部署位置。按实际请求主机判断：本机回环为 Local，其余为 Remote。</summary>
public enum AIProviderDeployment
{
    Remote,
    Local
}

/// <summary>
/// AI提供者配置
/// </summary>
public class AIProviderConfig
{
    public string Name { get; set; } = string.Empty;
    public AIProviderType ProviderType { get; set; } = AIProviderType.OpenAI;
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public List<string> SupportedModels { get; set; } = new();
    public bool IsDefault { get; set; }
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// 部署位置标记。Ollama / LM Studio 或回环地址为 Local，公网地址为 Remote。
    /// 不单独落库，由端点与类型推导。
    /// </summary>
    public AIProviderDeployment Deployment { get; set; } = AIProviderDeployment.Remote;

    /// <summary>
    /// 视觉模型名称，用于多模态图片识别
    /// 当请求包含图片时，系统会先调用此模型识别图片内容
    /// 支持的模型如：gpt-4o, gpt-4-turbo, claude-3-opus, claude-3-sonnet 等
    /// </summary>
    public string? VisionModel { get; set; }

    /// <summary>
    /// 是否自动启用多模态处理
    /// 当为 true 且检测到图片时，自动调用视觉模型识别
    /// </summary>
    public bool EnableMultimodal { get; set; } = true;

    /// <summary>
    /// 图片识别的系统提示词模板
    /// {image_count} - 图片数量
    /// </summary>
    public string? VisionPromptTemplate { get; set; } = "请描述这张图片的内容，包括图中的人物、物体、场景等细节。";
}

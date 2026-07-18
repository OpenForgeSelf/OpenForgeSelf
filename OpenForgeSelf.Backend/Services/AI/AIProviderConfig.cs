using OpenForgeSelf.Backend.Services.AI.Models;

namespace OpenForgeSelf.Backend.Services.AI;

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

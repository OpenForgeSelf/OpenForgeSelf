namespace ForgeSelf.Abstractions;

public class UnifiedChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Name { get; set; }
    public List<UnifiedToolCall>? ToolCalls { get; set; }
    public string? ToolCallId { get; set; }
    public string? ReasoningContent { get; set; }

    /// <summary>
    /// 多模态内容块，用于支持图片等非文本内容
    /// 当启用多模态时，Content 可能为空，内容通过 ContentBlocks 传递
    /// </summary>
    public List<ContentBlock>? ContentBlocks { get; set; }
}

/// <summary>
/// 多模态内容块，支持文本和图片
/// </summary>
public class ContentBlock
{
    /// <summary>
    /// 内容类型：text, image_url, image_base64
    /// </summary>
    public string Type { get; set; } = "text";

    /// <summary>
    /// 文本内容（Type=text 时使用）
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// 图片 URL（Type=image_url 时使用）
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// 图片 Base64 编码（Type=image_base64 时使用）
    /// </summary>
    public string? ImageBase64 { get; set; }

    /// <summary>
    /// 图片 MIME 类型（如 image/png, image/jpeg）
    /// </summary>
    public string? ImageMediaType { get; set; }

    /// <summary>
    /// 图片详细程度：low, high, auto（用于 GPT-4V）
    /// </summary>
    public string? ImageDetail { get; set; }
}

public class UnifiedToolCall
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
}

public class UnifiedUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}

public class UnifiedChatRequest
{
    public string Model { get; set; } = string.Empty;
    public List<UnifiedChatMessage> Messages { get; set; } = new();
    public double Temperature { get; set; } = 1.0;
    public double TopP { get; set; } = 1.0;
    public int? MaxTokens { get; set; }
    public bool Stream { get; set; }

    /// <summary>
    /// 流式选项（如 include_usage），由客户端透传到上游
    /// </summary>
    public UnifiedStreamOptions? StreamOptions { get; set; }

    public List<UnifiedToolDefinition>? Tools { get; set; }
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// 多模态内容块，用于支持图片等多模态输入
    /// 当消息包含图片时，在第一条用户消息中使用此字段传递多模态内容
    /// </summary>
    public List<ContentBlock>? ContentBlocks { get; set; }

    /// <summary>
    /// Anthropic 格式的多模态内容块（包含 type=image 而非 image_url）
    /// </summary>
    public List<ContentBlock>? OriginalContentBlocks { get; set; }
}

public class UnifiedStreamOptions
{
    public bool IncludeUsage { get; set; }
}

public class UnifiedToolDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public object? Parameters { get; set; }
}

public class UnifiedChatResponse
{
    public string Id { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public long Created { get; set; }
    public List<UnifiedChatMessage> Choices { get; set; } = new();
    public UnifiedUsage? Usage { get; set; }
    public string FinishReason { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
}

public class UnifiedStreamChunk
{
    public string Id { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public long Created { get; set; }
    public int ChoiceIndex { get; set; }
    public string DeltaContent { get; set; } = string.Empty;
    public string? DeltaRole { get; set; }
    public string? FinishReason { get; set; }
    public UnifiedToolCall? DeltaToolCall { get; set; }
    public UnifiedUsage? Usage { get; set; }
    public bool IsDone { get; set; }
}

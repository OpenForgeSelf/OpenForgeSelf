namespace ForgeSelf.Api.Models;

/// <summary>
/// 聊天消息模型（用于API交互）
/// </summary>
public class ChatMessageModel
{
    /// <summary>
    /// 消息ID
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 会话ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// 消息角色（user/assistant/system）
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// 消息内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdateTime { get; set; }
}

/// <summary>
/// 聊天请求
/// </summary>
public class ChatRequest
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// 用户消息（单条消息格式）
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 消息列表（OpenAI风格格式，兼容messages数组）
    /// </summary>
    public List<AIChatMessage>? Messages { get; set; }

    /// <summary>
    /// 是否流式响应
    /// </summary>
    public bool Stream { get; set; } = true;

    /// <summary>
    /// 聊天模型 id（格式：提供商:上游模型id，如 default:qwythos-9b-v2）。
    /// 为空时走默认 AI 配置；非空时按所选模型路由提供方。
    /// </summary>
    public string? ChatModelId { get; set; }

    /// <summary>
    /// 获取有效用户消息内容
    /// </summary>
    public string GetUserMessage()
    {
        if (!string.IsNullOrWhiteSpace(Message))
            return Message;

        if (Messages?.Count > 0)
        {
            var lastUserMessage = Messages.LastOrDefault(m =>
                m.Role.Equals("user", StringComparison.OrdinalIgnoreCase));
            if (lastUserMessage != null)
                return lastUserMessage.Content;
        }

        return string.Empty;
    }
}

/// <summary>
/// 聊天响应
/// </summary>
public class ChatResponse
{
    /// <summary>
    /// 消息ID
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 会话ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// 角色
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// 内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// AI API请求模型
/// </summary>
public class AIChatRequest
{
    public string Model { get; set; } = string.Empty;
    public List<AIChatMessage> Messages { get; set; } = new();
    public bool Stream { get; set; }
}

/// <summary>
/// AI API消息模型
/// </summary>
public class AIChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// AI API响应模型
/// </summary>
public class AIChatResponse
{
    public string Id { get; set; } = string.Empty;
    public string Object { get; set; } = string.Empty;
    public long Created { get; set; }
    public string Model { get; set; } = string.Empty;
    public List<AIChatChoice> Choices { get; set; } = new();
}

/// <summary>
/// AI API选择模型
/// </summary>
public class AIChatChoice
{
    public int Index { get; set; }
    public AIChatMessageDelta? Message { get; set; }
    public AIChatMessageDelta? Delta { get; set; }
    public string FinishReason { get; set; } = string.Empty;
}

/// <summary>
/// AI API消息增量模型
/// </summary>
public class AIChatMessageDelta
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ReasoningContent { get; set; } = string.Empty;
}
using OpenForgeSelf.Abstractions;

namespace OpenForgeSelf.Backend.Plugins.AIAgent.Models;

public class ChatMessageModel
{
    public long Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreateTime { get; set; }
    public DateTime UpdateTime { get; set; }
}

public class ChatRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool Stream { get; set; } = true;
}

public class ChatResponse
{
    public long Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreateTime { get; set; }
}

public class AIChatRequest
{
    public string Model { get; set; } = string.Empty;
    public List<AIChatMessage> Messages { get; set; } = new();
    public bool Stream { get; set; }
    public List<AIToolDefinition>? Tools { get; set; }
    public string? ToolChoice { get; set; }
}

public class AIChatResponse
{
    public string Id { get; set; } = string.Empty;
    public string Object { get; set; } = string.Empty;
    public long Created { get; set; }
    public string Model { get; set; } = string.Empty;
    public List<AIChatChoice> Choices { get; set; } = new();
}

public class AIChatChoice
{
    public int Index { get; set; }
    public AIChatMessageDelta? Message { get; set; }
    public AIChatMessageDelta? Delta { get; set; }
    public string FinishReason { get; set; } = string.Empty;
}

public class AIChatMessageDelta
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<AIToolCall>? ToolCalls { get; set; }
}

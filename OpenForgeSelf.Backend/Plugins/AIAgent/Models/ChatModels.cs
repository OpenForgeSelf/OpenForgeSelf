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

public class AIConfig
{
    public string ApiEndpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
}

public class AIChatRequest
{
    public string Model { get; set; } = string.Empty;
    public List<AIChatMessage> Messages { get; set; } = new();
    public bool Stream { get; set; }
    public List<AIToolDefinition>? Tools { get; set; }
    public string? ToolChoice { get; set; }
}

public class AIChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<AIToolCall>? ToolCalls { get; set; }
    public string? ToolCallId { get; set; }
    public string? Name { get; set; }
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

public class AIToolDefinition
{
    public string Type { get; set; } = "function";
    public AIFunctionDefinition Function { get; set; } = new();
}

public class AIFunctionDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public object? Parameters { get; set; }
}

public class AIToolCall
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = "function";
    public AIFunctionCall Function { get; set; } = new();
    public int? Index { get; set; }
}

public class AIFunctionCall
{
    public string Name { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
}

public class ToolValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public Dictionary<string, object>? ParsedParameters { get; set; }
}

public class ToolExecutionResult
{
    public bool Success { get; set; }
    public string Result { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public long DurationMs { get; set; }
}

public class ToolCallError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Suggestion { get; set; }
}

public class ToolCallResponse
{
    public bool Success { get; set; }
    public object? Data { get; set; }
    public ToolCallError? Error { get; set; }
    public long DurationMs { get; set; }

    public static ToolCallResponse Ok(object? data = null, long durationMs = 0)
    {
        return new ToolCallResponse
        {
            Success = true,
            Data = data,
            DurationMs = durationMs
        };
    }

    public static ToolCallResponse Fail(string code, string message, string? suggestion = null, long durationMs = 0)
    {
        return new ToolCallResponse
        {
            Success = false,
            Error = new ToolCallError
            {
                Code = code,
                Message = message,
                Suggestion = suggestion
            },
            DurationMs = durationMs
        };
    }
}

public static class ToolErrorCode
{
    public const string ToolNotFound = "TOOL_NOT_FOUND";
    public const string InvalidParameters = "INVALID_PARAMETERS";
    public const string ExecutionFailed = "EXECUTION_FAILED";
    public const string Timeout = "TIMEOUT";
    public const string PermissionDenied = "PERMISSION_DENIED";
    public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";
    public const string NotFound = "NOT_FOUND";
    public const string InternalError = "INTERNAL_ERROR";
}

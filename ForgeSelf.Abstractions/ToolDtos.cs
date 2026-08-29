namespace ForgeSelf.Abstractions;

/// <summary>
/// AI 工具定义（纯 DTO，宿主 IToolRegistry 契约用）。
/// 自 ForgeSelf.Api.Plugins.AIAgent.Models 迁入，公开签名不变。
/// </summary>
public class AIToolDefinition
{
    public string Type { get; set; } = "function";
    public AIFunctionDefinition Function { get; set; } = new();
}

/// <summary>
/// AI 函数定义（纯 DTO）。
/// </summary>
public class AIFunctionDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public object? Parameters { get; set; }
}

/// <summary>
/// 工具参数校验结果（纯 DTO）。
/// </summary>
public class ToolValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public Dictionary<string, object>? ParsedParameters { get; set; }
}

/// <summary>
/// 工具执行结果（纯 DTO）。
/// </summary>
public class ToolExecutionResult
{
    public bool Success { get; set; }
    public string Result { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public long DurationMs { get; set; }
}

/// <summary>
/// 工具调用错误（纯 DTO）。
/// </summary>
public class ToolCallError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Suggestion { get; set; }
}

/// <summary>
/// 工具调用响应（纯 DTO，含工厂方法）。
/// </summary>
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

/// <summary>
/// 工具错误码常量（纯 DTO）。
/// </summary>
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

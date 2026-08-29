namespace ForgeSelf.Api.Services;

/// <summary>
/// 工具执行管道的事件上下文，随 tools/pre-execute、tools/execute、tools/post-execute 事件传递。
/// </summary>
public sealed class ToolCallContext
{
    public string ToolName { get; init; } = "";
    public string Parameters { get; init; } = "";
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Result { get; set; }
    public long DurationMs { get; set; }
}

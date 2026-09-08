namespace ForgeSelf.Abstractions;

/// <summary>
/// Agent 工具循环的流式事件（契约层，随 <see cref="IAIAgentService.RunAgentLoopAsync"/> 产出）。
/// 控制器把它逐个写成结构化 SSE，前端据此做「逐字流出 + 工具调用可见」。
/// </summary>
public class AgentLoopEvent
{
    /// <summary>事件类型：content / tool_call / tool_result / usage / done / error。</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>content/done/error 事件的文本（content 为增量 token）。</summary>
    public string? Content { get; set; }

    /// <summary>tool_call / tool_result 事件的工具名。</summary>
    public string? Name { get; set; }

    /// <summary>tool_call 事件的参数 JSON。</summary>
    public string? Arguments { get; set; }

    /// <summary>tool_result 事件的工具返回。</summary>
    public string? Result { get; set; }

    /// <summary>tool_result 事件的成功标记。</summary>
    public bool? Success { get; set; }

    /// <summary>usage / done 事件的 token 用量。</summary>
    public UnifiedUsage? Usage { get; set; }
}

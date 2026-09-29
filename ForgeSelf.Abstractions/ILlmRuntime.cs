namespace ForgeSelf.Abstractions;

/// <summary>
/// 轻量消息：LLM 接缝的统一消息词汇（模型可见历史与请求均使用此类型）。
/// </summary>
public sealed class Message
{
    /// <summary>角色：system / user / assistant / tool 等。</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>文本内容。</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>工具结果消息对应的调用 ID（仅 tool 角色使用）。B1（040）新增，可选。</summary>
    public string? CallId { get; init; }

    /// <summary>助手消息携带的工具调用（仅 assistant 角色使用）。B1（040）新增，可选。</summary>
    public IReadOnlyList<ToolCallRef>? ToolCalls { get; init; }

    /// <summary>助手消息携带的用量（仅 assistant 角色使用）。B1（040）新增，可选。</summary>
    public UsageInfo? Usage { get; init; }
}

/// <summary>
/// 流式分片：LLM 流式输出的一小段增量。
/// </summary>
/// <remarks>
/// B8（A5）：扩展携带 <see cref="ToolCall"/> / <see cref="Usage"/> / <see cref="FinishReason"/>，
/// 使 ILlmRuntime 面与 <c>IAIProvider.ChatStreamAsync</c> 的 <c>UnifiedStreamChunk</c> 对齐——
/// 消费方可从任一接缝拿到工具调用增量与用量。legacy 适配器（<c>AIServiceLlmRuntime</c>）无法从
/// 旧 <c>IAIService.ChatStreamAsync</c>（纯文本分片）取到 Usage/ToolCall 时保持 null（有损面，注释声明）。
/// </remarks>
public sealed class StreamChunk
{
    /// <summary>本分片的增量文本。</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>是否为本轮流式输出的最后一个分片。</summary>
    public bool IsFinal { get; init; }

    /// <summary>本分片携带的工具调用增量（B8/A5；非工具分片为 null）。</summary>
    public ToolCallDelta? ToolCall { get; init; }

    /// <summary>用量（B8/A5；通常随最后一个内容分片下发，provider 不提供时为 null）。</summary>
    public UnifiedUsage? Usage { get; init; }

    /// <summary>结束原因（B8/A5；如 stop / length，provider 不提供时为 null）。</summary>
    public string? FinishReason { get; init; }
}

/// <summary>
/// LLM 运行时接缝：LLM 适配器 Provider 的统一入口。
/// 每个 Provider（OpenAI / Anthropic / Responses / Models / AgentChat）实现此接口，
/// 通过 IContext 注册，消费者经 ctx.Get&lt;ILlmRuntime&gt;() 获取，替换 Provider 零改动。
/// </summary>
public interface ILlmRuntime
{
    IAsyncEnumerable<StreamChunk> StreamAsync(IReadOnlyList<Message> messages, CancellationToken ct = default);
}

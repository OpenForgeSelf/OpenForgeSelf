namespace OpenForgeSelf.Abstractions;

/// <summary>
/// 轻量消息：LLM 接缝的统一消息词汇（模型可见历史与请求均使用此类型）。
/// </summary>
public sealed class Message
{
    /// <summary>角色：system / user / assistant / tool 等。</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>文本内容。</summary>
    public string Content { get; init; } = string.Empty;
}

/// <summary>
/// 流式分片：LLM 流式输出的一小段增量。
/// </summary>
public sealed class StreamChunk
{
    /// <summary>本分片的增量文本。</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>是否为本轮流式输出的最后一个分片。</summary>
    public bool IsFinal { get; init; }
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

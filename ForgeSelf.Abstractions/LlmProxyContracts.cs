namespace ForgeSelf.Abstractions;

/// <summary>
/// 线上报文协议。看的是 JSON 形状，不是厂商名字。
/// Anthropic 提供方若走 OpenAI 兼容层，线上协议仍是 <see cref="ChatCompletions"/>。
/// </summary>
public enum LlmWireProtocol
{
    ChatCompletions = 0,
    Responses = 1,
    AnthropicMessages = 2
}

/// <summary>
/// 一次模型调用的入站报文。调用方只负责把「自己的请求」收成这个对象。
/// </summary>
public sealed class LlmProxyRequest
{
    public required LlmWireProtocol Protocol { get; init; }

    /// <summary>原始 JSON 对象文本。管道只改 <c>model</c>，同协议其余字段原样发出。</summary>
    public required string BodyJson { get; init; }

    /// <summary>
    /// 入站 HTTP 头（可选）。上游粘滞策略只允许 allowlist 转发，不能把 Authorization 送到上游。
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// 管道结果。同协议时 <see cref="Body"/> 是上游字节；网关自己的 400 写在 <see cref="Body"/> 且 <see cref="Stream"/> 为 false。
/// </summary>
public sealed class LlmProxyResult : IAsyncDisposable
{
    public int StatusCode { get; init; }

    public bool Stream { get; init; }

    public string MediaType { get; init; } = "application/json";

    /// <summary>改写 model 之后、真正发出去的 JSON。旁路记录用，不参与第二次改写。</summary>
    public string? OutboundJson { get; init; }

    public Stream? Body { get; init; }

    public async ValueTask DisposeAsync()
    {
        if (Body != null)
            await Body.DisposeAsync();
    }
}

/// <summary>
/// 唯一的模型往返算法。公开代理和自带 Agent 都只构造 <see cref="LlmProxyRequest"/> 再调用本接口。
/// </summary>
public interface ILlmProxyPipeline
{
    Task<LlmProxyResult> ExecuteAsync(LlmProxyRequest request, CancellationToken cancellationToken = default);
}

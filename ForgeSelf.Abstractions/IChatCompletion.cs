namespace ForgeSelf.Abstractions;

/// <summary>
/// 跨插件「聊天补全」标准契约（L1 契约下沉）。
/// 消费方（如 IM 网关插件）经 <c>IContext.Get&lt;IChatCompletion&gt;()</c> 获取；
/// 提供方（AIAgent 插件）在 <c>Apply</c> 内注册到共享服务表。
/// 用途：把「Agent 能力」与「IM 平台协议」彻底解耦——IM 通道只管收发消息，
/// 经本契约拿到 AI 回复，不关心底层是哪家模型、挂了哪些工具。
/// </summary>
public interface IChatCompletion
{
    /// <summary>
    /// 完成一次对话补全（非流式，返回最终文本）。会按 <see cref="ChatCompletionRequest.SessionId"/> 维护历史上下文。
    /// </summary>
    /// <param name="request">补全请求（消息内容 + 可选会话/模型/Agent 路由）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>补全结果（会话 id、最终文本、触发的工具、成功标记）。</returns>
    Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default);
}

/// <summary>聊天补全请求（IM 通道 → Agent 的标准化入参）。</summary>
public class ChatCompletionRequest
{
    /// <summary>会话 id；为空则实现方新建会话。IM 网关用它把「某用户在某渠道的对话」映射到稳定会话。</summary>
    public string? SessionId { get; set; }

    /// <summary>用户本轮消息文本（必填）。</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>聊天模型 id（形如 provider:upstreamModelId）；空则回退默认 provider。</summary>
    public string? ChatModelId { get; set; }

    /// <summary>选中 Agent id；命中则注入其 SystemPrompt，空则不注入。</summary>
    public string? AgentId { get; set; }

    /// <summary>本会话启用工具名白名单；空/未传 = 默认全挂。</summary>
    public List<string>? EnabledToolNames { get; set; }
}

/// <summary>聊天补全结果（Agent → IM 通道的标准化出参）。</summary>
public class ChatCompletionResult
{
    /// <summary>实际使用的会话 id（实现方可能新建）。</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>AI 最终回复文本。</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>本轮触发的工具名列表（用于可观测，非必填）。</summary>
    public List<string> ToolCalls { get; set; } = new();

    /// <summary>是否成功（失败时 <see cref="Error"/> 携带原因）。</summary>
    public bool Success { get; set; } = true;

    /// <summary>失败原因（Success=false 时）。</summary>
    public string? Error { get; set; }
}

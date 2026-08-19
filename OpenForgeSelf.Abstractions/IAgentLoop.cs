namespace OpenForgeSelf.Abstractions;

/// <summary>
/// Agent 运行请求：一次 React 循环的入口。
/// </summary>
public sealed class AgentRunRequest
{
    /// <summary>会话 ID。</summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>本轮用户消息。</summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// 回合事件：Agent Loop 在某一阶段产生的一条可观察事实。
/// </summary>
public sealed class TurnEvent
{
    /// <summary>循环阶段：Claim / Assemble / Request / Execute / Repeat。</summary>
    public string Phase { get; init; } = string.Empty;

    /// <summary>阶段数据（可序列化 JSON 字符串）。</summary>
    public string Payload { get; init; } = string.Empty;

    /// <summary>阶段序号，从 0 递增。</summary>
    public long Sequence { get; init; }
}

/// <summary>
/// Agent Loop 接缝：React 循环（Claim→Assemble→Request→Execute→Repeat）。
/// 以回合事件流形式输出，供宿主/测试/审计消费。
/// </summary>
public interface IAgentLoop
{
    IAsyncEnumerable<TurnEvent> RunAsync(AgentRunRequest request, CancellationToken ct = default);
}

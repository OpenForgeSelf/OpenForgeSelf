using System.Runtime.CompilerServices;
using System.Text.Json;
using OpenForgeSelf.Abstractions;

namespace OpenForgeSelf.Backend.Services;

/// <summary>
/// 内存版 Agent Loop 实现（P4 会话/LLM 接缝接线）。
/// 以回合事件流形式输出 React 循环阶段（Claim→Assemble→Request→Execute→Repeat），
/// 供宿主/测试/审计消费。当前为骨架实现，Payload 为请求的 JSON 序列化。
/// </summary>
public sealed class InMemoryAgentLoop : IAgentLoop
{
    public async IAsyncEnumerable<TurnEvent> RunAsync(
        AgentRunRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var phases = new[] { "Claim", "Assemble", "Request", "Execute", "Repeat" };
        var payload = JsonSerializer.Serialize(new { request.SessionId, request.Message });

        for (var i = 0; i < phases.Length; i++)
        {
            ct.ThrowIfCancellationRequested();

            yield return new TurnEvent
            {
                Phase = phases[i],
                Payload = payload,
                Sequence = i
            };

            // 模拟异步处理延迟（可配置，当前为 0 以便测试）
            await Task.CompletedTask;
        }
    }
}

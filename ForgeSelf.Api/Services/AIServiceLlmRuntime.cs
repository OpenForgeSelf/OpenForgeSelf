using System.Runtime.CompilerServices;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Models;
using Message = ForgeSelf.Abstractions.Message;
using AIChatMessage = ForgeSelf.Api.Models.AIChatMessage;

namespace ForgeSelf.Api.Services;

/// <summary>
/// LLM 运行时适配器（P4 LLM 接缝实现）：把现有 <see cref="IAIService"/> 包装为 <see cref="ILlmRuntime"/> 接缝。
/// 消费方经 ILlmRuntime 调用；替换底层 Provider（如换成其它模型网关）零改动消费者。
/// </summary>
/// <remarks>
/// B8-8 缺陷修复：legacy <c>AIChatMessage</c> 只有 <c>Role/Content</c> 两字段，此前把
/// <see cref="Message"/> 降级转换时<b>静默丢掉</b> <c>CallId/ToolCalls</c>——
/// <c>DeriveMessages</c> 产出的 <c>tool</c> 角色消息根本进不了模型。本修复以标记文本承载结构化字段：
/// tool 角色内容前缀 <c>[tool_result call_id=...]</c>，assistant 的工具调用序列化为
/// <c>[tool_call id=... name=...]</c> 行——信息不再静默丢弃（有损面收敛，注释声明）。
/// Usage/FinishReason：legacy 流式接口只产纯文本分片，无法取到用量；<see cref="StreamChunk"/>
/// 结束分片补 <c>FinishReason="stop"</c>，Usage 保持 null（provider 升级到 Unified 面后自然填充）。
/// </remarks>
public sealed class AIServiceLlmRuntime : ILlmRuntime
{
    private readonly IAIService _aiService;

    public AIServiceLlmRuntime(IAIService aiService)
    {
        _aiService = aiService;
    }

    public async IAsyncEnumerable<StreamChunk> StreamAsync(
        IReadOnlyList<Message> messages,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var legacy = messages
            .Select(ToLegacyMessage)
            .ToList();

        await foreach (var chunk in _aiService.ChatStreamAsync(legacy, ct))
        {
            yield return new StreamChunk { Content = chunk, IsFinal = false };
        }

        yield return new StreamChunk { Content = string.Empty, IsFinal = true, FinishReason = "stop" };
    }

    /// <summary>
    /// 模型可见消息 → legacy 消息：不丢 tool 角色 CallId 与 assistant 工具调用（B8-8）。
    /// </summary>
    private static AIChatMessage ToLegacyMessage(Message m)
    {
        var content = m.Content ?? string.Empty;

        if (string.Equals(m.Role, "tool", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(m.CallId))
        {
            content = $"[tool_result call_id={m.CallId}] {content}";
        }

        if (m.ToolCalls is { Count: > 0 })
        {
            var callsText = string.Join(Environment.NewLine, m.ToolCalls.Select(tc =>
                $"[tool_call id={tc.CallId} name={tc.ToolName}] {tc.ArgsJson}"));
            content = string.IsNullOrEmpty(content) ? callsText : content + Environment.NewLine + callsText;
        }

        return new AIChatMessage { Role = m.Role, Content = content };
    }
}

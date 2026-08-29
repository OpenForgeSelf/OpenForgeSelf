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
            .Select(m => new AIChatMessage { Role = m.Role, Content = m.Content })
            .ToList();

        await foreach (var chunk in _aiService.ChatStreamAsync(legacy, ct))
        {
            yield return new StreamChunk { Content = chunk, IsFinal = false };
        }

        yield return new StreamChunk { Content = string.Empty, IsFinal = true };
    }
}

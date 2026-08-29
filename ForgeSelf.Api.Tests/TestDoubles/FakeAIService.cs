using System.Runtime.CompilerServices;
using ForgeSelf.Api.Models;
using ForgeSelf.Api.Services;
// 与宿主/测试保持一致：Abstractions.AIChatMessage 同名冲突，用别名消除 CS0104
using LegacyAIChatMessage = ForgeSelf.Api.Models.AIChatMessage;

namespace ForgeSelf.Api.Tests.TestDoubles;

/// <summary>
/// 模拟（假）LLM 服务：固定返回预设内容，完全不发起任何网络请求。
/// 用于替代真实 <see cref="AIService"/>，使依赖 LLM 的集成测试可稳定、离线、确定性运行，
/// 不再依赖真实的 AI 端点 / ApiKey（避免环境性 500 或需要联网）。
/// </summary>
public class FakeAIService : IAIService
{
    /// <summary>固定返回的聊天内容（确定性，便于断言）。</summary>
    public const string FixedReply = "你好！我是模拟AI助手（FakeAIService），这是用于测试的确定性回复。";

    /// <inheritdoc />
    public Task<string> ChatAsync(List<LegacyAIChatMessage> messages)
        => Task.FromResult(FixedReply);

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ChatStreamAsync(List<LegacyAIChatMessage> messages, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return FixedReply;
        await Task.CompletedTask;
    }
}

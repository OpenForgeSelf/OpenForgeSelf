using ForgeSelf.Abstractions;
using ForgeSelf.Api.Services;
using ForgeSelf.Api.Models;
using Moq;
using AIChatMessage = ForgeSelf.Api.Models.AIChatMessage;

namespace ForgeSelf.Api.Tests.Services;

public class SessionStoreAndLlmRuntimeTests
{
    [Fact]
    public void InMemorySessionStore_AppendReplayDerive_Works()
    {
        var store = new InMemorySessionStore();

        store.Append("s1", new SessionEvent { Type = "user", Payload = "你好" });
        store.Append("s1", new SessionEvent { Type = "assistant", Payload = "你好！" });
        store.Append("s1", new SessionEvent { Type = "internal", Payload = "不应投影" });

        var replay = store.Replay("s1");
        replay.Should().HaveCount(3);
        replay[0].Id.Should().Be(1);
        replay[1].Id.Should().Be(2);

        // DeriveMessages 只投影模型可见角色（user/assistant），internal 被跳过
        var messages = store.DeriveMessages("s1");
        messages.Should().HaveCount(2);
        messages[0].Role.Should().Be("user");
        messages[0].Content.Should().Be("你好");
        messages[1].Role.Should().Be("assistant");

        // 其它会话隔离
        store.Replay("other").Should().BeEmpty();
    }

    [Fact]
    public async Task AIServiceLlmRuntime_StreamAsync_WrapsAIService()
    {
        var chunks = new List<string> { "你", "好" }.ToAsyncEnumerable();
        var aiServiceMock = new Mock<IAIService>();
        aiServiceMock
            .Setup(s => s.ChatStreamAsync(It.IsAny<List<AIChatMessage>>(), It.IsAny<CancellationToken>()))
            .Returns(chunks);

        var runtime = new AIServiceLlmRuntime(aiServiceMock.Object);

        var received = new List<StreamChunk>();
        await foreach (var c in runtime.StreamAsync(new List<Message> { new() { Role = "user", Content = "你好" } }))
        {
            received.Add(c);
        }

        // 2 个内容分片 + 1 个 IsFinal 结束分片
        received.Should().HaveCount(3);
        received[0].Content.Should().Be("你");
        received[1].Content.Should().Be("好");
        received[2].IsFinal.Should().BeTrue();
    }
}

internal static class AsyncEnumerableHelper
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            yield return item;
            await Task.CompletedTask;
        }
    }
}

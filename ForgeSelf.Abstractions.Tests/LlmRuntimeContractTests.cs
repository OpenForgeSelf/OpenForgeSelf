using System.Runtime.CompilerServices;
using ForgeSelf.Abstractions;
using Xunit;

namespace ForgeSelf.Abstractions.Tests;

public class LlmRuntimeContractTests
{
    private sealed class FakeLlmRuntime : ILlmRuntime
    {
        public async IAsyncEnumerable<StreamChunk> StreamAsync(
            IReadOnlyList<Message> messages,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return new StreamChunk { Content = "hel", IsFinal = false };
            yield return new StreamChunk { Content = "lo", IsFinal = true };
        }
    }

    [Fact]
    public async Task StreamAsync_ReturnsChunksWithFinalMarker()
    {
        var runtime = new FakeLlmRuntime();
        var messages = new List<Message> { new() { Role = "user", Content = "hi" } };

        var chunks = new List<StreamChunk>();
        await foreach (var chunk in runtime.StreamAsync(messages))
        {
            chunks.Add(chunk);
        }

        Assert.Equal(2, chunks.Count);
        Assert.Equal("hel", chunks[0].Content);
        Assert.False(chunks[0].IsFinal);
        Assert.Equal("lo", chunks[1].Content);
        Assert.True(chunks[1].IsFinal);
    }

    [Fact]
    public void Message_DefaultRoleAndContent_AreEmpty()
    {
        var message = new Message();

        Assert.Equal(string.Empty, message.Role);
        Assert.Equal(string.Empty, message.Content);
    }
}

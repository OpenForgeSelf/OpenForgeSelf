using System.Diagnostics;
using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Entities;
using ForgeSelf.Api.Services;
using ForgeSelf.Api.Services.AI.Proxy;

namespace ForgeSelf.Api.Tests.Unit;

/// <summary>
/// 旁路记录失败或变慢时，队列自己消化，不把异常抛回入队方。
/// </summary>
public class LlmTurnAuditIsolationTests
{
    [Fact]
    public async Task FailedSave_DoesNotStopTheNextItem()
    {
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        var queue = new LlmTurnAuditQueue((_, _) =>
        {
            var n = Interlocked.Increment(ref count);
            if (n == 1)
            {
                first.TrySetResult();
                throw new InvalidOperationException("SQLite需要重建表");
            }

            second.TrySetResult();
            return Task.CompletedTask;
        });

        await queue.StartAsync(CancellationToken.None);
        try
        {
            queue.Enqueue(Sample("a"));
            await first.Task.WaitAsync(TimeSpan.FromSeconds(2));
            queue.Enqueue(Sample("b"));
            await second.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public void Enqueue_WhenSaveWouldBlock_ReturnsImmediately()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queue = new LlmTurnAuditQueue(async (_, _) => await release.Task);
        var sw = Stopwatch.StartNew();
        queue.Enqueue(Sample("fast"));
        sw.Stop();
        release.TrySetResult();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    public async Task SaveAsync_HeaderJsonOver1000Chars_PersistsFullText()
    {
        var payload = new string('z', 1500);
        var headers = new Dictionary<string, string> { ["X-Probe"] = payload };
        var expected = JsonSerializer.Serialize(headers, ForgeJson.Default);
        expected.Length.Should().BeGreaterThan(1000);

        ChatTurn? saved = null;
        var turns = new Mock<IChatTurnService>();
        turns.Setup(s => s.SaveTurnAsync(It.IsAny<ChatTurn>()))
            .Callback<ChatTurn>(t => saved = t)
            .Returns(Task.CompletedTask);
        var sessions = new Mock<IChatSessionService>();
        sessions.Setup(s => s.UpsertSessionAsync(
                It.IsAny<string>(), It.IsAny<SessionSource>(), It.IsAny<string?>(), It.IsAny<ClientKind>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<string?>()))
            .ReturnsAsync(new ChatSession { Id = 1, SessionKey = "probe", RequestCount = 1 });
        sessions.Setup(s => s.RecordTurnStatsAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        var log = new Mock<ILogService>();
        var store = new LlmTurnAuditStore(log.Object, turns.Object, new Mock<IChatTurnStreamRecorder>().Object, sessions.Object);

        await store.SaveAsync(new LlmTurnAuditWork
        {
            Style = "OpenAI_Responses",
            Path = "/v1/responses",
            StatusCode = 200,
            OutboundJson = """{"model":"m","input":"hi"}""",
            ResponseText = "ok",
            Headers = headers
        }, CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.RequestHeaders.Should().Be(expected);
        log.Verify(l => l.Warn(It.Is<string>(m => m.Contains("截断")), It.IsAny<object[]>()), Times.Never);
    }

    [Fact]
    public void GatewayHotPath_DoesNotCallTurnPersistence()
    {
        var root = RepoRoot();
        var hotPath = File.ReadAllText(Path.Combine(root, "ForgeSelf.Api/Services/AI/Proxy/LlmProxyHttp.cs"));
        hotPath.Should().Contain("Enqueue(");
        hotPath.Should().NotContain("SaveTurnAsync");
        hotPath.Should().NotContain("UpsertSessionAsync");
        hotPath.Should().NotContain("UpsertTurnAsync");
        hotPath.Should().NotContain("BeginAsync");

        string[] controllers =
        [
            "ForgeSelf.Api/Controllers/UnifiedAI/OpenAIChatController.cs",
            "ForgeSelf.Api/Controllers/UnifiedAI/OpenAIResponsesController.cs",
            "ForgeSelf.Api/Controllers/UnifiedAI/AnthropicMessagesController.cs",
        ];
        foreach (var rel in controllers)
        {
            var text = File.ReadAllText(Path.Combine(root, rel));
            text.Should().Contain("ILlmTurnAudit");
            text.Should().NotContain("SaveTurnAsync");
            text.Should().NotContain("UpsertSessionAsync");
        }
    }

    private static LlmTurnAuditWork Sample(string path) => new()
    {
        Style = "OpenAI_Chat",
        Path = path,
        StatusCode = 200,
        ResponseText = "ok"
    };

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ForgeSelf.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("找不到仓库根（ForgeSelf.slnx）");
    }
}

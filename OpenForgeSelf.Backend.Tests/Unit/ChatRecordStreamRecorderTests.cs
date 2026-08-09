using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using OpenForgeSelf.Backend.Entities;
using OpenForgeSelf.Backend.Services;
using Xunit;

namespace OpenForgeSelf.Backend.Tests.Unit;

/// <summary>
/// 聊天记录流式录制器单测：覆盖「首插→增量 flush→结束定稿→异常定稿→已完成守卫」。
/// 全程 mock IChatRecordService / IWebSocketBroadcaster，不触碰数据库。
/// </summary>
public class ChatRecordStreamRecorderTests
{
    private sealed class Capture
    {
        public string? EventType { get; set; }
        public object? Payload { get; set; }
    }

    private static (ChatRecordStreamRecorder recorder, Mock<IChatRecordService> svc, List<Capture> broadcasts) Build()
    {
        var svc = new Mock<IChatRecordService>();
        var broadcasts = new List<Capture>();
        var broadcaster = new Mock<IWebSocketBroadcaster>();
        broadcaster
            .Setup(b => b.BroadcastAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<string, object, CancellationToken>((t, p, _) => broadcasts.Add(new Capture { EventType = t, Payload = p }))
            .Returns(Task.CompletedTask);

        var recorder = new ChatRecordStreamRecorder(svc.Object, broadcaster.Object);
        return (recorder, svc, broadcasts);
    }

    private static string? TextOf(object? payload) =>
        payload?.GetType().GetProperty("text")?.GetValue(payload) as string;

    private static T? Prop<T>(object? payload, string name) where T : struct =>
        (T?)payload?.GetType().GetProperty(name)?.GetValue(payload);

    [Fact]
    public async Task BeginAsync_ShouldInsertRecordAndAssignRequestId()
    {
        var (recorder, svc, _) = Build();
        var record = new ChatRecord { SessionId = "s1" };

        var session = await recorder.BeginAsync(record);

        session.Should().NotBeNull();
        record.RequestId.Should().NotBeNullOrEmpty();
        session!.RequestId.Should().Be(record.RequestId);
        svc.Verify(s => s.UpsertRecordAsync(It.Is<ChatRecord>(r => r == record)), Times.Once);
    }

    [Fact]
    public async Task AppendChunkAsync_ShouldBroadcastEachChunk_AndFlushOnLengthThreshold()
    {
        var (recorder, svc, broadcasts) = Build();
        // 模拟插入时分配自增 Id，使后续为更新
        svc.Setup(s => s.UpsertRecordAsync(It.IsAny<ChatRecord>()))
            .Callback<ChatRecord>(r => { if (r.Id == 0) r.Id = 99; })
            .Returns(Task.CompletedTask);

        var record = new ChatRecord { SessionId = "s1", Style = "OpenAI_Chat" };
        var session = await recorder.BeginAsync(record);

        await session.AppendChunkAsync("hello ");
        await session.AppendChunkAsync("world");

        broadcasts.Should().HaveCount(2);
        TextOf(broadcasts[0].Payload).Should().Be("hello ");
        TextOf(broadcasts[1].Payload).Should().Be("world");
        // 未达阈值、未到时间，不落库、不写 ResponseText
        record.ResponseText.Should().BeNull();

        var big = new string('x', 1000);
        await session.AppendChunkAsync(big);

        broadcasts.Should().HaveCount(3);
        TextOf(broadcasts[2].Payload).Should().Be(big);
        // 长度阈值触发一次增量落库（首插 + flush）
        svc.Verify(s => s.UpsertRecordAsync(It.IsAny<ChatRecord>()), Times.Exactly(2));
        record.ResponseText.Should().Be("hello world" + big);
    }

    [Fact]
    public async Task AppendChunkAsync_ShouldIgnoreNullOrEmpty()
    {
        var (recorder, svc, broadcasts) = Build();
        var record = new ChatRecord { SessionId = "s1" };
        var session = await recorder.BeginAsync(record);

        await session.AppendChunkAsync(null);
        await session.AppendChunkAsync(string.Empty);
        await session.AppendChunkAsync("  ");

        // null/empty 不广播；"  " 会广播（非空）
        broadcasts.Should().HaveCount(1);
    }

    [Fact]
    public async Task CompleteAsync_ShouldFinalizeRecordAndBroadcastCompleted()
    {
        var (recorder, svc, broadcasts) = Build();
        svc.Setup(s => s.UpsertRecordAsync(It.IsAny<ChatRecord>()))
            .Callback<ChatRecord>(r => { if (r.Id == 0) r.Id = 7; })
            .Returns(Task.CompletedTask);

        var record = new ChatRecord { SessionId = "s1", Style = "OpenAI_Chat" };
        var session = await recorder.BeginAsync(record);
        await session.AppendChunkAsync("partial");

        await session.CompleteAsync("FINAL_JSON", 200, 1234);

        record.ResponseBody.Should().Be("FINAL_JSON");
        record.ResponseStatus.Should().Be(200);
        record.DurationMs.Should().Be(1234);
        record.ResponseText.Should().Be("partial");

        var completed = broadcasts.First(c => c.EventType == "chat_record_completed");
        Prop<long>(completed.Payload, "recordId").Should().Be(7);
        Prop<bool>(completed.Payload, "isDone").Should().BeTrue();

        // 已完成守卫：再次追加应为 no-op
        broadcasts.Clear();
        await session.AppendChunkAsync("more");
        broadcasts.Should().BeEmpty();
    }

    [Fact]
    public async Task FailAsync_ShouldBroadcastCompletedWithError()
    {
        var (recorder, svc, broadcasts) = Build();
        svc.Setup(s => s.UpsertRecordAsync(It.IsAny<ChatRecord>()))
            .Callback<ChatRecord>(r => { if (r.Id == 0) r.Id = 11; })
            .Returns(Task.CompletedTask);

        var record = new ChatRecord { SessionId = "s1", Style = "OpenAI_Chat" };
        record.ResponseBody = "{\"error\":\"boom\"}"; // 调用方已写入错误体
        var session = await recorder.BeginAsync(record);

        await session.FailAsync(500, 999);

        record.ResponseStatus.Should().Be(500);
        record.DurationMs.Should().Be(999);
        var completed = broadcasts.First(c => c.EventType == "chat_record_completed");
        Prop<bool>(completed.Payload, "error").Should().BeTrue();
    }
}

using ForgeSelf.Abstractions;
using Xunit;

namespace ForgeSelf.Abstractions.Tests;

/// <summary>
/// 收件箱契约测试（B6）。
/// </summary>
/// <remarks>
/// B5 时代此文件验的是「三个 void 写入方法被记录」；B6 契约改为 <c>Send/Claim/Peek/Clear</c> 后，
/// 重点转为验证<b>三语义到 target/wakeup 的映射</b>是否正确。
/// </remarks>
public class InboxContractTests
{
    /// <summary>记录 Send 参数的测试替身（只做映射观测，Claim/Peek/Clear 无真实语义）。</summary>
    private sealed class RecordingInbox : IInbox
    {
        public List<(string Content, InboxTarget Target, MessageSource Source, bool Wakeup)> Sends { get; } = new();

        public void Send(string sessionId, string content, InboxTarget target, MessageSource source, bool wakeup)
            => Sends.Add((content, target, source, wakeup));

        public InboxBatch Claim(string sessionId, bool atTurnBoundary) => new();

        public IReadOnlyList<InboxItem> Peek(string sessionId) => Array.Empty<InboxItem>();

        public void Clear(string sessionId) { }
    }

    [Fact]
    public void Followup_MapsToNextTurn_WithWakeup()
    {
        var inbox = new RecordingInbox();

        inbox.Followup("s1", "continue");

        var send = Assert.Single(inbox.Sends);
        Assert.Equal("continue", send.Content);
        Assert.Equal(InboxTarget.NextTurn, send.Target);
        Assert.True(send.Wakeup);
        Assert.Equal(MessageSource.Api, send.Source);
    }

    [Fact]
    public void Steer_MapsToNextStep_WithWakeup()
    {
        var inbox = new RecordingInbox();

        inbox.Steer("s1", "stop doing that");

        var send = Assert.Single(inbox.Sends);
        Assert.Equal(InboxTarget.NextStep, send.Target);
        Assert.True(send.Wakeup);
    }

    [Fact]
    public void Inject_MapsToNextStep_WithoutWakeup()
    {
        var inbox = new RecordingInbox();
        var block = new { key = "value" };

        inbox.Inject("s1", block);

        var send = Assert.Single(inbox.Sends);
        // inject 语义要点：下一个 step 边界消费，但**不唤醒**空闲 Agent
        Assert.Equal(InboxTarget.NextStep, send.Target);
        Assert.False(send.Wakeup);
        Assert.Equal(MessageSource.System, send.Source);
    }

    [Fact]
    public void Inject_NullBlock_StillSendsWithoutWakeup()
    {
        var inbox = new RecordingInbox();

        inbox.Inject("s1", null!);

        var send = Assert.Single(inbox.Sends);
        Assert.Equal(string.Empty, send.Content);
        Assert.False(send.Wakeup);
    }

    [Fact]
    public void Send_Explicit_OverridesConvenienceMapping()
    {
        var inbox = new RecordingInbox();

        // 直接 Send 时 target/wakeup 完全由调用方决定，扩展方法不介入
        inbox.Send("s1", "custom", InboxTarget.NextTurn, MessageSource.Plugin, wakeup: false);

        var send = Assert.Single(inbox.Sends);
        Assert.Equal(InboxTarget.NextTurn, send.Target);
        Assert.Equal(MessageSource.Plugin, send.Source);
        Assert.False(send.Wakeup);
    }

    [Fact]
    public void ThreeSemantics_ProduceDistinctTargetWakeupCombinations()
    {
        var inbox = new RecordingInbox();

        inbox.Followup("s1", "a");
        inbox.Steer("s1", "b");
        inbox.Inject("s1", "c");

        Assert.Equal(3, inbox.Sends.Count);
        // 三个通道的 (target, wakeup) 组合必须两两不同——否则语义退化成同一条通道
        var combos = inbox.Sends.Select(s => (s.Target, s.Wakeup)).ToList();
        Assert.Equal(combos.Count, combos.Distinct().Count());
        Assert.Contains((InboxTarget.NextTurn, true), combos);
        Assert.Contains((InboxTarget.NextStep, true), combos);
        Assert.Contains((InboxTarget.NextStep, false), combos);
    }
}

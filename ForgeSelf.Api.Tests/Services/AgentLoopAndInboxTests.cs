using ForgeSelf.Abstractions;
using ForgeSelf.Api.Services;

namespace ForgeSelf.Api.Tests.Services;

/// <summary>
/// 收件箱接缝测试（B6）。
/// </summary>
/// <remarks>
/// B5（041）：原「IAgentLoop / InMemoryAgentLoop」用例随旧契约一并删除
/// （旧契约生产消费方 0，循环由 <c>IAgentRegistry</c> + <c>ReactLoopAgent</c> 承载，
/// 见 <c>ForgeSelf.Api.Tests/Plugins/AIAgent/ReactLoopAgentTests.cs</c>）。
/// <para>
/// B6：<c>InMemoryInbox</c> 适配 <c>Send/Claim/Peek/Clear</c> 后，用例重写为验证
/// <b>Claim 在回合边界与 step 边界的差别</b>（A2/A3 的内存态对应物）：
/// step 边界只取 <c>NextStep</c> 项，<c>NextTurn</c> 项必须留到下一回合。
/// </para>
/// <para>
/// B6 切片 2b：<b>Claim 是提案、不删除</b>（R2 协议与 PersistentInbox 同构）——
/// ReactLoopAgent 的 A3 唤醒闸门会在「只有不唤醒的注入」时拒绝消费，
/// 取走即删会把被拒绝的输入丢掉。移除由 <see cref="IInboxConfirmation.ConfirmClaimed"/>
/// 在消费方成功提交后执行；原「claim 后 Peek 即空」的断言相应改为
/// 「claim 后仍在（提案不变异）→ 确认后才空」。
/// </para>
/// </remarks>
public class AgentLoopAndInboxTests
{
    [Fact]
    public void InMemoryInbox_ThreeSemantics_PeekPreservesOrderAndSource()
    {
        var inbox = new InMemoryInbox();
        var contextBlock = new { key = "value" };

        inbox.Followup("s1", "continue");
        inbox.Steer("s1", "stop");
        inbox.Inject("s1", contextBlock);

        var items = inbox.Peek("s1");
        items.Should().HaveCount(3);

        items[0].Content.Should().Be("continue");
        items[0].Source.Should().Be(MessageSource.Api);

        items[1].Content.Should().Be("stop");
        items[1].Source.Should().Be(MessageSource.Api);

        items[2].Content.Should().Be(contextBlock.ToString());
        items[2].Source.Should().Be(MessageSource.System);

        // Peek 不得消费
        inbox.Peek("s1").Should().HaveCount(3);
    }

    [Fact]
    public void Claim_AtTurnBoundary_TakesAllItems_AndReportsWakeup()
    {
        var inbox = new InMemoryInbox();

        inbox.Followup("s1", "a");   // NextTurn + wakeup
        inbox.Inject("s1", "b");     // NextStep + 不唤醒

        var batch = inbox.Claim("s1", atTurnBoundary: true);

        batch.Items.Should().HaveCount(2);
        // 批次含唤醒类项 → Wakeup 必须为 true
        batch.Wakeup.Should().BeTrue();

        // R2：Claim 是提案不删除——确认前两条都还在
        inbox.Peek("s1").Should().HaveCount(2);

        // 消费方成功提交后确认 → 才真正移除
        inbox.ConfirmClaimed("s1", batch.Items);
        inbox.Peek("s1").Should().BeEmpty();
    }

    [Fact]
    public void Claim_AtStepBoundary_TakesOnlyNextStep_LeavesNextTurn()
    {
        var inbox = new InMemoryInbox();

        inbox.Followup("s1", "next-turn");  // NextTurn
        inbox.Steer("s1", "next-step");     // NextStep + wakeup

        var batch = inbox.Claim("s1", atTurnBoundary: false);

        // step 边界只能取走 steer（NextStep），followup 必须留到下一回合
        batch.Items.Should().ContainSingle();
        batch.Items[0].Content.Should().Be("next-step");
        batch.Wakeup.Should().BeTrue();

        // R2：Claim 是提案不删除——确认前两条都还在（含已被「取走」的 steer）
        var remaining = inbox.Peek("s1");
        remaining.Should().HaveCount(2);

        // 确认本批（steer）后，followup 仍在 inbox 里等下一回合
        inbox.ConfirmClaimed("s1", batch.Items);
        var afterConfirm = inbox.Peek("s1");
        afterConfirm.Should().ContainSingle();
        afterConfirm[0].Content.Should().Be("next-turn");
    }

    [Fact]
    public void Claim_OnlyInject_BatchIsNonEmptyButNotWaking()
    {
        var inbox = new InMemoryInbox();

        // A3：inject 空闲时不唤醒——批次有内容但 Wakeup=false
        inbox.Inject("s1", "context-only");

        var batch = inbox.Claim("s1", atTurnBoundary: true);

        batch.Items.Should().ContainSingle();
        batch.Wakeup.Should().BeFalse();
    }

    [Fact]
    public void Claim_AtTurnBoundary_ConsumesEarlierInjectAlongsideFollowup()
    {
        var inbox = new InMemoryInbox();

        // A3：inject 留在 inbox 等唤醒；followup 到达后一同被认领
        inbox.Inject("s1", "stashed-context");
        inbox.Followup("s1", "now-do-it");

        var batch = inbox.Claim("s1", atTurnBoundary: true);

        batch.Items.Should().HaveCount(2);
        batch.Items.Select(i => i.Content).Should().BeEquivalentTo(["stashed-context", "now-do-it"]);
        batch.Wakeup.Should().BeTrue();

        // 消费确认后待处理集清空（R2：确认前两条仍在，提案不变异）
        inbox.Peek("s1").Should().HaveCount(2);
        inbox.ConfirmClaimed("s1", batch.Items);
        inbox.Peek("s1").Should().BeEmpty();
    }

    [Fact]
    public void Claim_EmptySession_ReturnsEmptyBatch_NotNull()
    {
        var inbox = new InMemoryInbox();

        var atTurn = inbox.Claim("s1", atTurnBoundary: true);
        var atStep = inbox.Claim("s1", atTurnBoundary: false);

        atTurn.Items.Should().BeEmpty();
        atTurn.Wakeup.Should().BeFalse();
        atStep.Items.Should().BeEmpty();
        atStep.Wakeup.Should().BeFalse();
    }

    [Fact]
    public void Inbox_SessionIsolation()
    {
        var inbox = new InMemoryInbox();

        inbox.Followup("s1", "msg1");
        inbox.Followup("s2", "msg2");

        inbox.Peek("s1").Should().ContainSingle();
        inbox.Peek("s1")[0].Content.Should().Be("msg1");
        inbox.Peek("s2").Should().ContainSingle();
        inbox.Peek("s2")[0].Content.Should().Be("msg2");

        // 不存在的会话返回空
        inbox.Peek("other").Should().BeEmpty();
    }

    [Fact]
    public void Clear_RemovesSessionItems()
    {
        var inbox = new InMemoryInbox();

        inbox.Followup("s1", "msg1");
        inbox.Followup("s1", "msg2");
        inbox.Peek("s1").Should().HaveCount(2);

        inbox.Clear("s1");
        inbox.Peek("s1").Should().BeEmpty();
    }

    [Fact]
    public void Send_EmptySessionId_Throws()
    {
        var inbox = new InMemoryInbox();

        // ArgumentException.ThrowIfNullOrEmpty 的既有语义：null → ArgumentNullException（ArgumentException 的子类），
        // 空串 → ArgumentException。此处按实际契约分别断言，不放宽成基类。
        Assert.Throws<ArgumentException>(() => inbox.Send("", "x", InboxTarget.NextTurn, MessageSource.Api, true));
        Assert.Throws<ArgumentNullException>(() => inbox.Claim(null!, atTurnBoundary: true));
        Assert.Throws<ArgumentException>(() => inbox.Peek(""));
        Assert.Throws<ArgumentNullException>(() => inbox.Clear(null!));
    }
}

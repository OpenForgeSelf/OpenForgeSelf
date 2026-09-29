using ForgeSelf.Abstractions;
using ForgeSelf.Api.Entities;
using ForgeSelf.Api.Services;
using XCode;
using XCode.DataAccessLayer;

namespace ForgeSelf.Api.Tests.Services;

/// <summary>
/// B6（040 §2.5）持久化收件箱测试。
/// </summary>
/// <remarks>
/// 隔离手法与 <see cref="PersistentSessionStoreTests"/> 同款：每用例独立临时库目录 +
/// <c>DAL.AddConnStr</c> + <c>EntityFactory.InitConnection</c>，类上挂 <c>[Collection("XCode")]</c> 串行。
/// <para>
/// 覆盖门禁（§2.5 门禁清单中可脱离 ReactLoopAgent 接线的持久化部分）：
/// 门禁 4 <c>Inbox_SurvivesRestart</c>（换实例 Peek 一致）、
/// 门禁 5 <c>Claim_AtomicOnCrash</c>（claim 后未落 claimed → 重启消息仍在）。
/// 门禁 1/2/3（Followup 唤醒空闲 / steer 下个 step 边界 / inject 不唤醒）是行为级判据，
/// 待 ReactLoopAgent 接线后补（切片 2b）。
/// </para>
/// </remarks>
[Collection("XCode")]
public class PersistentInboxTests
{
    private readonly string _dbDir;

    public PersistentInboxTests()
    {
        _dbDir = Path.Combine(Path.GetTempPath(), $"ForgeSelfInbox_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dbDir);

        DAL.AddConnStr("ForgeSelf", $"Data Source={Path.Combine(_dbDir, "ForgeSelf.db")}", null, "SQLite");
        EntityFactory.InitConnection("ForgeSelf");

        // XCode 实体缓存为进程级全局单例，跨用例共享；建完临时库后清空，保证读到当前库
        SessionEventEntity.Meta.Cache.Clear("test reset");
        SessionEventEntity.Meta.Cache.Expire = 0;
    }

    private PersistentInbox CreateInbox() => new(new PersistentSessionStore("ForgeSelf"));

    // ───────────── 门禁 4：Inbox_SurvivesRestart ─────────────

    [Fact]
    public void Inbox_SurvivesRestart()
    {
        var inbox = CreateInbox();
        inbox.Followup("s1", "重启前投递");
        inbox.Steer("s1", "转向指令");
        inbox.Inject("s1", "注入上下文");

        // 新实例（模拟进程重启）：只从库里重放
        var restarted = CreateInbox();
        var items = restarted.Peek("s1");

        Assert.Equal(3, items.Count);
        Assert.Equal(["重启前投递", "转向指令", "注入上下文"], items.Select(i => i.Content).ToArray());
        Assert.Equal(MessageSource.System, items[2].Source);
    }

    // ───────────── 门禁 5：Claim_AtomicOnCrash ─────────────

    [Fact]
    public void Claim_AtomicOnCrash()
    {
        var inbox = CreateInbox();
        inbox.Followup("s1", "不能丢的输入");

        // claim 返回提案批次（不落 claimed、不删任何东西）
        var batch = inbox.Claim("s1", atTurnBoundary: true);
        Assert.Single(batch.Items);

        // 模拟「claim 后、step 提交前进程崩溃」：直接换新实例读库
        // （期间没有任何 claimed 事件落库 → 输入必须还在）
        var restarted = CreateInbox();
        var after = restarted.Peek("s1");

        Assert.Single(after);
        Assert.Equal("不能丢的输入", after[0].Content);
    }

    // ───────────── 消费确认：ConfirmClaimed ─────────────

    [Fact]
    public void ConfirmClaimed_RemovesItems_EvenAfterRestart()
    {
        var inbox = CreateInbox();
        inbox.Followup("s1", "待处理");

        var batch = inbox.Claim("s1", atTurnBoundary: true);

        // loop 在 step 成功提交后确认
        inbox.ConfirmClaimed("s1", batch.Items);
        Assert.Empty(inbox.Peek("s1"));

        // 重启后也不复现（claimed 已落日志，重放按 MessageId 移除）
        Assert.Empty(CreateInbox().Peek("s1"));
    }

    // ───────────── Claim 提案语义（不变异） ─────────────

    [Fact]
    public void Claim_DoesNotMutate_InboxUnchanged()
    {
        var inbox = CreateInbox();
        inbox.Followup("s1", "a");
        inbox.Steer("s1", "b");

        var before = inbox.Peek("s1");
        inbox.Claim("s1", atTurnBoundary: true);
        inbox.Claim("s1", atTurnBoundary: false);
        var after = inbox.Peek("s1");

        // 连续两次 Claim 都不改变待处理集——这是 R2 与内存实现「取出即清空」的本质区别
        Assert.Equal(before.Count, after.Count);
        Assert.Equal(before.Select(i => i.Content), after.Select(i => i.Content));
    }

    // ───────────── Claim 边界语义（与 InMemory 同款判据，但基于日志派生） ─────────────

    [Fact]
    public void Claim_AtStepBoundary_TakesOnlyNextStep_LeavesNextTurn()
    {
        var inbox = CreateInbox();
        inbox.Followup("s1", "next-turn");
        inbox.Steer("s1", "next-step");

        var batch = inbox.Claim("s1", atTurnBoundary: false);

        Assert.Single(batch.Items);
        Assert.Equal("next-step", batch.Items[0].Content);
        Assert.True(batch.Wakeup);

        // R2：Claim 是提案、不删除——两条都应仍在 inbox（包括已被「取走」的 next-step，
        // 它要等 ConfirmClaimed 落 claimed 事件后才消失）
        var remaining = inbox.Peek("s1");
        Assert.Equal(2, remaining.Count);
        Assert.Equal("next-turn", remaining[0].Content);
        Assert.Equal("next-step", remaining[1].Content);
    }

    [Fact]
    public void Claim_OnlyInject_BatchNonEmptyButNotWaking()
    {
        var inbox = CreateInbox();
        inbox.Inject("s1", "context-only");

        var batch = inbox.Claim("s1", atTurnBoundary: true);

        Assert.Single(batch.Items);
        // inject 不唤醒：批次有内容但 Wakeup=false（A3 的持久化对应物）
        Assert.False(batch.Wakeup);
    }

    [Fact]
    public void Claim_AtTurnBoundary_ConsumesInjectAlongsideFollowup()
    {
        var inbox = CreateInbox();
        inbox.Inject("s1", "stashed-context");
        inbox.Followup("s1", "now-do-it");

        var batch = inbox.Claim("s1", atTurnBoundary: true);

        Assert.Equal(2, batch.Items.Count);
        Assert.True(batch.Wakeup);
    }

    // ───────────── Send 落库的 op 词汇与重放可逆性 ─────────────

    [Fact]
    public void Send_EventsCarryChannelOp_AndReplayRestoresWakeup()
    {
        var inbox = CreateInbox();
        inbox.Followup("s1", "a");
        inbox.Steer("s1", "b");
        inbox.Inject("s1", "c");

        var spliced = ReadSpliced();
        Assert.Equal(["followup", "steer", "inject"], spliced.Select(e => e.Op).ToArray());
        Assert.Equal(InboxTarget.NextTurn, spliced[0].Target);
        Assert.Equal(InboxTarget.NextStep, spliced[1].Target);
    }

    // ───────────── Clear ─────────────

    [Fact]
    public void Clear_RemovesAll_EvenAfterRestart()
    {
        var inbox = CreateInbox();
        inbox.Followup("s1", "a");
        inbox.Followup("s1", "b");
        Assert.Equal(2, inbox.Peek("s1").Count);

        inbox.Clear("s1");

        Assert.Empty(inbox.Peek("s1"));
        Assert.Empty(CreateInbox().Peek("s1"));
    }

    // ───────────── 未知 op 必炸 ─────────────

    [Fact]
    public void UnknownOp_Throws_NotSilentlySkipped()
    {
        var inbox = CreateInbox();
        inbox.Followup("s1", "a");

        // 直接往日志塞一个未知 op 的 spliced 事件（模拟未来词汇漂移/坏数据）
        var store = new PersistentSessionStore("ForgeSelf");
        store.Append("s1", new InboxSplicedEvent(0, "s1", DateTimeOffset.Now, InboxTarget.NextTurn, "bogus-op", []));

        Assert.Throws<InvalidOperationException>(() => inbox.Peek("s1"));
    }

    private System.Collections.Generic.IReadOnlyList<InboxSplicedEvent> ReadSpliced()
    {
        var store = new PersistentSessionStore("ForgeSelf");
        return store.Replay("s1").OfType<InboxSplicedEvent>().ToArray();
    }
}

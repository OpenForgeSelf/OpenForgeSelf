using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Services;

/// <summary>
/// 持久化收件箱（B6/040 §2.5）：inbox 是「模型可见输入」的入口通道，因此它自己也是日志投影。
/// </summary>
/// <remarks>
/// <para>
/// <b>零独立状态，一切从日志派生</b>：本实现不维护任何内存集合，<see cref="Peek"/>/<see cref="Claim"/>
/// 每次都从 <see cref="ISessionStore.Replay"/> 过滤 <c>agent/inbox/spliced</c> 事件推导当前待处理集，
/// 与「一切进模型的东西先落日志再派生」总原则一致，且天然满足门禁 A4（重启后 Peek 一致）。
/// </para>
/// <para>
/// <b>R2 防丢输入</b>：<see cref="Claim"/> 只返回「提案批次」——不 Append、不从日志删除任何东西。
/// 真正的消费确认由调用方（ReactLoopAgent）在 step 成功提交后经 <see cref="IInboxConfirmation"/>
/// 调用 <see cref="ConfirmClaimed"/> 落 <c>spliced(op="claimed")</c> 事件完成。claim 后、确认前崩溃 →
/// 日志无 claimed → 重启重放后消息仍在 inbox（门禁 A5）。
/// </para>
/// <para>
/// <b>op 词汇</b>：投递类 op 用通道名（<c>followup</c>/<c>steer</c>/<c>inject</c>），
/// 因为 <see cref="InboxSplicedEvent"/> 没有 wakeup 字段——重放时按 op 推导 wakeup
/// （op != inject 即唤醒），<see cref="InboxTarget"/> 用事件字段保真。消费 <c>claimed</c>，清空 <c>cleared</c>。
/// </para>
/// </remarks>
public sealed class PersistentInbox : IInbox, IInboxConfirmation
{
    private readonly ISessionStore _store;

    /// <summary>构造持久化收件箱。</summary>
    /// <param name="store">会话事件日志（唯一写入与回放来源）。</param>
    public PersistentInbox(ISessionStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <inheritdoc />
    /// <remarks>
    /// B8 观察 A：与 Append 侧对齐，<b>拒绝空白内容</b>（null/空串/纯空白一律抛）——
    /// 空白输入落进收件箱只会派生出空 user/message 扰动回合，在入口处拒绝最省事。
    /// </remarks>
    public void Send(string sessionId, string content, InboxTarget target, MessageSource source, bool wakeup)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var item = new InboxItem(NewId(), content, source);
        AppendSpliced(sessionId, target, OpFor(target, wakeup), [item]);
    }

    /// <summary>
    /// 认领：<b>提案批次，绝不删除</b>（R2）。
    /// 回合边界返回全部待处理；step 边界只返回 <c>NextStep</c> 项（steer/inject），<c>NextTurn</c> 项留到下一回合。
    /// </summary>
    public InboxBatch Claim(string sessionId, bool atTurnBoundary)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var entries = Derive(sessionId);
        var taken = atTurnBoundary
            ? entries
            : entries.Where(e => e.Target == InboxTarget.NextStep).ToList();

        return new InboxBatch
        {
            Items = taken.Select(e => e.Item).ToArray(),
            Wakeup = taken.Any(e => e.Wakeup)
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<InboxItem> Peek(string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        return Derive(sessionId).Select(e => e.Item).ToArray();
    }

    /// <inheritdoc />
    public void Clear(string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        AppendSpliced(sessionId, InboxTarget.NextTurn, InboxOps.Cleared, []);
    }

    /// <summary>
    /// 消费确认：把一批已成功提交进会话循环的提案落 <c>spliced(op="claimed")</c>，
    /// 重放时按 MessageId 从待处理集移除。由 ReactLoopAgent 在 step 成功提交后调用。
    /// </summary>
    public void ConfirmClaimed(string sessionId, IReadOnlyList<InboxItem> claimed)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentNullException.ThrowIfNull(claimed);
        if (claimed.Count == 0) return;

        AppendSpliced(sessionId, InboxTarget.NextStep, InboxOps.Claimed, claimed);
    }

    /// <summary>op 词汇：wakeup+NextTurn=followup / wakeup+NextStep=steer / 不唤醒=inject（Target 字段保真）。</summary>
    private static string OpFor(InboxTarget target, bool wakeup)
        => !wakeup ? InboxOps.Inject : target == InboxTarget.NextTurn ? InboxOps.Followup : InboxOps.Steer;

    private void AppendSpliced(string sessionId, InboxTarget target, string op, IReadOnlyList<InboxItem> items)
        => _store.Append(sessionId, new InboxSplicedEvent(0, sessionId, DateTimeOffset.Now, target, op, items));

    /// <summary>从日志重放推导当前待处理集（幂等：重复 MessageId 的 send 忽略）。</summary>
    private List<Entry> Derive(string sessionId)
    {
        var result = new List<Entry>();
        foreach (var evt in _store.Replay(sessionId))
        {
            if (evt is not InboxSplicedEvent spliced) continue;

            switch (spliced.Op)
            {
                case InboxOps.Followup:
                case InboxOps.Steer:
                case InboxOps.Inject:
                    foreach (var item in spliced.Items)
                    {
                        if (result.Any(e => e.Item.MessageId == item.MessageId)) continue;
                        result.Add(new Entry(item, spliced.Target, spliced.Op != InboxOps.Inject));
                    }
                    break;

                case InboxOps.Claimed:
                    var claimedIds = spliced.Items.Select(i => i.MessageId).ToHashSet();
                    result.RemoveAll(e => claimedIds.Contains(e.Item.MessageId));
                    break;

                case InboxOps.Cleared:
                    result.Clear();
                    break;

                default:
                    // 未知 op：严过关原则——炸出而非静默跳过（防 op 词汇漂移后悄悄丢语义）
                    throw new InvalidOperationException($"未知的 agent/inbox/spliced op: {spliced.Op}");
            }
        }

        return result;
    }

    private static string NewId() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>派生条目：item + 重放恢复的 target/wakeup 语义。</summary>
    private sealed record Entry(InboxItem Item, InboxTarget Target, bool Wakeup);
}

using System.Collections.Concurrent;
using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Services;

/// <summary>
/// 内存版收件箱（测试替身 / 宿主默认实现）。
/// </summary>
/// <remarks>
/// B6：适配新契约 <c>Send/Claim/Peek/Clear</c>。条目除 <see cref="InboxItem"/> 外还需记住
/// <c>target</c>（NextTurn/NextStep）与 <c>wakeup</c>，但 <see cref="InboxItem"/> 是 B1 定的三字段记录、
/// 不含这两项，故此处用内部 <c>Entry</c> 包装承载，对外仍只暴露 <see cref="InboxItem"/>。
/// <para>
/// <b>Claim 是提案、不删除（R2 协议与 PersistentInbox 同构）</b>：B6 切片 2b 起 ReactLoopAgent 的
/// 唤醒闸门（A3）会在「收件箱只有不唤醒的注入」时拒绝消费——若 Claim 取走即删，被拒绝的输入就丢了。
/// 因此本实现同样采用「Claim 返回提案 + <see cref="ConfirmClaimed"/> 按 MessageId 移除」，
/// 确认动作由 ReactLoopAgent 在 step 成功提交后经 <see cref="IInboxConfirmation"/> 调用
/// （持久化版把确认落日志，本替身无日志、直接改内存）。
/// </para>
/// </remarks>
public sealed class InMemoryInbox : IInbox, IInboxConfirmation
{
    private readonly ConcurrentDictionary<string, List<Entry>> _entries = new();

    /// <inheritdoc />
    public void Send(string sessionId, string content, InboxTarget target, MessageSource source, bool wakeup)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentNullException.ThrowIfNull(content);

        var list = _entries.GetOrAdd(sessionId, _ => new List<Entry>());
        lock (list)
        {
            list.Add(new Entry(
                new InboxItem(NewId(), content, source),
                target,
                wakeup));
        }
    }

    /// <summary>
    /// 认领：<b>提案批次，不删除</b>（R2，与 PersistentInbox 同构）。
    /// 回合边界返回全部；step 边界只返回 <c>NextStep</c> 项，<c>NextTurn</c> 项留到下一回合。
    /// 移除由 <see cref="ConfirmClaimed"/> 在消费方成功提交后执行。
    /// </summary>
    public InboxBatch Claim(string sessionId, bool atTurnBoundary)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        if (!_entries.TryGetValue(sessionId, out var list)) return new InboxBatch();

        lock (list)
        {
            var taken = atTurnBoundary
                ? list.ToList()
                : list.Where(e => e.Target == InboxTarget.NextStep).ToList();

            return new InboxBatch
            {
                Items = taken.Select(e => e.Item).ToArray(),
                Wakeup = taken.Any(e => e.Wakeup)
            };
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<InboxItem> Peek(string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        if (!_entries.TryGetValue(sessionId, out var list)) return Array.Empty<InboxItem>();

        lock (list)
        {
            return list.Select(e => e.Item).ToArray();
        }
    }

    /// <inheritdoc />
    public void Clear(string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        _entries.TryRemove(sessionId, out _);
    }

    /// <inheritdoc />
    /// <summary>消费确认：把一批已成功提交的提案按 MessageId 从待处理集移除（R2 协议的内存态实现）。</summary>
    public void ConfirmClaimed(string sessionId, IReadOnlyList<InboxItem> claimed)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentNullException.ThrowIfNull(claimed);
        if (claimed.Count == 0) return;

        if (!_entries.TryGetValue(sessionId, out var list)) return;

        var claimedIds = claimed.Select(i => i.MessageId).ToHashSet();
        lock (list)
        {
            list.RemoveAll(e => claimedIds.Contains(e.Item.MessageId));
        }
    }

    private static string NewId() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>内部条目：承载 <see cref="InboxItem"/> 之外的 target / wakeup 语义。</summary>
    private sealed record Entry(InboxItem Item, InboxTarget Target, bool Wakeup);
}

namespace ForgeSelf.Abstractions;

/// <summary>
/// 收件箱接缝：三种输入语义（followup / steer / inject）的统一入口。
/// </summary>
/// <remarks>
/// B6 契约改造：从「只写不读」的三 void 方法，改为「写入 + 认领 + 窥视 + 清空」四方法。
/// <para>
/// <b>Claim 不删除（R2 防丢输入）</b>：<see cref="Claim"/> 只返回「提案批次」，绝不从存储移除。
/// 真正删除发生在调用方把该批次内容成功落进会话日志之后（由 <c>agent/inbox/spliced(op="claimed")</c>
/// 事件表达）。这样「claim 后、落日志前崩溃」不会丢用户输入。
/// </para>
/// <para>
/// <c>InboxTarget</c> / <c>InboxItem</c> / <c>MessageSource</c> 均已由 B1 在
/// <c>SessionEvents.cs</c> 定义，本文件只引用、不重复定义。
/// </para>
/// </remarks>
public interface IInbox
{
    /// <summary>
    /// 投递一条输入。
    /// </summary>
    /// <param name="sessionId">目标会话。</param>
    /// <param name="content">输入内容。</param>
    /// <param name="target">消费时机：<c>NextTurn</c> 下一回合边界 / <c>NextStep</c> 下一 step 边界。</param>
    /// <param name="source">输入来源（与日志中的 <c>MessageSource</c> 同一套词汇）。</param>
    /// <param name="wakeup">是否唤醒空闲中的 Agent。<c>false</c> 时空闲 Agent 不被唤醒（inject 语义）。</param>
    void Send(string sessionId, string content, InboxTarget target, MessageSource source, bool wakeup);

    /// <summary>
    /// 认领一批待处理输入，<b>不删除</b>（见类型注释的 R2 说明）。
    /// </summary>
    /// <param name="sessionId">目标会话。</param>
    /// <param name="atTurnBoundary">
    /// true 表示当前处于回合边界（可取走 <c>NextTurn</c> 项）；
    /// false 表示处于回合内部的 step 边界（只取走 <c>NextStep</c> 项）。
    /// </param>
    InboxBatch Claim(string sessionId, bool atTurnBoundary);

    /// <summary>窥视当前待处理输入（不认领、不删除），供 UI 画待办。</summary>
    IReadOnlyList<InboxItem> Peek(string sessionId);

    /// <summary>清空指定会话的全部待处理输入。</summary>
    void Clear(string sessionId);
}

/// <summary>一次认领返回的批次。</summary>
public sealed class InboxBatch
{
    /// <summary>本批认领到的条目（可能为空，表示当前边界无可认领项）。</summary>
    public IReadOnlyList<InboxItem> Items { get; init; } = Array.Empty<InboxItem>();

    /// <summary>本批是否含唤醒意图（任一项 <c>wakeup=true</c>）。</summary>
    public bool Wakeup { get; init; }
}

/// <summary>
/// 三语义的便捷扩展：把常用组合收敛成固定 target/wakeup 的快捷方法，
/// 避免调用方到处手写枚举组合写错。
/// </summary>
public static class InboxExtensions
{
    /// <summary>续聊：用户对当前会话追加一条普通消息，在下一回合边界消费，并唤醒空闲 Agent。</summary>
    public static void Followup(this IInbox inbox, string sessionId, string message, MessageSource source = MessageSource.Api)
        => inbox.Send(sessionId, message, InboxTarget.NextTurn, source, true);

    /// <summary>转向：即时纠偏/指令，在下一个 step 边界消费，并唤醒空闲 Agent。</summary>
    public static void Steer(this IInbox inbox, string sessionId, string input, MessageSource source = MessageSource.Api)
        => inbox.Send(sessionId, input, InboxTarget.NextStep, source, true);

    /// <summary>注入：外部注入结构化上下文，在下一个 step 边界消费，<b>不唤醒</b>空闲 Agent。</summary>
    public static void Inject(this IInbox inbox, string sessionId, object contextBlock)
        => inbox.Send(sessionId, contextBlock?.ToString() ?? string.Empty, InboxTarget.NextStep, MessageSource.System, false);
}

/// <summary>
/// <c>agent/inbox/spliced</c> 事件的 op 词汇表（B6 切片 2b 提升为常量）。
/// </summary>
/// <remarks>
/// 投递类 op 用通道名（<see cref="Followup"/>/<see cref="Steer"/>/<see cref="Inject"/>），
/// 消费确认 <see cref="Claimed"/> 与清空 <see cref="Cleared"/> 由 <c>PersistentInbox</c> 与
/// <c>ReactLoopAgent</c> 两边共用——单一词汇源，防止字符串漂移后重放语义悄悄走样
/// （PersistentInbox 对未知 op 会直接炸出，见其 Derive 实现）。
/// </remarks>
public static class InboxOps
{
    /// <summary>续聊投递（NextTurn，唤醒）。</summary>
    public const string Followup = "followup";

    /// <summary>转向投递（NextStep，唤醒）。</summary>
    public const string Steer = "steer";

    /// <summary>注入投递（NextStep，不唤醒）。</summary>
    public const string Inject = "inject";

    /// <summary>消费确认：一批已成功提交进会话循环的提案，重放时按 MessageId 从待处理集移除。</summary>
    public const string Claimed = "claimed";

    /// <summary>清空：移除该会话全部待处理输入。</summary>
    public const string Cleared = "cleared";
}

/// <summary>
/// 收件箱消费确认能力（可选能力接口，B6 切片 2b）。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IInbox.Claim"/> 是「提案批次」（R2：不删除）；真正移除由消费方在 step 成功提交后确认。
/// 本接口是<b>可选能力</b>：实现方支持「Claim 提案 + 显式确认」协议时实现之——
/// 持久化实现把确认落 <c>agent/inbox/spliced(op=claimed)</c>（日志投影，重启后仍成立）；
/// 内存测试替身则按 MessageId 直接移除。
/// </para>
/// <para>
/// 为什么是独立能力接口而非并入 <see cref="IInbox"/>：确认是「实现方式」而非「接缝职责」
/// （日志派生实现与纯内存实现的确认动作完全不同），并入主契约会迫使所有实现（含测试替身）
/// 都背上确认语义；ReactLoopAgent 对不支持本能力的收件箱回退为自行落
/// <c>spliced(op=<see cref="InboxOps.Claimed"/>)</c>，两种路径共用同一 op 词汇源。
/// </para>
/// </remarks>
public interface IInboxConfirmation
{
    /// <summary>确认一批已成功提交进会话循环的提案，将其从待处理集移除。</summary>
    void ConfirmClaimed(string sessionId, IReadOnlyList<InboxItem> claimed);
}

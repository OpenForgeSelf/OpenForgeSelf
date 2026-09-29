using ForgeSelf.Abstractions;
using ForgeSelf.Api.Entities;

namespace ForgeSelf.Api.Services;

/// <summary>
/// 会话事件日志 → <c>ChatMessage</c> 只读视图的投影同步器（B4/040）。
/// </summary>
/// <remarks>
/// <para>
/// B4 改序后 <see cref="ISessionStore.Append"/> 是<b>唯一写路径</b>，<c>ChatMessage</c> 表降级为只读投影：
/// 控制器/服务一律不得再直接写它，本类是唯一合法写入者。
/// </para>
/// <para>
/// <b>时序契约（主理人裁决）</b>：<see cref="SyncAsync"/> 是幂等的<b>全量重投影</b>，调用方必须在响应返回前
/// <c>await</c> 它 —— 绝不用 <c>Observe</c> 订阅（async void 火后即忘）承担正确性，否则
/// 「POST 后立刻 GET history」会随机读到旧投影（QA 实测竞态）。<see cref="Subscribe"/> 只服务实时 UI 推送。
/// </para>
/// <para>
/// <b>幂等性</b>：以日志派生的模型可见消息为准，与 <c>ChatMessage</c> 现有行做<b>前缀对齐</b>——
/// 从头逐条比对 Role+Content，首个差异处截断重写、尾部补齐。同一事件集重跑 N 次结果一致（行集合不变），
/// 且能自愈「表里有、日志没有」的孤儿行（投影以日志为准，直接覆盖）。
/// </para>
/// </remarks>
public sealed class SessionProjectionService
{
    private readonly ISessionStore _store;
    private readonly ILogService _log;

    public SessionProjectionService(ISessionStore store, ILogService log)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>
    /// 从日志幂等重投影到 <c>ChatMessage</c> 只读视图。
    /// 同步等待完成 —— 调用方在响应返回前 await，杜绝 async void 竞态。
    /// </summary>
    /// <param name="sessionId">会话 ID。</param>
    /// <param name="ct">取消令牌（XCode 写入为同步 API，取消在调用方入口生效）。</param>
    /// <returns>投影完成。</returns>
    public Task SyncAsync(string sessionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ct.ThrowIfCancellationRequested();

        // 真相源：日志派生的模型可见消息（system/user/assistant/tool，顺序即日志顺序）
        var derived = _store.DeriveMessages(sessionId);

        // 现有投影行（按行主键升序 = 历史投影顺序）
        var existing = ChatMessage.FindAll(ChatMessage._.SessionId == sessionId)
            .OrderBy(m => m.Id)
            .ToList();

        // 前缀对齐：首个差异下标
        var i = 0;
        while (i < derived.Count && i < existing.Count
               && string.Equals(existing[i].Role, derived[i].Role, StringComparison.OrdinalIgnoreCase)
               && string.Equals(existing[i].Content, derived[i].Content, StringComparison.Ordinal))
        {
            i++;
        }

        // 差异处起的旧行全部以日志为准重写（含孤儿行自愈）
        for (var j = existing.Count - 1; j >= i; j--)
        {
            existing[j].Delete();
        }

        for (var k = i; k < derived.Count; k++)
        {
            var now = DateTime.Now;
            new ChatMessage
            {
                SessionId = sessionId,
                Role = derived[k].Role,
                Content = derived[k].Content,
                CreateTime = now,
                UpdateTime = now,
            }.Insert();
        }

        _log.Info("会话投影同步完成，SessionId: {0}, 日志派生 {1} 条，保留前缀 {2} 条，重写 {3} 条",
            sessionId, derived.Count, i, derived.Count - i);

        // XCode 写入为同步 API；异步签名仅为调用方 await 语义（杜绝 async void）
        return Task.CompletedTask;
    }

    /// <summary>
    /// 订阅实时事件流，供 UI 推送（不影响正确性，只影响实时性）。
    /// 正确性路径一律走 <see cref="SyncAsync"/>。
    /// </summary>
    /// <param name="sessionId">会话 ID。</param>
    /// <returns>退订句柄。</returns>
    public IDisposable Subscribe(string sessionId)
        => _store.Observe(sessionId).Subscribe(new NoopObserver());

    /// <summary>占位观察者：仅持有订阅，具体 UI 推送由后续批次按需替换。</summary>
    private sealed class NoopObserver : IObserver<SessionEvent>
    {
        public void OnCompleted() { }

        public void OnError(Exception error) { }

        public void OnNext(SessionEvent value) { }
    }
}

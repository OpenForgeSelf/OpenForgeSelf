using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

/// <summary>
/// Agent 一个回合的运行期依赖：由宿主编排层（<see cref="AIAgentService"/>）解析后注入，
/// 使 <see cref="ReactLoopAgent"/> 只关心状态机，不关心「provider 怎么找、工具 schema 怎么解析」。
/// </summary>
public sealed class AgentTurnRuntime
{
    /// <summary>宿主会话事件日志（唯一写路径 + 模型输入派生源）。</summary>
    public required ISessionStore Store { get; init; }

    /// <summary>本回合使用的 AI 提供方。</summary>
    public required IAIProvider Provider { get; init; }

    /// <summary>上游模型 id。</summary>
    public required string ModelId { get; init; }

    /// <summary>工具执行器（宿主工具注册表，含超时与权限拦截）。</summary>
    public required IToolRegistry ToolExecutor { get; init; }

    /// <summary>已渲染的系统提示；空则不注入。</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>本回合挂载的工具 schema；null/空表示不挂工具。</summary>
    public List<UnifiedToolDefinition>? ToolDefinitions { get; init; }

    /// <summary>事件总线（可为 null：无总线 = 无监听器 = 拦截点全部走默认决策）。</summary>
    public IEventBus? Events { get; init; }

    /// <summary>
    /// 记忆注入钩子（可选）：(最后一条用户消息, 本回合工具, ct) → 追加到系统提示前的记忆文本。
    /// 由 <see cref="AIAgentService"/> 提供（检索长期记忆并渲染成上下文块），保持状态机不依赖记忆实现。
    /// </summary>
    public Func<string, List<UnifiedToolDefinition>?, CancellationToken, Task<string?>>? MemoryPromptProvider { get; init; }

    /// <summary>
    /// 宿主收件箱（B6 软依赖，可为 null）。由 <see cref="AIAgentService"/> 经
    /// <c>_ctx.Get&lt;IInbox&gt;()</c> 运行期懒解析——插件子容器不含宿主契约，构造注入会 500。
    /// 为 null 时回合认领按空批次处理，<see cref="ReactLoopAgent.Inbox"/> 被访问则抛出。
    /// </summary>
    public IInbox? Inbox { get; init; }
}

// ---- agent/* 拦截点契约（payload/decision 类型；总线签名固定：fallback 必填、无 ct） ----

/// <summary>
/// <c>agent/pre-step</c> 拦截点上下文。<see cref="ClaimedInputs"/> 为本 step 边界认领到的输入
/// （steer/inject，B6 A2），监听器可据此在步前纠偏；首步为回合边界认领的全部输入。
/// </summary>
public sealed record PreStepContext(string SessionId, string TurnId, int StepIndex, int MessageCount,
    IReadOnlyList<string>? ClaimedInputs = null);

/// <summary><c>agent/pre-step</c> 决策：允许本步 / 中止回合。</summary>
public abstract record PreStepDecision
{
    /// <summary>按默认流程执行本步。</summary>
    public sealed record Allow : PreStepDecision;

    /// <summary>中止回合（携带原因）。</summary>
    public sealed record Abort(string Reason) : PreStepDecision;
}

/// <summary><c>agent/request</c> 拦截点上下文（请求发出前）。</summary>
public sealed record AgentRequestContext(string SessionId, string TurnId, string StepId, string ModelId, int MessageCount, int Attempt);

/// <summary><c>agent/request</c> 决策：放行 / 同步骤重试 / 直接失败。</summary>
public abstract record AgentRequestDecision
{
    /// <summary>放行本次请求。</summary>
    public sealed record Allow : AgentRequestDecision;

    /// <summary>请求失败后的处置：同一步骤内重试（不重发 step/start、不重复落 user/message）。</summary>
    public sealed record Retry : AgentRequestDecision;

    /// <summary>判定失败（不再重试），回合以 <see cref="TurnEndReason.Aborted"/> 收束。</summary>
    public sealed record Fail(string Error) : AgentRequestDecision;
}

/// <summary><c>agent/turn-stopping</c> 收敛检查点上下文。</summary>
public sealed record TurnStoppingContext(string SessionId, string TurnId, int StepIndex, bool ToolCallsPending);

/// <summary>
/// <c>ReactLoopAgent</c>：turn/step 状态机（041 设计 §3 的正式落地）。
/// </summary>
/// <remarks>
/// <para>
/// 事件序（严格照 041 主循环，落到 while(true) 内）：
/// <c>turn/start → claim(inbox) → assemble → agent/pre-step(waterfall) → step/start
/// → 【Append user/message（仅首步且有待处理输入）】→ request/header → agent/request(waterfall) → llm/stream
/// → assistant/message | assistant/attempt → tool/call* → 工具调度 → tool/result*
/// → step/end → 还欠请求？→ 下一 step → agent/turn-stopping(serial) → turn/end</c>
/// </para>
/// <para>
/// <b>turn-stopping 裁决语义</b>（主理人裁决，避免 041 示例「无监听永不收束」的死循环）：
/// 串行拦截点返回值语义为「是否收束」，<b>无监听器 → 返回 null → 视为收束</b>；
/// 代码写作 <c>if (bail) continue;</c>（bail = !stop），即「有监听器明确要求再跑一步」才继续。
/// 「还欠请求」（本步产生了工具调用）与「步数上限」是独立于拦截点的兜底条件。
/// </para>
/// <para>
/// B4 遗留收口：① 循环中间迭代的助手部分输出（伴随 tool_call 的那段文本）<b>逐条落 assistant/message</b>，
/// 不再只落最终消息；② 未闭合的工具调用（取消/失败导致的孤儿 call）落<b>补偿 tool/result（Skipped）</b> 并显式告警，
/// 不再静默丢弃。
/// </para>
/// </remarks>
public sealed class ReactLoopAgent : IAgent, IAsyncDisposable
{
    /// <summary>单步内请求失败的最大尝试次数（含首次）。</summary>
    private const int MaxAttemptsPerStep = 2;

    private readonly AgentTurnRuntime _runtime;
    private readonly object _sync = new();
    private readonly List<(string CallId, string ToolName)> _openCalls = new();

    private AgentOptions _options;
    private AgentStatus _status = AgentStatus.Idle;
    private CancellationTokenSource? _turnCts;
    private bool _disposed;

    public ReactLoopAgent(string sessionId, AgentOptions options, AgentTurnRuntime runtime)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("sessionId 不能为空", nameof(sessionId));
        SessionId = sessionId;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    /// <inheritdoc />
    public string SessionId { get; }

    /// <inheritdoc />
    public AgentOptions Options
    {
        get { lock (_sync) { return _options; } }
    }

    /// <inheritdoc />
    /// <remarks>
    /// B6 起收件箱由宿主注册（<c>PersistentInbox</c>，日志投影），经 <see cref="AgentTurnRuntime.Inbox"/>
    /// 软依赖注入；未注入时访问抛出（收件箱不是本 Agent 的私有内存态，缺了就是接线错误）。
    /// </remarks>
    public IInbox Inbox => _runtime.Inbox
        ?? throw new InvalidOperationException(
            $"宿主未注入 IInbox 契约，会话 {SessionId} 的收件箱不可用（检查 AgentTurnRuntime.Inbox 接线）");

    /// <inheritdoc />
    public AgentStatus Status
    {
        get { lock (_sync) { return _status; } }
    }

    /// <summary>
    /// 会话复用时更新运行选项（模型切换 / 工具白名单变化）：由 <see cref="AgentRuntimeRegistry"/> 在
    /// <c>GetOrCreateAsync</c> 命中既有 Agent 时调用，避免「换了模型却仍用旧 Agent」。
    /// </summary>
    public void ApplyOptions(AgentOptions options)
    {
        if (options == null) return;
        lock (_sync) { _options = options; }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TurnFrame> RunAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ReactLoopAgent));

        lock (_sync)
        {
            if (_status == AgentStatus.Running)
                throw new InvalidOperationException($"会话 {SessionId} 已有回合在运行，不允许并发 RunAsync");
            _status = AgentStatus.Running;
            _turnCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        }

        var turnCts = _turnCts!;

        try
        {
            var inbox = _runtime.Inbox;

            // claim：回合边界认领（提案批次，绝不删除——R2）。认领放在 turn/start 之前，
            // 因为 A3 唤醒闸门要先判断「是否值得开回合」。
            var turnBatch = inbox?.Claim(SessionId, atTurnBoundary: true) ?? new InboxBatch();
            var claimed = turnBatch.Items;
            var hasWakingInput = claimed.Count > 0 && turnBatch.Wakeup;

            // A3 唤醒闸门 + 无输入判断合并：没有「要唤醒」的输入，且日志尾不是 user/message
            // （IM 网关补全场景：调用方已先落 user/message）→ 不开回合，直接返回、日志零写入。
            // 只含 inject 的收件箱不被唤醒（A3），输入留在 inbox 等后续 followup 一并认领（门禁 3）。
            // 注意：闸门必须在 try 内——yield break 也要走 finally 复位 Running 状态与取消源。
            if (!hasWakingInput && !LogEndsWithUserMessage())
            {
                XTrace.Log.Info("[ReactLoopAgent] 无唤醒输入（收件箱仅 inject 或为空，且日志尾非 user/message），不开回合 SessionId={0}", SessionId);
                yield break;
            }

            var turnId = Guid.NewGuid().ToString("N")[..8];
            var turnEndReason = TurnEndReason.Completed;
            var maxTokensSticky = false;

            Append(new TurnStartEvent(0, SessionId, DateTimeOffset.Now, turnId));
            yield return new TurnStarted(turnId);

            if (claimed.Count > 0)
            {
                yield return new Preparing(claimed);
            }

            var pendingUser = string.Join(Environment.NewLine,
                claimed.Where(i => !string.IsNullOrWhiteSpace(i.Content)).Select(i => i.Content));

            var stepIndex = 0;
            while (true)
            {
                if (turnCts.IsCancellationRequested)
                {
                    turnEndReason = TurnEndReason.Aborted;
                    break;
                }

                if (stepIndex >= _options.MaxStepsPerTurn)
                {
                    XTrace.Log.Warn("[ReactLoopAgent] 达到 MaxStepsPerTurn={0}，强制收束回合 SessionId={1}, TurnId={2}",
                        _options.MaxStepsPerTurn, SessionId, turnId);
                    turnEndReason = maxTokensSticky ? TurnEndReason.MaxTokens : TurnEndReason.Completed;
                    break;
                }

                var stepId = $"{turnId}-{stepIndex}";
                var messages = _runtime.Store.DeriveMessages(SessionId);

                // step 边界认领（A2）：仅回合内部边界（stepIndex > 0）取 steer/inject（NextStep 项）；
                // 首步的输入（含 NextStep 项）已在回合边界认领，这里重复认领会双写 user/message。
                var stepClaimed = stepIndex > 0
                    ? inbox?.Claim(SessionId, atTurnBoundary: false).Items ?? Array.Empty<InboxItem>()
                    : Array.Empty<InboxItem>();

                Append(new StepStartEvent(0, SessionId, DateTimeOffset.Now, turnId, stepId, stepIndex));
                yield return new StepStarted(stepId, stepIndex);

                // 本步实际消费的收件箱输入：step 成功提交后据此落 claimed 确认（R2）；
                // 失败/中止路径不确认 → 输入留在 inbox，下一回合重新认领（A5 不丢输入）。
                IReadOnlyList<InboxItem>? consumed = null;

                if (stepIndex == 0 && claimed.Count > 0)
                {
                    // 首步：把回合边界认领的输入作为 user/message 落日志（模型可见 = 已记录）
                    Append(new UserMessageEvent(0, SessionId, DateTimeOffset.Now, pendingUser, claimed[0].Source));
                    consumed = claimed;
                    messages = _runtime.Store.DeriveMessages(SessionId);
                }
                else if (stepClaimed.Count > 0)
                {
                    // A2：steer/inject 在 step 边界逐条落 user/message（模型可见 = 已记录），进本步 pre-step 上下文
                    foreach (var item in stepClaimed.Where(i => !string.IsNullOrWhiteSpace(i.Content)))
                    {
                        Append(new UserMessageEvent(0, SessionId, DateTimeOffset.Now, item.Content, item.Source));
                    }
                    consumed = stepClaimed;
                    messages = _runtime.Store.DeriveMessages(SessionId);
                }

                // agent/pre-step（waterfall：无监听器走 Allow；携带本步认领的输入供监听器纠偏）
                var preStep = await PreStepAsync(turnId, stepIndex, messages.Count,
                    consumed?.Where(i => !string.IsNullOrWhiteSpace(i.Content)).Select(i => i.Content).ToArray(),
                    turnCts.Token);
                if (preStep is PreStepDecision.Abort abort)
                {
                    XTrace.Log.Warn("[ReactLoopAgent] agent/pre-step 中止回合: {0}", abort.Reason);
                    Append(new StepEndEvent(0, SessionId, DateTimeOffset.Now, turnId, stepId, StepEndReason.Aborted));
                    yield return new StepCompleted(stepId, StepEndReason.Aborted);
                    turnEndReason = TurnEndReason.Aborted;
                    break;
                }

                // request/header：请求信封先落日志（不变量 2：请求是日志的纯函数）
                var toolRefs = _runtime.ToolDefinitions?.Select(t => t.Name).ToList() ?? new List<string>();
                var renderedSystemPrompt = await RenderSystemPromptAsync(messages, toolRefs, turnCts.Token);
                Append(new RequestHeaderEvent(0, SessionId, DateTimeOffset.Now, _runtime.ModelId,
                    renderedSystemPrompt, toolRefs));

                // ---- llm/stream（含同步骤重试；重试不重发 step/start、不重复落 user/message） ----
                var content = new StringBuilder();
                var calls = new List<UnifiedToolCall>();
                UnifiedUsage? usage = null;
                string? finishReason = null;
                var aborted = false;
                var requestTimedOut = false;
                string? requestError = null;

                for (var attempt = 1; attempt <= MaxAttemptsPerStep; attempt++)
                {
                    content.Clear();
                    calls.Clear();
                    usage = null;
                    finishReason = null;
                    requestError = null;

                    // agent/request（waterfall：无监听器走 Allow；Events 为 null 时直接放行）
                    var decision = _runtime.Events == null
                        ? new AgentRequestDecision.Allow()
                        : await _runtime.Events.WaterfallAsync<AgentRequestContext, AgentRequestDecision>(
                            "agent/request",
                            new AgentRequestContext(SessionId, turnId, stepId, _runtime.ModelId, messages.Count, attempt),
                            () => Task.FromResult<AgentRequestDecision>(new AgentRequestDecision.Allow()));

                    if (decision is AgentRequestDecision.Fail fail)
                    {
                        requestError = fail.Error;
                        break;
                    }

                    var request = new UnifiedChatRequest
                    {
                        Model = _runtime.ModelId,
                        Messages = messages.Select(ToUnifiedMessage).ToList(),
                        Tools = _runtime.ToolDefinitions,
                        SystemPrompt = renderedSystemPrompt,
                        Stream = true,
                        StreamOptions = new UnifiedStreamOptions { IncludeUsage = true }
                    };

                    // B7 统一循环：请求级无进展看门狗（StepTimeout 逐块重置）——
                    // 相邻模型产出之间超过 StepTimeout 即判挂起（Suspended 语义），而非按请求总时长硬切：
                    // 长流式回复只要持续推进就不会被误杀；上游不可达/冷启动阻塞则被及时收束。
                    using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(turnCts.Token);
                    requestCts.CancelAfter(_options.StepTimeout);

                    var enumerator = _runtime.Provider.ChatStreamAsync(request, requestCts.Token)
                        .GetAsyncEnumerator(requestCts.Token);
                    try
                    {
                        while (true)
                        {
                            bool moved;
                            try
                            {
                                moved = await enumerator.MoveNextAsync();
                            }
                            catch (OperationCanceledException) when (turnCts.IsCancellationRequested)
                            {
                                // 用户/宿主取消（优先级最高）：保持既有 Aborted 契约
                                aborted = true;
                                break;
                            }
                            catch (OperationCanceledException) when (requestCts.IsCancellationRequested)
                            {
                                // 请求级无进展超时（requestCts 独立触发，非用户取消）：走挂起出口（见 requestTimedOut 块）
                                requestTimedOut = true;
                                break;
                            }
                            catch (OperationCanceledException)
                            {
                                // 上游自发抛出的取消（两令牌均未触发）：保持既有 Aborted 契约（B5 Cancel 语义）
                                aborted = true;
                                break;
                            }
                            catch (Exception ex)
                            {
                                requestError = ex.Message;
                                break;
                            }

                            if (!moved) break;

                            var chunk = enumerator.Current;
                            // 收到任何产出即视为有进展 → 重置看门狗
                            requestCts.CancelAfter(_options.StepTimeout);
                            if (!string.IsNullOrEmpty(chunk.DeltaContent))
                            {
                                content.Append(chunk.DeltaContent);
                                yield return new AssistantDelta(chunk.DeltaContent);
                            }
                            if (chunk.DeltaToolCall != null)
                            {
                                AccumulateToolCall(calls, chunk.DeltaToolCall);
                            }
                            if (chunk.Usage != null)
                            {
                                usage = chunk.Usage;
                            }
                            if (!string.IsNullOrEmpty(chunk.FinishReason))
                            {
                                finishReason = chunk.FinishReason;
                            }
                        }
                    }
                    finally
                    {
                        await enumerator.DisposeAsync();
                    }

                    if (aborted || requestError == null)
                    {
                        break;
                    }

                    // 失败：落 assistant/attempt（不入模型历史），同步骤内重试一次
                    Append(new AssistantAttemptEvent(0, SessionId, DateTimeOffset.Now, content.ToString(),
                        requestError, _runtime.ModelId));
                    XTrace.Log.Warn("[ReactLoopAgent] 请求失败，第 {0}/{1} 次重试 SessionId={2}, StepId={3}, 错误={4}",
                        attempt, MaxAttemptsPerStep, SessionId, stepId, requestError);

                    if (attempt >= MaxAttemptsPerStep)
                    {
                        break;
                    }

                    try
                    {
                        await Task.Delay(200, turnCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        aborted = true;
                        break;
                    }
                }

                // ---- 取消：不提交半截助手消息，只落 attempt；未闭合 call 落补偿结果 ----
                if (aborted)
                {
                    await CloseOpenCallsAsync();
                    Append(new AssistantAttemptEvent(0, SessionId, DateTimeOffset.Now, content.ToString(),
                        "aborted", _runtime.ModelId));
                    Append(new StepEndEvent(0, SessionId, DateTimeOffset.Now, turnId, stepId, StepEndReason.Aborted));
                    yield return new StepCompleted(stepId, StepEndReason.Aborted);
                    turnEndReason = TurnEndReason.Aborted;
                    break;
                }

                // ---- 请求级无进展超时（B7 统一循环）：挂起而非失败，保留现场可恢复 ----
                if (requestTimedOut)
                {
                    await CloseOpenCallsAsync();
                    Append(new AssistantAttemptEvent(0, SessionId, DateTimeOffset.Now, content.ToString(),
                        "request-timeout", _runtime.ModelId));
                    Append(new StepEndEvent(0, SessionId, DateTimeOffset.Now, turnId, stepId, StepEndReason.Suspended));
                    yield return new StepCompleted(stepId, StepEndReason.Suspended);
                    turnEndReason = TurnEndReason.Suspended;
                    XTrace.Log.Warn("[ReactLoopAgent] 请求级无进展超时（StepTimeout={0}），回合挂起 SessionId={1}, TurnId={2}",
                        _options.StepTimeout, SessionId, turnId);
                    break;
                }

                if (requestError != null)
                {
                    await CloseOpenCallsAsync();
                    Append(new AssistantAttemptEvent(0, SessionId, DateTimeOffset.Now, content.ToString(),
                        requestError, _runtime.ModelId));
                    Append(new StepEndEvent(0, SessionId, DateTimeOffset.Now, turnId, stepId, StepEndReason.Failed));
                    yield return new StepCompleted(stepId, StepEndReason.Failed);
                    yield return new TurnFailed(stepId, requestError);
                    turnEndReason = TurnEndReason.Aborted;
                    break;
                }

                var isMaxTokens = string.Equals(finishReason, "length", StringComparison.OrdinalIgnoreCase);
                if (isMaxTokens)
                {
                    maxTokensSticky = true;
                }

                // ---- 无工具调用：本步即最终答复 ----
                if (calls.Count == 0)
                {
                    Append(new AssistantMessageEvent(0, SessionId, DateTimeOffset.Now, content.ToString(),
                        null, ToUsage(usage), finishReason ?? "stop"));

                    var stepReason = isMaxTokens ? StepEndReason.MaxTokens : StepEndReason.Completed;
                    Append(new StepEndEvent(0, SessionId, DateTimeOffset.Now, turnId, stepId, stepReason));
                    yield return new StepCompleted(stepId, stepReason);

                    // R2：本步已成功提交 → 确认消费本步收下的收件箱输入（失败/中止路径不确认）
                    ConfirmIfCommitted(consumed);

                    // 收敛检查点：无监听即收束；监听器明确不收束（返回 false）→ 再跑一步
                    var bail = await ShouldTakeAnotherStepAsync(turnId, stepIndex, toolCallsPending: false, ct: turnCts.Token);
                    if (bail)
                    {
                        stepIndex++;
                        continue;
                    }

                    turnEndReason = maxTokensSticky ? TurnEndReason.MaxTokens : TurnEndReason.Completed;
                    break;
                }

                // ---- 有工具调用：中间迭代的助手输出同样落日志（B4 遗留收口①），随后逐步执行工具 ----
                Append(new AssistantMessageEvent(0, SessionId, DateTimeOffset.Now, content.ToString(),
                    calls.Select(c => new ToolCallRef(c.Id, c.Name, c.Arguments)).ToList(),
                    ToUsage(usage), "tool_calls"));

                var turnExit = false;

                // ---- B8：普通调用走 IToolRegistry.ExecuteBatchAsync（六闸门管线，model-ordered commit）----
                // 出口工具（B7）与普通调用的分界：首个出口工具命中即收束，其后调用不落轨迹（与 029 语义一致）；
                // 其之前的普通调用统一批量执行，结果按模型顺序逐个配对落 tool/result。
                var exitIndex = -1;
                for (var i = 0; i < calls.Count; i++)
                {
                    if (IsExitTool(calls[i].Name))
                    {
                        exitIndex = i;
                        break;
                    }
                }

                var batchCalls = new List<ToolCallRef>();
                for (var i = 0; i < calls.Count && (exitIndex < 0 || i < exitIndex); i++)
                {
                    batchCalls.Add(new ToolCallRef(calls[i].Id, calls[i].Name, calls[i].Arguments));
                }

                // 先落 tool/call + ToolStarted（模型已声明的调用尽早可见），执行后逐个配对 tool/result
                foreach (var call in batchCalls)
                {
                    _openCalls.Add((call.CallId, call.ToolName));
                    Append(new ToolCallEvent(0, SessionId, DateTimeOffset.Now, call.CallId, call.ToolName, call.ArgsJson));
                    yield return new ToolStarted(call.CallId, call.ToolName, call.ArgsJson);
                }

                var batchResults = new List<ToolExecutionResult>();
                if (batchCalls.Count > 0)
                {
                    try
                    {
                        batchResults.AddRange(await _runtime.ToolExecutor.ExecuteBatchAsync(batchCalls, SessionId, turnCts.Token));
                    }
                    catch (Exception ex)
                    {
                        // 双保险：批处理自身异常 → 全部调用合成失败结果，保持 N call N result（A4）
                        XTrace.Log.Error("[ReactLoopAgent] 工具批处理异常: {0}", ex.Message);
                        foreach (var call in batchCalls)
                        {
                            batchResults.Add(new ToolExecutionResult
                            {
                                CallId = call.CallId,
                                ToolName = call.ToolName,
                                Success = false,
                                ErrorMessage = ex.Message
                            });
                        }
                    }
                }

                foreach (var callResult in batchResults)
                {
                    // B8：结果定性直接来自管线（Ok/Error/Denied/Skipped），不再由 Success 反推
                    var callOutcome = callResult.Outcome ?? (callResult.Success ? ToolOutcome.Ok : ToolOutcome.Error);
                    Append(new ToolResultEvent(0, SessionId, DateTimeOffset.Now, callResult.CallId ?? string.Empty,
                        callResult.ToolName, callResult.Result, callOutcome, callResult.DurationMs));
                    _openCalls.RemoveAll(o => string.Equals(o.CallId, callResult.CallId, StringComparison.Ordinal));
                    yield return new ToolCompleted(callResult.CallId ?? string.Empty, callOutcome, callResult.DurationMs);
                }

                // ---- 出口工具路径（B7 语义保持）：声明即出口，不调 ToolExecutor ----
                // 落 tool/call + 合成 tool/result(Ok) 闭合轨迹 → step/end(ExitTool) → 出口帧交驱动方判定，
                // 回合以 Completed 收束（步骤的完成/卡住语义由驱动方依 ExitToolInvoked 判定）。
                if (exitIndex >= 0)
                {
                    var exitCall = calls[exitIndex];
                    Append(new ToolCallEvent(0, SessionId, DateTimeOffset.Now, exitCall.Id, exitCall.Name, exitCall.Arguments));
                    yield return new ToolStarted(exitCall.Id, exitCall.Name, exitCall.Arguments);
                    Append(new ToolResultEvent(0, SessionId, DateTimeOffset.Now, exitCall.Id, exitCall.Name,
                        "{\"success\":true}", ToolOutcome.Ok, 0));
                    yield return new ToolCompleted(exitCall.Id, ToolOutcome.Ok, 0);
                    yield return new ExitToolInvoked(exitCall.Name, exitCall.Arguments);

                    Append(new StepEndEvent(0, SessionId, DateTimeOffset.Now, turnId, stepId, StepEndReason.ExitTool));
                    yield return new StepCompleted(stepId, StepEndReason.ExitTool);

                    // 出口即成功提交 → 确认消费本步收下的收件箱输入（R2）
                    ConfirmIfCommitted(consumed);

                    turnEndReason = TurnEndReason.Completed;
                    turnExit = true;
                }

                if (turnExit)
                {
                    // 出口收束回合（029：一步一回合，complete_step/request_help 即步骤终点）
                    break;
                }

                if (aborted || turnCts.IsCancellationRequested)
                {
                    await CloseOpenCallsAsync();
                    Append(new StepEndEvent(0, SessionId, DateTimeOffset.Now, turnId, stepId, StepEndReason.Aborted));
                    yield return new StepCompleted(stepId, StepEndReason.Aborted);
                    turnEndReason = TurnEndReason.Aborted;
                    break;
                }

                Append(new StepEndEvent(0, SessionId, DateTimeOffset.Now, turnId, stepId, StepEndReason.ToolCallPending));
                yield return new StepCompleted(stepId, StepEndReason.ToolCallPending);

                // R2：本步已成功提交（工具结果已落日志）→ 确认消费本步收下的收件箱输入
                ConfirmIfCommitted(consumed);

                // 还欠请求（工具结果要喂回模型）→ 下一 step；步数上限由循环头部兜底
                stepIndex++;
            }

            Append(new TurnEndEvent(0, SessionId, DateTimeOffset.Now, turnId, turnEndReason));
            yield return new TurnCompleted(turnId, turnEndReason);
        }
        finally
        {
            lock (_sync)
            {
                _status = AgentStatus.Idle;
            }
            _turnCts?.Dispose();
            _turnCts = null;
        }
    }

    /// <inheritdoc />
    public Task CancelAsync(AgentCancelCause cause, bool keepInbox = false)
    {
        if (!keepInbox)
        {
            _runtime.Inbox?.Clear(SessionId);
        }

        XTrace.Log.Info("[ReactLoopAgent] 收到取消请求 SessionId={0}, Cause={1}", SessionId, cause.GetType().Name);
        _turnCts?.Cancel();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task WhenIdleAsync(CancellationToken ct = default)
    {
        while (Status == AgentStatus.Running && !ct.IsCancellationRequested)
        {
            await Task.Delay(10, ct);
        }
    }

    /// <summary>释放：取消在跑的回合并注销。</summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        // keepInbox: true——B6 起收件箱是宿主持久投影（比本运行时对象活得长），
        // 销毁 Agent 不得清空排队中的用户输入；取消语义（keepInbox=false）仅在显式 Cancel 时生效。
        await CancelAsync(new AgentCancelCause.Disposed(), keepInbox: true);
    }

    // ---- 内部：日志写入 / 拦截点 / 工具轨迹收口 ----

    /// <summary>唯一写路径：落一条会话事件。</summary>
    private void Append(SessionEvent evt)
        => _runtime.Store.Append(SessionId, evt);

    /// <summary>
    /// 消费确认（R2/A5）：本步成功提交后，把已消费的收件箱输入从待处理集移除。
    /// 优先走 <see cref="IInboxConfirmation"/> 能力接口——持久化实现把确认落
    /// <c>agent/inbox/spliced(op=<see cref="InboxOps.Claimed"/>)</c>（日志投影，重启后仍成立），
    /// 内存测试替身按 MessageId 移除；收件箱不支持该能力时回退为状态机自行落
    /// spliced(op=claimed) 事件（两种路径共用 <see cref="InboxOps"/> 单一 op 词汇源）。
    /// 失败/中止的 step 不确认 → 输入留在 inbox，下一回合重新认领（A5 不丢输入）。
    /// </summary>
    private void ConfirmIfCommitted(IReadOnlyList<InboxItem>? consumed)
    {
        if (consumed == null || consumed.Count == 0)
        {
            return;
        }

        if (_runtime.Inbox is IInboxConfirmation confirmable)
        {
            confirmable.ConfirmClaimed(SessionId, consumed);
            return;
        }

        Append(new InboxSplicedEvent(0, SessionId, DateTimeOffset.Now, InboxTarget.NextStep, InboxOps.Claimed, consumed));
    }

    /// <summary>
    /// 渲染本步请求实际使用的系统提示：基础系统提示 + 长期记忆上下文块（记忆钩子可用且挂了记忆检索工具时）。
    /// </summary>
    private async Task<string> RenderSystemPromptAsync(
        IReadOnlyList<Message> messages,
        IReadOnlyList<string> toolRefs,
        CancellationToken ct)
    {
        var systemPrompt = _runtime.SystemPrompt ?? string.Empty;

        if (_runtime.MemoryPromptProvider == null || !toolRefs.Contains("get_relevant_memories"))
        {
            return systemPrompt;
        }

        var lastUser = messages.LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))?.Content;
        if (string.IsNullOrWhiteSpace(lastUser))
        {
            return systemPrompt;
        }

        var memoryText = await _runtime.MemoryPromptProvider(lastUser!, _runtime.ToolDefinitions, ct);
        if (string.IsNullOrWhiteSpace(memoryText))
        {
            return systemPrompt;
        }

        return string.IsNullOrWhiteSpace(systemPrompt)
            ? memoryText!
            : memoryText + Environment.NewLine + Environment.NewLine + systemPrompt;
    }

    /// <summary>日志尾是否为 user/message（会话版循环由调用方先落输入的场景）。</summary>
    private bool LogEndsWithUserMessage()
    {
        var events = _runtime.Store.Replay(SessionId);
        if (events.Count == 0)
        {
            return false;
        }

        for (var i = events.Count - 1; i >= 0; i--)
        {
            if (events[i] is UserMessageEvent) return true;
            if (events[i] is SystemMessageEvent or AssistantMessageEvent or ToolResultEvent) return false;
        }

        return false;
    }

    /// <summary><c>agent/pre-step</c> 拦截点（无总线 = 无监听器 = Allow；<paramref name="claimedInputs"/> 为本步认领的输入）。</summary>
    private async Task<PreStepDecision> PreStepAsync(string turnId, int stepIndex, int messageCount,
        IReadOnlyList<string>? claimedInputs, CancellationToken ct)
    {
        if (_runtime.Events == null)
        {
            return new PreStepDecision.Allow();
        }

        return await _runtime.Events.WaterfallAsync<PreStepContext, PreStepDecision>(
            "agent/pre-step",
            new PreStepContext(SessionId, turnId, stepIndex, messageCount, claimedInputs),
            () => Task.FromResult<PreStepDecision>(new PreStepDecision.Allow()));
    }

    /// <summary>
    /// <c>agent/turn-stopping</c> 收敛检查点。
    /// 返回值语义：<b>true = 再跑一步（bail，不收束）</b>；<b>false = 收束</b>。
    /// 无监听器时 <c>SerialAsync</c> 返回 null → 视为「收束」（裁决：无监听即收束）。
    /// </summary>
    private async Task<bool> ShouldTakeAnotherStepAsync(string turnId, int stepIndex, bool toolCallsPending, CancellationToken ct)
    {
        if (_runtime.Events == null)
        {
            return false;
        }

        // 注意用 bool?（而非 bool）：无监听器时 SerialAsync 返回 default（null），
        // 只有显式返回 true/false 的监听器才是「有监听」——这正是「无监听即收束」的判据来源。
        var stop = await _runtime.Events.SerialAsync<TurnStoppingContext, bool?>(
            "agent/turn-stopping",
            new TurnStoppingContext(SessionId, turnId, stepIndex, toolCallsPending)) ?? true;

        return !stop; // bail：监听器明确要求不收束 → 再跑一步
    }

    /// <summary>是否出口工具（B7 统一循环）：命中 <see cref="AgentOptions.ExitToolNames"/> 即声明出口。</summary>
    private bool IsExitTool(string toolName)
        => _options.ExitToolNames is { Count: > 0 } exits
           && exits.Contains(toolName, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 未闭合工具调用收口（B4 遗留收口②）：补一条 Skipped 的 tool/result 并显式告警，
    /// 杜绝「N 个 call 无 result」导致的轨迹缺口与投影静默丢弃。
    /// </summary>
    private Task CloseOpenCallsAsync()
    {
        if (_openCalls.Count == 0)
        {
            return Task.CompletedTask;
        }

        foreach (var (callId, toolName) in _openCalls)
        {
            XTrace.Log.Warn("[ReactLoopAgent] 工具调用未闭合（无 tool/result 配对），落补偿事件: SessionId={0}, CallId={1}, Tool={2}",
                SessionId, callId, toolName);
            Append(new ToolResultEvent(0, SessionId, DateTimeOffset.Now, callId, toolName,
                "{\"success\":false,\"error\":\"未闭合：无配对结果\"}", ToolOutcome.Skipped, 0));
        }

        _openCalls.Clear();
        return Task.CompletedTask;
    }

    /// <summary>统一用量 → 日志用量。</summary>
    private static UsageInfo? ToUsage(UnifiedUsage? usage)
        => usage == null ? null : new UsageInfo(usage.PromptTokens, usage.CompletionTokens, null);

    /// <summary>模型可见消息 → 统一请求消息（含工具调用字段）。</summary>
    private static UnifiedChatMessage ToUnifiedMessage(Message m)
        => new()
        {
            Role = m.Role,
            Content = m.Content,
            ToolCallId = m.CallId,
            ToolCalls = m.ToolCalls?.Select(tc => new UnifiedToolCall
            {
                Id = tc.CallId,
                Name = tc.ToolName,
                Arguments = tc.ArgsJson
            }).ToList()
        };

    /// <summary>累积流式工具调用增量：首块带 Id 视为新调用，后续块向末位追加参数。</summary>
    private static void AccumulateToolCall(List<UnifiedToolCall> pending, UnifiedToolCall delta)
    {
        if (!string.IsNullOrEmpty(delta.Id))
        {
            pending.Add(new UnifiedToolCall { Id = delta.Id, Name = delta.Name, Arguments = delta.Arguments });
            return;
        }
        if (pending.Count == 0) return;
        var last = pending[^1];
        if (!string.IsNullOrEmpty(delta.Name)) last.Name += delta.Name;
        last.Arguments += delta.Arguments;
    }
}

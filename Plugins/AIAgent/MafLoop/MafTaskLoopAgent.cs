using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.AIAgent.Services;
using ForgeSelf.Api.Plugins.AIAgent.Services.ToolFunctions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.AIAgent.MafLoop;

/// <summary>
/// <see cref="MafTaskLoopAgent"/> 的跨层桥：工具执行层（AIFunction/FunctionInvoker）向
/// 主帧循环递交 live 帧与日志事件，主帧循环在消费 MAF 流式更新间隙取走（PILOT-051 FR-2.4）。
/// </summary>
/// <remarks>
/// 日志事件由生产者**同步直写** <see cref="ISessionStore.Append"/>（顺序即调用序）；
/// live 帧不落盘、只入队，由主循环取走转发 SSE。出口（task_complete）状态以单一记录承载。
/// </remarks>
public sealed class MafLoopBridge
{
    private readonly ConcurrentQueue<TurnFrame> _frames = new();
    private readonly object _exitSync = new();
    private (string ToolName, string ArgsJson, string Summary)? _exit;

    public required ISessionStore Store { get; init; }

    public required string SessionId { get; init; }

    /// <summary>工具执行开始：落 tool/call 日志 + ToolStarted 帧（与 ReactLoopAgent 顺序一致）。</summary>
    public Task RecordToolStartAsync(string callId, string toolName, string argsJson, CancellationToken ct)
    {
        Store.Append(SessionId, new ToolCallEvent(0, SessionId, DateTimeOffset.Now, callId, toolName, argsJson));
        _frames.Enqueue(new ToolStarted(callId, toolName, argsJson));
        return Task.CompletedTask;
    }

    /// <summary>工具执行结束：落 tool/result 日志（四态闭合）+ ToolCompleted 帧。</summary>
    public Task RecordToolEndAsync(string callId, string toolName, string resultJson, ToolOutcome outcome,
        long durationMs, CancellationToken ct)
    {
        Store.Append(SessionId, new ToolResultEvent(0, SessionId, DateTimeOffset.Now, callId, toolName,
            resultJson, outcome, durationMs));
        _frames.Enqueue(new ToolCompleted(callId, outcome, durationMs));
        return Task.CompletedTask;
    }

    /// <summary>出口命中（task_complete / ExitToolNames）：记录出口声明供主循环收束回合。</summary>
    public void SetExit(string toolName, string argsJson, string summary)
    {
        lock (_exitSync)
        {
            _exit ??= (toolName, argsJson, summary);
        }
    }

    public (string ToolName, string ArgsJson, string Summary)? TryTakeExit()
    {
        lock (_exitSync)
        {
            var exit = _exit;
            _exit = null;
            return exit;
        }
    }

    /// <summary>取走全部积压 live 帧。</summary>
    public List<TurnFrame> DrainFrames()
    {
        var drained = new List<TurnFrame>();
        while (_frames.TryDequeue(out var frame))
        {
            drained.Add(frame);
        }
        return drained;
    }
}

/// <summary>
/// MAF（Microsoft Agent Framework）驱动的任务循环 Agent（PILOT-051 FR-2.3）：
/// 实现 <see cref="IAgent"/> 既有契约（turn/step 帧序、会话日志不变量、收件箱、取消、看门狗），
/// 每步经 MAF <see cref="ChatClientAgent"/> 的自动 function-invocation 管线驱动模型-工具循环。
/// </summary>
/// <remarks>
/// <para>
/// 分层：<c>ChatClientAgent（UseProvidedChatClientAsIs=true）</c> →
/// <c>FunctionInvokingChatClient（自装，MaximumIterationsPerRequest=MaxStepsPerTurn，FunctionInvoker 短路出口）</c> →
/// <c>ForgeModelCallLogger（逐模型请求落 request/header + assistant/message）</c> →
/// <c>ForgeChatClientAdapter（IAIProvider 适配）</c>。
/// 会话历史唯一真源仍是宿主日志：每次 MAF Run 全新 <see cref="AgentSession"/> + 全量派生消息输入，
/// 不引入 MAF 持久历史（Spec Compatibility）。
/// </para>
/// <para>
/// 任务循环语义（TaskLoopMode）：MAF 内层循环在模型持续请求工具时自动推进；
/// 模型只产出纯文本且本回合已用过工具时，**不结束回合**——追加续跑指令 + <c>ToolMode=RequireAny</c>
/// 再跑一次 MAF Run，直到模型调用 <c>task_complete</c> 出口或触达 MaxStepsPerTurn。
/// 纯问答（整个回合零工具调用）照旧收束。
/// </para>
/// </remarks>
public sealed class MafTaskLoopAgent : IAgent, IAsyncDisposable
{
    private readonly AgentTurnRuntime _runtime;
    private readonly object _sync = new();

    private AgentOptions _options;
    private AgentStatus _status = AgentStatus.Idle;
    private CancellationTokenSource? _turnCts;
    private bool _disposed;

    public MafTaskLoopAgent(string sessionId, AgentOptions options, AgentTurnRuntime runtime)
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
    public IInbox Inbox => _runtime.Inbox
        ?? throw new InvalidOperationException(
            $"宿主未注入 IInbox 契约，会话 {SessionId} 的收件箱不可用（检查 AgentTurnRuntime.Inbox 接线）");

    /// <inheritdoc />
    public AgentStatus Status
    {
        get { lock (_sync) { return _status; } }
    }

    /// <summary>会话复用时更新运行选项（与 ReactLoopAgent.ApplyOptions 同语义）。</summary>
    public void ApplyOptions(AgentOptions options)
    {
        if (options == null) return;
        lock (_sync) { _options = options; }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TurnFrame> RunAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(MafTaskLoopAgent));

        lock (_sync)
        {
            if (_status == AgentStatus.Running)
                throw new InvalidOperationException($"会话 {SessionId} 已有回合在运行，不允许并发 RunAsync");
            _status = AgentStatus.Running;
            _turnCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        }

        var turnCts = _turnCts!;
        var bridge = new MafLoopBridge { Store = _runtime.Store, SessionId = SessionId };

        try
        {
            var inbox = _runtime.Inbox;

            var turnBatch = inbox?.Claim(SessionId, atTurnBoundary: true) ?? new InboxBatch();
            var claimed = turnBatch.Items;
            var hasWakingInput = claimed.Count > 0 && turnBatch.Wakeup;

            if (!hasWakingInput && !LogEndsWithUserMessage())
            {
                XTrace.Log.Info("[MafTaskLoopAgent] 无唤醒输入（收件箱仅 inject 或为空，且日志尾非 user/message），不开回合 SessionId={0}", SessionId);
                yield break;
            }

            var turnId = Guid.NewGuid().ToString("N")[..8];
            var turnEndReason = TurnEndReason.Completed;

            Append(bridge, new TurnStartEvent(0, SessionId, DateTimeOffset.Now, turnId));
            yield return new TurnStarted(turnId);

            if (claimed.Count > 0)
            {
                yield return new Preparing(claimed);
            }

            // 任务循环：task_complete 出口工具进出口集合（挂载经 ExtraTools/内置声明）。
            var exitToolNames = new List<string>(_options.ExitToolNames ?? Array.Empty<string>());
            var toolFunctions = new List<AITool>();
            var extraToolDeclarations = new List<IToolFunctionExtension>();

            foreach (var definition in _runtime.ToolDefinitions ?? new List<UnifiedToolDefinition>())
            {
                toolFunctions.Add(new RegistryToolFunction(
                    definition.Name, definition.Description, ToJsonElement(definition.Parameters),
                    _runtime.ToolExecutor, SessionId, bridge, turnId));
            }

            foreach (var extra in _options.ExtraTools ?? Array.Empty<IToolFunctionExtension>())
            {
                extraToolDeclarations.Add(extra);
                toolFunctions.Add(new ExitToolAIFunction(extra.Name, extra.Description, extra.ParametersJsonSchema));
                if (!exitToolNames.Contains(extra.Name, StringComparer.OrdinalIgnoreCase))
                {
                    exitToolNames.Add(extra.Name);
                }
            }

            if (_options.TaskLoopMode && !exitToolNames.Contains(AgentOptions.TaskCompleteToolName, StringComparer.OrdinalIgnoreCase))
            {
                var taskComplete = new TaskCompleteToolFunction("ai-agent");
                extraToolDeclarations.Add(taskComplete);
                toolFunctions.Add(new TaskCompleteAIFunction(taskComplete.Description, taskComplete.ParametersJsonSchema));
                exitToolNames.Add(AgentOptions.TaskCompleteToolName);
            }

            var exitSet = new HashSet<string>(exitToolNames, StringComparer.OrdinalIgnoreCase);
            var protocolPrompt = _options.TaskLoopMode ? TaskLoopPrompts.Protocol(exitToolNames) : string.Empty;

            var stepIndex = 0;
            var firstStep = true;
            // 上一步是否为「纯文本未出口」的中间汇报步：是则本步强制工具调用（TaskLoop 续跑）。
            var forceToolCallThisStep = false;

            while (true)
            {
                if (turnCts.IsCancellationRequested)
                {
                    turnEndReason = TurnEndReason.Aborted;
                    break;
                }

                if (stepIndex >= _options.MaxStepsPerTurn)
                {
                    XTrace.Log.Warn("[MafTaskLoopAgent] 达到 MaxStepsPerTurn={0}，强制收束回合 SessionId={1}, TurnId={2}",
                        _options.MaxStepsPerTurn, SessionId, turnId);
                    turnEndReason = TurnEndReason.Completed;
                    break;
                }

                var stepId = $"{turnId}-{stepIndex}";
                var isContinuation = _options.TaskLoopMode && forceToolCallThisStep;
                var messages = _runtime.Store.DeriveMessages(SessionId);

                var stepClaimed = stepIndex > 0
                    ? inbox?.Claim(SessionId, atTurnBoundary: false).Items ?? Array.Empty<InboxItem>()
                    : Array.Empty<InboxItem>();

                Append(bridge, new StepStartEvent(0, SessionId, DateTimeOffset.Now, turnId, stepId, stepIndex));
                yield return new StepStarted(stepId, stepIndex);

                IReadOnlyList<InboxItem>? consumed = null;

                if (firstStep && claimed.Count > 0)
                {
                    Append(bridge, new UserMessageEvent(0, SessionId, DateTimeOffset.Now,
                        string.Join(Environment.NewLine, claimed.Where(i => !string.IsNullOrWhiteSpace(i.Content)).Select(i => i.Content)),
                        claimed[0].Source));
                    consumed = claimed;
                    firstStep = false;
                    messages = _runtime.Store.DeriveMessages(SessionId);
                }
                else
                {
                    firstStep = false;
                    foreach (var item in stepClaimed.Where(i => !string.IsNullOrWhiteSpace(i.Content)))
                    {
                        Append(bridge, new UserMessageEvent(0, SessionId, DateTimeOffset.Now, item.Content, item.Source));
                    }
                    consumed = stepClaimed;
                    messages = _runtime.Store.DeriveMessages(SessionId);
                }

                // agent/pre-step（waterfall，与 ReactLoopAgent 同拦截点）
                var preStep = _runtime.Events == null
                    ? new PreStepDecision.Allow()
                    : await _runtime.Events.WaterfallAsync<PreStepContext, PreStepDecision>(
                        "agent/pre-step",
                        new PreStepContext(SessionId, turnId, stepIndex, messages.Count,
                            consumed?.Where(i => !string.IsNullOrWhiteSpace(i.Content)).Select(i => i.Content).ToArray()),
                        () => Task.FromResult<PreStepDecision>(new PreStepDecision.Allow()));
                if (preStep is PreStepDecision.Abort abort)
                {
                    XTrace.Log.Warn("[MafTaskLoopAgent] agent/pre-step 中止回合: {0}", abort.Reason);
                    Append(bridge, new StepEndEvent(0, SessionId, DateTimeOffset.Now, turnId, stepId, StepEndReason.Aborted));
                    yield return new StepCompleted(stepId, StepEndReason.Aborted);
                    turnEndReason = TurnEndReason.Aborted;
                    break;
                }

                // request/header：请求信封先落日志（不变量 2）
                var toolRefs = toolFunctions.OfType<RegistryToolFunction>().Select(t => t.Name)
                    .Concat(extraToolDeclarations.Select(t => t.Name)).ToList();
                var renderedSystemPrompt = await RenderSystemPromptAsync(messages, toolRefs, turnCts.Token);
                if (!string.IsNullOrEmpty(protocolPrompt))
                {
                    renderedSystemPrompt = string.IsNullOrWhiteSpace(renderedSystemPrompt)
                        ? protocolPrompt
                        : renderedSystemPrompt + "\n\n" + protocolPrompt;
                }
                if (isContinuation)
                {
                    renderedSystemPrompt = (renderedSystemPrompt ?? string.Empty) + "\n\n" + TaskLoopPrompts.Continuation(exitToolNames);
                }
                // request/header 由 ForgeModelCallLogger 在每次内层模型请求发出前落盘（含 MAF 注入的完整历史），
                // 此处不再重复落一份步级信封（避免双写）。

                // ---- MAF 管线：一步 = 一次 MAF Run（内层 function-invocation 迭代自动推进） ----
                var chatClient = BuildPipeline(bridge, exitSet, toolRefs);
                // 工具只放在 per-run ChatOptions：MAF 会把 agent 级默认 ChatOptions.Tools 与 run 级合并
                // （实测重复追加），两处都放会导致同一工具声明两遍。
                var agentOptions = new ChatClientAgentOptions
                {
                    Id = $"maf-{SessionId}",
                    Name = "ForgeSelf Agent",
                    UseProvidedChatClientAsIs = true,
                    ChatOptions = new ChatOptions
                    {
                        ModelId = _runtime.ModelId,
                        Instructions = string.IsNullOrWhiteSpace(renderedSystemPrompt) ? null : renderedSystemPrompt
                    }
                };
                var agent = new ChatClientAgent(chatClient, agentOptions, NullLoggerFactory.Instance, null);
                var session = await agent.CreateSessionAsync(turnCts.Token).ConfigureAwait(false);

                var runOptions = new ChatClientAgentRunOptions(new ChatOptions
                {
                    ModelId = _runtime.ModelId,
                    Tools = toolFunctions.Count > 0 ? toolFunctions : null,
                    ToolMode = isContinuation ? ChatToolMode.RequireAny : ChatToolMode.Auto
                });

                var runMessages = messages.Select(ToChatMessage).ToList();

                // B7 请求级无进展看门狗：每收到一个 MAF 更新即重置（长流式回复不误杀）。
                using var stepCts = CancellationTokenSource.CreateLinkedTokenSource(turnCts.Token);
                stepCts.CancelAfter(_options.StepTimeout);

                // 泵：MAF Run 的消费在非迭代器方法内做（C# 迭代器禁止在含 catch 的 try 中 yield），
                // live 帧经无界通道转交本迭代器——AssistantDelta 仍实时流出。
                var channel = Channel.CreateUnbounded<TurnFrame>(new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = true
                });
                var runState = new MafRunState();
                var pump = PumpRunAsync(agent, session, runOptions, runMessages, stepCts, turnCts, channel.Writer, bridge, runState);

                await foreach (var frame in channel.Reader.ReadAllAsync(CancellationToken.None).ConfigureAwait(false))
                {
                    yield return frame;
                }

                await pump.ConfigureAwait(false); // 异常已在泵内捕获，此处仅观察完成

                string? requestError = runState.Error;
                var requestTimedOut = runState.TimedOut;
                var aborted = runState.Aborted;

                // ---- 取消 / 超时 / 失败出口（与 ReactLoopAgent 同语义） ----
                if (aborted)
                {
                    Append(bridge, new AssistantAttemptEvent(0, SessionId, DateTimeOffset.Now, string.Empty, "aborted", _runtime.ModelId));
                    Append(bridge, new StepEndEvent(0, SessionId, DateTimeOffset.Now, turnId, stepId, StepEndReason.Aborted));
                    yield return new StepCompleted(stepId, StepEndReason.Aborted);
                    turnEndReason = TurnEndReason.Aborted;
                    break;
                }

                if (requestTimedOut)
                {
                    Append(bridge, new AssistantAttemptEvent(0, SessionId, DateTimeOffset.Now, string.Empty, "request-timeout", _runtime.ModelId));
                    Append(bridge, new StepEndEvent(0, SessionId, DateTimeOffset.Now, turnId, stepId, StepEndReason.Suspended));
                    yield return new StepCompleted(stepId, StepEndReason.Suspended);
                    turnEndReason = TurnEndReason.Suspended;
                    break;
                }

                if (requestError != null)
                {
                    Append(bridge, new AssistantAttemptEvent(0, SessionId, DateTimeOffset.Now, string.Empty, requestError, _runtime.ModelId));
                    Append(bridge, new StepEndEvent(0, SessionId, DateTimeOffset.Now, turnId, stepId, StepEndReason.Failed));
                    yield return new StepCompleted(stepId, StepEndReason.Failed);
                    yield return new TurnFailed(stepId, requestError);
                    turnEndReason = TurnEndReason.Aborted;
                    break;
                }

                // ---- 出口判定 ----
                var exit = bridge.TryTakeExit();
                if (exit != null)
                {
                    XTrace.Log.Info("[MafTaskLoopAgent] 出口工具 {0} 收束回合 SessionId={1}, TurnId={2}", exit.Value.ToolName, SessionId, turnId);
                    Append(bridge, new StepEndEvent(0, SessionId, DateTimeOffset.Now, turnId, stepId, StepEndReason.ExitTool));
                    yield return new StepCompleted(stepId, StepEndReason.ExitTool);
                    yield return new ExitToolInvoked(exit.Value.ToolName, exit.Value.ArgsJson);
                    ConfirmIfCommitted(bridge, consumed);
                    turnEndReason = TurnEndReason.Completed;
                    break;
                }

                // ---- 空响应检测：本步 MAF Run 未产生任何模型产出（无正文、无工具调用、无出口） ----
                if (!runState.HasText && !runState.HasToolCall)
                {
                    var emptyError = $"MAF Run 未产生任何模型产出：模型 {_runtime.ModelId} 可能不存在、未启用或上游不可用";
                    XTrace.Log.Warn("[MafTaskLoopAgent] {0}（SessionId={1}, StepId={2}）", emptyError, SessionId, stepId);
                    Append(bridge, new AssistantAttemptEvent(0, SessionId, DateTimeOffset.Now, string.Empty, emptyError, _runtime.ModelId));
                    Append(bridge, new StepEndEvent(0, SessionId, DateTimeOffset.Now, turnId, stepId, StepEndReason.Failed));
                    yield return new StepCompleted(stepId, StepEndReason.Failed);
                    yield return new TurnFailed(stepId, emptyError);
                    turnEndReason = TurnEndReason.Aborted;
                    break;
                }

                // ---- 纯文本且未出口 ----
                if (_options.TaskLoopMode && exitSet.Count > 0 && HadToolCallInTurn())
                {
                    // 本回合已用过工具且模型只回了文本：落中间汇报（已由 logger 落 assistant/message），
                    // 不结束回合 → 续跑（下一步强制工具调用）。
                    XTrace.Log.Info("[MafTaskLoopAgent] 任务循环续跑（模型纯文本未出口）SessionId={0}, TurnId={1}, Step={2}",
                        SessionId, turnId, stepIndex);
                    Append(bridge, new StepEndEvent(0, SessionId, DateTimeOffset.Now, turnId, stepId, StepEndReason.Completed));
                    yield return new StepCompleted(stepId, StepEndReason.Completed);
                    ConfirmIfCommitted(bridge, consumed);
                    forceToolCallThisStep = true;
                    stepIndex++;
                    continue;
                }

                // agent/turn-stopping（与 ReactLoopAgent 同拦截点，串行裁决）：无监听器 → null → 视为收束。
                var stopping = _runtime.Events == null
                    ? true
                    : await _runtime.Events.SerialAsync<TurnStoppingContext, bool?>(
                        "agent/turn-stopping",
                        new TurnStoppingContext(SessionId, turnId, stepIndex, ToolCallsPending: false)) ?? true;
                if (!stopping)
                {
                    // 监听器明确要求再跑一步：落本步 end，继续（不强制工具调用）。
                    Append(bridge, new StepEndEvent(0, SessionId, DateTimeOffset.Now, turnId, stepId, StepEndReason.Completed));
                    yield return new StepCompleted(stepId, StepEndReason.Completed);
                    ConfirmIfCommitted(bridge, consumed);
                    forceToolCallThisStep = false;
                    stepIndex++;
                    continue;
                }

                // 无续跑语义（纯问答或未开 TaskLoop）：本步即最终答复，回合收束。
                Append(bridge, new StepEndEvent(0, SessionId, DateTimeOffset.Now, turnId, stepId, StepEndReason.Completed));
                yield return new StepCompleted(stepId, StepEndReason.Completed);
                ConfirmIfCommitted(bridge, consumed);
                forceToolCallThisStep = false;
                break;
            }

            Append(bridge, new TurnEndEvent(0, SessionId, DateTimeOffset.Now, turnId, turnEndReason));
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

        XTrace.Log.Info("[MafTaskLoopAgent] 收到取消请求 SessionId={0}, Cause={1}", SessionId, cause.GetType().Name);
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
        if (_disposed) return;
        _disposed = true;
        await CancelAsync(new AgentCancelCause.Disposed(), keepInbox: true);
    }

    // ---- 内部 ----

    /// <summary>一次 MAF Run 的终态（泵线程写、主循环读）。</summary>
    private sealed class MafRunState
    {
        public bool Aborted;
        public bool TimedOut;
        public string? Error;

        /// <summary>本步是否产出过正文（上游空响应检测用）。</summary>
        public bool HasText;

        /// <summary>本步是否发生过工具执行（含出口工具）。</summary>
        public bool HasToolCall;
    }

    /// <summary>
    /// 消费一次 MAF Run 的流式输出：AssistantDelta → 通道；桥上积压帧 → 通道；
    /// 取消/超时/异常写终态。放非迭代器方法里是为了合法使用 try/catch。
    /// </summary>
    private async Task PumpRunAsync(
        ChatClientAgent agent,
        AgentSession session,
        ChatClientAgentRunOptions runOptions,
        List<ChatMessage> runMessages,
        CancellationTokenSource stepCts,
        CancellationTokenSource turnCts,
        ChannelWriter<TurnFrame> writer,
        MafLoopBridge bridge,
        MafRunState state)
    {
        try
        {
            await foreach (var update in agent.RunStreamingAsync(runMessages, session, runOptions, stepCts.Token)
                .ConfigureAwait(false))
            {
                // 有产出即重置看门狗
                stepCts.CancelAfter(_options.StepTimeout);

                if (update.Contents != null)
                {
                    foreach (var content in update.Contents)
                    {
                        if (content is TextContent { Text: { Length: > 0 } text })
                        {
                            state.HasText = true;
                            await writer.WriteAsync(new AssistantDelta(text)).ConfigureAwait(false);
                        }
                        // FunctionCallContent/FunctionResultContent 的日志与帧由
                        // RegistryToolFunction（执行层）落；此处不再重复（避免双写）。
                    }
                }

                foreach (var frame in bridge.DrainFrames())
                {
                    if (frame is ToolStarted) state.HasToolCall = true;
                    await writer.WriteAsync(frame).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (turnCts.IsCancellationRequested)
        {
            state.Aborted = true;
        }
        catch (OperationCanceledException) when (stepCts.IsCancellationRequested)
        {
            state.TimedOut = true;
        }
        catch (Exception ex)
        {
            state.Error = ex.Message;
            XTrace.Log.Error("[MafTaskLoopAgent] MAF Run 失败 SessionId={0}: {1}", SessionId, ex.Message);
        }
        finally
        {
            foreach (var frame in bridge.DrainFrames())
            {
                await writer.WriteAsync(frame).ConfigureAwait(false);
            }
            writer.TryComplete();
        }
    }

    /// <summary>
    /// 构建 MAF 客户端管线：FunctionInvokingChatClient（短路出口 + 迭代上限）→
    /// ForgeModelCallLogger（逐模型请求日志）→ ForgeChatClientAdapter（IAIProvider 适配）。
    /// </summary>
    private IChatClient BuildPipeline(MafLoopBridge bridge, HashSet<string> exitSet, IReadOnlyList<string> toolRefs)
    {
        var adapter = new ForgeChatClientAdapter(_runtime.Provider, _runtime.ModelId);
        var logger = new ForgeModelCallLogger(adapter, _runtime.Store, SessionId, _runtime.ModelId, toolRefs);

        return new ChatClientBuilder(logger)
            .UseFunctionInvocation(NullLoggerFactory.Instance, fcc =>
            {
                fcc.MaximumIterationsPerRequest = Math.Max(1, _options.MaxStepsPerTurn);
                fcc.IncludeDetailedErrors = false;
                fcc.FunctionInvoker = (context, invokeCt) =>
                {
                    var toolName = context.CallContent?.Name ?? string.Empty;
                    if (exitSet.Contains(toolName))
                    {
                        return ExitToolInvokeAsync(bridge, context, toolName);
                    }
                    return context.Function?.InvokeAsync(context.Arguments, invokeCt)
                        ?? ValueTask.FromResult<object?>(null);
                };
            })
            .Build();
    }

    private static string ForgeChatMessageMapperSerialize(IDictionary<string, object?>? arguments)
    {
        if (arguments == null || arguments.Count == 0) return "{}";
        try
        {
            return JsonSerializer.Serialize(arguments);
        }
        catch
        {
            return "{}";
        }
    }

    /// <summary>
    /// 出口工具短路执行：按 B7「声明即出口」语义落 tool/call + tool/result（Ok）日志，
    /// 记录出口声明，置 <see cref="FunctionInvocationContext.Terminate"/> 收束 MAF 内层循环。
    /// </summary>
    private static async ValueTask<object?> ExitToolInvokeAsync(
        MafLoopBridge bridge, FunctionInvocationContext context, string toolName)
    {
        var argsJson = ForgeChatMessageMapperSerialize(context.CallContent?.Arguments);
        var summary = ExtractTaskCompleteSummary(argsJson);
        var callId = string.IsNullOrEmpty(context.CallContent?.CallId)
            ? $"exit-{Guid.NewGuid().ToString("N")[..8]}"
            : context.CallContent!.CallId;

        await bridge.RecordToolStartAsync(callId, toolName, argsJson, default).ConfigureAwait(false);
        bridge.SetExit(toolName, argsJson, summary);
        var resultJson = JsonSerializer.Serialize(new { success = true, recorded = true });
        await bridge.RecordToolEndAsync(callId, toolName, resultJson, ToolOutcome.Ok, 0, default).ConfigureAwait(false);

        context.Terminate = true;
        return resultJson;
    }

    private static string ExtractTaskCompleteSummary(string argsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            if (doc.RootElement.TryGetProperty("summary", out var summary)
                && summary.ValueKind == JsonValueKind.String)
            {
                return summary.GetString() ?? string.Empty;
            }
        }
        catch
        {
            // 非法参数：摘要留空，原始参数仍在 ArgsJson 里可审计
        }
        return string.Empty;
    }

    /// <summary>渲染本步系统提示：基础系统提示 + 长期记忆上下文块（与 ReactLoopAgent 同语义）。</summary>
    private async Task<string> RenderSystemPromptAsync(IReadOnlyList<Message> messages, IReadOnlyList<string> toolRefs, CancellationToken ct)
    {
        var systemPrompt = _runtime.SystemPrompt ?? string.Empty;

        if (_runtime.MemoryPromptProvider == null || !toolRefs.Contains("memory_relevant"))
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

    /// <summary>本回合到当前为止是否发生过工具调用（依据日志 tool/call 事件，纯函数于日志）。</summary>
    private bool HadToolCallInTurn()
    {
        // 工具轨迹要看原始日志（模型派生消息不含轨迹）：从队尾扫回最近的 turn/start。
        var events = _runtime.Store.Replay(SessionId);
        for (var i = events.Count - 1; i >= 0; i--)
        {
            if (events[i] is ToolCallEvent) return true;
            if (events[i] is TurnStartEvent) return false; // 只看本回合
        }
        return false;
    }

    /// <summary>日志尾是否为 user/message（与 ReactLoopAgent 同语义）。</summary>
    private bool LogEndsWithUserMessage()
    {
        var events = _runtime.Store.Replay(SessionId);
        if (events.Count == 0) return false;

        for (var i = events.Count - 1; i >= 0; i--)
        {
            if (events[i] is UserMessageEvent) return true;
            if (events[i] is SystemMessageEvent or AssistantMessageEvent or ToolResultEvent) return false;
        }

        return false;
    }

    private void Append(MafLoopBridge bridge, SessionEvent evt)
        => _runtime.Store.Append(SessionId, evt);

    private void ConfirmIfCommitted(MafLoopBridge bridge, IReadOnlyList<InboxItem>? consumed)
    {
        if (consumed == null || consumed.Count == 0) return;

        if (_runtime.Inbox is IInboxConfirmation confirmable)
        {
            confirmable.ConfirmClaimed(SessionId, consumed);
            return;
        }

        _runtime.Store.Append(SessionId, new InboxSplicedEvent(0, SessionId, DateTimeOffset.Now,
            InboxTarget.NextStep, InboxOps.Claimed, consumed));
    }

    /// <summary>宿主派生消息 → M.E.AI ChatMessage。</summary>
    private static ChatMessage ToChatMessage(Message m)
    {
        var contents = new List<AIContent>();

        if (!string.IsNullOrEmpty(m.Content))
        {
            contents.Add(new TextContent(m.Content));
        }

        var isToolRole = string.Equals(m.Role, "tool", StringComparison.OrdinalIgnoreCase);
        if (isToolRole && !string.IsNullOrEmpty(m.CallId))
        {
            // 工具结果消息：FunctionResultContent 承载（role=Tool 由 ChatRole 表达）
            contents.Clear();
            contents.Add(new FunctionResultContent(m.CallId, m.Content ?? string.Empty));
            return new ChatMessage(ChatRole.Tool, contents);
        }

        if (m.ToolCalls is { Count: > 0 })
        {
            foreach (var tc in m.ToolCalls)
            {
                // PC-015 自愈（2026-10-29）：历史里的空名调用（旧缺陷落库的空壳）不再出门，
                // 否则 OpenAISdkAdapter.BuildChatMessages 构造 ChatToolCall(functionName:"") 直接抛参异常，
                // 毒化会话此后每条消息 1~2ms 内必败。空壳的 CallId 均无真实 tool/result 配对，无孤儿问题。
                if (string.IsNullOrWhiteSpace(tc.ToolName))
                {
                    continue;
                }
                contents.Add(new FunctionCallContent(tc.CallId, tc.ToolName, ParseCallArguments(tc.ArgsJson)));
            }
        }

        var role = string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase) ? ChatRole.Assistant
            : string.Equals(m.Role, "system", StringComparison.OrdinalIgnoreCase) ? ChatRole.System
            : ChatRole.User;

        return new ChatMessage(role, contents);
    }

    private static Dictionary<string, object?> ParseCallArguments(string? argsJson)
    {
        var dict = new Dictionary<string, object?>();
        if (string.IsNullOrWhiteSpace(argsJson)) return dict;
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                dict[prop.Name] = prop.Value.Clone();
            }
        }
        catch
        {
        }
        return dict;
    }

    private static JsonElement ToJsonElement(object? parameters)
    {
        switch (parameters)
        {
            case JsonElement element when element.ValueKind is JsonValueKind.Object:
                return element.Clone();
            case string text when !string.IsNullOrWhiteSpace(text):
                try { return JsonSerializer.Deserialize<JsonElement>(text); }
                catch { break; }
        }
        return JsonSerializer.Deserialize<JsonElement>("{\"type\":\"object\"}");
    }
}

/// <summary>
/// 内层模型请求日志（PILOT-051 FR-2.4）：每个 MAF 内层模型请求落 <c>request/header</c>，
/// 响应完成后落 <c>assistant/message</c>（含 ToolCallRef 与 usage）——保持与 ReactLoopAgent
/// 逐请求一致的日志序（request/header → assistant/message → tool/call → tool/result）。
/// </summary>
internal sealed class ForgeModelCallLogger : DelegatingChatClient
{
    private readonly ISessionStore _store;
    private readonly string _sessionId;
    private readonly string _modelId;
    private readonly IReadOnlyList<string> _toolRefs;

    public ForgeModelCallLogger(IChatClient inner, ISessionStore store, string sessionId, string modelId,
        IReadOnlyList<string> toolRefs)
        : base(inner)
    {
        _store = store;
        _sessionId = sessionId;
        _modelId = modelId;
        _toolRefs = toolRefs;
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var renderedSystemPrompt = ForgeChatMessageMapper.RenderSystemPrompt(messages, options);
        _store.Append(_sessionId, new RequestHeaderEvent(0, _sessionId, DateTimeOffset.Now, _modelId,
            renderedSystemPrompt, _toolRefs));

        var contentBuilder = new StringBuilder();
        List<ToolCallRef>? calls = null;
        UsageInfo? usage = null;
        string? finish = null;

        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken)
            .ConfigureAwait(false))
        {
            if (update.Contents != null)
            {
                foreach (var content in update.Contents)
                {
                    switch (content)
                    {
                        case TextContent { Text: { Length: > 0 } text }:
                            contentBuilder.Append(text);
                            break;
                        case FunctionCallContent fcc:
                            calls ??= new List<ToolCallRef>();
                            calls.Add(new ToolCallRef(fcc.CallId, fcc.Name ?? string.Empty,
                                JsonSerializer.Serialize(fcc.Arguments ?? new Dictionary<string, object?>())));
                            break;
                        case UsageContent usageContent:
                            usage = new UsageInfo(
                                usageContent.Details.InputTokenCount ?? 0,
                                usageContent.Details.OutputTokenCount ?? 0,
                                usageContent.Details.CachedInputTokenCount);
                            break;
                    }
                }
            }

            if (update.FinishReason != null)
            {
                finish = update.FinishReason.Value.Value;
            }

            yield return update;
        }

        _store.Append(_sessionId, new AssistantMessageEvent(0, _sessionId, DateTimeOffset.Now,
            contentBuilder.ToString(), calls, usage, finish ?? "stop"));
    }
}

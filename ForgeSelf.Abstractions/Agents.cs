namespace ForgeSelf.Abstractions;

/// <summary>
/// B5（041）Agent 运行时契约：把旧的 <c>for(i&lt;10)</c> 循环换成有身份、有状态、可取消、可恢复的 turn/step 状态机。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>分层</b>：<see cref="IAgent"/> 是「一次会话的一个可运行体」；<see cref="IAgentRegistry"/> 按会话 id
/// 管理其生命周期（创建 / 复用 / 注销）。</item>
/// <item><b>持久事实走日志</b>：一切需要落盘的事实（user/message、request/header、assistant/message、
/// tool/call、tool/result、turn/start、turn/end、step/start、step/end）一律走 <see cref="SessionEvent"/>；
/// <see cref="TurnFrame"/> 是<b>运行期 live 帧</b>（不落盘），只供 SSE / 审计消费。</item>
/// <item><b>取消与收束</b>：取消经 <see cref="AgentCancelCause"/> 表达原因；回合收束经
/// <c>agent/turn-stopping</c> 串行拦截点裁决（无监听器即收束，见实现注释）。</item>
/// </list>
/// </remarks>

/// <summary>Agent 运行状态：空闲 / 运行中（同一会话同时只允许一个回合在跑）。</summary>
public enum AgentStatus
{
    Idle,
    Running
}

/// <summary>Agent 运行选项（由宿主编排层解析模型、系统提示、工具白名单后传入）。</summary>
public sealed class AgentOptions
{
    /// <summary>聊天模型 id（形如 <c>provider:upstreamModelId</c>）；空则由实现回退默认模型。</summary>
    public string ModelId { get; init; } = string.Empty;

    /// <summary>已渲染的系统提示（Agent 人设 + 技能 + 关联工作流）；空则不注入。</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>工具白名单；null 表示全部可用。</summary>
    public IReadOnlyList<string>? ToolAllowlist { get; init; }

    /// <summary>单回合最大步骤数（兜底强制收束，杜绝 turn-stopping 语义反转导致的死循环）。</summary>
    public int MaxStepsPerTurn { get; init; } = 10;

    /// <summary>单个步骤（含工具执行）的超时时间。B7 起同时作为<b>请求级无进展看门狗</b>：
    /// 相邻模型产出之间超过该时长即判挂起（<see cref="TurnEndReason.Suspended"/>），
    /// 而非按请求总时长硬切——长流式回复不应被总时长误杀。</summary>
    public TimeSpan StepTimeout { get; init; } = TimeSpan.FromSeconds(45);

    /// <summary>
    /// 出口工具名集合（B7 统一循环，029 计划驱动语义进状态机）：
    /// 模型调用其中任一工具时，本步以该工具<b>声明出口</b>——不调 <see cref="IToolRegistry"/> 真实执行，
    /// 落 <c>tool/call + 合成 tool/result(Ok) + step/end(ExitTool)</c>，经由
    /// <see cref="ExitToolInvoked"/> 帧把工具名与参数交给驱动方判定（complete_step → 完成 /
    /// request_help → 卡住 / submit_plan → 规划提交）。null/空 = 无出口语义（纯 FreeLoop）。
    /// </summary>
    public IReadOnlyList<string>? ExitToolNames { get; init; }

    /// <summary>
    /// 额外挂载的工具（B7 统一循环）：出口工具 schema（RunFlowToolSet 等）经此进入统一循环，
    /// <b>不随 <see cref="ToolAllowlist"/> 过滤</b>、也不要求在宿主 <see cref="IToolRegistry"/> 注册
    /// （出口工具是声明不是执行，无注册需求）。
    /// </summary>
    public IReadOnlyList<IToolFunctionExtension>? ExtraTools { get; init; }
}

/// <summary>
/// 取消原因（可辨识联合）：区分「用户主动取消 / 父级要求停止 / 超时 / 实例释放」，
/// 便于日志与 UI 给出不同文案，也便于 B6/B7 做差异化收尾。
/// </summary>
public abstract record AgentCancelCause
{
    /// <summary>用户主动取消。</summary>
    public sealed record User : AgentCancelCause;

    /// <summary>父级（宿主/编排层）要求停止。</summary>
    public sealed record Parent(string Reason) : AgentCancelCause;

    /// <summary>步骤/回合超时。</summary>
    public sealed record Timeout(string Reason) : AgentCancelCause;

    /// <summary>Agent 实例被释放（注销会话）。</summary>
    public sealed record Disposed : AgentCancelCause;
}

/// <summary>
/// 回合帧（live）：运行期可观察事实，<b>不落盘</b>；持久事实例外走 <see cref="SessionEvent"/>。
/// </summary>
public abstract record TurnFrame;

/// <summary>回合开始。</summary>
public sealed record TurnStarted(string TurnId) : TurnFrame;

/// <summary>已认领收件箱条目（followup/steer/inject 落地前的准备阶段）。</summary>
public sealed record Preparing(IReadOnlyList<InboxItem> Claimed) : TurnFrame;

/// <summary>步骤开始。</summary>
public sealed record StepStarted(string StepId, int Index) : TurnFrame;

/// <summary>助手流式增量文本（对应落盘的 <c>assistant/message</c> 事件在步骤结束时一次性写入）。</summary>
public sealed record AssistantDelta(string Content) : TurnFrame;

/// <summary>工具开始执行。</summary>
public sealed record ToolStarted(string CallId, string ToolName, string ArgsJson) : TurnFrame;

/// <summary>工具执行结束（含四态结果与耗时）。</summary>
public sealed record ToolCompleted(string CallId, ToolOutcome Outcome, long DurationMs) : TurnFrame;

/// <summary>步骤结束（携带步骤结束原因）。</summary>
public sealed record StepCompleted(string StepId, StepEndReason Reason) : TurnFrame;

/// <summary>回合结束（携带回合结束原因）。</summary>
public sealed record TurnCompleted(string TurnId, TurnEndReason Reason) : TurnFrame;

/// <summary>回合失败（不可恢复错误）。</summary>
public sealed record TurnFailed(string StepId, string Error) : TurnFrame;

/// <summary>
/// 出口工具声明（B7 统一循环）：模型调用 <see cref="AgentOptions.ExitToolNames"/> 中的工具时产出。
/// 出口工具<b>不真实执行</b>（声明即出口），工具名与原始参数经本帧交给驱动方判定
/// （029：complete_step → 步骤完成 / request_help → 步骤卡住；规划：submit_plan → Plan 提交）。
/// </summary>
public sealed record ExitToolInvoked(string ToolName, string ArgsJson) : TurnFrame;

/// <summary>
/// Agent 接口：一次会话的可运行体。同一时刻只允许一个回合（<see cref="Status"/> 为
/// <see cref="AgentStatus.Running"/> 时不应再次 <see cref="RunAsync"/>）。
/// </summary>
public interface IAgent
{
    /// <summary>所属会话 id。</summary>
    string SessionId { get; }

    /// <summary>当前运行选项（模型 / 系统提示 / 工具白名单 / 步数上限）。</summary>
    AgentOptions Options { get; }

    /// <summary>本 Agent 的输入收件箱：followup（续聊）/ steer（转向）/ inject（注入）。</summary>
    IInbox Inbox { get; }

    /// <summary>运行状态。</summary>
    AgentStatus Status { get; }

    /// <summary>
    /// 跑一个回合：产出 live 帧流，同时把持久事实逐条落会话日志。
    /// </summary>
    /// <param name="ct">取消令牌；取消时回合以 <see cref="TurnEndReason.Aborted"/> 收束且不提交半截助手消息。</param>
    IAsyncEnumerable<TurnFrame> RunAsync(CancellationToken ct = default);

    /// <summary>请求取消当前回合。</summary>
    /// <param name="cause">取消原因。</param>
    /// <param name="keepInbox">是否保留收件箱未消费条目（默认清空）。</param>
    Task CancelAsync(AgentCancelCause cause, bool keepInbox = false);

    /// <summary>等待 Agent 回到空闲（供并发编排/测试同步）。</summary>
    Task WhenIdleAsync(CancellationToken ct = default);
}

/// <summary>
/// Agent 注册表：按会话 id 管理 Agent 生命周期（创建 / 复用 / 注销）。
/// </summary>
/// <remarks>
/// 命名注意：本接口是<b>运行时注册表</b>，与插件既有的「Agent 人设注册表」
/// （<c>IAgentRegistryService</c> / <c>AgentRegistryService.cs</c>）<b>不是同一个东西</b>，
/// 实现类因此命名为 <c>AgentRuntimeRegistry</c>，避免同目录同名撞车（架构师 §1 B5-4）。
/// </remarks>
public interface IAgentRegistry
{
    /// <summary>取会话 Agent；不存在则按 <paramref name="options"/> 创建并登记。</summary>
    Task<IAgent> GetOrCreateAsync(string sessionId, AgentOptions? options = null, CancellationToken ct = default);

    /// <summary>探测式取会话 Agent；不存在返回 null。</summary>
    Task<IAgent?> TryGetAsync(string sessionId);

    /// <summary>注销会话 Agent（返回是否确实注销了一个）。</summary>
    Task<bool> DisposeAsync(string sessionId, CancellationToken ct = default);
}

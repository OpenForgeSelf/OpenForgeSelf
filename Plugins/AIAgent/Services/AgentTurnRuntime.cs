using ForgeSelf.Abstractions;
using ForgeSelf.Core;
using ForgeSelf.Api.Plugins.AIAgent.MafLoop;

namespace ForgeSelf.Api.Plugins.AIAgent.Services;

/// <summary>
/// Agent 一个回合的运行期依赖：由宿主编排层（<see cref="AIAgentService"/>）解析后注入，
/// 使 <see cref="MafLoop.MafTaskLoopAgent"/> 只关心状态机，不关心「provider 怎么找、工具 schema 怎么解析」。
/// </summary>
/// <remarks>
/// B5（2026-11-02-b5-legacy-cleanup）：原与 ReactLoopAgent 同文件，引擎删除后本文件承载
/// <b>共享运行时依赖 + agent/* 拦截点契约</b>（MafTaskLoopAgent 与测试监听器消费，语义零变化）。
/// </remarks>
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
    /// 为 null 时回合认领按空批次处理，<see cref="MafLoop.MafTaskLoopAgent.Inbox"/> 被访问则抛出。
    /// </summary>
    public IInbox? Inbox { get; init; }

    /// <summary>
    /// 工具审批服务（P0，可选）。MAF 泵落 <c>ToolApprovalRequestContent</c>、续跑消费决策。
    /// 为 null 时高风险工具仍包 <c>ApprovalRequiredAIFunction</c>，但无法持久化/续跑（挂起后需外置审批）。
    /// </summary>
    public IApprovalService? ApprovalService { get; init; }

    /// <summary>
    /// Run 级 Verify 校验器（B2②，可选）：回合正常收束且 <see cref="AgentOptions.ExitCriteria"/> 已配置时
    /// 由引擎调用做确定性验收并落 <c>run/verify</c> 事件。为 null 且判据已配置 = fail-safe 不通过
    /// （校验器缺席不得假绿，BR-2）。
    /// </summary>
    public IRunVerifyService? RunVerify { get; init; }

    /// <summary>
    /// 权限策略服务（B4，可选）：配置了显式规则的工具让位给 ToolRegistry pre-execute 决策（防双问）。
    /// 为 null 时高风险包装判断维持 HighRiskToolPolicy 硬名单（零配置红线）。
    /// </summary>
    public IPermissionProfileService? PermissionProfiles { get; init; }
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

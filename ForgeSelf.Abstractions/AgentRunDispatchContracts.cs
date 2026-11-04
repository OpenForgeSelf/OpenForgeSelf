namespace ForgeSelf.Abstractions;

/// <summary>
/// 「把一条任务派给 AIAgent 后台执行（MAF Run）」的能力接缝（PILOT-079 FR-1/FR-9 契约）。
///
/// <para>
/// 提供方：<c>ai-agent</c> 插件（在 <c>Apply(IContext)</c> 里 eager 构造并
/// <c>ctx.Register&lt;IAgentRunDispatch&gt;(provider)</c>；提供即 effect，插件卸载自动摘除 —— 同
/// <see cref="IScheduledAgentRunTrigger"/> / <see cref="IBuiltInAgentExecution"/> 的注册先例）。
/// 消费方：<c>im-gateway</c>（ImTaskDispatcher 派发 / ImTaskWatcher 终态跟进），
/// 一律 <c>ctx.Get&lt;IAgentRunDispatch&gt;()</c> <b>每次用每次取</b>，禁止把实例缓存为字段
/// （提供方热重载后共享表自动摘除，缓存会造成悬空引用 —— 同 <see cref="IAgentDelegation"/> 的告诫）。
/// </para>
///
/// <para>
/// 为什么走契约而不是 HTTP：插件间不得用直连 HTTP / 共享文件 / 静态类传递数据
/// （architecture-design 铁律 2/3；接缝登记见 docs/01-architecture/host-capability-seams.md）。
/// 本接缝同进程直调 <c>IRunOrchestratorService</c>（RunAsync / ResumeAsync 的后台驱动薄封装，
/// 先例 <see cref="IScheduledAgentRunTrigger"/>），<b>禁 HTTP 自调</b>；写操作仍走现有审批，
/// 不关闭审批、不代跑系统命令、不做云端。
/// </para>
///
/// <para>失败语义（对齐 <see cref="IAgentDelegation"/>）：契约失败一律
/// <see cref="AgentRunDispatchOutcome.Success"/>=false + <see cref="AgentRunDispatchOutcome.Error"/> 中文原文，
/// <b>不抛业务异常</b>，以便消费方把原因原样转给 IM 用户。</para>
///
/// <para>状态词表：与任务中心（B3 §3.2 / <c>TasksOverviewController</c>）完全一致的小写词表
/// <c>running / planning / pending / completed / failed / stuck / cancelled / unknown</c>，不造第二套。</para>
/// </summary>
public interface IAgentRunDispatch
{
    /// <summary>
    /// 提交一次任务：后台 Task 驱动 <c>IRunOrchestratorService.RunAsync</c>，立即返回（不阻塞等待 Run 跑完）。
    /// 校验失败（任务原文/会话为空、Agent 不存在/停用）或同会话已有未终态 Run 时返回
    /// <see cref="AgentRunDispatchOutcome.Success"/>=false + <see cref="AgentRunDispatchOutcome.Error"/> 中文原文，不抛业务异常。
    /// </summary>
    Task<AgentRunDispatchOutcome> SubmitAsync(AgentRunDispatchRequest request, CancellationToken ct = default);

    /// <summary>
    /// 按任务键读回当前快照。不存在/键失效返回 null（消费方映射「未找到」）。
    /// <paramref name="taskKey"/> 为纯数字时按 RunId 直查（<b>宿主重启后的恢复路径</b>——内存快照随进程消失，
    /// 只剩 AgentRun 落库事实可投影）。
    /// </summary>
    Task<AgentRunDispatchSnapshot?> FindAsync(string taskKey, CancellationToken ct = default);

    /// <summary>
    /// 进程内续跑（PILOT-079 FR-9 契约增量）：同进程调 <c>IRunOrchestratorService.ResumeAsync(runId)</c>
    /// 并后台驱动（语义同 <see cref="SubmitAsync"/>，立即返回）。<b>仅 Status ∈ {stuck, failed} 允许</b>，
    /// 服务审批（IM 审批闭环）与人工续跑共用本入口。
    /// <paramref name="note"/> 仅落 XTrace 审计（如 <c>imgateway:wecom 经 IM 决策 @时间</c>），不改变续跑语义。
    /// </summary>
    Task<AgentRunDispatchOutcome> ResumeAsync(string taskKey, string? note, CancellationToken ct = default);
}

/// <summary>派发请求（消费方组装；字段名与 <c>RunRequest</c> 对齐，避免两套词汇）。</summary>
public class AgentRunDispatchRequest
{
    /// <summary>任务原文（必填，非空）。全文进 Run（orchestrator 既有行为），消费方自行另存摘要。</summary>
    public string TaskInput { get; set; } = string.Empty;

    /// <summary>
    /// Agent 引用：AgentId（如 <c>agent.programmer</c>）或显示名（精确匹配）。null/空 = 交由实现方默认链。
    /// 解析失败（不存在/停用）返回失败并列出可用候选。
    /// </summary>
    public string? AgentRef { get; set; }

    /// <summary>
    /// Run 会话 id（必填）。约定 <c>imtask:</c> 前缀 + 消费方会话键（如 <c>imtask:{聊天会话Id}</c>），
    /// 稳定可回溯；同会话同时只允许 1 个未终态 Run。
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>聊天模型 id（形如 provider:upstreamModelId；空 = 交给 orchestrator 既有 IChatModelResolver 链明确失败，不新增回退猜测，PC-013 lens）。</summary>
    public string? ChatModelId { get; set; }

    /// <summary>发起方标识（如 <c>imgateway:wecom</c>），用于审计与统计归因。</summary>
    public string? CreatedBy { get; set; }
}

/// <summary>派发提交结果（即时返回；RunId 在规划完成前为 null）。</summary>
public class AgentRunDispatchOutcome
{
    /// <summary>是否成功受理（受理 ≠ 执行完成；执行结果经 <see cref="FindAsync"/> 轮询）。</summary>
    public bool Success { get; set; }

    /// <summary>任务外部键（GUID "N" 形式），后续状态回读与续跑都用它。</summary>
    public string? TaskKey { get; set; }

    /// <summary>本机 Run id；提交时规划未完成恒为 null（快照回读后回填）。</summary>
    public long? RunId { get; set; }

    /// <summary>实际选中的 Agent id（解析失败时为空）。</summary>
    public string? AgentId { get; set; }

    /// <summary>实际选中的 Agent 名（展示用，如 ack 回执「已指派 X」）。</summary>
    public string? AgentName { get; set; }

    /// <summary>提交后的即时状态（统一小写词表，通常为 <c>pending</c>；续跑时为 <c>running</c>）。</summary>
    public string? Status { get; set; }

    /// <summary>失败原因（中文原文，可直接展示；成功时为 null）。</summary>
    public string? Error { get; set; }

    /// <summary>成功受理。</summary>
    public static AgentRunDispatchOutcome Ok(string taskKey, string? agentId, string? agentName, string status, long? runId = null) =>
        new() { Success = true, TaskKey = taskKey, AgentId = agentId, AgentName = agentName, Status = status, RunId = runId };

    /// <summary>失败（原因原文）。</summary>
    public static AgentRunDispatchOutcome Fail(string error) =>
        new() { Success = false, Error = error };
}

/// <summary>任务状态快照（只读投影；词表同任务中心）。</summary>
public class AgentRunDispatchSnapshot
{
    /// <summary>任务外部键（按 RunId 数字键回读时 = 传入的键）。</summary>
    public string TaskKey { get; set; } = string.Empty;

    /// <summary>本机 Run id（规划完成前为 null）。</summary>
    public long? RunId { get; set; }

    /// <summary>当前状态（统一小写词表）。</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 是否终态（completed / failed / cancelled）。
    /// stuck <b>不算</b>终态——它可经审批/人工续跑回到 running，消费方（Watcher）须继续跟进。
    /// </summary>
    public bool Terminal { get; set; }

    /// <summary>结果文本（done 终局全文 / run_stuck 原因 / error 原文；尽力而为，可能为 null）。</summary>
    public string? ResultText { get; set; }

    /// <summary>指派的 Agent 名（展示用）。</summary>
    public string? AgentName { get; set; }

    /// <summary>开始时间（后台驱动拉起时刻；DB 投影路径 = Run 创建时间）。</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>终态时间（未终态为 null；DB 投影路径 = Run 最后更新时间）。</summary>
    public DateTime? EndedAt { get; set; }

    /// <summary>当前步骤下标（0 基；未规划/未知为 null。T16 进展推送用）。</summary>
    public int? CurrentStepIndex { get; set; }

    /// <summary>计划步骤总数（未知为 null。T16 进展推送用）。</summary>
    public int? StepCount { get; set; }
}

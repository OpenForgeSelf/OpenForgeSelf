namespace ForgeSelf.Abstractions;

/// <summary>
/// 「把一条任务交给外部 agent 执行」的能力接缝（L1 契约）。
///
/// <para>
/// 提供方：<c>agent-hub</c> 插件（在 <c>Apply(IContext)</c> 里 eager 构造并
/// <c>ctx.Register&lt;IAgentDelegation&gt;(provider)</c>；提供即 effect，插件卸载自动摘除）。
/// 消费方：任何需要「下发任务给 agent」的插件（首个消费方 = <c>todo-tracker</c>），
/// 一律 <c>ctx.Get&lt;IAgentDelegation&gt;()</c> <b>每次用每次取</b>，禁止把实例缓存为字段
/// （提供方热重载后共享表自动摘除，缓存会造成悬空引用 —— 同 <see cref="IProjectRegistry"/> 的告诫）。
/// </para>
///
/// <para>
/// 为什么走契约而不是 HTTP：插件间不得用直连 HTTP / 共享文件 / 静态类传递数据
/// （architecture-design 铁律 2/3；接缝登记见 docs/01-architecture/host-capability-seams.md）。
/// 本接缝只包住 AgentHub 既有 <c>DelegationRuntime</c> 的「入队 + 读回」，<b>不新增</b>任何执行语义；
/// 状态机、cwd 白名单、权限审批仍由 AgentHub 单方持有（单一真相）。
/// </para>
///
/// <para>消费方在接缝缺席（未装 / 未启用 agent-hub）时必须<b>降级并可解释</b>：
/// <c>Get</c> 返回 null ⇒ 对外映射 503 + 明确文案，禁止静默当成功。</para>
/// </summary>
public interface IAgentDelegation
{
    /// <summary>
    /// 提交一次委派（入队后立即返回，不阻塞等待 agent 跑完）。
    /// 失败（prompt 空、agent 不存在/停用、cwd 不存在或不在白名单、权限模式被禁等）
    /// 一律返回 <see cref="AgentDelegationOutcome.Success"/>=false + <see cref="AgentDelegationOutcome.Error"/> 原文，
    /// <b>不抛业务异常</b>，以便消费方把原因原样转给用户。
    /// </summary>
    Task<AgentDelegationOutcome> SubmitAsync(AgentDelegationRequest request, CancellationToken ct = default);

    /// <summary>按 <c>taskKey</c> 读回委派任务的当前快照。不存在返回 null（消费方映射 404）。</summary>
    Task<AgentDelegationSnapshot?> FindAsync(string taskKey, CancellationToken ct = default);

    /// <summary>
    /// 按 <c>taskKey</c> 把委派任务标记为「已由外部回报完成」（PILOT-057B）。
    /// 语义：回报优先于进程退出判定 —— agent 已把结果写回台账（如 todo stageTo=Review）即视为完成；
    /// 已处于终态的任务不改（先到先得），不存在返回 false，不抛业务异常。
    /// </summary>
    Task<bool> MarkCompletedAsync(string taskKey, CancellationToken ct = default);

    /// <summary>
    /// 当前可用的 agent 候选（仅 <c>enabled</c> 的那些），供消费方在下发前自检/提示。
    /// 无候选返回空列表；提供方不可用时返回空列表而非抛异常。
    /// </summary>
    IReadOnlyList<AgentDelegationAgent> ListAvailableAgents();
}

/// <summary>委派请求（键名与 AgentHub <c>DelegationRequest</c> 对齐，避免两套词汇）。</summary>
public class AgentDelegationRequest
{
    /// <summary>提示词（必填，非空）。由消费方组装，提供方不解释其内容。</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>指定 agent Id；null/0 = 交由提供方按 facet/tag 自动选路。</summary>
    public int? AgentId { get; set; }

    /// <summary>工作目录（绝对路径）。null/空 = 用该 agent 的默认目录。受 AgentHub cwd 白名单约束。</summary>
    public string? Cwd { get; set; }

    /// <summary>
    /// 权限模式（<c>read-only</c> / <c>workspace-write</c> / <c>accept-edits</c>）。
    /// null = 由提供方取默认。<b>无限权限类模式（yolo 等）一律被提供方拒绝</b>，
    /// 消费方也应自行先拒（双层，不互相替代）。
    /// </summary>
    public string? PermissionMode { get; set; }

    /// <summary>发起方标识（如 <c>ui</c> / <c>todo-tracker</c> / <c>agent:xxx</c>），用于审计与统计归因。</summary>
    public string? CreatedBy { get; set; }
}

/// <summary>委派提交结果。</summary>
public class AgentDelegationOutcome
{
    /// <summary>是否成功入队。</summary>
    public bool Success { get; set; }

    /// <summary>失败原因（中文原文，可直接展示；成功时为 null）。</summary>
    public string? Error { get; set; }

    /// <summary>入队成功后的任务外部键（GUID "N"），后续状态回读与回报都用它。</summary>
    public string? TaskKey { get; set; }

    /// <summary>实际选中的 agent Id（0=未知）。</summary>
    public int AgentId { get; set; }

    /// <summary>实际选中的 agent 名（展示用）。</summary>
    public string? AgentName { get; set; }

    /// <summary>提交后的即时状态（通常为 <c>Queued</c>）。</summary>
    public string? Status { get; set; }

    /// <summary>成功。</summary>
    public static AgentDelegationOutcome Ok(string taskKey, int agentId, string? agentName, string status) =>
        new() { Success = true, TaskKey = taskKey, AgentId = agentId, AgentName = agentName, Status = status };

    /// <summary>失败（原因原文）。</summary>
    public static AgentDelegationOutcome Fail(string error) =>
        new() { Success = false, Error = error };
}

/// <summary>委派任务状态快照（只读投影）。状态词表由 AgentHub 单方持有：
/// <c>Queued|Running|AwaitingPermission|Succeeded|Failed|Cancelled|Timeout|Interrupted</c>。</summary>
public class AgentDelegationSnapshot
{
    /// <summary>任务外部键。</summary>
    public string TaskKey { get; set; } = string.Empty;

    /// <summary>当前状态（同上词表）。</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>是否终态（Succeeded/Failed/Cancelled/Timeout/Interrupted）。消费方据此决定是否继续轮询。</summary>
    public bool Terminal { get; set; }

    /// <summary>退出码（未结束时为 null；注意 0 不等于成功，判定以 <see cref="Status"/> 为准）。</summary>
    public int? ExitCode { get; set; }

    /// <summary>错误码（如 <c>cwd_busy</c> / <c>cancelled</c> / <c>host_restart</c>）。</summary>
    public string? ErrorCode { get; set; }

    /// <summary>执行耗时（毫秒，不含审批等待）。</summary>
    public long ElapsedMs { get; set; }

    /// <summary>结果文本（agent 输出累积，可能被截断）。</summary>
    public string? ResultText { get; set; }

    /// <summary>本次委派改动的文件（由提供方从自身产物 JSON 解析后结构化交出，消费方不必了解内部格式）。</summary>
    public IReadOnlyList<AgentDelegationArtifact> Artifacts { get; set; } = [];

    /// <summary>实际使用的工作目录（回填给消费方做证据）。</summary>
    public string? Cwd { get; set; }
}

/// <summary>委派产物中的单个文件变更（由提供方解析自身 <c>ArtifactsJson</c> 得到，格式不外泄）。</summary>
public class AgentDelegationArtifact
{
    /// <summary>事件类型（<c>FileChange</c> / <c>Diff</c>）。</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>产生它的工具名（可空）。</summary>
    public string? Tool { get; set; }

    /// <summary>文本（路径 / diff 片段）。</summary>
    public string? Text { get; set; }
}

/// <summary>可用 agent 候选（下发前自检用）。</summary>
public class AgentDelegationAgent
{
    /// <summary>Agent Id（提交请求时回传用）。</summary>
    public int Id { get; set; }

    /// <summary>显示名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>厂商/profile 键（如 <c>codex</c> / <c>qodercli</c>）。</summary>
    public string Vendor { get; set; } = string.Empty;

    /// <summary>默认工作目录（可空）。</summary>
    public string? DefaultCwd { get; set; }
}

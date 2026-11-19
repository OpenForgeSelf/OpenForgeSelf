namespace ForgeSelf.Abstractions;

/// <summary>
/// 「外部 Coding Agent Runtime」能力接缝（L1 契约）。
///
/// <para>
/// 提供方：<c>agent-hub</c> 插件（P3：同实例
/// <c>services.AddSingleton&lt;IAgentRuntime&gt;(instance)</c> +
/// <c>ctx.Register&lt;IAgentRuntime&gt;(instance)</c>；Worker 清理由 <c>ctx.Effect</c>）。
/// 消费方：一律 <c>ctx.Get&lt;IAgentRuntime&gt;()</c> <b>每次用每次取</b>，禁止字段缓存。
/// 缺席（未装 / 未启用 agent-hub / OAR 不可用）⇒ <c>Get</c> 返回 null，消费方须降级并可解释。
/// </para>
///
/// <para>
/// 与 <see cref="IAgentDelegation"/> 共存：Delegation = 入队+读回薄接缝；本接口 = Session/任务/输入/控制/事件全集。
/// 与本机 <see cref="IAgent"/> / MAF 正交（ADR-005 / ADR-007）：本接口不驱动本机智能体循环。
/// 禁止在契约或 DTO 中泄漏 OAR / Node Worker 类型。
/// </para>
/// </summary>
public interface IAgentRuntime
{
    /// <summary>列出当前可用的外部 Agent / Runtime 描述（含能力与可用性提示）。</summary>
    Task<IReadOnlyList<AgentDescriptor>> ListAgentsAsync(CancellationToken ct = default);

    /// <summary>
    /// 提交一次 Runtime 任务（可附带既有 <see cref="AgentTaskRequest.SessionKey"/> 续接）。
    /// 失败不得静默成功；具体 Outcome 风格由提供方映射（控制类见 <see cref="AgentControlResult"/>）。
    /// </summary>
    Task<AgentTaskHandle> SubmitAsync(AgentTaskRequest request, CancellationToken ct = default);

    /// <summary>按任务键读快照；不存在返回 null。</summary>
    Task<AgentTaskSnapshot?> GetTaskAsync(string taskKey, CancellationToken ct = default);

    /// <summary>监视任务事件流（<paramref name="afterSequence"/> 之后的铸己匣序号）。</summary>
    IAsyncEnumerable<AgentTaskEvent> WatchTaskAsync(string taskKey, long afterSequence = 0, CancellationToken ct = default);

    /// <summary>向会话投递输入（prompt / deliver 等由提供方按 Runtime 能力选择）。</summary>
    Task<AgentControlResult> SendInputAsync(string sessionKey, AgentInput input, CancellationToken ct = default);

    /// <summary>请求取消任务；结果用 <see cref="AgentControlResult"/>，禁止纯 bool。</summary>
    Task<AgentControlResult> CancelAsync(string taskKey, CancellationToken ct = default);

    /// <summary>关闭会话（释放 Runtime 侧资源；不暗示历史任务改写为成功）。</summary>
    Task CloseSessionAsync(string sessionKey, CancellationToken ct = default);
}

/// <summary>外部 Agent / Runtime 描述（跨插件可见；无 OAR 原始类型）。</summary>
public class AgentDescriptor
{
    /// <summary>铸己匣侧 Agent Id（可与 AccessPoint 关联；0=仅 Runtime 级）。</summary>
    public int AgentId { get; set; }

    /// <summary>显示名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>厂商/profile 键（如 <c>codex</c>）；勿与 RuntimeId 混用。</summary>
    public string Vendor { get; set; } = string.Empty;

    /// <summary>OAR registry id（如 <c>claude</c> / <c>grok</c>）；独立字段。产品面不含测试用 <c>scripted</c>。</summary>
    public string RuntimeId { get; set; } = string.Empty;

    /// <summary>Transport 提示（<c>Cli</c> / <c>Oar</c> 等）。</summary>
    public string Transport { get; set; } = string.Empty;

    /// <summary>
    /// 是否当前可用：仅当本机有可执行安装（OAR <c>installation.kind=available</c> 且 <c>via=executable</c>）
    /// 或已登记 Agent 健康可用时为 true。禁止把未探测 / not_found / bundled 标成可用。
    /// </summary>
    public bool Available { get; set; }

    /// <summary>不可用时的可解释原因（可空）。</summary>
    public string? UnavailableReason { get; set; }

    /// <summary>OAR installation.kind（<c>available</c> / <c>not_found</c> / <c>unsupported</c> 等；可空=未探测）。</summary>
    public string? InstallationKind { get; set; }

    /// <summary>OAR installation.via（如 <c>executable</c> / <c>bundled</c>；可空）。</summary>
    public string? InstallationVia { get; set; }

    /// <summary>能力标签（如 prompt/deliver/abort/resume）；以 Runtime 声明为准。</summary>
    public IReadOnlyList<string> Capabilities { get; set; } = [];
}

/// <summary>Runtime 任务提交请求。</summary>
public class AgentTaskRequest
{
    /// <summary>提示词（必填，非空）。</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>指定铸己匣 Agent Id；null = 由提供方选路。</summary>
    public int? AgentId { get; set; }

    /// <summary>指定 OAR RuntimeId（如 <c>scripted</c>）；与 AgentId 二选一或组合由提供方裁决。</summary>
    public string? RuntimeId { get; set; }

    /// <summary>标签 / facet（选路辅助，可空）。</summary>
    public string? Tag { get; set; }

    /// <summary>续接既有会话；null = 开新会话。</summary>
    public string? SessionKey { get; set; }

    /// <summary>工作目录（绝对路径）；受 AgentHub cwd 白名单约束。</summary>
    public string? Cwd { get; set; }

    /// <summary>权限模式；无限权限类一律拒绝。</summary>
    public string? PermissionMode { get; set; }

    /// <summary>模型提示（可空；以 Runtime 支持为准）。</summary>
    public string? Model { get; set; }

    /// <summary>发起方标识（审计）。</summary>
    public string? CreatedBy { get; set; }
}

/// <summary>任务提交后的句柄。</summary>
public class AgentTaskHandle
{
    /// <summary>任务外部键。</summary>
    public string TaskKey { get; set; } = string.Empty;

    /// <summary>会话外部键（对外只暴露本键，不暴露 OAR session id）。</summary>
    public string SessionKey { get; set; } = string.Empty;

    /// <summary>即时状态（通常 Queued/Running）。</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>实际 RuntimeId。</summary>
    public string? RuntimeId { get; set; }

    /// <summary>实际 Agent Id（0=未知）。</summary>
    public int AgentId { get; set; }
}

/// <summary>任务状态快照。</summary>
public class AgentTaskSnapshot
{
    /// <summary>任务外部键。</summary>
    public string TaskKey { get; set; } = string.Empty;

    /// <summary>会话外部键。</summary>
    public string SessionKey { get; set; } = string.Empty;

    /// <summary>当前状态词表由 AgentHub 持有。</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>是否终态。</summary>
    public bool Terminal { get; set; }

    /// <summary>铸己匣事件序号（最新）。</summary>
    public long AgentHubSequence { get; set; }

    /// <summary>错误码（可空）。</summary>
    public string? ErrorCode { get; set; }

    /// <summary>结果文本摘要（可截断；默认不含 reasoning 正文）。</summary>
    public string? ResultText { get; set; }
}

/// <summary>任务/会话上层事件（供 Watch 与 UI）。</summary>
public class AgentTaskEvent
{
    /// <summary>铸己匣单调序号。</summary>
    public long AgentHubSequence { get; set; }

    /// <summary>任务键（可空：纯会话事件）。</summary>
    public string? TaskKey { get; set; }

    /// <summary>会话键。</summary>
    public string? SessionKey { get; set; }

    /// <summary>事件类型（如 status / message / error / control）。</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>发生时间（UTC）。</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>可选载荷（已脱敏；禁止默认塞 reasoning 正文）。</summary>
    public string? PayloadJson { get; set; }
}

/// <summary>会话输入。</summary>
public class AgentInput
{
    /// <summary>文本内容。</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 投递模式提示：<c>prompt</c> / <c>deliver</c> / <c>now</c> / <c>after_turn</c> / <c>when_idle</c>。
    /// null = 由提供方按 Runtime 能力选择默认。
    /// </summary>
    public string? Mode { get; set; }
}

/// <summary>控制类结果（禁止用纯 bool 掩盖拒绝原因）。</summary>
public class AgentControlResult
{
    /// <summary>是否被 Runtime/策略接受。</summary>
    public bool Accepted { get; set; }

    /// <summary>稳定错误/拒绝码（成功可空或 <c>ok</c>）。</summary>
    public string? Code { get; set; }

    /// <summary>可展示原因原文。</summary>
    public string? Reason { get; set; }

    /// <summary>成功。</summary>
    public static AgentControlResult Ok(string? code = "ok", string? reason = null) =>
        new() { Accepted = true, Code = code, Reason = reason };

    /// <summary>拒绝或失败。</summary>
    public static AgentControlResult Reject(string code, string reason) =>
        new() { Accepted = false, Code = code, Reason = reason };
}

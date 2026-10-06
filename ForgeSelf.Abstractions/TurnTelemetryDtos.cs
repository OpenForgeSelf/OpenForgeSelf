namespace ForgeSelf.Abstractions;

/// <summary>一轮 ChatTurn 的遥测只读投影（成本归因与 trace 的数据单元）。</summary>
/// <remarks>
/// 字段来自 <c>ChatTurn</c> 的真实列（SessionKey / Style / Model / PromptTokens / CompletionTokens / DurationMs / CreatedTime），
/// 并预留 <see cref="AgentRunId"/> 关联键（由 A9/A10 回填，主聊天 legacy 接缝未回填时为 null，不伪造）。
/// 全部为不可变 <c>record</c>，禁止可变 setter，确保下游聚合无副作用。
/// </remarks>
public sealed record TurnTelemetryRecord
{
    /// <summary>轮次 ID（= ChatTurn.Id）。</summary>
    public long Id { get; init; }

    /// <summary>所属会话 ID（= ChatTurn.ChatSessionId）。</summary>
    public long ChatSessionId { get; init; }

    /// <summary>会话内第几轮（= ChatTurn.TurnIndex，从 1 递增）。</summary>
    public int TurnIndex { get; init; }

    /// <summary>统一会话键（= ChatTurn.SessionKey，冗余以便免 JOIN）。</summary>
    public string SessionKey { get; init; } = string.Empty;

    /// <summary>API 风格（= ChatTurn.Style，如 openai / anthropic），模型→供应商解析的源字段之一。</summary>
    public string Style { get; init; } = string.Empty;

    /// <summary>模型名称（= ChatTurn.Model），模型→供应商 4 级解析的源字段。</summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>提示词 token 数（= ChatTurn.PromptTokens）。</summary>
    public int PromptTokens { get; init; }

    /// <summary>补全 token 数（= ChatTurn.CompletionTokens）。</summary>
    public int CompletionTokens { get; init; }

    /// <summary>本轮耗时毫秒（= ChatTurn.DurationMs）。</summary>
    public long DurationMs { get; init; }

    /// <summary>
    /// 首字延迟毫秒（= ChatTurn.FirstTokenMs）；<c>0</c> 表示上游未回填。
    /// A7 补入（2026-10-06）：FR-4.3 要求延迟分位对 <c>FirstTokenMs</c> 与 <c>DurationMs</c> <b>分别出</b>，
    /// 缺此字段则该端点无法实现。
    /// </summary>
    public long FirstTokenMs { get; init; }

    /// <summary>
    /// 响应状态码（= ChatTurn.ResponseStatus）；<c>200</c> 视为成功。
    /// A7 补入（2026-10-06）：FR-4.4 错误率与 BR-4「<c>ResponseStatus != 200</c> 计入失败且不计成本」依赖此字段。
    /// </summary>
    public int ResponseStatus { get; init; }

    /// <summary>
    /// 错误信息（= ChatTurn.ErrorMessage），用于错误分布归类；无错误时为 <see langword="null"/>（不伪造空串）。
    /// A7 补入（2026-10-06）。
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>创建时间（= ChatTurn.CreatedTime）。</summary>
    public DateTime CreatedTime { get; init; }

    /// <summary>关联的一次 Agent 运行 ID；主聊天 legacy 接缝未回填时为 null（不伪造 0/空）。</summary>
    public string? AgentRunId { get; init; }
}

/// <summary>轮次遥测查询过滤（时间窗 + 可选模型/风格）。</summary>
public sealed record TurnTelemetryQueryFilter
{
    /// <summary>起始时间（含），null 表示不限。</summary>
    public DateTime? From { get; init; }

    /// <summary>结束时间（含），null 表示不限。</summary>
    public DateTime? To { get; init; }

    /// <summary>模型名称精确过滤，null 表示不限。</summary>
    public string? Model { get; init; }

    /// <summary>API 风格过滤，null 表示不限。</summary>
    public string? Style { get; init; }
}

/// <summary>一次 Agent 运行的 trace 只读投影（AgentRun + 其步骤）。</summary>
/// <remarks>自身包含 AgentRun 的稳定键与汇总量；步骤明细见 <see cref="Steps"/>。</remarks>
public sealed record AgentRunTelemetry
{
    /// <summary>Agent 运行 ID（= AgentRun 主键 / 关联键）。</summary>
    public string AgentRunId { get; init; } = string.Empty;

    /// <summary>统一会话键（= AgentRun.SessionKey）。</summary>
    public string SessionKey { get; init; } = string.Empty;

    /// <summary>步骤总数（= AgentRun.StepCount）。</summary>
    public int StepCount { get; init; }

    /// <summary>
    /// 本次运行的开始时间（= AgentRun.CreateTime）。
    /// A10 补入（2026-10-06）：<b>插件侧时间窗关联</b>的时间锚点之一。
    /// </summary>
    public DateTime? StartedTime { get; init; }

    /// <summary>
    /// 本次运行的结束时间（= AgentRun.UpdateTime）；仍在运行或未回填时为 <see langword="null"/>。
    /// A10 补入（2026-10-06）。
    /// </summary>
    public DateTime? EndedTime { get; init; }

    /// <summary>该运行消耗的总 token 数（= AgentRun.TotalTokens）。</summary>
    public long TotalTokens { get; init; }

    /// <summary>有序步骤明细（按 StepIndex 升序）。</summary>
    public IReadOnlyList<AgentStepTelemetry> Steps { get; init; } = Array.Empty<AgentStepTelemetry>();
}

/// <summary>单个 Agent 步骤的 trace 只读投影。</summary>
public sealed record AgentStepTelemetry
{
    /// <summary>步骤序号（从 0/1 递增，由实现方定义）。</summary>
    public int StepIndex { get; init; }

    /// <summary>步骤类型（如 think / tool / llm），由实现方约定。</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>该步骤耗时毫秒。</summary>
    public long DurationMs { get; init; }

    /// <summary>该步骤消耗的 token 数。</summary>
    public int Tokens { get; init; }

    /// <summary>步骤名（= AgentStepRun.Name），用于瀑布节点标题。A10 补入（2026-10-06）。</summary>
    public string? Name { get; init; }

    /// <summary>
    /// 步骤开始时间（= AgentStepRun.StartedAt）；缺失时 <see cref="DurationMs"/> 仍可用（相对排序兜底）。
    /// A10 补入（2026-10-06）。
    /// </summary>
    public DateTime? StartedAt { get; init; }
}

/// <summary>
/// 模型身份只读投影（模型→供应商 4 级解析的目录单元，FR-3.8 / BC-3 / BC-5）。
/// </summary>
/// <remarks>
/// 三个可匹配字段即回退优先级来源（032 §3.2）：
/// <c>ChatModelId</c> &gt; <c>UpstreamModelId</c>/<c>Alias</c> &gt; 大小写不敏感 &gt; 未归属。
/// 全字段默认为空串（非空引用），便于解析侧直接做相等比较，无需空判。
/// 2026-10-06 原子任务 **A4** 补入（A1 产物原缺此 DTO）。
/// </remarks>
public sealed record ModelIdentityDto
{
    /// <summary>对外暴露的模型 ID（= AIModel.ChatModelId），级别 1 匹配字段。</summary>
    public string ChatModelId { get; init; } = string.Empty;

    /// <summary>上游真实模型 ID（= AIModel.UpstreamModelId），级别 2 匹配字段。</summary>
    public string UpstreamModelId { get; init; } = string.Empty;

    /// <summary>模型别名（= AIModel.Alias），级别 2 匹配字段。</summary>
    public string Alias { get; init; } = string.Empty;

    /// <summary>供应商名（= AIProvider.Name），解析命中后回带给聚合侧。</summary>
    public string ProviderName { get; init; } = string.Empty;
}

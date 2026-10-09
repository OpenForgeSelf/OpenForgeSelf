namespace ForgeSelf.Abstractions;

/// <summary>
/// 「把一条任务交给本工具内置 AI Agent 执行」的能力接缝（L1 契约，与 <see cref="IAgentDelegation"/> 对称）。
///
/// <para>
/// 提供方：<c>ai-agent</c> 插件（在 <c>Apply(IContext)</c> 里 eager 构造并
/// <c>ctx.Register&lt;IBuiltInAgentExecution&gt;(provider)</c>；提供即 effect，插件卸载自动摘除）。
/// 消费方：任何需要「把任务交给内置角色 agent（协调者/分析师/评论家/通用助手/写作者/研究员/程序员）」的插件
/// （首个消费方 = <c>todo-tracker</c>），一律 <c>ctx.Get&lt;IBuiltInAgentExecution&gt;()</c> <b>每次用每次取</b>，
/// 禁止把实例缓存为字段（提供方热重载后共享表自动摘除，缓存会造成悬空引用）。
/// </para>
///
/// <para>
/// 为什么走契约而不是 HTTP：插件间不得用直连 HTTP / 共享文件 / 静态类传递数据
/// （architecture-design 铁律 2/3；接缝登记见 docs/01-architecture/host-capability-seams.md）。
/// 本接缝包住 AIAgent 插件既有「计划驱动执行」（<c>POST /api/ai-agent/runs</c> 的后台形态）：
/// 触发一次 Run 并读回快照；不新增任何执行语义。状态机、模型通道、工具集仍由 AIAgent 单方持有（单一真相）。
/// </para>
///
/// <para>消费方在接缝缺席（未装 / 未启用 ai-agent）时必须<b>降级并可解释</b>：
/// <c>Get</c> 返回 null ⇒ 对外映射 503 + 明确文案，禁止静默当成功。</para>
/// </summary>
public interface IBuiltInAgentExecution
{
    /// <summary>
    /// 触发一次计划驱动执行（后台跑完，立即返回 runId 与初始状态）。
    /// 失败（prompt 空、角色不存在/停用、模型通道不可用等）一律返回
    /// <see cref="BuiltInAgentOutcome.Success"/>=false + <see cref="BuiltInAgentOutcome.Error"/> 原文，
    /// <b>不抛业务异常</b>，以便消费方把原因原样转给用户。
    /// </summary>
    Task<BuiltInAgentOutcome> StartAsync(BuiltInAgentRequest request, CancellationToken ct = default);

    /// <summary>按 <c>runId</c> 读回 Run 的当前快照。不存在返回 null（消费方映射 404）。</summary>
    Task<BuiltInAgentSnapshot?> FindAsync(long runId, CancellationToken ct = default);

    /// <summary>
    /// 当前可用的角色 agent 候选（仅 <c>enabled</c> 的那些），供消费方在下发前自检/提示。
    /// 无候选返回空列表；接缝不可用时返回空列表而非抛异常。
    /// </summary>
    IReadOnlyList<BuiltInAgentOption> ListAgents();
}

/// <summary>内置执行请求（消费方组装，提供方不解释 Prompt 内容）。</summary>
public class BuiltInAgentRequest
{
    /// <summary>任务原文（必填，非空）。</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>角色 agent id（如 <c>agent.programmer</c>）；null/空 = 由提供方按任务 best-match 选路。</summary>
    public string? AgentRoleId { get; set; }

    /// <summary>聊天模型 id（如 <c>default:ornith-1.0-9b</c>）；null = 由提供方取内部默认。</summary>
    public string? ChatModelId { get; set; }

    /// <summary>工作目录（项目根，归一化绝对路径）；非空时执行前设为当前工作区，
    /// 内置引擎的写文件/读文件/终端等工具都在此目录下运行（todo 委派 = 任务的 ProjectRoot）。</summary>
    public string? Cwd { get; set; }

    /// <summary>发起方标识（如 <c>ui</c> / <c>todo-tracker</c>），用于审计与统计归因。</summary>
    public string? CreatedBy { get; set; }
}

/// <summary>内置执行触发结果。</summary>
public class BuiltInAgentOutcome
{
    /// <summary>是否成功触发（Run 已创建并进入后台执行）。</summary>
    public bool Success { get; set; }

    /// <summary>失败原因（中文原文，可直接展示；成功时为 null）。</summary>
    public string? Error { get; set; }

    /// <summary>Run id（成功后有值，后续状态回读用它）。</summary>
    public long RunId { get; set; }

    /// <summary>实际选中的角色名（展示用，可空）。</summary>
    public string? AgentName { get; set; }

    /// <summary>触发后的即时状态（通常为 <c>Pending</c> / <c>Planning</c>）。</summary>
    public string? Status { get; set; }

    /// <summary>成功。</summary>
    public static BuiltInAgentOutcome Ok(long runId, string? agentName, string status) =>
        new() { Success = true, RunId = runId, AgentName = agentName, Status = status };

    /// <summary>失败（原因原文）。</summary>
    public static BuiltInAgentOutcome Fail(string error) =>
        new() { Success = false, Error = error };
}

/// <summary>内置执行快照（只读投影）。状态词表由 AIAgent 单方持有：
/// <c>Pending|Planning|Running|Completed|Stuck|Failed|Cancelled</c>（AgentRunStatus 枚举，camelCase）。
/// 终态 = Completed / Failed / Cancelled；Stuck 非终态（可 resume / 人工介入）。</summary>
public class BuiltInAgentSnapshot
{
    /// <summary>Run id。</summary>
    public long RunId { get; set; }

    /// <summary>当前状态（同上词表）。</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>是否终态（Completed/Failed/Cancelled）。消费方据此决定是否继续轮询。</summary>
    public bool Terminal { get; set; }

    /// <summary>执行耗时（毫秒）。</summary>
    public long ElapsedMs { get; set; }

    /// <summary>结果摘要（complete_step 声明产出 / 卡住原因 / 失败原因）。</summary>
    public string? ResultSummary { get; set; }

    /// <summary>本次执行改动的文件路径列表（由提供方从步骤工具轨迹解析，消费方不必了解内部格式）。</summary>
    public List<string> FilesChanged { get; set; } = [];

    /// <summary>错误/卡住说明（可空）。</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>实际使用的角色名（展示用，可空）。</summary>
    public string? AgentName { get; set; }
}

/// <summary>可用角色 agent 候选（下发前自检用）。</summary>
public class BuiltInAgentOption
{
    /// <summary>角色 id（如 <c>agent.programmer</c>），提交请求时回传。</summary>
    public string RoleId { get; set; } = string.Empty;

    /// <summary>显示名（如 <c>程序员</c>）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>固定为 <c>builtin</c>（区分外部厂商）。</summary>
    public string Vendor { get; set; } = "builtin";
}

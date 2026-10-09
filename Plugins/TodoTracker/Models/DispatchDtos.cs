namespace ForgeSelf.Api.Plugins.TodoTracker.Models;

/// <summary>
/// 下发载荷（PILOT-054 · FR-1 / FR-6）。
/// 界面「下发预览」与工具函数读的都是这一份，避免"预览是一套、真发出去是另一套"。
/// </summary>
public class DispatchPreviewDto
{
    /// <summary>任务外部键（界面复制提示词时一并给出，agent 回报要用它）。</summary>
    public string TaskKey { get; set; } = string.Empty;

    /// <summary>可直接粘贴给任意 agent 的提示词。</summary>
    public string PromptMarkdown { get; set; } = string.Empty;

    /// <summary>交给委派接口的 JSON 载荷（prompt/cwd/permissionMode/createdBy）。</summary>
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>必填缺口（objective/content/acceptance/verification）。</summary>
    public List<string> Missing { get; set; } = [];

    /// <summary>软提醒（不拦下发，但要让用户看见）。</summary>
    public List<string> Warnings { get; set; } = [];

    /// <summary>能否下发（Missing 为空）。</summary>
    public bool CanDispatch { get; set; }

    /// <summary>能否一键交给 AgentHub：能下发 + 委派接缝在场。</summary>
    public bool CanDelegate { get; set; }

    /// <summary>委派接缝是否在场（false=未装/未启用 agent-hub，界面应置灰并说明原因）。</summary>
    public bool DelegationAvailable { get; set; }

    /// <summary>接缝缺席时的说明（可空）。</summary>
    public string? DelegationError { get; set; }

    /// <summary>本工具 AI Agent 接缝是否在场（false=未装/未启用 ai-agent，界面应置灰并说明原因）。</summary>
    public bool BuiltInAvailable { get; set; }

    /// <summary>接缝缺席时的说明（可空）。</summary>
    public string? BuiltInError { get; set; }

    /// <summary>当前可用 agent 候选（供界面下拉；接缝缺席时为空）。</summary>
    public List<AgentOptionDto> Agents { get; set; } = [];

    /// <summary>本工具内置角色候选（供界面下拉；接缝缺席时为空）。</summary>
    public List<AgentOptionDto> BuiltInAgents { get; set; } = [];
}

/// <summary>agent 候选项。</summary>
public class AgentOptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Vendor { get; set; } = string.Empty;
    public string? DefaultCwd { get; set; }
}

/// <summary>一键交给 agent 的请求。</summary>
public class DelegateToAgentRequest
{
    /// <summary>委派引擎：agenthub=外部 AgentHub（默认）/ builtin=本工具 AI Agent。</summary>
    public string? Engine { get; set; }

    /// <summary>指定 agent；null/0=由 AgentHub 自动选路（仅 agenthub 引擎）。</summary>
    public int? AgentId { get; set; }

    /// <summary>内置角色 id（如 agent.programmer；仅 builtin 引擎）。</summary>
    public string? AgentRoleId { get; set; }

    /// <summary>权限模式 read-only|workspace-write|accept-edits；空=任务上的默认值。</summary>
    public string? PermissionMode { get; set; }

    /// <summary>成功后是否顺带把提示词正文写进任务 Content（默认 false，正文保持来源工件原样）。</summary>
    public bool PersistPromptAsContent { get; set; }
}

/// <summary>一键执行结果。</summary>
public class DelegateToAgentResultDto
{
    public bool Ok { get; set; }

    /// <summary>失败原因原文（AgentHub 的裁决，如 cwd 不在白名单 / agent 已停用，一律原样透传）。</summary>
    public string? Error { get; set; }

    public string TaskKey { get; set; } = string.Empty;
    public int AgentId { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Cwd { get; set; }

    /// <summary>台账回填是否成功（false=已入队但没回填上，必须让用户看见，不许吞）。</summary>
    public bool Backfilled { get; set; }

    /// <summary>
    /// 失败是否因委派能力缺席（未装/未启用 agent-hub）。
    /// 控制器据此选 503/400 —— <b>不许改成嗅探原因字符串</b>：措辞一改映射就静默失效，
    /// 而"没装插件"和"参数不对"是用户两种完全不同的下一步。
    /// </summary>
    public bool SeamMissing { get; set; }

    /// <summary>回填失败时的告警文本。</summary>
    public string? BackfillWarning { get; set; }

    /// <summary>本插件任务 id。</summary>
    public int TodoId { get; set; }

    /// <summary>写入的执行记录 id（下发/委派留痕）。</summary>
    public int? RecordId { get; set; }

    /// <summary>本次为进入执行态自动补的跳转（如 Ready → Dispatched），让人看清台账为什么多了一条下发痕。</summary>
    public List<string> Steps { get; set; } = [];
}

/// <summary>委派任务状态回读。</summary>
public class AgentStatusDto
{
    public bool Ok { get; set; }
    public string? Error { get; set; }

    /// <summary>本插件任务 id。</summary>
    public int TodoId { get; set; }

    /// <summary>委派任务键。</summary>
    public string TaskKey { get; set; } = string.Empty;

    /// <summary>执行 agent 名（由 todo.AgentId 从网关可用 agent 清单解析；未知/已失效为 null，界面兜底 agent#id）。</summary>
    public string? AgentName { get; set; }

    /// <summary>Queued|Running|AwaitingPermission|Succeeded|Failed|Cancelled|Timeout|Interrupted。</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>是否终态。</summary>
    public bool Terminal { get; set; }

    public int? ExitCode { get; set; }
    public string? ErrorCode { get; set; }
    public long ElapsedMs { get; set; }
    public string? ResultSummary { get; set; }
    public List<ChangedFileDto> FilesChanged { get; set; } = [];
    public string? Cwd { get; set; }

    /// <summary>回读到的验证侧信息（委派状态 / 退出码 / 耗时 / 产物数），回写成记录时进 verification 栏。</summary>
    public string Verification { get; set; } = string.Empty;

    /// <summary>委派任务已不存在（被清理/换库）。</summary>
    public bool NotFound { get; set; }

    /// <summary>失败（HTTP 码由控制器直接取 <see cref="StatusCode"/>）。</summary>
    public static AgentStatusDto Failed(string error, int statusCode) => new()
    {
        Ok = false,
        Error = error,
        StatusCode = statusCode,
        NotFound = statusCode == 404
    };

    /// <summary>失败时的 HTTP 码（400 未委派 / 404 任务不存在 / 503 接缝缺席）。</summary>
    public int StatusCode { get; set; } = 200;
}

/// <summary>把 agent 结果落成执行记录的结果。</summary>
public class RecordAgentResultDto
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public int? RecordId { get; set; }
    public int Seq { get; set; }
    public string Stage { get; set; } = string.Empty;
}

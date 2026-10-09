namespace ForgeSelf.Api.Plugins.TodoTracker.Models;

/// <summary>
/// 待办/任务出参。
/// <para>
/// 兼容口径（PILOT-054）：既有 8 个键（id/title/remark/status/dueDate/createdAt/updatedAt/completedAt）
/// 名称与语义<b>不变</b> —— <c>Plugins/Home/web/src/homeStore.ts</c> 与既有 e2e 在直接消费它们；
/// 新增键只增不减，老待办读出来即"下发字段全空 + stage=Draft"。
/// </para>
/// </summary>
public class TodoDto
{
    public int Id { get; set; }

    /// <summary>任务外部键 GUID(N)，agent 侧引用任务用。</summary>
    public string TaskKey { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string? Remark { get; set; }

    /// <summary>旧二元状态 Pending/Completed，由 Stage 派生（Home 面板与旧 e2e 在用）。</summary>
    public string Status { get; set; } = "Pending";

    /// <summary>下发阶段 Draft/Ready/Dispatched/Running/Blocked/Review/Done/Cancelled。</summary>
    public string Stage { get; set; } = "Draft";

    /// <summary>下发阶段中文标签（界面直接展示，避免前端再抄一份映射）。</summary>
    public string StageLabel { get; set; } = "草稿";

    /// <summary>1=P1 2=P2 3=P3。</summary>
    public int Priority { get; set; } = 1;

    /// <summary>下发对象（agent 名 / manual）。</summary>
    public string Assignee { get; set; } = string.Empty;

    /// <summary>可验证目标（一句话）。</summary>
    public string Objective { get; set; } = string.Empty;

    /// <summary>任务正文（markdown，可由工件组装）。</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>允许改动范围（每行一条）。</summary>
    public string AllowedScope { get; set; } = string.Empty;

    /// <summary>禁止改动范围（每行一条）。</summary>
    public string ForbiddenScope { get; set; } = string.Empty;

    /// <summary>验收判据（每行一条）。</summary>
    public string Acceptance { get; set; } = string.Empty;

    /// <summary>验证命令（每行一条）。</summary>
    public string Verification { get; set; } = string.Empty;

    /// <summary>关联宿主项目 Id，0=未关联。</summary>
    public int ProjectId { get; set; }

    /// <summary>归一后的项目根。</summary>
    public string ProjectRoot { get; set; } = string.Empty;

    /// <summary>用户输入的原始路径写法（如 /d/project）。</summary>
    public string ProjectPathRaw { get; set; } = string.Empty;

    /// <summary>项目名（读时经宿主注册表补齐；项目档案已删除时为空）。</summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>来源工件目录（相对项目根）。</summary>
    public string ArtifactRef { get; set; } = string.Empty;

    public DateTime? DueDate { get; set; }

    /// <summary>下发时间，null=未下发。</summary>
    public DateTime? DispatchedAt { get; set; }

    /// <summary>委派任务 key（AgentHub taskKey 或 本工具 AI Agent run:<id>，一键执行后回填）。</summary>
    public string AgentTaskKey { get; set; } = string.Empty;

    /// <summary>委派 agent Id（AgentHub Id 或 内置角色序号 1..7），0=自动选路/未知。</summary>
    public int AgentId { get; set; }

    /// <summary>委派引擎：agenthub=外部 AgentHub / builtin=本工具 AI Agent。</summary>
    public string AgentEngine { get; set; } = "agenthub";

    /// <summary>委派权限模式。</summary>
    public string PermissionMode { get; set; } = "read-only";

    /// <summary>执行记录条数。</summary>
    public int RecordCount { get; set; }

    /// <summary>
    /// 当前阶段可流转到的下一阶段（英文名）。
    /// 由服务端从 <c>TodoStage</c> 单点算出并随出参交出 —— 界面自己抄一份流转表就是第二份真相，
    /// 状态机一改就会出现"按钮能点、后端 409"的分裂。
    /// </summary>
    public List<string> AllowedTargets { get; set; } = [];

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>下发必填缺口（objective/content/acceptance/verification 中哪些还空着）。</summary>
    public List<string> Missing { get; set; } = [];
}

/// <summary>创建请求。既有三字段保持原形状（Home 插件只传这三个）。</summary>
public class CreateTodoRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public DateTime? DueDate { get; set; }

    // === PILOT-054 下发字段（全部可选）===
    public string? Objective { get; set; }
    public string? Content { get; set; }
    public string? AllowedScope { get; set; }
    public string? ForbiddenScope { get; set; }
    public string? Acceptance { get; set; }
    public string? Verification { get; set; }
    public int? Priority { get; set; }
    public string? Assignee { get; set; }

    /// <summary>直接给阶段（名称或数字）。不给即 Draft。</summary>
    public string? Stage { get; set; }

    /// <summary>项目根（任何写法，服务端归一后关联/登记）。</summary>
    public string? ProjectPath { get; set; }

    public int? ProjectId { get; set; }
}

/// <summary>
/// 更新请求。<b>部分更新</b>：null=不改；空串=显式清空（内容类字段允许清空回草稿）。
/// 状态流转不走这里，必须用 <c>/{id}/stage</c>（否则"改标题"顺手把任务状态挪了）。
/// </summary>
public class UpdateTodoRequest
{
    public string? Title { get; set; }
    public string? Remark { get; set; }
    public DateTime? DueDate { get; set; }

    public string? Objective { get; set; }
    public string? Content { get; set; }
    public string? AllowedScope { get; set; }
    public string? ForbiddenScope { get; set; }
    public string? Acceptance { get; set; }
    public string? Verification { get; set; }
    public int? Priority { get; set; }
    public string? Assignee { get; set; }
}

/// <summary>分页壳（沿用既有形状，Total 必须由 RetrieveTotalCount 真取）。</summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

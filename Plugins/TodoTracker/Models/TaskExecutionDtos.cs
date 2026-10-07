namespace ForgeSelf.Api.Plugins.TodoTracker.Models;

/// <summary>
/// 执行记录（PILOT-054 · FR-4）。一条任务 1:N，<b>append-only</b>：只增不改不删，
/// 任务历史因此可回放（"谁、在哪、按哪份工件、做了什么、结果如何"）。
/// </summary>
public class TaskExecutionDto
{
    public int Id { get; set; }
    public int TodoId { get; set; }

    /// <summary>任务内序号，从 1 递增。</summary>
    public int Seq { get; set; }

    /// <summary>执行者（agent 名 / manual / agent-hub）。</summary>
    public string Actor { get; set; } = string.Empty;

    /// <summary>做了什么操作（一句话，必填）。</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>操作明细。</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>什么结果。</summary>
    public string Result { get; set; } = string.Empty;

    /// <summary>改了哪些文件（结构化，界面按路径列表展示）。</summary>
    public List<ChangedFileDto> FilesChanged { get; set; } = [];

    /// <summary>改了哪些文件的原始文本（结构化解析失败时的兜底，不丢信息）。</summary>
    public string FilesChangedRaw { get; set; } = string.Empty;

    /// <summary>跑了什么验证 + 结果。</summary>
    public string Verification { get; set; } = string.Empty;

    public string Risks { get; set; } = string.Empty;
    public string Residuals { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;

    /// <summary>流转前阶段（空串=未流转）。</summary>
    public string StageFrom { get; set; } = string.Empty;

    /// <summary>流转后阶段（空串=未流转）。</summary>
    public string StageTo { get; set; } = string.Empty;

    public long ElapsedMs { get; set; }
    public string BlockReason { get; set; } = string.Empty;

    /// <summary>下一步（回流入口，对齐 AGENTS §7.5.2 日记「下一步」字段）。</summary>
    public string NextStep { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

/// <summary>单个文件变更。</summary>
public class ChangedFileDto
{
    /// <summary>路径（绝对或相对项目根，由回报方决定，服务端不改写）。</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>变更类型 A/M/D/R 等（原样保留）。</summary>
    public string Change { get; set; } = string.Empty;
}

/// <summary>
/// 追加执行记录的请求。三面（REST / AI 工具函数 / 界面）走<b>同一个形状</b>，
/// 保证"从哪写进来的记录长一样"。
/// </summary>
public class CreateTaskExecutionRequest
{
    /// <summary>执行者；空=按调用面默认（REST 默认 manual，工具默认 agent:&lt;name&gt;）。</summary>
    public string? Actor { get; set; }

    /// <summary>做了什么操作（必填，≤300）。</summary>
    public string? Action { get; set; }

    public string? Detail { get; set; }
    public string? Result { get; set; }

    /// <summary>改了哪些文件：既接受结构化数组，也接受一行一个路径的纯文本（服务端归一）。</summary>
    public List<ChangedFileDto>? FilesChanged { get; set; }
    public string? FilesChangedText { get; set; }

    public string? Verification { get; set; }
    public string? Risks { get; set; }
    public string? Residuals { get; set; }
    public string? Evidence { get; set; }

    /// <summary>同一次调用里把状态一并流转（名称或数字）；不传=不改状态。</summary>
    public string? StageTo { get; set; }

    public long? ElapsedMs { get; set; }
    public string? BlockReason { get; set; }
    public string? NextStep { get; set; }
}

/// <summary>改状态请求。</summary>
public class StageChangeRequest
{
    /// <summary>目标阶段（名称或数字）。</summary>
    public string? Stage { get; set; }

    /// <summary>变更说明；写成一条执行记录的 Action 文本（留痕）。</summary>
    public string? Reason { get; set; }

    /// <summary>阻塞原因（流转到 Blocked 时必填）。</summary>
    public string? BlockReason { get; set; }
}

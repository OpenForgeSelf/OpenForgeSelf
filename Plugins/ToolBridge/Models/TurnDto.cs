using ForgeSelf.Api.Plugins.ToolBridge.Services;

namespace ForgeSelf.Api.Plugins.ToolBridge.Models;

/// <summary>POST parse / POST turn 的请求体（02-spec Input）。</summary>
public sealed class TextRequest
{
    public string? Text { get; set; }
    public string? Mode { get; set; }
}

/// <summary>POST execute 的请求体：直接执行已解析出的调用（AC 界面「只执行选中条」用）。</summary>
public sealed class ExecuteRequest
{
    public List<ParsedCall>? Calls { get; set; }
}

/// <summary>PUT workspace 的请求体。</summary>
public sealed class WorkspaceRequest
{
    public string? Root { get; set; }

    /// <summary>危险工作根（盘符根 / 用户目录根 / git 仓库树内）需显式确认（BC-11）。</summary>
    public bool ConfirmUnsafe { get; set; }
}

/// <summary>一轮回合的完整记录（FR-6.1）。台账落盘的就是这个形状。</summary>
public sealed class TurnRecord
{
    public string TurnId { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string SpecVersion { get; set; } = ToolSpec.SpecVersion;
    public string WorkspaceRoot { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public List<ParsedCall> Calls { get; set; } = new();
    public List<UnknownCall> Unknown { get; set; } = new();
    public List<UnparsedFragment> Unparsed { get; set; } = new();
    public List<ToolResult> Results { get; set; } = new();
    public string ResultTextJson { get; set; } = string.Empty;
    public string ResultTextPlain { get; set; } = string.Empty;
    public TurnStats Stats { get; set; } = new();
}

public sealed class TurnStats
{
    public int Recognized { get; set; }
    public int Unknown { get; set; }
    public int Unparsed { get; set; }
    public int Executed { get; set; }
    public int Rejected { get; set; }
    public long DurationMs { get; set; }
}

/// <summary>列表项（不含粘贴原文与结果全文，AC11）。</summary>
public sealed class TurnSummary
{
    public string TurnId { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public TurnStats Stats { get; set; } = new();
    public string Preview { get; set; } = string.Empty;
    public bool Corrupt { get; set; }
}

public sealed class TurnList
{
    public List<TurnSummary> Items { get; set; } = new();
    public int Total { get; set; }
    public string LedgerDirectory { get; set; } = string.Empty;
    public string Note { get; set; } = "记录只追加、不自动删除（插件数据目录内，见 README 已知限制）";
}

/// <summary>POST turn 的响应。</summary>
public sealed class TurnResponse
{
    public string TurnId { get; set; } = string.Empty;
    public List<ParsedCall> Calls { get; set; } = new();
    public List<UnknownCall> Unknown { get; set; } = new();
    public List<UnparsedFragment> Unparsed { get; set; } = new();
    public List<ToolResult> Results { get; set; } = new();
    public string ResultTextJson { get; set; } = string.Empty;
    public string ResultTextPlain { get; set; } = string.Empty;
    public TurnStats Stats { get; set; } = new();

    /// <summary>台账没记上时的原因（图 2 缺口④：结果已得但落盘失败必须可见）。</summary>
    public string? LedgerError { get; set; }
}

/// <summary>POST execute 的响应：只执行不重新解析、不落台账，但同样给出可复制的回粘文本。</summary>
public sealed class ExecuteResponse
{
    public List<ToolResult> Results { get; set; } = new();
    public string ResultTextJson { get; set; } = string.Empty;
    public string ResultTextPlain { get; set; } = string.Empty;
    public int Executed { get; set; }
    public int Rejected { get; set; }
    public long DurationMs { get; set; }
}

/// <summary>GET workspace 的 data。</summary>
public sealed class WorkspaceResponse
{
    public string Root { get; set; } = string.Empty;
    public bool Exists { get; set; }
    public string DefaultRoot { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public bool Dangerous { get; set; }
}

/// <summary>GET prompt 的 data。</summary>
public sealed class PromptResponse
{
    public string Text { get; set; } = string.Empty;
    public string SpecVersion { get; set; } = ToolSpec.SpecVersion;
    public JsonArray Tools { get; set; } = new();
}

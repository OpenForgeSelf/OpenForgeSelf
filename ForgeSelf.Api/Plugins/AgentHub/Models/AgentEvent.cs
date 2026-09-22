using System.Text.Json.Nodes;

namespace ForgeSelf.Api.Plugins.AgentHub.Models;

/// <summary>
/// 归一化事件类型（各 agent 的私有输出格式统一映射到此词表）。
///
/// 与 DelegationEvent.Type 落库字段一一对应——**两端必须同步**：
/// 新增类型时先加这里，再检查前端渲染分支与 e2e 断言。
/// </summary>
public static class AgentEventTypes
{
    /// <summary>模型/agent 的普通文本输出</summary>
    public const String Text = "Text";

    /// <summary>思考过程（chain-of-thought / reasoning）</summary>
    public const String Thought = "Thought";

    /// <summary>工具调用（含工具名与参数）</summary>
    public const String ToolCall = "ToolCall";

    /// <summary>工具调用结果</summary>
    public const String ToolResult = "ToolResult";

    /// <summary>文件变更（新增/修改/删除）</summary>
    public const String FileChange = "FileChange";

    /// <summary>代码差异</summary>
    public const String Diff = "Diff";

    /// <summary>权限申请（人在回路审批）</summary>
    public const String PermissionRequest = "PermissionRequest";

    /// <summary>错误</summary>
    public const String Error = "Error";

    /// <summary>进程退出（含退出码）</summary>
    public const String Exit = "Exit";

    /// <summary>元信息（会话 id、模型、usage 等）</summary>
    public const String Meta = "Meta";

    /// <summary>全量词表</summary>
    public static readonly String[] All =
    [
        Text, Thought, ToolCall, ToolResult, FileChange, Diff, PermissionRequest, Error, Exit, Meta
    ];
}

/// <summary>归一化事件（transports 输出、EventPipeline 产出、UI/SSE 消费的统一结构）。</summary>
public class AgentEvent
{
    /// <summary>事件类型（取值见 <see cref="AgentEventTypes"/>）</summary>
    public String Type { get; set; } = AgentEventTypes.Text;

    /// <summary>文本载荷（Text / Thought / Error 用）</summary>
    public String? Text { get; set; }

    /// <summary>工具名（ToolCall / ToolResult / FileChange 用）</summary>
    public String? Tool { get; set; }

    /// <summary>结构化载荷（原样保留上游片段，便于复盘）</summary>
    public JsonNode? Payload { get; set; }

    /// <summary>退出码（Exit 事件用）</summary>
    public Int32? ExitCode { get; set; }

    /// <summary>发生时间</summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>是否为截断过的大内容（超过单事件上限时置位，前端显式提示）</summary>
    public Boolean Truncated { get; set; }

    /// <summary>构造文本事件</summary>
    /// <param name="text">文本</param>
    /// <param name="type">事件类型</param>
    /// <returns>事件</returns>
    public static AgentEvent FromText(String text, String type = AgentEventTypes.Text) => new()
    {
        Type = type,
        Text = text
    };

    /// <summary>构造错误事件</summary>
    /// <param name="message">错误信息</param>
    /// <returns>事件</returns>
    public static AgentEvent FromError(String message) => new()
    {
        Type = AgentEventTypes.Error,
        Text = message
    };
}

/// <summary>一次委派运行请求（transport 入参）。</summary>
public class AgentRunRequest
{
    /// <summary>提示词</summary>
    public String Prompt { get; set; } = String.Empty;

    /// <summary>工作目录</summary>
    public String? Cwd { get; set; }

    /// <summary>权限模式（read-only / workspace-write / ...）</summary>
    public String PermissionMode { get; set; } = "read-only";

    /// <summary>要续接的外部会话 id（为空表示新会话）</summary>
    public String? SessionRef { get; set; }

    /// <summary>执行超时（毫秒）</summary>
    public Int32 TimeoutMs { get; set; } = 600_000;
}

/// <summary>一次委派运行的会话句柄（供取消与续接）。</summary>
public class AgentSession
{
    /// <summary>任务主键</summary>
    public Int32 TaskId { get; set; }

    /// <summary>交互口主键</summary>
    public Int32 AccessPointId { get; set; }

    /// <summary>transport 类型标识</summary>
    public String Kind { get; set; } = "Cli";

    /// <summary>外部会话 id（从输出中提取，用于续接）</summary>
    public String? SessionRef { get; set; }

    /// <summary>是否支持会话内取消（Sessionful 才有）</summary>
    public Boolean CancelSupported { get; set; }

    /// <summary>进程号（Cli 用）</summary>
    public Int32 ProcessId { get; set; }
}

namespace OpenForgeSelf.Abstractions;

/// <summary>
/// 脚本执行记录（纯 DTO，随 IScriptExecutor/IScriptService 契约迁入 Abstractions）。
/// 注意与 ScriptRunner 插件数据库实体（OpenForgeSelf.Backend.Plugins.ScriptRunner.Entities.ScriptExecution）区分。
/// </summary>
public class ScriptExecution
{
    public long Id { get; set; }
    public long ScriptId { get; set; }
    public string ScriptName { get; set; } = string.Empty;
    public ScriptExecutionStatus Status { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int? ExitCode { get; set; }
    public string Output { get; set; } = string.Empty;
    public string ErrorOutput { get; set; } = string.Empty;
    public long DurationMs { get; set; }
    public string ParametersJson { get; set; } = string.Empty;
    public List<ScriptExecutionLog> OutputLogs { get; set; } = [];
}

/// <summary>
/// 脚本执行输出日志条目（纯 DTO）。
/// </summary>
public class ScriptExecutionLog
{
    public DateTime Timestamp { get; set; }
    public string StreamType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 脚本执行状态（纯枚举）。
/// </summary>
public enum ScriptExecutionStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
    Timeout
}

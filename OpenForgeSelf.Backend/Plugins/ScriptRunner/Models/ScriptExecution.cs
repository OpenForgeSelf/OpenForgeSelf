namespace OpenForgeSelf.Backend.Plugins.ScriptRunner.Models;

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

public class ScriptExecutionLog
{
    public DateTime Timestamp { get; set; }
    public string StreamType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class RuntimeEnvironment
{
    public ScriptLanguage Language { get; set; }
    public bool IsAvailable { get; set; }
    public string InterpreterPath { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
}

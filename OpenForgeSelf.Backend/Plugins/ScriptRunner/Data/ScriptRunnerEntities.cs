using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenForgeSelf.Backend.Plugins.ScriptRunner.Data;

public class ScriptEntity
{
    [Key]
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Language { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string TagsJson { get; set; } = "[]";
    public bool IsFavorite { get; set; }
    public string ParametersJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int UsageCount { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public int TimeoutSeconds { get; set; } = 300;
}

public class ScriptExecutionEntity
{
    [Key]
    public long Id { get; set; }
    public long ScriptId { get; set; }
    public string ScriptName { get; set; } = string.Empty;
    public int Status { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int? ExitCode { get; set; }
    public string Output { get; set; } = string.Empty;
    public string ErrorOutput { get; set; } = string.Empty;
    public long DurationMs { get; set; }
    public string ParametersJson { get; set; } = string.Empty;
    public string OutputLogsJson { get; set; } = "[]";
}

public class CodeSnippetEntity
{
    [Key]
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string TagsJson { get; set; } = "[]";
    public bool IsFavorite { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int UsageCount { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public int Source { get; set; }
    public long? SourceScriptId { get; set; }
}

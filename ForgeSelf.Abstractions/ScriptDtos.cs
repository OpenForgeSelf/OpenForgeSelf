namespace ForgeSelf.Abstractions;

/// <summary>
/// 脚本定义（纯 DTO，随 IScriptService 契约迁入 Abstractions）。
/// 注意与 ScriptRunner 插件数据库实体（ForgeSelf.Api.Plugins.ScriptRunner.Entities.Script）区分。
/// </summary>
public class Script
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ScriptLanguage Language { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public bool IsFavorite { get; set; }
    public List<ScriptParameter> Parameters { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int UsageCount { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public int TimeoutSeconds { get; set; } = 300;
}

public class CreateScriptRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ScriptLanguage Language { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public List<ScriptParameter> Parameters { get; set; } = [];
    public int TimeoutSeconds { get; set; } = 300;
}

public class UpdateScriptRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ScriptLanguage Language { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public List<ScriptParameter> Parameters { get; set; } = [];
    public int TimeoutSeconds { get; set; } = 300;
}

public class ScriptListResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<Script> Items { get; set; } = [];
}

public class ExecutionListResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<ScriptExecution> Items { get; set; } = [];
}

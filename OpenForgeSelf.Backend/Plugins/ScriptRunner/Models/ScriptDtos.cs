namespace OpenForgeSelf.Backend.Plugins.ScriptRunner.Models;

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

public class ExecuteScriptRequest
{
    public Dictionary<string, object?>? Parameters { get; set; }
    public string? WorkingDirectory { get; set; }
}

public class ExecuteCodeRequest
{
    public string Code { get; set; } = string.Empty;
    public ScriptLanguage Language { get; set; }
    public Dictionary<string, object?>? Parameters { get; set; }
    public string? WorkingDirectory { get; set; }
}

public class ExecutionListResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<ScriptExecution> Items { get; set; } = [];
}

using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Plugins.ScriptRunner.Models;

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

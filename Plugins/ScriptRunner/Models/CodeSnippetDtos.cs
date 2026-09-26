namespace ForgeSelf.Api.Plugins.ScriptRunner.Models;

public class CreateCodeSnippetRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public CodeSnippetSource Source { get; set; } = CodeSnippetSource.Manual;
    public long? SourceScriptId { get; set; }
}

public class UpdateCodeSnippetRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
}

public class CodeSnippetListResponse
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<CodeSnippet> Items { get; set; } = [];
}

public class CreateFromScriptRequest
{
    public string? Title { get; set; }
}

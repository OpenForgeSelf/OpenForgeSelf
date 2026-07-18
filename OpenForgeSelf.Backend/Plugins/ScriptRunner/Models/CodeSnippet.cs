namespace OpenForgeSelf.Backend.Plugins.ScriptRunner.Models;

public enum CodeSnippetSource
{
    Manual = 0,
    Script = 1,
    AIGenerated = 2
}

public class CodeSnippet
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public bool IsFavorite { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int UsageCount { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public CodeSnippetSource Source { get; set; }
    public long? SourceScriptId { get; set; }
}

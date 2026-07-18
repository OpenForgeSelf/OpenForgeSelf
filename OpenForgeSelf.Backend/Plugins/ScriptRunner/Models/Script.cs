namespace OpenForgeSelf.Backend.Plugins.ScriptRunner.Models;

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

public class ScriptParameter
{
    public string Name { get; set; } = string.Empty;
    public ScriptParameterType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? DefaultValue { get; set; }
    public bool IsRequired { get; set; }
    public List<string>? Options { get; set; }
}

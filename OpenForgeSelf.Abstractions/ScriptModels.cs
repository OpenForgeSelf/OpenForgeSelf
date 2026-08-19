namespace OpenForgeSelf.Abstractions;

public enum ScriptLanguage
{
    PowerShell,
    Python,
    NodeJs,
    Shell,
    Cmd
}

public enum ScriptParameterType
{
    String,
    Number,
    Boolean,
    Select,
    FilePath,
    DirectoryPath
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

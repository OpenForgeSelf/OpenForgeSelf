namespace OpenForgeSelf.Abstractions;

public class ScriptTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ScriptLanguage Language { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public List<ScriptParameter> Parameters { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public string Version { get; set; } = "1.0.0";
    public string Author { get; set; } = "OpenForgeSelf Team";
}

public class ScriptTemplateCategory
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TemplateCount { get; set; }
}

public class GenerateScriptRequest
{
    public string Language { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Requirements { get; set; }
}

public class GenerateScriptResponse
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<ScriptParameter> Parameters { get; set; } = [];
    public string Language { get; set; } = string.Empty;
}

public class AnalyzeScriptErrorRequest
{
    public string Language { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

public class AnalyzeScriptErrorResponse
{
    public string ErrorAnalysis { get; set; } = string.Empty;
    public List<string> PossibleCauses { get; set; } = [];
    public string Suggestion { get; set; } = string.Empty;
}

public class SuggestScriptFixRequest
{
    public string Language { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

public class SuggestScriptFixResponse
{
    public string OriginalCode { get; set; } = string.Empty;
    public string FixedCode { get; set; } = string.Empty;
    public string ErrorAnalysis { get; set; } = string.Empty;
    public List<string> Changes { get; set; } = [];
    public string Explanation { get; set; } = string.Empty;
}

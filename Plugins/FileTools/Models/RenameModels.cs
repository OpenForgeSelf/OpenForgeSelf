namespace ForgeSelf.Api.Plugins.FileTools.Models;

public enum RenameRuleType
{
    Sequence,
    Date,
    FindReplace,
    Regex,
    Prefix,
    Suffix,
    ExtensionChange
}

public class RenameRule
{
    public RenameRuleType RuleType { get; set; }
    public int Order { get; set; }
    public bool Enabled { get; set; } = true;
    public Dictionary<string, object> Parameters { get; set; } = new();
}

public class RenamePreviewRequest
{
    public List<string> Files { get; set; } = new();
    public List<RenameRule> Rules { get; set; } = new();
}

public class RenameExecuteRequest
{
    public List<string> Files { get; set; } = new();
    public List<RenameRule> Rules { get; set; } = new();
}

public class RenamePreviewItem
{
    public string OriginalPath { get; set; } = string.Empty;
    public string NewPath { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string NewName { get; set; } = string.Empty;
    public bool WillConflict { get; set; }
    public string? ConflictWith { get; set; }
}

public class RenameExecuteResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<RenameResultItem> Results { get; set; } = new();
    public string? OperationId { get; set; }
}

public class RenameResultItem
{
    public string OriginalPath { get; set; } = string.Empty;
    public string NewPath { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class RenameUndoRequest
{
    public string OperationId { get; set; } = string.Empty;
}

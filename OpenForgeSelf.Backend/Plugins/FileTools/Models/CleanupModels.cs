namespace OpenForgeSelf.Backend.Plugins.FileTools.Models;

public enum CleanupRuleType
{
    ByExtension,
    BySize,
    ByDate
}

public class CleanupRule
{
    public CleanupRuleType RuleType { get; set; }
    public bool Enabled { get; set; } = true;
    public Dictionary<string, object> Parameters { get; set; } = new();
}

public class CleanupPreviewRequest
{
    public string Directory { get; set; } = string.Empty;
    public List<CleanupRule> Rules { get; set; } = new();
    public bool Recursive { get; set; } = true;
}

public class CleanupExecuteRequest
{
    public string Directory { get; set; } = string.Empty;
    public List<CleanupRule> Rules { get; set; } = new();
    public bool Recursive { get; set; } = true;
    public bool DeletePermanently { get; set; } = false;
}

public class CleanupFileItem
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string SizeFormatted { get; set; } = string.Empty;
    public DateTime CreatedTime { get; set; }
    public DateTime ModifiedTime { get; set; }
    public string Extension { get; set; } = string.Empty;
}

public class CleanupPreviewResult
{
    public int FileCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public string TotalSizeFormatted { get; set; } = string.Empty;
    public List<CleanupFileItem> Files { get; set; } = new();
}

public class CleanupExecuteResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public string TotalSizeFormatted { get; set; } = string.Empty;
    public List<CleanupResultItem> Results { get; set; } = new();
    public string? OperationId { get; set; }
}

public class CleanupResultItem
{
    public string FilePath { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class EmptyFolderItem
{
    public string FolderPath { get; set; } = string.Empty;
    public string FolderName { get; set; } = string.Empty;
    public DateTime CreatedTime { get; set; }
}

public class EmptyFoldersRequest
{
    public string Directory { get; set; } = string.Empty;
    public bool Recursive { get; set; } = true;
}

public class EmptyFoldersResult
{
    public int FolderCount { get; set; }
    public List<EmptyFolderItem> Folders { get; set; } = new();
}

public class DuplicateFileGroup
{
    public string Md5Hash { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public long SingleFileSizeBytes { get; set; }
    public string SingleFileSizeFormatted { get; set; } = string.Empty;
    public long WastedSpaceBytes { get; set; }
    public string WastedSpaceFormatted { get; set; } = string.Empty;
    public List<CleanupFileItem> Files { get; set; } = new();
}

public class DuplicateFilesRequest
{
    public string Directory { get; set; } = string.Empty;
    public bool Recursive { get; set; } = true;
    public long MinSizeBytes { get; set; } = 1024;
}

public class DuplicateFilesResult
{
    public int GroupCount { get; set; }
    public int TotalDuplicateFiles { get; set; }
    public long TotalWastedSpaceBytes { get; set; }
    public string TotalWastedSpaceFormatted { get; set; } = string.Empty;
    public List<DuplicateFileGroup> Groups { get; set; } = new();
}

namespace ForgeSelf.Api.Plugins.FileTools.Models;

public enum FileSortBy
{
    Size,
    Name,
    CreatedTime,
    ModifiedTime,
    Extension
}

public class DirectoryStatsRequest
{
    public string Directory { get; set; } = string.Empty;
    public bool Recursive { get; set; } = true;
}

public class DirectoryStatsResult
{
    public string Directory { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public int DirectoryCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public string TotalSizeFormatted { get; set; } = string.Empty;
    public List<FileTypeItem> FileTypeBreakdown { get; set; } = new();
    public DateTime OldestFileTime { get; set; }
    public DateTime NewestFileTime { get; set; }
}

public class FileTypeItem
{
    public string Extension { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public string TotalSizeFormatted { get; set; } = string.Empty;
    public double Percentage { get; set; }
}

public class LargeFilesRequest
{
    public string Directory { get; set; } = string.Empty;
    public bool Recursive { get; set; } = true;
    public int Limit { get; set; } = 20;
}

public class LargeFilesResult
{
    public int TotalCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public string TotalSizeFormatted { get; set; } = string.Empty;
    public List<FileItem> Files { get; set; } = new();
}

public class FileItem
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string SizeFormatted { get; set; } = string.Empty;
    public DateTime CreatedTime { get; set; }
    public DateTime ModifiedTime { get; set; }
}

public class FileTypesBreakdownRequest
{
    public string Directory { get; set; } = string.Empty;
    public bool Recursive { get; set; } = true;
}

public class FileTypesBreakdownResult
{
    public int TotalFileCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public string TotalSizeFormatted { get; set; } = string.Empty;
    public List<FileTypeItem> Types { get; set; } = new();
}

public class SortFilesRequest
{
    public List<string> Files { get; set; } = new();
    public FileSortBy SortBy { get; set; } = FileSortBy.Size;
    public bool Ascending { get; set; } = false;
}

public class SortFilesResult
{
    public List<FileItem> Files { get; set; } = new();
}

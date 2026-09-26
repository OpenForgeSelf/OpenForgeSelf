namespace ForgeSelf.Api.Plugins.FileTools.Models;

public enum ArchiveFormat
{
    Zip
}

public class CompressRequest
{
    public List<string> Files { get; set; } = new();
    public string OutputPath { get; set; } = string.Empty;
    public ArchiveFormat Format { get; set; } = ArchiveFormat.Zip;
    public string? Password { get; set; }
    public long? VolumeSizeBytes { get; set; }
    public int CompressionLevel { get; set; } = 5;
}

public class CompressResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public long OutputSizeBytes { get; set; }
    public string OutputSizeFormatted { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public long OriginalSizeBytes { get; set; }
    public string OriginalSizeFormatted { get; set; } = string.Empty;
    public double CompressionRatio { get; set; }
}

public class ExtractRequest
{
    public string ArchivePath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string? Password { get; set; }
    public bool Overwrite { get; set; } = false;
}

public class ExtractResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public long TotalExtractedSizeBytes { get; set; }
    public string TotalExtractedSizeFormatted { get; set; } = string.Empty;
}

public class ArchiveInfoRequest
{
    public string ArchivePath { get; set; } = string.Empty;
}

public class ArchiveEntryInfo
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string SizeFormatted { get; set; } = string.Empty;
    public long CompressedSizeBytes { get; set; }
    public string CompressedSizeFormatted { get; set; } = string.Empty;
    public DateTime? ModifiedTime { get; set; }
    public bool IsDirectory { get; set; }
    public double CompressionRatio { get; set; }
}

public class ArchiveInfoResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ArchivePath { get; set; } = string.Empty;
    public ArchiveFormat Format { get; set; }
    public long TotalSizeBytes { get; set; }
    public string TotalSizeFormatted { get; set; } = string.Empty;
    public long UncompressedSizeBytes { get; set; }
    public string UncompressedSizeFormatted { get; set; } = string.Empty;
    public int FileCount { get; set; }
    public int DirectoryCount { get; set; }
    public double CompressionRatio { get; set; }
    public bool HasPassword { get; set; }
    public List<ArchiveEntryInfo> Entries { get; set; } = new();
}

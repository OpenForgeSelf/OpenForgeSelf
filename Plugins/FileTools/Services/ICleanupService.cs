using ForgeSelf.Api.Plugins.FileTools.Models;

namespace ForgeSelf.Api.Plugins.FileTools.Services;

public interface ICleanupService
{
    Task<CleanupPreviewResult> PreviewCleanupAsync(string directory, List<CleanupRule> rules, bool recursive = true);
    Task<CleanupExecuteResult> ExecuteCleanupAsync(string directory, List<CleanupRule> rules, bool recursive = true, bool deletePermanently = false);
    Task<EmptyFoldersResult> FindEmptyFoldersAsync(string directory, bool recursive = true);
    Task<DuplicateFilesResult> FindDuplicateFilesAsync(string directory, bool recursive = true, long minSizeBytes = 1024);
}

using OpenForgeSelf.Backend.Plugins.FileTools.Models;

namespace OpenForgeSelf.Backend.Plugins.FileTools.Services;

public interface IFileStatsService
{
    Task<DirectoryStatsResult> GetDirectoryStatsAsync(string directory, bool recursive = true);
    Task<LargeFilesResult> GetLargeFilesAsync(string directory, int limit = 20, bool recursive = true);
    Task<FileTypesBreakdownResult> GetFileTypesBreakdownAsync(string directory, bool recursive = true);
    Task<SortFilesResult> SortFilesAsync(List<string> files, FileSortBy sortBy = FileSortBy.Size, bool ascending = false);
}

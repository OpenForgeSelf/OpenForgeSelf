using ForgeSelf.Api.Plugins.FileTools.Models;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.FileTools.Services;

public class FileStatsService : IFileStatsService
{
    public Task<DirectoryStatsResult> GetDirectoryStatsAsync(string directory, bool recursive = true)
    {
        XTrace.Log.Debug("[FileTools] 获取目录统计: {0}, 递归: {1}", directory, recursive);

        var result = new DirectoryStatsResult
        {
            Directory = directory,
            FileTypeBreakdown = new List<FileTypeItem>()
        };

        try
        {
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException($"目录不存在: {directory}");
            }

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var allFiles = Directory.GetFiles(directory, "*", searchOption);
            var allDirs = Directory.GetDirectories(directory, "*", searchOption);

            result.DirectoryCount = allDirs.Length;
            result.FileCount = allFiles.Length;

            var extStats = new Dictionary<string, (int Count, long Size)>(StringComparer.OrdinalIgnoreCase);
            long totalSize = 0;
            DateTime oldest = DateTime.MaxValue;
            DateTime newest = DateTime.MinValue;

            foreach (var filePath in allFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    totalSize += fileInfo.Length;

                    var ext = fileInfo.Extension.TrimStart('.').ToLowerInvariant();
                    if (string.IsNullOrEmpty(ext))
                        ext = "(无扩展名)";

                    if (!extStats.ContainsKey(ext))
                    {
                        extStats[ext] = (0, 0);
                    }
                    extStats[ext] = (extStats[ext].Count + 1, extStats[ext].Size + fileInfo.Length);

                    if (fileInfo.LastWriteTime < oldest)
                        oldest = fileInfo.LastWriteTime;
                    if (fileInfo.LastWriteTime > newest)
                        newest = fileInfo.LastWriteTime;
                }
                catch (Exception ex)
                {
                    XTrace.Log.Warn("[FileTools] 统计文件 {0} 出错: {1}", filePath, ex.Message);
                }
            }

            result.TotalSizeBytes = totalSize;
            result.TotalSizeFormatted = FileSizeFormatter.FormatSize(totalSize);

            foreach (var kvp in extStats.OrderByDescending(x => x.Value.Size))
            {
                result.FileTypeBreakdown.Add(new FileTypeItem
                {
                    Extension = kvp.Key,
                    FileCount = kvp.Value.Count,
                    TotalSizeBytes = kvp.Value.Size,
                    TotalSizeFormatted = FileSizeFormatter.FormatSize(kvp.Value.Size),
                    Percentage = totalSize > 0 ? (double)kvp.Value.Size / totalSize * 100 : 0
                });
            }

            result.OldestFileTime = oldest == DateTime.MaxValue ? DateTime.MinValue : oldest;
            result.NewestFileTime = newest == DateTime.MinValue ? DateTime.MinValue : newest;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[FileTools] 获取目录统计异常: {0}", ex.Message);
            throw;
        }

        return Task.FromResult(result);
    }

    public Task<LargeFilesResult> GetLargeFilesAsync(string directory, int limit = 20, bool recursive = true)
    {
        XTrace.Log.Debug("[FileTools] 获取大文件列表: {0}, 限制: {1}", directory, limit);

        var result = new LargeFilesResult
        {
            Files = new List<FileItem>()
        };

        try
        {
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException($"目录不存在: {directory}");
            }

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var allFiles = Directory.GetFiles(directory, "*", searchOption);

            var fileList = new List<FileInfo>();
            long totalSize = 0;

            foreach (var filePath in allFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    fileList.Add(fileInfo);
                    totalSize += fileInfo.Length;
                }
                catch (Exception ex)
                {
                    XTrace.Log.Warn("[FileTools] 处理文件 {0} 出错: {1}", filePath, ex.Message);
                }
            }

            var largeFiles = fileList.OrderByDescending(f => f.Length).Take(limit);

            foreach (var fileInfo in largeFiles)
            {
                result.Files.Add(CreateFileItem(fileInfo));
            }

            result.TotalCount = fileList.Count;
            result.TotalSizeBytes = totalSize;
            result.TotalSizeFormatted = FileSizeFormatter.FormatSize(totalSize);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[FileTools] 获取大文件列表异常: {0}", ex.Message);
            throw;
        }

        return Task.FromResult(result);
    }

    public Task<FileTypesBreakdownResult> GetFileTypesBreakdownAsync(string directory, bool recursive = true)
    {
        XTrace.Log.Debug("[FileTools] 获取文件类型分布: {0}", directory);

        var result = new FileTypesBreakdownResult
        {
            Types = new List<FileTypeItem>()
        };

        try
        {
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException($"目录不存在: {directory}");
            }

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var allFiles = Directory.GetFiles(directory, "*", searchOption);

            var extStats = new Dictionary<string, (int Count, long Size)>(StringComparer.OrdinalIgnoreCase);
            long totalSize = 0;

            foreach (var filePath in allFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    totalSize += fileInfo.Length;

                    var ext = fileInfo.Extension.TrimStart('.').ToLowerInvariant();
                    if (string.IsNullOrEmpty(ext))
                        ext = "(无扩展名)";

                    if (!extStats.ContainsKey(ext))
                    {
                        extStats[ext] = (0, 0);
                    }
                    extStats[ext] = (extStats[ext].Count + 1, extStats[ext].Size + fileInfo.Length);
                }
                catch (Exception ex)
                {
                    XTrace.Log.Warn("[FileTools] 统计文件 {0} 出错: {1}", filePath, ex.Message);
                }
            }

            result.TotalFileCount = allFiles.Length;
            result.TotalSizeBytes = totalSize;
            result.TotalSizeFormatted = FileSizeFormatter.FormatSize(totalSize);

            foreach (var kvp in extStats.OrderByDescending(x => x.Value.Size))
            {
                result.Types.Add(new FileTypeItem
                {
                    Extension = kvp.Key,
                    FileCount = kvp.Value.Count,
                    TotalSizeBytes = kvp.Value.Size,
                    TotalSizeFormatted = FileSizeFormatter.FormatSize(kvp.Value.Size),
                    Percentage = totalSize > 0 ? (double)kvp.Value.Size / totalSize * 100 : 0
                });
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[FileTools] 获取文件类型分布异常: {0}", ex.Message);
            throw;
        }

        return Task.FromResult(result);
    }

    public Task<SortFilesResult> SortFilesAsync(List<string> files, FileSortBy sortBy = FileSortBy.Size, bool ascending = false)
    {
        XTrace.Log.Debug("[FileTools] 文件排序，文件数: {0}, 排序方式: {1}", files.Count, sortBy);

        var result = new SortFilesResult
        {
            Files = new List<FileItem>()
        };

        try
        {
            var fileList = new List<FileInfo>();

            foreach (var filePath in files)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        fileList.Add(new FileInfo(filePath));
                    }
                }
                catch (Exception ex)
                {
                    XTrace.Log.Warn("[FileTools] 处理文件 {0} 出错: {1}", filePath, ex.Message);
                }
            }

            IEnumerable<FileInfo> sorted = sortBy switch
            {
                FileSortBy.Size => ascending ? fileList.OrderBy(f => f.Length) : fileList.OrderByDescending(f => f.Length),
                FileSortBy.Name => ascending ? fileList.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase) : fileList.OrderByDescending(f => f.Name, StringComparer.OrdinalIgnoreCase),
                FileSortBy.CreatedTime => ascending ? fileList.OrderBy(f => f.CreationTime) : fileList.OrderByDescending(f => f.CreationTime),
                FileSortBy.ModifiedTime => ascending ? fileList.OrderBy(f => f.LastWriteTime) : fileList.OrderByDescending(f => f.LastWriteTime),
                FileSortBy.Extension => ascending ? fileList.OrderBy(f => f.Extension, StringComparer.OrdinalIgnoreCase) : fileList.OrderByDescending(f => f.Extension, StringComparer.OrdinalIgnoreCase),
                _ => fileList.OrderByDescending(f => f.Length)
            };

            foreach (var fileInfo in sorted)
            {
                result.Files.Add(CreateFileItem(fileInfo));
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[FileTools] 文件排序异常: {0}", ex.Message);
            throw;
        }

        return Task.FromResult(result);
    }

    private static FileItem CreateFileItem(FileInfo fileInfo)
    {
        return new FileItem
        {
            FilePath = fileInfo.FullName,
            FileName = fileInfo.Name,
            Extension = fileInfo.Extension.TrimStart('.'),
            SizeBytes = fileInfo.Length,
            SizeFormatted = FileSizeFormatter.FormatSize(fileInfo.Length),
            CreatedTime = fileInfo.CreationTime,
            ModifiedTime = fileInfo.LastWriteTime
        };
    }
}

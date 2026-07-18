using System.Collections.Concurrent;
using System.Security.Cryptography;
using OpenForgeSelf.Backend.Plugins.FileTools.Models;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.FileTools.Services;

public class CleanupService : ICleanupService
{
    private static readonly ConcurrentDictionary<string, List<string>> _recycleBin = new();

    public Task<CleanupPreviewResult> PreviewCleanupAsync(string directory, List<CleanupRule> rules, bool recursive = true)
    {
        XTrace.Log.Debug("[FileTools] 预览清理，目录: {0}, 递归: {1}", directory, recursive);

        var result = new CleanupPreviewResult
        {
            Files = new List<CleanupFileItem>()
        };

        try
        {
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException($"目录不存在: {directory}");
            }

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var allFiles = Directory.GetFiles(directory, "*", searchOption);
            var enabledRules = rules.Where(r => r.Enabled).ToList();

            foreach (var filePath in allFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    var matchesAllRules = enabledRules.Count == 0 || enabledRules.All(rule => MatchesRule(fileInfo, rule));

                    if (matchesAllRules)
                    {
                        result.Files.Add(CreateCleanupFileItem(fileInfo));
                        result.TotalSizeBytes += fileInfo.Length;
                        result.FileCount++;
                    }
                }
                catch (Exception ex)
                {
                    XTrace.Log.Warn("[FileTools] 处理文件 {0} 时出错: {1}", filePath, ex.Message);
                }
            }

            result.TotalSizeFormatted = FileSizeFormatter.FormatSize(result.TotalSizeBytes);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[FileTools] 预览清理异常: {0}", ex.Message);
            throw;
        }

        return Task.FromResult(result);
    }

    public async Task<CleanupExecuteResult> ExecuteCleanupAsync(string directory, List<CleanupRule> rules, bool recursive = true, bool deletePermanently = false)
    {
        XTrace.Log.Info("[FileTools] 执行清理，目录: {0}, 永久删除: {1}", directory, deletePermanently);

        var result = new CleanupExecuteResult
        {
            Results = new List<CleanupResultItem>()
        };

        try
        {
            var preview = await PreviewCleanupAsync(directory, rules, recursive);
            result.TotalCount = preview.FileCount;
            result.TotalSizeBytes = preview.TotalSizeBytes;
            result.TotalSizeFormatted = preview.TotalSizeFormatted;

            var operationId = Guid.NewGuid().ToString();
            var recycledFiles = new List<string>();

            foreach (var fileItem in preview.Files)
            {
                var resultItem = new CleanupResultItem
                {
                    FilePath = fileItem.FilePath
                };

                try
                {
                    if (deletePermanently)
                    {
                        File.Delete(fileItem.FilePath);
                    }
                    else
                    {
                        var recycleDir = Path.Combine(Path.GetTempPath(), "OpenForgeSelfRecycleBin", operationId);
                        Directory.CreateDirectory(recycleDir);

                        var destPath = Path.Combine(recycleDir, Path.GetFileName(fileItem.FilePath));
                        var counter = 1;
                        while (File.Exists(destPath))
                        {
                            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileItem.FilePath);
                            var ext = Path.GetExtension(fileItem.FilePath);
                            destPath = Path.Combine(recycleDir, $"{nameWithoutExt}_{counter}{ext}");
                            counter++;
                        }

                        File.Move(fileItem.FilePath, destPath);
                        recycledFiles.Add(fileItem.FilePath);
                    }

                    resultItem.Success = true;
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    XTrace.Log.Error("[FileTools] 删除文件失败 {0}: {1}", fileItem.FilePath, ex.Message);
                    resultItem.Success = false;
                    resultItem.ErrorMessage = ex.Message;
                    result.FailedCount++;
                }

                result.Results.Add(resultItem);
            }

            if (recycledFiles.Count > 0 && !deletePermanently)
            {
                _recycleBin[operationId] = recycledFiles;
                result.OperationId = operationId;
            }

            result.Success = result.FailedCount == 0;
            result.Message = result.Success
                ? $"成功清理 {result.SuccessCount} 个文件，共 {result.TotalSizeFormatted}"
                : $"清理完成，成功 {result.SuccessCount} 个，失败 {result.FailedCount} 个";
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[FileTools] 执行清理异常: {0}", ex.Message);
            result.Success = false;
            result.Message = $"清理执行失败: {ex.Message}";
        }

        return result;
    }

    public Task<EmptyFoldersResult> FindEmptyFoldersAsync(string directory, bool recursive = true)
    {
        XTrace.Log.Debug("[FileTools] 查找空文件夹，目录: {0}, 递归: {1}", directory, recursive);

        var result = new EmptyFoldersResult
        {
            Folders = new List<EmptyFolderItem>()
        };

        try
        {
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException($"目录不存在: {directory}");
            }

            var allDirectories = recursive
                ? Directory.GetDirectories(directory, "*", SearchOption.AllDirectories)
                : Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly);

            foreach (var dirPath in allDirectories)
            {
                try
                {
                    if (IsDirectoryEmpty(dirPath))
                    {
                        var dirInfo = new DirectoryInfo(dirPath);
                        result.Folders.Add(new EmptyFolderItem
                        {
                            FolderPath = dirPath,
                            FolderName = dirInfo.Name,
                            CreatedTime = dirInfo.CreationTime
                        });
                        result.FolderCount++;
                    }
                }
                catch (Exception ex)
                {
                    XTrace.Log.Warn("[FileTools] 检查目录 {0} 时出错: {1}", dirPath, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[FileTools] 查找空文件夹异常: {0}", ex.Message);
            throw;
        }

        return Task.FromResult(result);
    }

    public Task<DuplicateFilesResult> FindDuplicateFilesAsync(string directory, bool recursive = true, long minSizeBytes = 1024)
    {
        XTrace.Log.Debug("[FileTools] 查找重复文件，目录: {0}, 最小大小: {1}B", directory, minSizeBytes);

        var result = new DuplicateFilesResult
        {
            Groups = new List<DuplicateFileGroup>()
        };

        try
        {
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException($"目录不存在: {directory}");
            }

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var allFiles = Directory.GetFiles(directory, "*", searchOption);

            var sizeGroups = new Dictionary<long, List<FileInfo>>();

            foreach (var filePath in allFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.Length < minSizeBytes)
                        continue;

                    if (!sizeGroups.ContainsKey(fileInfo.Length))
                    {
                        sizeGroups[fileInfo.Length] = new List<FileInfo>();
                    }
                    sizeGroups[fileInfo.Length].Add(fileInfo);
                }
                catch (Exception ex)
                {
                    XTrace.Log.Warn("[FileTools] 处理文件 {0} 时出错: {1}", filePath, ex.Message);
                }
            }

            var md5Groups = new Dictionary<string, List<FileInfo>>();

            foreach (var sizeGroup in sizeGroups.Where(g => g.Value.Count > 1))
            {
                foreach (var fileInfo in sizeGroup.Value)
                {
                    try
                    {
                        var md5 = ComputeMD5(fileInfo.FullName);
                        if (!md5Groups.ContainsKey(md5))
                        {
                            md5Groups[md5] = new List<FileInfo>();
                        }
                        md5Groups[md5].Add(fileInfo);
                    }
                    catch (Exception ex)
                    {
                        XTrace.Log.Warn("[FileTools] 计算MD5失败 {0}: {1}", fileInfo.FullName, ex.Message);
                    }
                }
            }

            foreach (var md5Group in md5Groups.Where(g => g.Value.Count > 1))
            {
                var files = md5Group.Value;
                var group = new DuplicateFileGroup
                {
                    Md5Hash = md5Group.Key,
                    FileCount = files.Count,
                    SingleFileSizeBytes = files[0].Length,
                    SingleFileSizeFormatted = FileSizeFormatter.FormatSize(files[0].Length),
                    WastedSpaceBytes = files[0].Length * (files.Count - 1),
                    Files = new List<CleanupFileItem>()
                };
                group.WastedSpaceFormatted = FileSizeFormatter.FormatSize(group.WastedSpaceBytes);

                foreach (var fileInfo in files)
                {
                    group.Files.Add(CreateCleanupFileItem(fileInfo));
                }

                result.Groups.Add(group);
                result.TotalDuplicateFiles += files.Count;
                result.TotalWastedSpaceBytes += group.WastedSpaceBytes;
            }

            result.GroupCount = result.Groups.Count;
            result.TotalWastedSpaceFormatted = FileSizeFormatter.FormatSize(result.TotalWastedSpaceBytes);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[FileTools] 查找重复文件异常: {0}", ex.Message);
            throw;
        }

        return Task.FromResult(result);
    }

    private static bool MatchesRule(FileInfo fileInfo, CleanupRule rule)
    {
        switch (rule.RuleType)
        {
            case CleanupRuleType.ByExtension:
                var extensions = GetStringListParameter(rule.Parameters, "extensions");
                var matchMode = GetStringParameter(rule.Parameters, "mode", "include");
                var fileExt = fileInfo.Extension.TrimStart('.').ToLowerInvariant();
                var extList = extensions.Select(e => e.Trim().TrimStart('.').ToLowerInvariant()).ToList();
                var hasExt = extList.Contains(fileExt);
                return matchMode == "include" ? hasExt : !hasExt;

            case CleanupRuleType.BySize:
                var minSize = GetLongParameter(rule.Parameters, "minSizeBytes");
                var maxSize = GetLongParameter(rule.Parameters, "maxSizeBytes");
                if (minSize.HasValue && fileInfo.Length < minSize.Value)
                    return false;
                if (maxSize.HasValue && fileInfo.Length > maxSize.Value)
                    return false;
                return true;

            case CleanupRuleType.ByDate:
                var dateType = GetStringParameter(rule.Parameters, "dateType", "modified");
                var beforeDate = GetDateTimeParameter(rule.Parameters, "beforeDate");
                var afterDate = GetDateTimeParameter(rule.Parameters, "afterDate");

                var date = dateType switch
                {
                    "created" => fileInfo.CreationTime,
                    "accessed" => fileInfo.LastAccessTime,
                    _ => fileInfo.LastWriteTime
                };

                if (beforeDate.HasValue && date > beforeDate.Value)
                    return false;
                if (afterDate.HasValue && date < afterDate.Value)
                    return false;
                return true;

            default:
                return true;
        }
    }

    private static bool IsDirectoryEmpty(string path)
    {
        try
        {
            return !Directory.EnumerateFileSystemEntries(path).Any();
        }
        catch
        {
            return false;
        }
    }

    private static string ComputeMD5(string filePath)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        var hash = md5.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private static CleanupFileItem CreateCleanupFileItem(FileInfo fileInfo)
    {
        return new CleanupFileItem
        {
            FilePath = fileInfo.FullName,
            FileName = fileInfo.Name,
            SizeBytes = fileInfo.Length,
            SizeFormatted = FileSizeFormatter.FormatSize(fileInfo.Length),
            CreatedTime = fileInfo.CreationTime,
            ModifiedTime = fileInfo.LastWriteTime,
            Extension = fileInfo.Extension.TrimStart('.')
        };
    }

    private static string GetStringParameter(Dictionary<string, object> parameters, string key, string defaultValue = "")
    {
        if (parameters.TryGetValue(key, out var value) && value != null)
        {
            return value.ToString() ?? defaultValue;
        }
        return defaultValue;
    }

    private static List<string> GetStringListParameter(Dictionary<string, object> parameters, string key)
    {
        if (parameters.TryGetValue(key, out var value) && value != null)
        {
            if (value is string strValue)
            {
                if (!string.IsNullOrEmpty(strValue))
                {
                    return strValue.Split(',').Select(s => s.Trim()).ToList();
                }
                return new List<string>();
            }
            if (value is System.Collections.IEnumerable enumerable)
            {
                var result = new List<string>();
                foreach (var item in enumerable)
                {
                    result.Add(item?.ToString() ?? string.Empty);
                }
                return result;
            }
        }
        return new List<string>();
    }

    private static long? GetLongParameter(Dictionary<string, object> parameters, string key)
    {
        if (parameters.TryGetValue(key, out var value) && value != null)
        {
            if (long.TryParse(value.ToString(), out var result))
                return result;
        }
        return null;
    }

    private static DateTime? GetDateTimeParameter(Dictionary<string, object> parameters, string key)
    {
        if (parameters.TryGetValue(key, out var value) && value != null)
        {
            if (DateTime.TryParse(value.ToString(), out var result))
                return result;
        }
        return null;
    }
}

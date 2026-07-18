using System.IO.Compression;
using OpenForgeSelf.Backend.Plugins.FileTools.Models;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.FileTools.Services;

public class ArchiveService : IArchiveService
{
    public async Task<CompressResult> CompressAsync(List<string> files, string outputPath, ArchiveFormat format = ArchiveFormat.Zip, string? password = null, long? volumeSizeBytes = null, int compressionLevel = 5)
    {
        XTrace.Log.Info("[FileTools] 开始压缩，文件数: {0}, 输出: {1}", files.Count, outputPath);

        var result = new CompressResult
        {
            OutputPath = outputPath
        };

        try
        {
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            var actualFiles = new List<string>();
            long originalSize = 0;

            foreach (var path in files)
            {
                if (File.Exists(path))
                {
                    actualFiles.Add(path);
                    originalSize += new FileInfo(path).Length;
                }
                else if (Directory.Exists(path))
                {
                    var dirInfo = new DirectoryInfo(path);
                    var dirFiles = dirInfo.GetFiles("*", SearchOption.AllDirectories);
                    foreach (var f in dirFiles)
                    {
                        actualFiles.Add(f.FullName);
                        originalSize += f.Length;
                    }
                }
            }

            result.FileCount = actualFiles.Count;
            result.OriginalSizeBytes = originalSize;
            result.OriginalSizeFormatted = FileSizeFormatter.FormatSize(originalSize);

            var level = compressionLevel switch
            {
                <= 0 => CompressionLevel.NoCompression,
                <= 3 => CompressionLevel.Fastest,
                <= 7 => CompressionLevel.Optimal,
                _ => CompressionLevel.SmallestSize
            };

            await Task.Run(() =>
            {
                using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);
                foreach (var filePath in actualFiles)
                {
                    var entryName = GetRelativeEntryName(files, filePath);
                    archive.CreateEntryFromFile(filePath, entryName, level);
                }
            });

            var outputFileInfo = new FileInfo(outputPath);
            result.OutputSizeBytes = outputFileInfo.Length;
            result.OutputSizeFormatted = FileSizeFormatter.FormatSize(result.OutputSizeBytes);
            result.CompressionRatio = originalSize > 0 ? (double)result.OutputSizeBytes / originalSize : 0;
            result.Success = true;
            result.Message = $"压缩完成，共 {result.FileCount} 个文件";

            XTrace.Log.Info("[FileTools] 压缩完成，输出大小: {0}", result.OutputSizeFormatted);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[FileTools] 压缩异常: {0}", ex.Message);
            result.Success = false;
            result.Message = $"压缩失败: {ex.Message}";
        }

        return result;
    }

    public async Task<ExtractResult> ExtractAsync(string archivePath, string outputPath, string? password = null, bool overwrite = false)
    {
        XTrace.Log.Info("[FileTools] 开始解压: {0} -> {1}", archivePath, outputPath);

        var result = new ExtractResult
        {
            OutputPath = outputPath
        };

        try
        {
            if (!File.Exists(archivePath))
            {
                throw new FileNotFoundException($"压缩包不存在: {archivePath}");
            }

            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            long totalExtractedSize = 0;
            int fileCount = 0;

            await Task.Run(() =>
            {
                using var archive = ZipFile.OpenRead(archivePath);
                foreach (var entry in archive.Entries)
                {
                    var destinationPath = Path.Combine(outputPath, entry.FullName);

                    if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                    {
                        Directory.CreateDirectory(destinationPath);
                        continue;
                    }

                    var entryDir = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(entryDir) && !Directory.Exists(entryDir))
                    {
                        Directory.CreateDirectory(entryDir);
                    }

                    if (File.Exists(destinationPath) && !overwrite)
                    {
                        continue;
                    }

                    entry.ExtractToFile(destinationPath, overwrite);
                    totalExtractedSize += entry.Length;
                    fileCount++;
                }
            });

            result.FileCount = fileCount;
            result.TotalExtractedSizeBytes = totalExtractedSize;
            result.TotalExtractedSizeFormatted = FileSizeFormatter.FormatSize(totalExtractedSize);
            result.Success = true;
            result.Message = $"解压完成，共 {fileCount} 个文件";

            XTrace.Log.Info("[FileTools] 解压完成，文件数: {0}", fileCount);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[FileTools] 解压异常: {0}", ex.Message);
            result.Success = false;
            result.Message = $"解压失败: {ex.Message}";
        }

        return result;
    }

    public Task<ArchiveInfoResult> GetArchiveInfoAsync(string archivePath)
    {
        XTrace.Log.Debug("[FileTools] 获取压缩包信息: {0}", archivePath);

        var result = new ArchiveInfoResult
        {
            ArchivePath = archivePath,
            Entries = new List<ArchiveEntryInfo>()
        };

        try
        {
            if (!File.Exists(archivePath))
            {
                throw new FileNotFoundException($"压缩包不存在: {archivePath}");
            }

            var fileInfo = new FileInfo(archivePath);
            result.TotalSizeBytes = fileInfo.Length;
            result.TotalSizeFormatted = FileSizeFormatter.FormatSize(fileInfo.Length);

            long uncompressedSize = 0;
            int fileCount = 0;
            int dirCount = 0;

            using var archive = ZipFile.OpenRead(archivePath);
            result.Format = ArchiveFormat.Zip;

            foreach (var entry in archive.Entries)
            {
                var isDir = entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\') || string.IsNullOrEmpty(entry.Name);

                var entryInfo = new ArchiveEntryInfo
                {
                    Name = entry.Name,
                    FullName = entry.FullName,
                    SizeBytes = entry.Length,
                    SizeFormatted = FileSizeFormatter.FormatSize(entry.Length),
                    CompressedSizeBytes = 0,
                    CompressedSizeFormatted = "0 B",
                    ModifiedTime = entry.LastWriteTime.DateTime,
                    IsDirectory = isDir,
                    CompressionRatio = entry.Length > 0 ? 0 : 0
                };

                if (isDir)
                {
                    dirCount++;
                }
                else
                {
                    fileCount++;
                    uncompressedSize += entry.Length;
                }

                result.Entries.Add(entryInfo);
            }

            result.FileCount = fileCount;
            result.DirectoryCount = dirCount;
            result.UncompressedSizeBytes = uncompressedSize;
            result.UncompressedSizeFormatted = FileSizeFormatter.FormatSize(uncompressedSize);
            result.CompressionRatio = uncompressedSize > 0 ? (double)result.TotalSizeBytes / uncompressedSize : 0;
            result.Success = true;
            result.Message = "获取压缩包信息成功";
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("[FileTools] 获取压缩包信息异常: {0}", ex.Message);
            result.Success = false;
            result.Message = $"获取压缩包信息失败: {ex.Message}";
        }

        return Task.FromResult(result);
    }

    private static string GetRelativeEntryName(List<string> sourcePaths, string filePath)
    {
        foreach (var sourcePath in sourcePaths)
        {
            if (Directory.Exists(sourcePath))
            {
                var dirFullName = sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (filePath.StartsWith(dirFullName + Path.DirectorySeparatorChar) ||
                    filePath.StartsWith(dirFullName + Path.AltDirectorySeparatorChar))
                {
                    var dirName = Path.GetFileName(dirFullName);
                    var relativePath = filePath[(dirFullName.Length + 1)..];
                    return Path.Combine(dirName, relativePath);
                }
            }
        }

        return Path.GetFileName(filePath);
    }
}

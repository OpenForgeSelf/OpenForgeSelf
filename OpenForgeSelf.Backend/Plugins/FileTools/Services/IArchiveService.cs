using OpenForgeSelf.Backend.Plugins.FileTools.Models;

namespace OpenForgeSelf.Backend.Plugins.FileTools.Services;

public interface IArchiveService
{
    Task<CompressResult> CompressAsync(List<string> files, string outputPath, ArchiveFormat format = ArchiveFormat.Zip, string? password = null, long? volumeSizeBytes = null, int compressionLevel = 5);
    Task<ExtractResult> ExtractAsync(string archivePath, string outputPath, string? password = null, bool overwrite = false);
    Task<ArchiveInfoResult> GetArchiveInfoAsync(string archivePath);
}

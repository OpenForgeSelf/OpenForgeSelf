using System.IO.Compression;
using System.Text.Encodings.Web;
using System.Text.Json;
using ForgeSelf.Abstractions;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.Services;

public class PluginPackagerService
{
    private readonly PluginManager _pluginManager;

    public PluginPackagerService(PluginManager pluginManager)
    {
        _pluginManager = pluginManager;
    }

    public byte[] PackagePlugin(string pluginId)
    {
        XTrace.Log.Info("打包插件: {0}", pluginId);

        var metadata = _pluginManager.GetPluginMetadata(pluginId);
        if (metadata == null)
        {
            throw new ArgumentException($"插件不存在: {pluginId}");
        }

        try
        {
            var version = PluginVersionLayout.ReadCurrentVersion(metadata.PluginDirectory);
            if (string.IsNullOrWhiteSpace(version))
                version = metadata.Version;
            var versionDir = PluginVersionLayout.VersionDirectory(metadata.PluginDirectory, version);
            var root = Directory.Exists(versionDir) ? versionDir : metadata.PluginDirectory;

            using var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(root, file).Replace('\\', '/');
                    if (IsExcludedFromPackage(relativePath))
                        continue;

                    var entry = archive.CreateEntry(relativePath);
                    using var entryStream = entry.Open();
                    using var fileStream = File.OpenRead(file);
                    fileStream.CopyTo(entryStream);
                }
            }

            XTrace.Log.Info("插件打包成功: {0}, 大小: {1} 字节", pluginId, ms.Length);
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("打包插件失败 [{0}]: {1}", pluginId, ex.Message);
            throw;
        }
    }

    /// <summary>打包排除源码、版本库、测试、作业目录和宿主共享程序集。旧扩展名不进包。</summary>
    internal static bool IsExcludedFromPackage(string relativePath)
    {
        var parts = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(p => p is "src" or ".git" or "tests" or ".forge" or ".github"))
            return true;

        var name = parts.Length == 0 ? relativePath : parts[^1];
        if (name.EndsWith(PluginArtifactValidator.LegacyPackageExtension, StringComparison.OrdinalIgnoreCase))
            return true;
        return IsHostProvidedPackageFile(name);
    }

    /// <summary>
    /// 宿主已经加载的共享程序集及其 pdb，以及 Microsoft.Extensions.* 这类宿主提供的依赖，不进插件包。
    /// 插件自己的 dll / pdb / deps.json 保留。
    /// </summary>
    internal static bool IsHostProvidedPackageFile(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        if (!ext.Equals(".dll", StringComparison.OrdinalIgnoreCase) &&
            !ext.Equals(".pdb", StringComparison.OrdinalIgnoreCase))
            return false;

        if (fileName.StartsWith("ForgeSelf.", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("NewLife.", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("Microsoft.Extensions.", StringComparison.OrdinalIgnoreCase))
            return true;

        return fileName.Equals("XCode.dll", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("XCode.pdb", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("MX.dll", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("MX.pdb", StringComparison.OrdinalIgnoreCase);
    }

    public bool ValidatePackage(string packagePath)
    {
        XTrace.Log.Info("校验插件包: {0}", packagePath);

        try
        {
            if (PluginArtifactValidator.IsLegacyPackage(packagePath))
            {
                XTrace.Log.Error("已停止接受 .forgeself-plugin: {0}", packagePath);
                return false;
            }

            if (!File.Exists(packagePath))
            {
                XTrace.Log.Error("插件包文件不存在: {0}", packagePath);
                return false;
            }

            using var archive = ZipFile.OpenRead(packagePath);
            PluginArtifactValidator.EnsureSafe(archive);

            var manifestEntry = archive.GetEntry("plugin.json");
            if (manifestEntry == null)
            {
                XTrace.Log.Error("插件包缺少 plugin.json");
                return false;
            }

            using var stream = manifestEntry.Open();
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            var metadata = JsonSerializer.Deserialize<PluginMetadata>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                // PC-027：统一不变量——所有 JsonSerializerOptions 一律声明 Encoder（反序列化时 Encoder 被忽略，零读侧风险）
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            if (metadata == null || string.IsNullOrWhiteSpace(metadata.Id))
            {
                XTrace.Log.Error("plugin.json 解析失败或缺少 Id");
                return false;
            }

            if (string.IsNullOrWhiteSpace(metadata.Name))
            {
                XTrace.Log.Error("plugin.json 缺少 Name");
                return false;
            }

            if (string.IsNullOrWhiteSpace(metadata.Version))
            {
                XTrace.Log.Error("plugin.json 缺少 Version");
                return false;
            }

            XTrace.Log.Info("插件包校验通过: {0} v{1}", metadata.Name, metadata.Version);
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("校验插件包失败 [{0}]: {1}", packagePath, ex.Message);
            return false;
        }
    }

    public PluginMetadata? ExtractPackage(string packagePath, string targetDir)
    {
        XTrace.Log.Info("解压插件包: {0} -> {1}", packagePath, targetDir);

        try
        {
            if (!File.Exists(packagePath))
            {
                throw new FileNotFoundException($"插件包文件不存在: {packagePath}");
            }

            if (!ValidatePackage(packagePath))
            {
                throw new InvalidDataException("插件包校验失败");
            }

            using (var archive = ZipFile.OpenRead(packagePath))
            {
                PluginArtifactValidator.ExtractSafe(archive, targetDir);
            }

            var manifestPath = Path.Combine(targetDir, "plugin.json");
            var json = File.ReadAllText(manifestPath);
            var metadata = JsonSerializer.Deserialize<PluginMetadata>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                // PC-027：统一不变量——所有 JsonSerializerOptions 一律声明 Encoder（反序列化时 Encoder 被忽略，零读侧风险）
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            if (metadata != null)
            {
                metadata.PluginDirectory = targetDir;
            }

            XTrace.Log.Info("插件包解压成功: {0}", targetDir);
            return metadata;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("解压插件包失败 [{0}]: {1}", packagePath, ex.Message);
            throw;
        }
    }

    public PluginMetadata? ReadPackageMetadata(string packagePath)
    {
        XTrace.Log.Info("读取插件包元数据: {0}", packagePath);

        try
        {
            if (PluginArtifactValidator.IsLegacyPackage(packagePath) || !File.Exists(packagePath))
                return null;

            if (PluginArtifactValidator.IsDll(packagePath))
                return PluginArtifactValidator.ReadDllManifest(packagePath);

            using var archive = ZipFile.OpenRead(packagePath);
            PluginArtifactValidator.EnsureSafe(archive);
            var manifestEntry = archive.GetEntry("plugin.json");
            if (manifestEntry == null)
            {
                return null;
            }

            using var stream = manifestEntry.Open();
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            var metadata = JsonSerializer.Deserialize<PluginMetadata>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                // PC-027：统一不变量——所有 JsonSerializerOptions 一律声明 Encoder（反序列化时 Encoder 被忽略，零读侧风险）
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            return metadata;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("读取插件包元数据失败 [{0}]: {1}", packagePath, ex.Message);
            return null;
        }
    }
}

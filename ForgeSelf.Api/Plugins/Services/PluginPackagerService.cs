using System.IO.Compression;
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
            using var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                var pluginDir = metadata.PluginDirectory;
                var files = Directory.GetFiles(pluginDir, "*", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    var relativePath = Path.GetRelativePath(pluginDir, file);
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

    public bool ValidatePackage(string packagePath)
    {
        XTrace.Log.Info("校验插件包: {0}", packagePath);

        try
        {
            if (!File.Exists(packagePath))
            {
                XTrace.Log.Error("插件包文件不存在: {0}", packagePath);
                return false;
            }

            using var archive = ZipFile.OpenRead(packagePath);

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
                PropertyNameCaseInsensitive = true
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

            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            ZipFile.ExtractToDirectory(packagePath, targetDir, true);

            var manifestPath = Path.Combine(targetDir, "plugin.json");
            var json = File.ReadAllText(manifestPath);
            var metadata = JsonSerializer.Deserialize<PluginMetadata>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
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
            if (!File.Exists(packagePath))
            {
                return null;
            }

            using var archive = ZipFile.OpenRead(packagePath);
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
                PropertyNameCaseInsensitive = true
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

using System.Text.Json;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.Abstractions;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.Services;

public class PluginInstallerService
{
    private readonly PluginManager _pluginManager;
    private readonly PluginPackagerService _packagerService;
    private readonly PluginVersionService _versionService;
    private string _pluginsDirectory = string.Empty;

    public PluginInstallerService(
        PluginManager pluginManager,
        PluginPackagerService packagerService,
        PluginVersionService versionService)
    {
        _pluginManager = pluginManager;
        _packagerService = packagerService;
        _versionService = versionService;
    }

    public void Initialize(string pluginsDirectory)
    {
        _pluginsDirectory = pluginsDirectory;
    }

    public PluginMetadata? InstallFromPackage(string packagePath)
    {
        XTrace.Log.Info("从包安装插件: {0}", packagePath);

        try
        {
            if (!_packagerService.ValidatePackage(packagePath))
            {
                throw new InvalidDataException("插件包校验失败");
            }

            var metadata = _packagerService.ReadPackageMetadata(packagePath);
            if (metadata == null)
            {
                throw new InvalidDataException("无法读取插件元数据");
            }

            var validationResult = ValidateDependencies(metadata);
            if (!validationResult.IsValid)
            {
                throw new InvalidOperationException($"依赖校验失败: {string.Join(", ", validationResult.Errors)}");
            }

            var existingMetadata = _pluginManager.GetPluginMetadata(metadata.Id);
            if (existingMetadata != null)
            {
                XTrace.Log.Info("插件已存在，进行更新: {0}", metadata.Id);
                return UpdateFromPackage(packagePath);
            }

            var targetDir = Path.Combine(_pluginsDirectory, metadata.Id);
            if (Directory.Exists(targetDir))
            {
                XTrace.Log.Warn("目标目录已存在，将被覆盖: {0}", targetDir);
            }

            var extractedMetadata = _packagerService.ExtractPackage(packagePath, targetDir);
            if (extractedMetadata == null)
            {
                throw new InvalidOperationException("插件包解压失败");
            }

            _versionService.BackupPlugin(metadata.Id);

            XTrace.Log.Info("插件安装成功: {0} v{1}", metadata.Name, metadata.Version);
            return extractedMetadata;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("安装插件失败 [{0}]: {1}", packagePath, ex.Message);
            throw;
        }
    }

    public PluginMetadata? UpdateFromPackage(string packagePath)
    {
        XTrace.Log.Info("从包更新插件: {0}", packagePath);

        try
        {
            var metadata = _packagerService.ReadPackageMetadata(packagePath);
            if (metadata == null)
            {
                throw new InvalidDataException("无法读取插件元数据");
            }

            var existingMetadata = _pluginManager.GetPluginMetadata(metadata.Id);
            if (existingMetadata == null)
            {
                throw new InvalidOperationException($"插件不存在，无法更新: {metadata.Id}");
            }

            _versionService.BackupPlugin(metadata.Id);

            var wasRunning = _pluginManager.GetPluginState(metadata.Id) == PluginState.Running;
            if (wasRunning)
            {
                _pluginManager.DisablePlugin(metadata.Id);
            }

            var targetDir = existingMetadata.PluginDirectory;
            var extractedMetadata = _packagerService.ExtractPackage(packagePath, targetDir);

            if (wasRunning)
            {
                _pluginManager.EnablePlugin(metadata.Id);
            }

            XTrace.Log.Info("插件更新成功: {0} v{1}", metadata.Name, metadata.Version);
            return extractedMetadata;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("更新插件失败 [{0}]: {1}", packagePath, ex.Message);
            throw;
        }
    }

    public bool UninstallPlugin(string pluginId)
    {
        XTrace.Log.Info("卸载插件: {0}", pluginId);

        try
        {
            var metadata = _pluginManager.GetPluginMetadata(pluginId);
            if (metadata == null)
            {
                XTrace.Log.Warn("插件不存在，无需卸载: {0}", pluginId);
                return true;
            }

            var state = _pluginManager.GetPluginState(pluginId);
            if (state == PluginState.Running)
            {
                _pluginManager.DisablePlugin(pluginId);
            }

            _versionService.BackupPlugin(pluginId);

            var pluginDir = metadata.PluginDirectory;
            if (Directory.Exists(pluginDir))
            {
                Directory.Delete(pluginDir, true);
                XTrace.Log.Info("插件目录已删除: {0}", pluginDir);
            }

            XTrace.Log.Info("插件卸载成功: {0}", pluginId);
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("卸载插件失败 [{0}]: {1}", pluginId, ex.Message);
            return false;
        }
    }

    public ValidationResult ValidateDependencies(PluginMetadata metadata)
    {
        XTrace.Log.Debug("校验插件依赖: {0}", metadata.Id);

        var result = new ValidationResult { IsValid = true };

        if (metadata.Dependencies == null || metadata.Dependencies.Count == 0)
        {
            return result;
        }

        foreach (var depId in metadata.Dependencies)
        {
            var depMetadata = _pluginManager.GetPluginMetadata(depId);
            if (depMetadata == null)
            {
                result.IsValid = false;
                result.Errors.Add($"缺少依赖插件: {depId}");
            }
        }

        return result;
    }

    public ValidationResult ValidateCompatibility(PluginMetadata metadata)
    {
        var result = new ValidationResult { IsValid = true };

        if (string.IsNullOrWhiteSpace(metadata.Id))
        {
            result.IsValid = false;
            result.Errors.Add("插件Id不能为空");
        }

        if (string.IsNullOrWhiteSpace(metadata.Name))
        {
            result.IsValid = false;
            result.Errors.Add("插件名称不能为空");
        }

        if (string.IsNullOrWhiteSpace(metadata.Version))
        {
            result.IsValid = false;
            result.Errors.Add("插件版本不能为空");
        }

        if (string.IsNullOrWhiteSpace(metadata.EntryAssembly))
        {
            result.IsValid = false;
            result.Errors.Add("入口程序集不能为空");
        }

        if (string.IsNullOrWhiteSpace(metadata.EntryType))
        {
            result.IsValid = false;
            result.Errors.Add("入口类型不能为空");
        }

        return result;
    }
}

public class ValidationResult
{
    public bool IsValid { get; set; }

    public List<string> Errors { get; set; } = new();

    public List<string> Warnings { get; set; } = new();
}

using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.Abstractions;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.Services;

public class PluginInstallerService
{
    private readonly PluginManager _pluginManager;
    private readonly PluginPackagerService _packagerService;
    private readonly IDataLocationService? _dataLocation;
    private string _pluginsDirectory = string.Empty;

    public PluginInstallerService(
        PluginManager pluginManager,
        PluginPackagerService packagerService,
        PluginVersionService versionService,
        IDataLocationService? dataLocation = null)
    {
        _pluginManager = pluginManager;
        _packagerService = packagerService;
        _dataLocation = dataLocation;
        _ = versionService;
    }

    public void Initialize(string pluginsDirectory)
    {
        _pluginsDirectory = pluginsDirectory;
    }

    /// <summary>用户插件目录。优先数据根 <c>plugins/{id}</c>，测试未注入数据根时回落到 Initialize 目录。</summary>
    public string? ResolveUserPluginDirectory(string pluginId)
    {
        if (_dataLocation != null)
            return _dataLocation.GetPluginDataDirectory(pluginId);
        if (!string.IsNullOrWhiteSpace(_pluginsDirectory))
            return Path.Combine(_pluginsDirectory, pluginId);
        return null;
    }

    public PluginMetadata? InstallFromPackage(string packagePath)
    {
        XTrace.Log.Info("从包安装插件: {0}", packagePath);
        RejectLegacy(packagePath);

        var metadata = ReadIncoming(packagePath);
        if (metadata == null)
            throw new InvalidDataException("无法读取插件元数据");
        ValidateManifestContract(metadata);

        var validationResult = ValidateDependencies(metadata);
        if (!validationResult.IsValid)
            throw new InvalidOperationException($"依赖校验失败: {string.Join(", ", validationResult.Errors)}");

        var existing = _pluginManager.GetPluginMetadata(metadata.Id);
        if (existing != null)
        {
            XTrace.Log.Info("插件已存在，只暂存不激活: {0}", metadata.Id);
            return UpdateFromPackage(packagePath);
        }

        var pluginDir = ResolveUserPluginDirectory(metadata.Id)
            ?? throw new InvalidOperationException("数据根未接线，无法安装插件");
        return StageInto(pluginDir, packagePath, metadata, writeCurrent: true);
    }

    /// <summary>再次安装或远程来源：只写入 versions/，不切换 current。</summary>
    public PluginMetadata? StageFromPackage(string packagePath)
    {
        RejectLegacy(packagePath);
        var metadata = ReadIncoming(packagePath);
        if (metadata == null)
            throw new InvalidDataException("无法读取插件元数据");
        ValidateManifestContract(metadata);

        var existing = _pluginManager.GetPluginMetadata(metadata.Id);
        if (existing != null && !string.IsNullOrWhiteSpace(existing.PluginDirectory))
        {
            EnsureHigherThanCurrent(metadata, existing);
            return StageInto(existing.PluginDirectory, packagePath, metadata, writeCurrent: false);
        }

        var pluginDir = ResolveUserPluginDirectory(metadata.Id)
            ?? throw new InvalidOperationException("数据根未接线，无法暂存插件");
        return StageInto(pluginDir, packagePath, metadata, writeCurrent: false);
    }

    public PluginMetadata? UpdateFromPackage(string packagePath)
    {
        XTrace.Log.Info("从包暂存插件（不激活）: {0}", packagePath);
        RejectLegacy(packagePath);

        var metadata = ReadIncoming(packagePath);
        if (metadata == null)
            throw new InvalidDataException("无法读取插件元数据");
        ValidateManifestContract(metadata);

        var existing = _pluginManager.GetPluginMetadata(metadata.Id);
        if (existing == null || string.IsNullOrWhiteSpace(existing.PluginDirectory))
            throw new InvalidOperationException($"插件不存在，无法更新: {metadata.Id}");

        EnsureHigherThanCurrent(metadata, existing);
        var staged = StageInto(existing.PluginDirectory, packagePath, metadata, writeCurrent: false);
        XTrace.Log.Info("插件已暂存: {0} v{1}", metadata.Name, metadata.Version);
        return staged;
    }

    public bool UninstallPlugin(string pluginId)
    {
        XTrace.Log.Info("卸载插件运行面: {0}", pluginId);

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
                _pluginManager.DisablePlugin(pluginId);

            var pluginDir = metadata.PluginDirectory;
            if (!string.IsNullOrWhiteSpace(pluginDir) && Directory.Exists(pluginDir))
            {
                var manifest = Path.Combine(pluginDir, "plugin.json");
                if (File.Exists(manifest))
                    File.Delete(manifest);
                var pointer = PluginVersionLayout.CurrentPointerPath(pluginDir);
                if (File.Exists(pointer))
                    File.Delete(pointer);
                var versions = PluginVersionLayout.VersionsDirectory(pluginDir);
                if (Directory.Exists(versions))
                    Directory.Delete(versions, true);
                XTrace.Log.Info("已删除运行面，保留 src 与 .forge: {0}", pluginDir);
            }

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
        var result = new ValidationResult { IsValid = true };
        if (metadata.Dependencies == null || metadata.Dependencies.Count == 0)
            return result;

        foreach (var depId in metadata.Dependencies)
        {
            if (_pluginManager.GetPluginMetadata(depId) == null)
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

    private PluginMetadata StageInto(string pluginDir, string packagePath, PluginMetadata metadata, bool writeCurrent)
    {
        if (string.IsNullOrWhiteSpace(metadata.Version) || !PluginVersionService.IsReleaseVersion(metadata.Version))
            throw new InvalidDataException($"包版本号非法: {metadata.Version}");

        PluginVersionLayout.EnsureLayout(pluginDir);
        var versionDir = PluginVersionLayout.VersionDirectory(pluginDir, metadata.Version);
        if (!Directory.Exists(versionDir))
        {
            Directory.CreateDirectory(versionDir);
            if (PluginArtifactValidator.IsDll(packagePath))
                PluginArtifactValidator.InstallDll(packagePath, versionDir);
            else
                _packagerService.ExtractPackage(packagePath, versionDir);
        }

        var versionManifest = Path.Combine(versionDir, "plugin.json");
        var rootManifest = Path.Combine(pluginDir, "plugin.json");
        if (File.Exists(versionManifest) && (writeCurrent || !File.Exists(rootManifest)))
            File.Copy(versionManifest, rootManifest, true);
        if (writeCurrent)
            PluginVersionLayout.WriteCurrentVersion(pluginDir, metadata.Version);

        metadata.PluginDirectory = pluginDir;
        XTrace.Log.Info("插件版本目录已写入: {0}（current={1}）", versionDir, writeCurrent);
        return metadata;
    }

    private static void EnsureHigherThanCurrent(PluginMetadata incoming, PluginMetadata existing)
    {
        if (string.IsNullOrWhiteSpace(incoming.Version) || !PluginVersionService.IsReleaseVersion(incoming.Version))
            throw new InvalidDataException($"包版本号非法: {incoming.Version}");
        if (new VersionComparer().Compare(incoming.Version, existing.Version) <= 0)
        {
            throw new InvalidOperationException(
                $"包版本 {incoming.Version} 未高于当前生效版本 {existing.Version}，拒绝覆盖式更新（版本化布局只允许升级）");
        }
    }

    private static void RejectLegacy(string packagePath)
    {
        if (PluginArtifactValidator.IsLegacyPackage(packagePath))
            throw new InvalidDataException("已停止接受 .forgeself-plugin，请使用 .zip");
    }

    private PluginMetadata? ReadIncoming(string packagePath)
    {
        if (PluginArtifactValidator.IsLegacyPackage(packagePath))
            return null;
        if (PluginArtifactValidator.IsDll(packagePath))
            return PluginArtifactValidator.ReadDllManifest(packagePath);
        return _packagerService.ReadPackageMetadata(packagePath);
    }

    private static void ValidateManifestContract(PluginMetadata metadata)
    {
        if (!string.IsNullOrWhiteSpace(metadata.HostApiVersion) &&
            !string.Equals(metadata.HostApiVersion, "1.0", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"hostApiVersion 仅支持 1.0，实际为 {metadata.HostApiVersion}");
        }

        if (!string.IsNullOrWhiteSpace(metadata.RepositoryUrl))
        {
            if (!Uri.TryCreate(metadata.RepositoryUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidDataException("repositoryUrl 必须是 http(s) 绝对地址");
            }
        }

        if (metadata.Permissions != null && metadata.Permissions.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("permissions 含空白项");
    }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

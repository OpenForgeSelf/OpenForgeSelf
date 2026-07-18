using System.IO.Compression;
using System.Text.Json;
using OpenForgeSelf.Backend.Models.Plugins;
using OpenForgeSelf.Backend.Plugins.Abstractions;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins.Services;

public class PluginVersionService
{
    private readonly PluginManager _pluginManager;
    private string _backupsDirectory = string.Empty;

    public PluginVersionService(PluginManager pluginManager)
    {
        _pluginManager = pluginManager;
    }

    public void Initialize(string pluginsDirectory)
    {
        _backupsDirectory = Path.Combine(pluginsDirectory, "_backups");
        if (!Directory.Exists(_backupsDirectory))
        {
            Directory.CreateDirectory(_backupsDirectory);
        }
        XTrace.Log.Info("插件版本管理服务初始化，备份目录: {0}", _backupsDirectory);
    }

    public List<PluginUpdateInfo> CheckForUpdates()
    {
        XTrace.Log.Info("检查插件更新");

        var updates = new List<PluginUpdateInfo>();
        var metadatas = _pluginManager.GetAllMetadatas();

        foreach (var metadata in metadatas)
        {
            var backupDir = Path.Combine(_backupsDirectory, metadata.Id);
            if (!Directory.Exists(backupDir))
                continue;

            var versionDirs = Directory.GetDirectories(backupDir)
                .Select(Path.GetFileName)
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList();

            if (versionDirs.Count == 0)
                continue;

            var latestVersion = versionDirs
                .OrderByDescending(v => v, new VersionComparer())
                .First();

            if (new VersionComparer().Compare(latestVersion, metadata.Version) > 0)
            {
                updates.Add(new PluginUpdateInfo
                {
                    PluginId = metadata.Id,
                    PluginName = metadata.Name,
                    CurrentVersion = metadata.Version,
                    LatestVersion = latestVersion!,
                    HasUpdate = true
                });
            }
        }

        XTrace.Log.Info("检查更新完成，发现 {0} 个插件可更新", updates.Count);
        return updates;
    }

    public List<PluginVersionInfo> GetPluginVersions(string pluginId)
    {
        XTrace.Log.Info("获取插件版本历史: {0}", pluginId);

        var versions = new List<PluginVersionInfo>();
        var backupDir = Path.Combine(_backupsDirectory, pluginId);

        if (!Directory.Exists(backupDir))
            return versions;

        var versionDirs = Directory.GetDirectories(backupDir)
            .Select(d => new DirectoryInfo(d))
            .OrderByDescending(d => d.Name, new VersionComparer())
            .ToList();

        foreach (var dir in versionDirs)
        {
            var versionInfo = new PluginVersionInfo
            {
                Version = dir.Name,
                ReleasedAt = dir.CreationTime
            };

            var notesPath = Path.Combine(dir.FullName, "releasenotes.txt");
            if (File.Exists(notesPath))
            {
                versionInfo.ReleaseNotes = File.ReadAllText(notesPath);
            }

            versions.Add(versionInfo);
        }

        var metadata = _pluginManager.GetPluginMetadata(pluginId);
        if (metadata != null && !string.IsNullOrEmpty(metadata.ReleaseNotes))
        {
            versions.Insert(0, new PluginVersionInfo
            {
                Version = metadata.Version,
                ReleasedAt = metadata.UpdatedAt ?? DateTime.Now,
                ReleaseNotes = metadata.ReleaseNotes
            });
        }

        return versions;
    }

    public bool UpdatePlugin(string pluginId)
    {
        XTrace.Log.Info("更新插件: {0}", pluginId);

        var metadata = _pluginManager.GetPluginMetadata(pluginId);
        if (metadata == null)
        {
            XTrace.Log.Error("插件不存在: {0}", pluginId);
            return false;
        }

        var backupDir = Path.Combine(_backupsDirectory, pluginId);
        if (!Directory.Exists(backupDir))
        {
            XTrace.Log.Warn("没有可用的更新版本: {0}", pluginId);
            return false;
        }

        var versionDirs = Directory.GetDirectories(backupDir)
            .Select(Path.GetFileName)
            .Where(v => !string.IsNullOrEmpty(v))
            .OrderByDescending(v => v, new VersionComparer())
            .ToList();

        if (versionDirs.Count == 0)
            return false;

        var latestVersion = versionDirs.First()!;

        if (new VersionComparer().Compare(latestVersion, metadata.Version) <= 0)
        {
            XTrace.Log.Info("已经是最新版本: {0} v{1}", pluginId, metadata.Version);
            return true;
        }

        try
        {
            BackupPlugin(pluginId);

            var sourceDir = Path.Combine(backupDir, latestVersion);
            var targetDir = metadata.PluginDirectory;

            var wasRunning = _pluginManager.GetPluginState(pluginId) == PluginState.Running;
            if (wasRunning)
            {
                _pluginManager.DisablePlugin(pluginId);
            }

            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceDir, file);
                var destFile = Path.Combine(targetDir, relativePath);
                var destDir = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }
                File.Copy(file, destFile, true);
            }

            var manifestPath = Path.Combine(targetDir, "plugin.json");
            if (File.Exists(manifestPath))
            {
                var json = File.ReadAllText(manifestPath);
                var updatedMetadata = JsonSerializer.Deserialize<PluginMetadata>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (updatedMetadata != null)
                {
                    updatedMetadata.PluginDirectory = targetDir;
                }
            }

            if (wasRunning)
            {
                _pluginManager.EnablePlugin(pluginId);
            }

            XTrace.Log.Info("插件更新成功: {0} v{1}", pluginId, latestVersion);
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("更新插件失败 [{0}]: {1}", pluginId, ex.Message);
            return false;
        }
    }

    public bool RollbackPlugin(string pluginId, string version)
    {
        XTrace.Log.Info("回滚插件: {0} 到版本 {1}", pluginId, version);

        var metadata = _pluginManager.GetPluginMetadata(pluginId);
        if (metadata == null)
        {
            XTrace.Log.Error("插件不存在: {0}", pluginId);
            return false;
        }

        var backupDir = Path.Combine(_backupsDirectory, pluginId, version);
        if (!Directory.Exists(backupDir))
        {
            XTrace.Log.Error("备份版本不存在: {0} {1}", pluginId, version);
            return false;
        }

        try
        {
            BackupPlugin(pluginId);

            var wasRunning = _pluginManager.GetPluginState(pluginId) == PluginState.Running;
            if (wasRunning)
            {
                _pluginManager.DisablePlugin(pluginId);
            }

            var targetDir = metadata.PluginDirectory;

            foreach (var file in Directory.GetFiles(backupDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(backupDir, file);
                var destFile = Path.Combine(targetDir, relativePath);
                var destDir = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }
                File.Copy(file, destFile, true);
            }

            if (wasRunning)
            {
                _pluginManager.EnablePlugin(pluginId);
            }

            XTrace.Log.Info("插件回滚成功: {0} v{1}", pluginId, version);
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("回滚插件失败 [{0}]: {1}", pluginId, ex.Message);
            return false;
        }
    }

    public string? BackupPlugin(string pluginId)
    {
        XTrace.Log.Info("备份插件: {0}", pluginId);

        var metadata = _pluginManager.GetPluginMetadata(pluginId);
        if (metadata == null)
        {
            XTrace.Log.Error("插件不存在: {0}", pluginId);
            return null;
        }

        try
        {
            var backupId = Guid.NewGuid().ToString("N");
            var backupDir = Path.Combine(_backupsDirectory, pluginId, metadata.Version);
            var backupPath = Path.Combine(_backupsDirectory, pluginId, backupId);

            if (Directory.Exists(backupDir))
            {
                XTrace.Log.Debug("版本备份已存在，跳过: {0} v{1}", pluginId, metadata.Version);
                return backupDir;
            }

            Directory.CreateDirectory(backupDir);

            foreach (var file in Directory.GetFiles(metadata.PluginDirectory, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(metadata.PluginDirectory, file);
                var destFile = Path.Combine(backupDir, relativePath);
                var destDir = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }
                File.Copy(file, destFile, true);
            }

            if (!string.IsNullOrEmpty(metadata.ReleaseNotes))
            {
                File.WriteAllText(Path.Combine(backupDir, "releasenotes.txt"), metadata.ReleaseNotes);
            }

            XTrace.Log.Info("插件备份成功: {0} v{1}", pluginId, metadata.Version);
            return backupDir;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("备份插件失败 [{0}]: {1}", pluginId, ex.Message);
            return null;
        }
    }

    public List<PluginBackupInfo> GetBackupList(string pluginId)
    {
        XTrace.Log.Info("获取插件备份列表: {0}", pluginId);

        var backups = new List<PluginBackupInfo>();
        var backupDir = Path.Combine(_backupsDirectory, pluginId);

        if (!Directory.Exists(backupDir))
            return backups;

        var versionDirs = Directory.GetDirectories(backupDir)
            .Select(d => new DirectoryInfo(d))
            .OrderByDescending(d => d.CreationTime)
            .ToList();

        foreach (var dir in versionDirs)
        {
            backups.Add(new PluginBackupInfo
            {
                BackupId = dir.Name,
                Version = dir.Name,
                CreatedAt = dir.CreationTime,
                Size = GetDirectorySize(dir.FullName)
            });
        }

        return backups;
    }

    public bool RestoreFromBackup(string pluginId, string backupId)
    {
        return RollbackPlugin(pluginId, backupId);
    }

    private long GetDirectorySize(string path)
    {
        if (!Directory.Exists(path)) return 0;
        return new DirectoryInfo(path)
            .GetFiles("*", SearchOption.AllDirectories)
            .Sum(f => f.Length);
    }
}

public class VersionComparer : IComparer<string>
{
    public int Compare(string? x, string? y)
    {
        if (x == null && y == null) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        if (Version.TryParse(x, out var vx) && Version.TryParse(y, out var vy))
        {
            return vx.CompareTo(vy);
        }

        return string.Compare(x, y, StringComparison.Ordinal);
    }
}

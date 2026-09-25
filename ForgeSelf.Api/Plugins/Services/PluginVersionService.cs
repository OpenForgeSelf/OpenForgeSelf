using System.IO.Compression;
using System.Text.Json;
using ForgeSelf.Api.Models.Plugins;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.Abstractions;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.Services;

public class PluginVersionService
{
    /// <summary>side-by-side 保留的最近版本数（当前 + 上一版）。</summary>
    private const int MaxRetainedVersions = 2;

    private readonly PluginManager _pluginManager;
    private string _pluginsDirectory = string.Empty;
    private string _backupsDirectory = string.Empty;

    public PluginVersionService(PluginManager pluginManager)
    {
        _pluginManager = pluginManager;
    }

    public void Initialize(string pluginsDirectory)
    {
        _pluginsDirectory = pluginsDirectory;
        _backupsDirectory = Path.Combine(pluginsDirectory, "_backups");
        if (!Directory.Exists(_backupsDirectory))
        {
            Directory.CreateDirectory(_backupsDirectory);
        }
        XTrace.Log.Info("插件版本管理服务初始化，插件目录: {0}，备份目录: {1}", pluginsDirectory, _backupsDirectory);
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
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddVersion(string version, DateTime releasedAt, string notes)
        {
            if (string.IsNullOrWhiteSpace(version) || !seen.Add(version))
                return;
            versions.Add(new PluginVersionInfo
            {
                Version = version,
                ReleasedAt = releasedAt,
                ReleaseNotes = notes
            });
        }

        // ① 已安装快照（versions/<ver>/，side-by-side 布局；扁平存量迁移后也会落这里）
        var pluginDir = Path.Combine(_pluginsDirectory, pluginId);
        var versionsDir = Path.Combine(pluginDir, PluginVersionLayout.VersionsFolderName);
        if (Directory.Exists(versionsDir))
        {
            foreach (var dir in Directory.GetDirectories(versionsDir)
                         .Select(d => new DirectoryInfo(d))
                         .OrderByDescending(d => d.Name, new VersionComparer()))
            {
                var notes = "";
                var notesPath = Path.Combine(dir.FullName, "releasenotes.txt");
                if (File.Exists(notesPath))
                    notes = File.ReadAllText(notesPath);
                AddVersion(dir.Name, dir.CreationTime, notes);
            }
        }

        // ② 已暂存（_backups/<id>/，待更新的新版本）
        var backupDir = Path.Combine(_backupsDirectory, pluginId);
        if (Directory.Exists(backupDir))
        {
            foreach (var dir in Directory.GetDirectories(backupDir)
                         .Select(d => new DirectoryInfo(d))
                         .OrderByDescending(d => d.Name, new VersionComparer()))
            {
                var notes = "";
                var notesPath = Path.Combine(dir.FullName, "releasenotes.txt");
                if (File.Exists(notesPath))
                    notes = File.ReadAllText(notesPath);
                AddVersion(dir.Name, dir.CreationTime, notes);
            }
        }

        // ③ 当前清单版本（带 release notes；已在上方出现过则跳过，避免重复）
        var metadata = _pluginManager.GetPluginMetadata(pluginId);
        if (metadata != null)
        {
            AddVersion(metadata.Version, metadata.UpdatedAt ?? DateTime.Now, metadata.ReleaseNotes);
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

        var pluginDir = metadata.PluginDirectory;
        var sourceDir = Path.Combine(backupDir, latestVersion);

        // 1. 下载/解包到新版本目录（side-by-side，绝不覆盖正在加载的 DLL）
        if (!StageVersion(pluginId, latestVersion, sourceDir))
            return false;

        // 2. 记录旧版入口程序集路径（供破锁探测与延迟删除）
        var oldEntryPath = _pluginManager.GetPluginEntryAssemblyPath(pluginId);
        var wasRunning = _pluginManager.GetPluginState(pluginId) == PluginState.Running;

        try
        {
            // 3. 停用旧版（fiber.Dispose + ALC Unload）
            if (wasRunning)
                _pluginManager.DisablePlugin(pluginId);

            // 4. 强制回收，释放旧 DLL 句柄
            PluginAssemblyUnloader.ForceCollect();

            // 5. 确认旧 DLL 句柄可释放（未锁则后续可删；仍锁则交给延迟删除，绝不阻塞）
            if (!string.IsNullOrWhiteSpace(oldEntryPath))
            {
                if (PluginAssemblyUnloader.TryOpenExclusive(oldEntryPath))
                    XTrace.Log.Info("旧 DLL 句柄已释放: {0}", oldEntryPath);
                else
                    XTrace.Log.Warn("旧 DLL 仍被占用，延迟删除: {0}", oldEntryPath);
            }

            // 6. 切换 current 指针到新版本
            PluginVersionLayout.WriteCurrentVersion(pluginDir, latestVersion);

            // 7. 同步活动清单 plugin.json（使 DiscoverPlugins / 下次启动读到新版本元数据）
            SyncActiveManifest(pluginId, latestVersion);

            // 8. 刷新内存元数据并加载新版
            _pluginManager.RefreshMetadataFromDisk(pluginId);
            if (wasRunning)
                _pluginManager.EnablePlugin(pluginId);

            // 9. 保留最近 N 个版本，更旧版本延迟删除（不阻塞）
            PruneVersions(pluginId);

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

        var pluginDir = metadata.PluginDirectory;
        var targetVersionDir = PluginVersionLayout.VersionDirectory(pluginDir, version);

        // 目标版本若未安装，尝试从备份目录 stage 进来（side-by-side，不覆盖现有）
        if (!Directory.Exists(targetVersionDir))
        {
            var backupDir = Path.Combine(_backupsDirectory, pluginId, version);
            if (!Directory.Exists(backupDir))
            {
                XTrace.Log.Error("目标版本不存在: {0} {1}", pluginId, version);
                return false;
            }

            if (!StageVersion(pluginId, version, backupDir))
                return false;
        }

        var wasRunning = _pluginManager.GetPluginState(pluginId) == PluginState.Running;
        var oldEntryPath = _pluginManager.GetPluginEntryAssemblyPath(pluginId);

        try
        {
            if (wasRunning)
                _pluginManager.DisablePlugin(pluginId);

            PluginAssemblyUnloader.ForceCollect();

            if (!string.IsNullOrWhiteSpace(oldEntryPath) &&
                !PluginAssemblyUnloader.TryOpenExclusive(oldEntryPath))
            {
                XTrace.Log.Warn("旧 DLL 仍被占用，延迟删除: {0}", oldEntryPath);
            }

            PluginVersionLayout.WriteCurrentVersion(pluginDir, version);
            SyncActiveManifest(pluginId, version);
            _pluginManager.RefreshMetadataFromDisk(pluginId);
            if (wasRunning)
                _pluginManager.EnablePlugin(pluginId);

            XTrace.Log.Info("插件回滚成功: {0} v{1}", pluginId, version);
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("回滚插件失败 [{0}]: {1}", pluginId, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 将源目录（下载/解包产物或备份目录）复制到 side-by-side 版本目录，
    /// 目标为全新版本目录，绝不覆盖正在被 ALC 加载的 DLL。
    /// </summary>
    private bool StageVersion(string pluginId, string version, string sourceDir)
    {
        try
        {
            var metadata = _pluginManager.GetPluginMetadata(pluginId);
            if (metadata == null || string.IsNullOrWhiteSpace(metadata.PluginDirectory))
            {
                XTrace.Log.Error("插件元数据缺失，无法写入版本目录: {0}", pluginId);
                return false;
            }

            if (!Directory.Exists(sourceDir))
            {
                XTrace.Log.Error("版本源目录不存在: {0}", sourceDir);
                return false;
            }

            var targetDir = PluginVersionLayout.VersionDirectory(metadata.PluginDirectory, version);
            Directory.CreateDirectory(targetDir);

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

            XTrace.Log.Info("版本目录写入成功: {0} v{1}", pluginId, version);
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("写入版本目录失败 [{0}] v{1}: {2}", pluginId, version, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 把当前版本目录下的 plugin.json 同步到插件根目录活动清单，
    /// 使 <see cref="PluginManager.DiscoverPlugins"/> / 下次启动读到新版本元数据。
    /// plugin.json 为小文本文件且仅整读，覆盖不会触发文件锁。
    /// </summary>
    private void SyncActiveManifest(string pluginId, string version)
    {
        var metadata = _pluginManager.GetPluginMetadata(pluginId);
        if (metadata == null || string.IsNullOrWhiteSpace(metadata.PluginDirectory))
            return;

        var versionManifest = Path.Combine(
            PluginVersionLayout.VersionDirectory(metadata.PluginDirectory, version), "plugin.json");
        if (!File.Exists(versionManifest))
            return;

        var activeManifest = Path.Combine(metadata.PluginDirectory, "plugin.json");
        File.Copy(versionManifest, activeManifest, true);
    }

    /// <summary>
    /// 保留最近 <see cref="MaxRetainedVersions"/> 个版本（当前 + 上一版），
    /// 更旧版本走 <see cref="PluginAssemblyUnloader.TryDeleteDirectory"/> 延迟删除：
    /// 被占用则跳过（下轮再试），绝不阻塞。
    /// </summary>
    private void PruneVersions(string pluginId)
    {
        var metadata = _pluginManager.GetPluginMetadata(pluginId);
        if (metadata == null || string.IsNullOrWhiteSpace(metadata.PluginDirectory))
            return;

        var versionsDir = PluginVersionLayout.VersionsDirectory(metadata.PluginDirectory);
        if (!Directory.Exists(versionsDir))
            return;

        var current = PluginVersionLayout.ReadCurrentVersion(metadata.PluginDirectory);
        var versionDirs = Directory.GetDirectories(versionsDir)
            .Where(d => !string.IsNullOrEmpty(Path.GetFileName(d)))
            .OrderByDescending(d => Path.GetFileName(d), new VersionComparer())
            .ToList();

        foreach (var dir in versionDirs.Skip(MaxRetainedVersions))
        {
            if (string.Equals(Path.GetFileName(dir), current, StringComparison.OrdinalIgnoreCase))
                continue; // 防御：绝不删除当前生效版本

            if (PluginAssemblyUnloader.TryDeleteDirectory(dir))
                XTrace.Log.Info("删除旧版本目录: {0}", dir);
            else
                XTrace.Log.Warn("旧版本目录仍被占用，延迟删除: {0}", dir);
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

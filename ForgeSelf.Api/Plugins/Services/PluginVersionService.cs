using System.IO.Compression;
using System.Text.Json;
using ForgeSelf.Api.Models.Plugins;
using ForgeSelf.Abstractions;
using ForgeSelf.Api.Plugins.Abstractions;
using ForgeSelf.Api.Services;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.Services;

/// <summary>
/// 插件版本管理：side-by-side 版本目录（<c>versions/&lt;ver&gt;/</c> + <c>current</c> 指针）唯一布局。
/// <c>_backups</c> 备份/暂存目录已移除（2026-09-28 输入31）：新版本（包源/侧载）直接 stage 到
/// <c>versions/&lt;ver&gt;/</c>（未切 current 即惰性，side-by-side 天然回滚），回滚走 versions/ 内
/// 保留版本（当前 + 上一版，<see cref="MaxRetainedVersions"/>）。
/// 规则真源：docs/04-standards/packaging-upgrade-backup.md §3-T4 / §4-R4。
/// </summary>
public class PluginVersionService
{
    /// <summary>side-by-side 保留的最近版本数（当前 + 上一版）。</summary>
    private const int MaxRetainedVersions = 2;

    private readonly PluginManager _pluginManager;
    private readonly PluginUpdateSettingsService _pluginUpdateSettings;
    private readonly PluginPackagerService _packagerService;
    private string _pluginsDirectory = string.Empty;

    public PluginVersionService(
        PluginManager pluginManager,
        PluginUpdateSettingsService pluginUpdateSettings,
        PluginPackagerService packagerService)
    {
        _pluginManager = pluginManager;
        _pluginUpdateSettings = pluginUpdateSettings;
        _packagerService = packagerService;
    }

    public void Initialize(string pluginsDirectory)
    {
        _pluginsDirectory = pluginsDirectory;
        XTrace.Log.Info("插件版本管理服务初始化，插件目录: {0}（布局 = versions/<ver>/ + current，无备份目录）", pluginsDirectory);
    }

    public List<PluginUpdateInfo> CheckForUpdates()
    {
        XTrace.Log.Info("检查插件更新");

        var updates = new List<PluginUpdateInfo>();
        var metadatas = _pluginManager.GetAllMetadatas();

        foreach (var metadata in metadatas)
        {
            // ① versions/<id>/ 内已 staged 但未生效的更高版本（side-by-side 直落，source=staged）
            string? latestFromStaged = GetHighestStagedVersion(metadata.Id);

            // ② 插件更新源本地包目录（输入27）：顶层 *.forgeself-plugin 包，版本高于当前生效才列为可更新
            string? latestFromPackage = ScanPackageSource(metadata.Id, metadata.Version);

            string? latestVersion = null;
            string source = "staged";
            if (latestFromStaged != null && latestFromPackage != null)
            {
                if (new VersionComparer().Compare(latestFromStaged, latestFromPackage) >= 0)
                {
                    latestVersion = latestFromStaged;
                    source = "staged";
                }
                else
                {
                    latestVersion = latestFromPackage;
                    source = "package";
                }
            }
            else if (latestFromStaged != null)
            {
                latestVersion = latestFromStaged;
                source = "staged";
            }
            else if (latestFromPackage != null)
            {
                latestVersion = latestFromPackage;
                source = "package";
            }

            if (latestVersion == null)
                continue;

            if (new VersionComparer().Compare(latestVersion, metadata.Version) > 0)
            {
                updates.Add(new PluginUpdateInfo
                {
                    PluginId = metadata.Id,
                    PluginName = metadata.Name,
                    CurrentVersion = metadata.Version,
                    LatestVersion = latestVersion!,
                    HasUpdate = true,
                    Source = source
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

        // 版本目录（versions/<ver>/，side-by-side 布局；含已 staged 未生效的新版本与扁平迁移快照）
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

        // 当前清单版本（带 release notes；已在上方出现过则跳过，避免重复）
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

        // 输入27：插件更新源本地包目录——若 versions/ 无更高版本，尝试从包直接 stage 到 versions/<ver>/。
        // 必须在下方提前 return 之前执行，保证纯包源（从未 stage 过）场景也能更新。
        EnsureStagedFromPackageSource(pluginId, metadata.Version);

        var latestVersion = GetHighestStagedVersion(pluginId);
        if (latestVersion == null ||
            new VersionComparer().Compare(latestVersion, metadata.Version) <= 0)
        {
            XTrace.Log.Info("已经是最新版本: {0} v{1}", pluginId, metadata.Version);
            return true;
        }

        var targetVersionDir = PluginVersionLayout.VersionDirectory(metadata.PluginDirectory, latestVersion);
        if (!Directory.Exists(targetVersionDir))
        {
            XTrace.Log.Warn("没有可用的更新版本: {0}", pluginId);
            return false;
        }

        // 版本已直落 versions/（包源/侧载 stage 产物），无需复制，直接激活（切指针 + 同步清单 + 热切换）
        return ActivateVersion(pluginId, latestVersion);
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

        // 版本目录必须在 versions/ 内（保留策略 = 当前 + 上一版；更旧版本已被裁剪，无备份可恢复）
        if (!Directory.Exists(targetVersionDir))
        {
            XTrace.Log.Error("目标版本不存在（versions/ 仅保留当前+上一版）: {0} {1}", pluginId, version);
            return false;
        }

        return ActivateVersion(pluginId, version);
    }

    /// <summary>
    /// 激活指定版本：停用旧版（如运行中）→ 强制回收释放 DLL 句柄 → 切 current 指针 →
    /// 同步活动清单 plugin.json → 刷新内存元数据 → 启用新版 → 裁剪更旧版本。
    /// 不复制任何文件：目标版本必须已存在于 versions/&lt;ver&gt;/（直落布局）。
    /// </summary>
    private bool ActivateVersion(string pluginId, string version)
    {
        var metadata = _pluginManager.GetPluginMetadata(pluginId);
        if (metadata == null || string.IsNullOrWhiteSpace(metadata.PluginDirectory))
        {
            XTrace.Log.Error("插件元数据缺失，无法激活版本: {0}", pluginId);
            return false;
        }

        var oldEntryPath = _pluginManager.GetPluginEntryAssemblyPath(pluginId);
        var wasRunning = _pluginManager.GetPluginState(pluginId) == PluginState.Running;

        try
        {
            // 1. 停用旧版（fiber.Dispose + ALC Unload）
            if (wasRunning)
                _pluginManager.DisablePlugin(pluginId);

            // 2. 强制回收，释放旧 DLL 句柄
            PluginAssemblyUnloader.ForceCollect();

            // 3. 确认旧 DLL 句柄可释放（未锁则后续可删；仍锁则交给延迟删除，绝不阻塞）
            if (!string.IsNullOrWhiteSpace(oldEntryPath))
            {
                if (PluginAssemblyUnloader.TryOpenExclusive(oldEntryPath))
                    XTrace.Log.Info("旧 DLL 句柄已释放: {0}", oldEntryPath);
                else
                    XTrace.Log.Warn("旧 DLL 仍被占用，延迟删除: {0}", oldEntryPath);
            }

            // 4. 切换 current 指针到目标版本
            PluginVersionLayout.WriteCurrentVersion(metadata.PluginDirectory, version);

            // 5. 同步活动清单 plugin.json（使 DiscoverPlugins / 下次启动读到新版本元数据）
            SyncActiveManifest(pluginId, version);

            // 6. 刷新内存元数据并加载新版
            _pluginManager.RefreshMetadataFromDisk(pluginId);
            if (wasRunning)
                _pluginManager.EnablePlugin(pluginId);

            // 7. 保留最近 N 个版本，更旧版本延迟删除（不阻塞）
            PruneVersions(pluginId);

            XTrace.Log.Info("插件版本激活成功: {0} v{1}", pluginId, version);
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("插件版本激活失败 [{0}] v{1}: {2}", pluginId, version, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 把上传的 .forgeself-plugin 包解包直落 stage 到 versions/&lt;id&gt;/&lt;ver&gt;/（批次2.5，输入34）：
    /// 与 038 包源同一布局——不覆盖活动目录，多版本共存即回滚。已存在目标版本目录则保留（幂等）。
    /// 校验：包 Id 匹配、版本号合法（防路径穿越）、包内存在入口 DLL、ValidatePackage 通过。
    /// </summary>
    public bool StageUploadedPackage(string pluginId, string packagePath)
    {
        try
        {
            PluginMetadata? meta = null;
            try
            {
                meta = _packagerService.ReadPackageMetadata(packagePath);
            }
            catch (Exception ex)
            {
                XTrace.Log.Error("上传包读取失败: {0}", ex.Message);
                return false;
            }
            if (meta == null || !string.Equals(meta.Id, pluginId, StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.IsNullOrWhiteSpace(meta.Version) || !IsValidVersion(meta.Version))
                return false;
            if (string.IsNullOrWhiteSpace(meta.EntryAssembly) ||
                !PackageContainsEntryAssembly(packagePath, meta.EntryAssembly))
            {
                XTrace.Log.Warn("上传包缺少入口程序集 {0}，拒绝 stage: {1}", meta.EntryAssembly ?? "?", packagePath);
                return false;
            }
            if (!_packagerService.ValidatePackage(packagePath))
            {
                XTrace.Log.Error("上传包校验失败，拒绝 stage: {0}", packagePath);
                return false;
            }

            var installed = _pluginManager.GetPluginMetadata(pluginId);
            if (installed == null || string.IsNullOrWhiteSpace(installed.PluginDirectory))
                return false;

            var stagedDir = PluginVersionLayout.VersionDirectory(installed.PluginDirectory, meta.Version);
            if (Directory.Exists(stagedDir))
            {
                XTrace.Log.Warn("上传包：目标版本目录已存在，保留现有 staged: {0}", stagedDir);
                return true;
            }
            Directory.CreateDirectory(stagedDir);
            _packagerService.ExtractPackage(packagePath, stagedDir);

            XTrace.Log.Info("上传包已直落 staged 到 {0}", stagedDir);
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("上传包 stage 失败 [{0}]: {1}", pluginId, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 从插件更新源本地包目录把更高版本包解包直落 stage 到 versions/&lt;id&gt;/&lt;ver&gt;/（输入27，2026-09-28 去 _backups）。
    /// 未切 current 即惰性（side-by-side 安全）。全程内部捕获异常：失败记日志并返回 false（视为无包源），不传播 500。
    /// </summary>
    private bool EnsureStagedFromPackageSource(string pluginId, string currentVersion)
    {
        var localDir = _pluginUpdateSettings.Current.LocalDir;
        if (string.IsNullOrWhiteSpace(localDir) || !Directory.Exists(localDir))
            return false;

        try
        {
            string? bestPkg = null;
            string? bestVer = null;
            foreach (var pkg in Directory.GetFiles(localDir, "*.forgeself-plugin", SearchOption.TopDirectoryOnly))
            {
                PluginMetadata? meta = null;
                try
                {
                    meta = _packagerService.ReadPackageMetadata(pkg);
                }
                catch (Exception ex)
                {
                    XTrace.Log.Warn("插件更新源：包读取失败，跳过 {0}: {1}", pkg, ex.Message);
                    continue;
                }
                if (meta == null || !string.Equals(meta.Id, pluginId, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.IsNullOrWhiteSpace(meta.Version) || !IsValidVersion(meta.Version))
                    continue;
                if (new VersionComparer().Compare(meta.Version, currentVersion) <= 0)
                    continue;
                // P1-6：入口 DLL 校验（ValidatePackage 只查 plugin.json 三字段，不查 EntryAssembly）
                if (string.IsNullOrWhiteSpace(meta.EntryAssembly) ||
                    !PackageContainsEntryAssembly(pkg, meta.EntryAssembly))
                {
                    XTrace.Log.Warn("插件更新源：包缺少入口程序集 {0}，跳过: {1}", meta.EntryAssembly ?? "?", pkg);
                    continue;
                }

                // P1-2 统一版本基准（与 CheckForUpdates 展示一致）：包版本必须高于 versions/ 现有最高 staged 版本
                var highestStaged = GetHighestStagedVersion(pluginId);
                if (highestStaged != null && new VersionComparer().Compare(meta.Version, highestStaged) <= 0)
                    continue;

                if (bestVer == null || new VersionComparer().Compare(meta.Version, bestVer) > 0)
                {
                    bestVer = meta.Version;
                    bestPkg = pkg;
                }
            }

            if (bestPkg == null || bestVer == null)
                return false;

            if (!_packagerService.ValidatePackage(bestPkg))
            {
                XTrace.Log.Error("插件更新源：包校验失败，拒绝 stage: {0}", bestPkg);
                return false;
            }

            // 解包直落 versions/<id>/<ver>/（全新版本目录，side-by-side；包内布局根 = plugin.json + DLL + web/dist）
            var metadata = _pluginManager.GetPluginMetadata(pluginId);
            if (metadata == null || string.IsNullOrWhiteSpace(metadata.PluginDirectory))
            {
                XTrace.Log.Warn("插件更新源：插件元数据缺失，无法定位版本目录: {0}", pluginId);
                return false;
            }

            var stagedDir = PluginVersionLayout.VersionDirectory(metadata.PluginDirectory, bestVer);
            if (Directory.Exists(stagedDir))
            {
                XTrace.Log.Warn("插件更新源：目标版本目录已存在，保留现有 staged: {0}", stagedDir);
                return true;
            }
            Directory.CreateDirectory(stagedDir);
            _packagerService.ExtractPackage(bestPkg, stagedDir);

            XTrace.Log.Info("插件更新源：包已直落 staged 到 {0}", stagedDir);
            return true;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("插件更新源：从包 stage 失败 [{0}]: {1}", pluginId, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 扫描插件更新源本地包目录（输入27）：顶层 *.forgeself-plugin 包，读取包内 plugin.json，
    /// 返回版本高于 currentVersion 且高于 versions/ 现有最高 staged 版本的最高包版本；无包源/无效包/异常返回 null。
    /// </summary>
    private string? ScanPackageSource(string pluginId, string currentVersion)
    {
        var localDir = _pluginUpdateSettings.Current.LocalDir;
        if (string.IsNullOrWhiteSpace(localDir) || !Directory.Exists(localDir))
            return null;

        string? best = null;
        try
        {
            foreach (var pkg in Directory.GetFiles(localDir, "*.forgeself-plugin", SearchOption.TopDirectoryOnly))
            {
                PluginMetadata? meta = null;
                try
                {
                    meta = _packagerService.ReadPackageMetadata(pkg);
                }
                catch (Exception ex)
                {
                    XTrace.Log.Warn("插件更新源：包读取失败，跳过 {0}: {1}", pkg, ex.Message);
                    continue;
                }
                if (meta == null || !string.Equals(meta.Id, pluginId, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.IsNullOrWhiteSpace(meta.Version) || !IsValidVersion(meta.Version))
                    continue;
                if (new VersionComparer().Compare(meta.Version, currentVersion) <= 0)
                    continue;
                // P1-6：入口 DLL 校验（ValidatePackage 只查 plugin.json 三字段，不查 EntryAssembly）
                if (string.IsNullOrWhiteSpace(meta.EntryAssembly) ||
                    !PackageContainsEntryAssembly(pkg, meta.EntryAssembly))
                {
                    XTrace.Log.Warn("插件更新源：包缺少入口程序集 {0}，跳过: {1}", meta.EntryAssembly ?? "?", pkg);
                    continue;
                }

                var highestStaged = GetHighestStagedVersion(pluginId);
                if (highestStaged != null && new VersionComparer().Compare(meta.Version, highestStaged) <= 0)
                    continue;

                if (best == null || new VersionComparer().Compare(meta.Version, best) > 0)
                    best = meta.Version;
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("插件更新源：扫描本地包目录失败 {0}: {1}", localDir, ex.Message);
            return null;
        }

        return best;
    }

    /// <summary>versions/&lt;id&gt;/ 内已 staged（含未生效）的最高合法版本号；无则 null。</summary>
    private string? GetHighestStagedVersion(string pluginId)
    {
        var metadata = _pluginManager.GetPluginMetadata(pluginId);
        if (metadata == null || string.IsNullOrWhiteSpace(metadata.PluginDirectory))
            return null;

        var versionsDir = PluginVersionLayout.VersionsDirectory(metadata.PluginDirectory);
        if (!Directory.Exists(versionsDir))
            return null;

        var versions = Directory.GetDirectories(versionsDir)
            .Select(Path.GetFileName)
            .Where(v => !string.IsNullOrEmpty(v) && IsValidVersion(v!))
            .ToList();
        if (versions.Count == 0)
            return null;

        return versions.OrderByDescending(v => v, new VersionComparer()).First();
    }

    /// <summary>校验包内根存在 EntryAssembly 对应文件（P1-6，防包缺入口 DLL 导致假成功）。</summary>
    private bool PackageContainsEntryAssembly(string packagePath, string entryAssembly)
    {
        try
        {
            using var archive = ZipFile.OpenRead(packagePath);
            return archive.GetEntry(entryAssembly) != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>版本号必须为 x[.y[.z[.w]]] 数字序列（防路径穿越，P1-5）。</summary>
    private static bool IsValidVersion(string version)
        => System.Text.RegularExpressions.Regex.IsMatch(version, @"^\d+(\.\d+){0,3}$");

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

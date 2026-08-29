using Microsoft.Extensions.Hosting;
using NewLife.Log;

namespace ForgeSelf.Api.Plugins.Services;

/// <summary>
/// 插件文件级热更新监听器：FileSystemWatcher 监听 <c>Plugins/</c> 下的版本目录 /
/// <c>current</c> 指针 / DLL 落盘变更，debounce 300ms 后对受影响插件执行热重载
/// （复用 <see cref="PluginManager.ReloadPlugin"/>）。失败仅记日志并回退，不影响宿主与其它插件。
/// </summary>
public class PluginHotReloadWatcher : IHostedService, IDisposable
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(300);

    private readonly PluginManager _pluginManager;
    private readonly IHostEnvironment? _hostEnvironment;
    private readonly object _gate = new();
    private readonly Dictionary<string, CancellationTokenSource> _pending = new();
    private FileSystemWatcher? _watcher;
    private bool _disposed;

    public PluginHotReloadWatcher(PluginManager pluginManager, IHostEnvironment? hostEnvironment = null)
    {
        _pluginManager = pluginManager;
        _hostEnvironment = hostEnvironment;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // 测试环境不启动（WebApplicationFactory 集成测试中避免后台 watcher 干扰）
        if (_hostEnvironment?.IsEnvironment("Testing") == true)
            return Task.CompletedTask;

        var dir = _pluginManager.PluginsDirectory;
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            return Task.CompletedTask;

        try
        {
            _watcher = new FileSystemWatcher(dir)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName
                    | NotifyFilters.DirectoryName
                    | NotifyFilters.LastWrite
                    | NotifyFilters.CreationTime,
                EnableRaisingEvents = false
            };

            _watcher.Created += OnChanged;
            _watcher.Changed += OnChanged;
            _watcher.Renamed += OnChanged;
            _watcher.Deleted += OnChanged;
            _watcher.Error += OnWatcherError;

            _watcher.EnableRaisingEvents = true;
            XTrace.Log.Info("插件热更新监听器已启动: {0}", dir);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("启动插件热更新监听器失败: {0}", ex.Message);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        Stop();
        return Task.CompletedTask;
    }

    /// <summary>停止监听。</summary>
    public void Stop()
    {
        lock (_gate)
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        var pluginId = ResolvePluginId(e.FullPath);
        if (string.IsNullOrWhiteSpace(pluginId))
            return;

        ScheduleReload(pluginId);
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        XTrace.Log.Error("插件热更新监听器错误: {0}", e.GetException().Message);
    }

    /// <summary>
    /// 从变更路径解析受影响插件 ID。路径须位于 Plugins/&lt;folder&gt;/... 下，
    /// 顶层文件夹名映射到插件 <see cref="PluginMetadata.PluginDirectory"/>。
    /// <c>_backups</c> 等非插件目录返回 null（不触发重载）。
    /// </summary>
    public string? ResolvePluginId(string fullPath)
    {
        var root = _pluginManager.PluginsDirectory;
        if (string.IsNullOrWhiteSpace(root))
            return null;

        var folder = GetPluginFolder(root, fullPath);
        if (string.IsNullOrWhiteSpace(folder))
            return null;

        var dir = Path.GetFullPath(Path.Combine(root, folder));
        foreach (var metadata in _pluginManager.GetAllMetadatas())
        {
            if (string.Equals(
                    Path.GetFullPath(metadata.PluginDirectory),
                    dir,
                    StringComparison.OrdinalIgnoreCase))
            {
                return metadata.Id;
            }
        }

        return null;
    }

    /// <summary>
    /// 提取变更路径相对 Plugins 根的顶层文件夹名（即插件目录名）。
    /// 非插件目录（_backups、_trash、根外路径等）返回 null。
    /// </summary>
    public static string? GetPluginFolder(string pluginsRoot, string fullPath)
    {
        if (string.IsNullOrWhiteSpace(pluginsRoot) || string.IsNullOrWhiteSpace(fullPath))
            return null;

        string relative;
        try
        {
            relative = Path.GetRelativePath(pluginsRoot, fullPath);
        }
        catch (ArgumentException)
        {
            return null;
        }

        if (relative.StartsWith("..", StringComparison.Ordinal))
            return null;

        var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (segments.Length == 0)
            return null;

        var first = segments[0];
        if (string.IsNullOrWhiteSpace(first))
            return null;

        // 备份/回收站等内部目录不是插件，忽略
        if (first.Equals("_backups", StringComparison.OrdinalIgnoreCase) ||
            first.Equals("_trash", StringComparison.OrdinalIgnoreCase))
            return null;

        return first;
    }

    private void ScheduleReload(string pluginId)
    {
        lock (_gate)
        {
            if (_pending.TryGetValue(pluginId, out var existing))
                existing.Cancel();

            var cts = new CancellationTokenSource();
            _pending[pluginId] = cts;
            _ = DebounceAndReloadAsync(pluginId, cts);
        }
    }

    private async Task DebounceAndReloadAsync(string pluginId, CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(DebounceDelay, cts.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }
        finally
        {
            lock (_gate)
            {
                if (_pending.TryGetValue(pluginId, out var current) && ReferenceEquals(current, cts))
                    _pending.Remove(pluginId);
            }
        }

        OnPluginChanged(pluginId);
    }

    /// <summary>
    /// 变更后的热重载入口（抽成可测方法）：对受影响插件执行「停用旧 Fiber → 卸载 ALC →
    /// 重载新版」。失败仅记日志并回退上一可用版本，绝不向上抛异常。
    /// </summary>
    public bool OnPluginChanged(string pluginId)
    {
        try
        {
            return _pluginManager.ReloadPlugin(pluginId);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("热重载插件失败 [{0}]: {1}", pluginId, ex.Message);
            return false;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        Stop();
        lock (_gate)
        {
            foreach (var cts in _pending.Values)
                cts.Cancel();
            _pending.Clear();
        }
    }
}

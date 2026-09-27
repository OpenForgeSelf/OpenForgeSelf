using System.Text.Json;
using NewLife.Log;
using ForgeSelf.Api.Models;

namespace ForgeSelf.Api.Services;

/// <summary>
/// 运行时可变更新配置（2026-09-27 新增）。
/// 初始值来自 appsettings.json 的 "Update" 节；设置页修改后落盘到
/// {数据根}/Config/update-settings.json，下次启动时该文件优先于 appsettings 生效。
/// UpdateChecker / UpdateController 持同一个 <see cref="UpdateConfig"/> 实例引用，
/// 因此运行时修改立即可见，无需重启宿主。
/// </summary>
public class UpdateSettingsService
{
    private readonly UpdateConfig _config;
    private readonly string _settingsFilePath;
    private readonly object _gate = new();

    /// <summary>
    /// 初始化运行时更新配置：以 appsettings 的 "Update" 节为基座，
    /// 若用户配置文件已存在则覆盖生效。
    /// </summary>
    /// <param name="initial">appsettings "Update" 节解析出的初始配置</param>
    /// <param name="settingsFilePath">用户配置落盘路径 {数据根}/Config/update-settings.json</param>
    public UpdateSettingsService(UpdateConfig initial, string settingsFilePath)
    {
        _config = initial ?? throw new ArgumentNullException(nameof(initial));
        _settingsFilePath = settingsFilePath ?? throw new ArgumentNullException(nameof(settingsFilePath));
        LoadPersisted();
    }

    /// <summary>当前生效配置（与调用方共享同一实例引用，属性变更立即可见）。</summary>
    public UpdateConfig Current
    {
        get { lock (_gate) return _config; }
    }

    /// <summary>
    /// 修改配置并落盘。mutate 在锁内对 <see cref="Current"/> 执行属性变更。
    /// </summary>
    public void Update(Action<UpdateConfig> mutate)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        lock (_gate)
        {
            mutate(_config);
            Save();
            XTrace.Log.Info("UpdateSettings: 配置已更新并落盘 {0}", _settingsFilePath);
        }
    }

    private void LoadPersisted()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
                return;

            var json = File.ReadAllText(_settingsFilePath);
            var persisted = JsonSerializer.Deserialize<UpdateConfig>(json);
            if (persisted == null)
                return;

            _config.Provider = persisted.Provider;
            _config.ServerUrl = persisted.ServerUrl ?? "";
            _config.GitHubApiUrl = persisted.GitHubApiUrl ?? _config.GitHubApiUrl;
            _config.GitHubRepo = persisted.GitHubRepo ?? "";
            _config.LocalDir = persisted.LocalDir ?? "";
            _config.Channel = persisted.Channel ?? _config.Channel;
            _config.CheckIntervalMinutes = persisted.CheckIntervalMinutes;
            _config.CheckTimeoutSeconds = persisted.CheckTimeoutSeconds;
            _config.DownloadTimeoutSeconds = persisted.DownloadTimeoutSeconds;

            XTrace.Log.Info("UpdateSettings: 已加载用户更新配置 {0}（provider={1}）",
                _settingsFilePath, _config.Provider);
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("UpdateSettings: 读取用户配置失败，使用默认配置: {0}", ex.Message);
        }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_config,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);
        }
        catch (Exception ex)
        {
            XTrace.Log.Error("UpdateSettings: 配置落盘失败: {0}", ex.Message);
        }
    }
}

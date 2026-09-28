using System.Text.Json;
using ForgeSelf.Api.Models.Plugins;
using NewLife.Log;

namespace ForgeSelf.Api.Services;

/// <summary>
/// 运行时可变插件更新源配置（2026-09-28 新增，输入27）。
/// 同构 <see cref="UpdateSettingsService"/>：初始为空配置，设置页修改后落盘到
/// {数据根}/Config/plugin-update-settings.json，下次启动时该文件生效；
/// PluginVersionService / PluginController 持同一个 <see cref="PluginUpdateSettings"/> 实例引用，
/// 因此运行时修改立即可见，无需重启宿主。
/// </summary>
public class PluginUpdateSettingsService
{
    private readonly PluginUpdateSettings _config;
    private readonly string _settingsFilePath;
    private readonly object _gate = new();

    /// <summary>
    /// 初始化插件更新源配置。
    /// </summary>
    /// <param name="initial">初始配置（默认空 = 未配置）</param>
    /// <param name="settingsFilePath">用户配置落盘路径 {数据根}/Config/plugin-update-settings.json</param>
    public PluginUpdateSettingsService(PluginUpdateSettings initial, string settingsFilePath)
    {
        _config = initial ?? throw new ArgumentNullException(nameof(initial));
        _settingsFilePath = settingsFilePath ?? throw new ArgumentNullException(nameof(settingsFilePath));
        LoadPersisted();
    }

    /// <summary>当前生效配置（与调用方共享同一实例引用，属性变更立即可见）。</summary>
    public PluginUpdateSettings Current
    {
        get { lock (_gate) return _config; }
    }

    /// <summary>
    /// 修改配置并落盘。mutate 在锁内对 <see cref="Current"/> 执行属性变更。
    /// </summary>
    public void Update(Action<PluginUpdateSettings> mutate)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        lock (_gate)
        {
            mutate(_config);
            Save();
            XTrace.Log.Info("PluginUpdateSettings: 配置已更新并落盘 {0}", _settingsFilePath);
        }
    }

    private void LoadPersisted()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
                return;

            var json = File.ReadAllText(_settingsFilePath);
            var persisted = JsonSerializer.Deserialize<PluginUpdateSettings>(json);
            if (persisted == null)
                return;

            _config.LocalDir = persisted.LocalDir ?? "";

            XTrace.Log.Info("PluginUpdateSettings: 已加载用户插件更新源配置 {0}（localDir={1}）",
                _settingsFilePath, string.IsNullOrEmpty(_config.LocalDir) ? "(未配置)" : _config.LocalDir);
        }
        catch (Exception ex)
        {
            XTrace.Log.Warn("PluginUpdateSettings: 读取用户配置失败，使用默认配置: {0}", ex.Message);
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
            XTrace.Log.Error("PluginUpdateSettings: 配置落盘失败: {0}", ex.Message);
        }
    }
}

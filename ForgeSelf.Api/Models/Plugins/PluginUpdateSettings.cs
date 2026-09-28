namespace ForgeSelf.Api.Models.Plugins;

/// <summary>
/// 插件更新源配置（2026-09-28 新增，输入27）。
/// MVP 仅支持「本地插件包目录」：目录内放置 package-plugin.ps1 打出的
/// &lt;id&gt;-&lt;ver&gt;.forgeself-plugin 包，插件市场「检查更新」自动扫描发现更高版本。
/// 运行时可由设置页修改并落盘到 {数据根}/Config/plugin-update-settings.json
/// （见 <see cref="ForgeSelf.Api.Services.PluginUpdateSettingsService"/>）。
/// LocalDir 为空字符串表示未配置/停用更新源。
/// </summary>
public class PluginUpdateSettings
{
    /// <summary>
    /// 插件包更新源目录。空字符串 = 未配置（停用）。
    /// 非空时目录必须存在，否则保存接口返回 400。
    /// </summary>
    public string LocalDir { get; set; } = "";
}

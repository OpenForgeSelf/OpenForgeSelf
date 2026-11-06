namespace ForgeSelf.Api.Models.Plugins;

/// <summary>
/// 插件更新源配置（2026-09-28 新增，输入27）。
/// MVP 支持本地目录（只扫 <c>*.zip</c>）、GitHub Release 与 NuGet。
/// 不再使用 <c>.forgeself-plugin</c>。
/// 运行时可由设置页修改并落盘到 {数据根}/Config/plugin-update-settings.json
/// （见 <see cref="ForgeSelf.Api.Services.PluginUpdateSettingsService"/>）。
/// LocalDir 为空字符串表示未配置/停用更新源。
/// </summary>
public class PluginUpdateSettings
{
    /// <summary>
    /// 插件包更新源目录。空字符串 = 未配置（停用）。
    /// 本地目录只扫描顶层 <c>*.zip</c>。不再读取 <c>.forgeself-plugin</c>。
    /// </summary>
    public string LocalDir { get; set; } = "";

    /// <summary>GitHub API 根。密钥只来自环境变量 <c>GITHUB_TOKEN</c>，不写进本配置。</summary>
    public string GitHubApiBase { get; set; } = "https://api.github.com";

    /// <summary>NuGet v3 服务索引。下载走 flat container。密钥只来自 <c>NUGET_API_KEY</c>。</summary>
    public string NuGetV3Index { get; set; } = "https://api.nuget.org/v3/index.json";
}

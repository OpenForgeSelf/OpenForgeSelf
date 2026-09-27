namespace ForgeSelf.Api.Models;

/// <summary>
/// 更新配置。初始值从 appsettings.json 的 "Update" 节读取；
/// 运行时可通过设置页修改并落盘到 {数据根}/Config/update-settings.json（见 <see cref="UpdateSettingsService"/>）。
/// 支持三种更新源（<see cref="Provider"/>）：
/// stardust = StarServer 版本接口（008 原有协议）；
/// github = GitHub Releases（spec 036，打 tag 自动发布后页面自动更新）；
/// local = 本地目录（本机打包脚本 release-local.ps1 输出的 zip 所在目录，离线/内网更新）。
/// </summary>
public class UpdateConfig
{
    /// <summary>更新源类型："stardust"（StarServer，默认）、"github"（GitHub Releases）或 "local"（本地目录）</summary>
    public string Provider { get; set; } = "stardust";

    /// <summary>StarServer 更新服务器地址。空字符串表示不使用自动更新（仅手动检查）</summary>
    public string ServerUrl { get; set; } = "";

    /// <summary>GitHub API 基址（默认 https://api.github.com，可指向 GHES）</summary>
    public string GitHubApiUrl { get; set; } = "https://api.github.com";

    /// <summary>GitHub 仓库标识 "owner/repo"，Provider=github 时必填。更新源为公开仓库，匿名访问即可</summary>
    public string GitHubRepo { get; set; } = "";

    /// <summary>
    /// 本地更新目录（Provider=local 时必填）：放置打包脚本输出的
    /// OpenForgeSelf-&lt;ver&gt;-win-x64.zip（连同 SHA256SUMS.txt / RELEASE-NOTES-&lt;ver&gt;.md）的目录。
    /// </summary>
    public string LocalDir { get; set; } = "";

    /// <summary>更新通道：stable（稳定版）、beta（测试版）</summary>
    public string Channel { get; set; } = "stable";

    /// <summary>自动检查间隔（分钟）。0 表示仅启动时检查一次</summary>
    public int CheckIntervalMinutes { get; set; } = 60;

    /// <summary>启动时版本检查超时（秒），超时视为无更新，不阻塞启动</summary>
    public int CheckTimeoutSeconds { get; set; } = 5;

    /// <summary>更新包下载超时（秒）</summary>
    public int DownloadTimeoutSeconds { get; set; } = 300;
}
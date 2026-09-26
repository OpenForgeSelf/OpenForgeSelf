namespace ForgeSelf.Api.Models;

/// <summary>
/// 更新配置，从 appsettings.json 的 "Update" 节读取。
/// 支持两种更新源（<see cref="Provider"/>）：
/// stardust = StarServer 版本接口（008 原有协议）；github = GitHub Releases（spec 036）。
/// </summary>
public class UpdateConfig
{
    /// <summary>更新源类型："stardust"（StarServer，默认）或 "github"（GitHub Releases）</summary>
    public string Provider { get; set; } = "stardust";

    /// <summary>StarServer 更新服务器地址。空字符串表示不使用自动更新（仅手动检查）</summary>
    public string ServerUrl { get; set; } = "";

    /// <summary>GitHub API 基址（默认 https://api.github.com，可指向 GHES）</summary>
    public string GitHubApiUrl { get; set; } = "https://api.github.com";

    /// <summary>GitHub 仓库标识 "owner/repo"，Provider=github 时必填。更新源为公开仓库，匿名访问即可</summary>
    public string GitHubRepo { get; set; } = "";

    /// <summary>更新通道：stable（稳定版）、beta（测试版）</summary>
    public string Channel { get; set; } = "stable";

    /// <summary>自动检查间隔（分钟）。0 表示仅启动时检查一次</summary>
    public int CheckIntervalMinutes { get; set; } = 60;

    /// <summary>启动时版本检查超时（秒），超时视为无更新，不阻塞启动</summary>
    public int CheckTimeoutSeconds { get; set; } = 5;

    /// <summary>更新包下载超时（秒）</summary>
    public int DownloadTimeoutSeconds { get; set; } = 300;
}
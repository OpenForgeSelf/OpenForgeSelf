namespace OpenForgeSelf.Backend.Models;

/// <summary>
/// 更新配置，从 appsettings.json 的 "Update" 节读取。
/// 用于配置 StarServer 更新服务器地址、通道、超时等参数。
/// </summary>
public class UpdateConfig
{
    /// <summary>StarServer 更新服务器地址。空字符串表示不使用自动更新（仅手动检查）</summary>
    public string ServerUrl { get; set; } = "";

    /// <summary>更新通道：stable（稳定版）、beta（测试版）</summary>
    public string Channel { get; set; } = "stable";

    /// <summary>自动检查间隔（分钟）。0 表示仅启动时检查一次</summary>
    public int CheckIntervalMinutes { get; set; } = 60;

    /// <summary>启动时版本检查超时（秒），超时视为无更新，不阻塞启动</summary>
    public int CheckTimeoutSeconds { get; set; } = 5;

    /// <summary>更新包下载超时（秒）</summary>
    public int DownloadTimeoutSeconds { get; set; } = 300;
}
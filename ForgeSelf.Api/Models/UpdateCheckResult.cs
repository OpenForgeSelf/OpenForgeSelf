namespace ForgeSelf.Api.Models;

/// <summary>
/// 版本检查结果。每次版本检查时实时生成，不持久化。
/// 参考 data-model.md §2 UpdateCheckResult。
/// </summary>
public class UpdateCheckResult
{
    /// <summary>当前本地版本号</summary>
    public Version? CurrentVersion { get; set; }

    /// <summary>服务器上最新版本号</summary>
    public Version? LatestVersion { get; set; }

    /// <summary>是否有可用更新（LatestVersion > CurrentVersion）</summary>
    public bool HasUpdate { get; set; }

    /// <summary>更新包下载地址</summary>
    public string? DownloadUrl { get; set; }

    /// <summary>更新说明（Markdown 格式）</summary>
    public string? ReleaseNotes { get; set; }

    /// <summary>更新包 SHA256 哈希（格式：sha256:&lt;hex&gt;）</summary>
    public string? PackageHash { get; set; }

    /// <summary>更新包大小（字节）</summary>
    public long PackageSize { get; set; }

    /// <summary>检查时间戳</summary>
    public DateTime CheckTime { get; set; }

    /// <summary>检查失败时的错误信息</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>检查是否成功（网络/服务器错误时 false）</summary>
    public bool IsSuccess { get; set; }
}
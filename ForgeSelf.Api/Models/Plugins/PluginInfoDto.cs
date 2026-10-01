using ForgeSelf.Api.Plugins.Abstractions;

namespace ForgeSelf.Api.Models.Plugins;

public class PluginInfoDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string IconUrl { get; set; } = string.Empty;

    public PluginState State { get; set; }

    /// <summary>
    /// 插件最近一次装载/注册/初始化失败的完整错误摘要（消息 + 异常类型）；
    /// 无错误为 null（DTO 向前兼容，Production 亦填充——原 Error 态零信息可用）。
    /// </summary>
    public string? Error { get; set; }

    public bool IsEnabled { get; set; }

    public string Category { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = new();

    public long InstallCount { get; set; }

    public double Rating { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

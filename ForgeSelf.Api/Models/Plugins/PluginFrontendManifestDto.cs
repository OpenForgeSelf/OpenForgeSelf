using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Models.Plugins;

/// <summary>
/// 前端插件清单条目：返回已发现插件的前端贡献声明，供前端运行时驱动菜单/视图。
/// </summary>
public class PluginFrontendManifestDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 插件版本。供前端拼装界面资源 URL 的缓存标识（<c>?v={version}</c>），
    /// 版本提升即 URL 变化，刷新即可取到新界面（FR-008）。
    /// 为 010-plugin-frontend-runtime 新增字段，既有消费方不受影响。
    /// </summary>
    public string Version { get; set; } = string.Empty;

    public FrontendContributes? Frontend { get; set; }

    public bool IsEnabled { get; set; }
}

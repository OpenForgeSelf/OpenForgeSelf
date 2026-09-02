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

    /// <summary>
    /// 界面资源缓存标识（内容指纹）：基于插件 <c>web/dist</c> 入口脚本与样式文件内容计算的短哈希。
    /// 内容变化即变化，无需手动升版本即可让浏览器刷新取到新界面（解决重发插件不升版本时旧资源被
    /// <c>immutable</c> 缓存复用的问题）。前端拼装 JS 与 CSS 资源 URL 时优先用此值（回退 <see cref="Version"/>）。
    /// 计算失败或无界面资源时为 <see cref="string.Empty"/>。
    /// </summary>
    public string WebVersion { get; set; } = string.Empty;

    public FrontendContributes? Frontend { get; set; }

    public bool IsEnabled { get; set; }
}

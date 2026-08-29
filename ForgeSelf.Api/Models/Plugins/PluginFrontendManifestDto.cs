using ForgeSelf.Abstractions;

namespace ForgeSelf.Api.Models.Plugins;

/// <summary>
/// 前端插件清单条目：返回已发现插件的前端贡献声明，供前端运行时驱动菜单/视图。
/// </summary>
public class PluginFrontendManifestDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public FrontendContributes? Frontend { get; set; }

    public bool IsEnabled { get; set; }
}

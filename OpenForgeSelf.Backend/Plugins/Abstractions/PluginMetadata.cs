namespace OpenForgeSelf.Backend.Plugins.Abstractions;

/// <summary>
/// 插件元数据类，从 plugin.json 中解析
/// </summary>
public class PluginMetadata
{
    /// <summary>
    /// 获取或设置插件唯一标识
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置插件名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置插件版本
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置插件作者
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置插件描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置插件图标URL
    /// </summary>
    public string IconUrl { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置插件入口程序集名称
    /// </summary>
    public string EntryAssembly { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置插件入口类全名
    /// </summary>
    public string EntryType { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置插件依赖的其他插件ID列表
    /// </summary>
    public List<string> Dependencies { get; set; } = new();

    /// <summary>
    /// 获取或设置插件所需的权限列表
    /// </summary>
    public List<string> Permissions { get; set; } = new();

    /// <summary>
    /// 获取或设置插件目录路径
    /// </summary>
    public string PluginDirectory { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置插件分类
    /// </summary>
    public string Category { get; set; } = "工具";

    /// <summary>
    /// 获取或设置插件标签列表
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// 获取或设置截图URL列表
    /// </summary>
    public List<string> Screenshots { get; set; } = new();

    /// <summary>
    /// 获取或设置主页URL
    /// </summary>
    public string HomepageUrl { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置代码仓库URL
    /// </summary>
    public string RepositoryUrl { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置许可证
    /// </summary>
    public string License { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置发布说明/更新日志
    /// </summary>
    public string ReleaseNotes { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置最后更新时间
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// 获取或设置安装量
    /// </summary>
    public long InstallCount { get; set; }

    /// <summary>
    /// 获取或设置评分（0-5）
    /// </summary>
    public double Rating { get; set; }
}

namespace OpenForgeSelf.Backend.Plugins.Abstractions;

/// <summary>
/// 设置页扩展点接口，用于向系统添加设置页面
/// </summary>
public interface ISettingPageExtension : IExtensionPoint
{
    /// <summary>
    /// 获取设置页图标
    /// </summary>
    string Icon { get; }

    /// <summary>
    /// 获取设置页路由路径
    /// </summary>
    string Path { get; }

    /// <summary>
    /// 获取设置页分组
    /// </summary>
    string Category { get; }

    /// <summary>
    /// 获取排序值（越小越靠前）
    /// </summary>
    int Order { get; }

    /// <summary>
    /// 获取设置页描述
    /// </summary>
    string Description { get; }
}

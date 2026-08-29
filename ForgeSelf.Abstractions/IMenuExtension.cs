namespace ForgeSelf.Abstractions;

/// <summary>
/// 菜单扩展点接口，用于向系统添加菜单项
/// </summary>
public interface IMenuExtension : IExtensionPoint
{
    /// <summary>
    /// 获取菜单项图标
    /// </summary>
    string Icon { get; }

    /// <summary>
    /// 获取菜单项导航路径
    /// </summary>
    string Path { get; }

    /// <summary>
    /// 获取菜单项排序值（越小越靠前）
    /// </summary>
    int Order { get; }

    /// <summary>
    /// 获取父菜单ID（顶级菜单为null或空）
    /// </summary>
    string? ParentId { get; }

    /// <summary>
    /// 获取子菜单项
    /// </summary>
    IReadOnlyList<IMenuExtension>? Children { get; }
}

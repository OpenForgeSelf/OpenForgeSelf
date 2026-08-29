namespace ForgeSelf.Abstractions;

/// <summary>
/// 扩展点基接口，所有扩展点都必须实现此接口
/// </summary>
public interface IExtensionPoint
{
    /// <summary>
    /// 获取扩展点唯一标识
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 获取扩展点名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 获取提供此扩展点的插件ID
    /// </summary>
    string PluginId { get; }
}

namespace OpenForgeSelf.Backend.Plugins.Abstractions;

/// <summary>
/// 插件核心接口，所有插件必须实现此接口
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// 获取插件唯一标识
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 获取插件名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 获取插件版本
    /// </summary>
    string Version { get; }

    /// <summary>
    /// 获取插件作者
    /// </summary>
    string Author { get; }

    /// <summary>
    /// 获取插件描述
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 获取插件图标URL
    /// </summary>
    string IconUrl { get; }

    /// <summary>
    /// 初始化插件
    /// </summary>
    /// <param name="services">服务提供程序</param>
    void Initialize(IServiceProvider services);

    /// <summary>
    /// 启动插件
    /// </summary>
    void Start();

    /// <summary>
    /// 停止插件
    /// </summary>
    void Stop();

    /// <summary>
    /// 销毁插件
    /// </summary>
    void Destroy();
}

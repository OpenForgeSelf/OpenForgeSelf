namespace ForgeSelf.Api.Plugins.Abstractions;

/// <summary>
/// 插件状态枚举
/// </summary>
public enum PluginState
{
    /// <summary>
    /// 未加载
    /// </summary>
    NotLoaded,

    /// <summary>
    /// 已加载
    /// </summary>
    Loaded,

    /// <summary>
    /// 正在初始化
    /// </summary>
    Initializing,

    /// <summary>
    /// 已初始化
    /// </summary>
    Initialized,

    /// <summary>
    /// 正在启动
    /// </summary>
    Starting,

    /// <summary>
    /// 运行中
    /// </summary>
    Running,

    /// <summary>
    /// 正在停止
    /// </summary>
    Stopping,

    /// <summary>
    /// 已停止
    /// </summary>
    Stopped,

    /// <summary>
    /// 正在销毁
    /// </summary>
    Destroying,

    /// <summary>
    /// 已销毁
    /// </summary>
    Destroyed,

    /// <summary>
    /// 错误状态
    /// </summary>
    Error
}

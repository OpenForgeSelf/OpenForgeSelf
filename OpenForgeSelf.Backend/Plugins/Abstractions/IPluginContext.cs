using OpenForgeSelf.Backend.Services.UsageStats;

namespace OpenForgeSelf.Backend.Plugins.Abstractions;

/// <summary>
/// 插件上下文接口，提供插件运行环境
/// </summary>
public interface IPluginContext
{
    /// <summary>
    /// 获取插件元数据
    /// </summary>
    PluginMetadata Metadata { get; }

    /// <summary>
    /// 获取服务提供程序
    /// </summary>
    IServiceProvider Services { get; }

    /// <summary>
    /// 获取插件数据目录
    /// </summary>
    string DataDirectory { get; }

    /// <summary>
    /// 获取插件配置目录
    /// </summary>
    string ConfigDirectory { get; }

    /// <summary>
    /// 获取插件当前状态
    /// </summary>
    PluginState State { get; }

    /// <summary>
    /// 获取使用统计服务
    /// </summary>
    IUsageStatsService UsageStats { get; }

    /// <summary>
    /// 记录信息日志
    /// </summary>
    /// <param name="message">日志消息</param>
    void LogInfo(string message);

    /// <summary>
    /// 记录警告日志
    /// </summary>
    /// <param name="message">日志消息</param>
    void LogWarn(string message);

    /// <summary>
    /// 记录错误日志
    /// </summary>
    /// <param name="message">日志消息</param>
    void LogError(string message);

    /// <summary>
    /// 记录工具使用统计
    /// </summary>
    /// <param name="toolId">工具ID</param>
    /// <param name="actionType">操作类型</param>
    /// <param name="durationMs">持续时间（毫秒）</param>
    /// <param name="metadata">元数据</param>
    /// <returns>记录ID</returns>
    Task<long> RecordUsageAsync(
        string toolId,
        string actionType,
        long? durationMs = null,
        Dictionary<string, object>? metadata = null);
}

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

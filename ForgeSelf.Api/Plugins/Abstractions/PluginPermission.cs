namespace ForgeSelf.Api.Plugins.Abstractions;

/// <summary>
/// 插件权限枚举
/// </summary>
[Flags]
public enum PluginPermission
{
    /// <summary>
    /// 无特殊权限
    /// </summary>
    None = 0,

    /// <summary>
    /// 访问文件系统
    /// </summary>
    FileSystem = 1,

    /// <summary>
    /// 访问网络
    /// </summary>
    Network = 2,

    /// <summary>
    /// 访问数据库
    /// </summary>
    Database = 4,

    /// <summary>
    /// 访问配置
    /// </summary>
    Configuration = 8,

    /// <summary>
    /// 注册扩展点
    /// </summary>
    ExtensionPoint = 16,

    /// <summary>
    /// 访问AI服务
    /// </summary>
    AIService = 32,

    /// <summary>
    /// 管理其他插件
    /// </summary>
    PluginManagement = 64,

    /// <summary>
    /// 所有权限
    /// </summary>
    All = FileSystem | Network | Database | Configuration | ExtensionPoint | AIService | PluginManagement
}

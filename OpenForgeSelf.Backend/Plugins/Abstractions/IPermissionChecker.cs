namespace OpenForgeSelf.Backend.Plugins.Abstractions;

/// <summary>
/// 权限校验接口
/// </summary>
public interface IPermissionChecker
{
    /// <summary>
    /// 检查插件是否具有指定权限
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <param name="permission">权限</param>
    /// <returns>是否有权限</returns>
    bool HasPermission(string pluginId, PluginPermission permission);

    /// <summary>
    /// 授权插件权限
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <param name="permission">权限</param>
    void GrantPermission(string pluginId, PluginPermission permission);

    /// <summary>
    /// 撤销插件权限
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <param name="permission">权限</param>
    void RevokePermission(string pluginId, PluginPermission permission);

    /// <summary>
    /// 获取插件的所有权限
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>权限集合</returns>
    PluginPermission GetPermissions(string pluginId);
}

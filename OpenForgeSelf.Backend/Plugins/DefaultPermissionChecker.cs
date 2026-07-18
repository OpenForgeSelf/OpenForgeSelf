using OpenForgeSelf.Backend.Plugins.Abstractions;
using NewLife.Log;

namespace OpenForgeSelf.Backend.Plugins;

/// <summary>
/// 默认权限校验器实现
/// </summary>
public class DefaultPermissionChecker : IPermissionChecker
{
    private readonly Dictionary<string, PluginPermission> _permissions = new();

    /// <summary>
    /// 检查插件是否具有指定权限
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <param name="permission">权限</param>
    /// <returns>是否有权限</returns>
    public bool HasPermission(string pluginId, PluginPermission permission)
    {
        if (permission == PluginPermission.None)
            return true;

        if (_permissions.TryGetValue(pluginId, out var pluginPerms))
        {
            return (pluginPerms & permission) == permission;
        }
        return false;
    }

    /// <summary>
    /// 授权插件权限
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <param name="permission">权限</param>
    public void GrantPermission(string pluginId, PluginPermission permission)
    {
        if (_permissions.TryGetValue(pluginId, out var existing))
        {
            _permissions[pluginId] = existing | permission;
        }
        else
        {
            _permissions[pluginId] = permission;
        }

        XTrace.Log.Debug("授权插件[{0}]权限: {1}", pluginId, permission);
    }

    /// <summary>
    /// 撤销插件权限
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <param name="permission">权限</param>
    public void RevokePermission(string pluginId, PluginPermission permission)
    {
        if (_permissions.TryGetValue(pluginId, out var existing))
        {
            _permissions[pluginId] = existing & ~permission;
            XTrace.Log.Debug("撤销插件[{0}]权限: {1}", pluginId, permission);
        }
    }

    /// <summary>
    /// 获取插件的所有权限
    /// </summary>
    /// <param name="pluginId">插件ID</param>
    /// <returns>权限集合</returns>
    public PluginPermission GetPermissions(string pluginId)
    {
        if (_permissions.TryGetValue(pluginId, out var permissions))
        {
            return permissions;
        }
        return PluginPermission.None;
    }
}
